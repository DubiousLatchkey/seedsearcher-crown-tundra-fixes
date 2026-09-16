using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SeedSearcherGui;

namespace SeedSearcherTest
{
    [TestClass]
    public class GpuRuntimeTests
    {
        private static GpuSearchSession CudaSession()
        {
            var devices = SeedSearcherGPU.UseableGPU();
            if (devices.Length == 0) Assert.Inconclusive("CUDA hardware is required.");
            return new GpuSearchSession(devices[0]);
        }

        private static void CountKernel(Index1D index, SearchKernelData data)
        {
            int input = (int)data.InputStart + index.X;
            for (int coefficient = data.CoefficientStart; coefficient < data.CoefficientEnd; coefficient++)
                Atomic.Add(ref data.allIVs[input * data.numElems + coefficient], 1);
        }
        private static void MatchKernel(Index1D index, SearchKernelData data)
        {
            long input = data.InputStart + index.X;
            if (data.l == 1 || input == (long)data.iv0)
                if (Atomic.CompareExchange(ref data.Found[0], 0, 1) == 0)
                    data.Result[0] = data.l == 1 ? (ulong)input : 0;
        }

        private static void CheckCoverage(GpuSearchSession session)
        {
            using (session)
            using (var counts = session.Accelerator.Allocate1D<int>(23 * 513))
            {
                counts.MemSetToZero();
                session.BatchSize = 7;
                session.AdaptiveBatches = false;
                var kernel = session.Compile(CountKernel);
                var data = new SearchKernelData { allIVs = counts.View.BaseView, numElems = 513 };
                Assert.IsNull(session.Run(kernel, 23, 513, data));
                foreach (int value in counts.GetAsArray1D()) Assert.AreEqual(1, value, "Skipped or duplicated work.");
                Assert.AreEqual(12L, session.CompletedBatches);
            }
        }
        [TestMethod]
        public void Cuda_BatchAndCoefficientBoundaries() => CheckCoverage(CudaSession());

        [TestMethod]
        public void CpuAccelerator_BoundedCoverage()
        {
            using (var context = Context.Create(b => b.CPU()))
                CheckCoverage(new GpuSearchSession(context.GetCPUDevice(0), context));
        }
        [TestMethod]
        public void Cuda_AtomicWinnerAndZeroSeed()
        {
            using (var session = CudaSession())
            {
                session.BatchSize = 7;
                session.AdaptiveBatches = false;
                var kernel = session.Compile(MatchKernel);
                foreach (ulong match in new ulong[] { 0, 6, 7, 22 })
                {
                    var seed = session.Run(kernel, 23, 1, new SearchKernelData { iv0 = match });
                    Assert.IsTrue(seed.HasValue);
                    Assert.AreEqual(0UL, seed.Value);
                }
                for (int repeat = 0; repeat < 30; repeat++)
                {
                    var seed = session.Run(kernel, 4096, 1, new SearchKernelData { l = 1 });
                    Assert.IsTrue(seed.HasValue && seed.Value < 7);
                }
                Assert.IsNull(session.Run(kernel, 23, 1, new SearchKernelData { iv0 = 23 }));
            }
        }
        [TestMethod]
        public void Cuda_CancelAndRestart()
        {
            using (var session = CudaSession())
            {
                var kernel = session.Compile(MatchKernel);
                // Warm up before measuring cancellation: compilation is not preemptible.
                session.Run(kernel, 1, 1, new SearchKernelData { iv0 = 2 });
                using (var started = new ManualResetEventSlim())
                {
                    var work = Task.Run(() =>
                    {
                        started.Set();
                        Assert.ThrowsException<OperationCanceledException>(() =>
                            session.Run(kernel, long.MaxValue, 1, new SearchKernelData { iv0 = ulong.MaxValue }));
                    });
                    started.Wait();
                    Thread.Sleep(100);
                    var watch = Stopwatch.StartNew();
                    SeedSearcherGPU.StopSearchCommand = true;
                    try { Assert.IsTrue(work.Wait(1000), "Cancellation exceeded one second."); }
                    finally
                    {
                        // Do not release resources while an asynchronous launch is still running.
                        work.Wait();
                        SeedSearcherGPU.StopSearchCommand = false;
                    }
                    Console.WriteLine("Cancellation ms: " + watch.ElapsedMilliseconds);
                    Assert.AreEqual(0UL, session.Run(kernel, 1, 1, new SearchKernelData()).Value);
                }
            }
        }
        [TestMethod]
        public void Cuda_BufferReuseAndFlagLayout()
        {
            using (var session = CudaSession())
            using (var buffers = new SearchBuffers(session.Accelerator))
            {
                var flags = new[] { false, true, false };
                var first = buffers.Upload("flags", flags);
                Assert.AreEqual(first, buffers.Upload("flags", flags));
                var second = buffers.Upload("flags", new[] { true, false, true });
                Assert.AreEqual(first, second, "Equal-sized transfers should reuse allocation.");
                // Exercise repeated allocation/disposal, including changed buffer sizes.
                for (int i = 1; i <= 100; i++) buffers.Upload("changing", new ulong[i]);
            }
        }
        private sealed class CompilationListener : TraceListener
        {
            internal readonly ManualResetEventSlim Ready = new ManualResetEventSlim();
            public override void Write(string message) { }
            public override void WriteLine(string message)
            {
                if (message != null && message.StartsWith("ILGPU compilation:")) Ready.Set();
            }
        }
        [TestMethod]
        public void Cuda_RealSearchCancelAndRestart()
        {
            if (SeedSearcherGPU.UseableGPU().Length == 0) Assert.Inconclusive("CUDA hardware required.");
            var search = new SeedSearcher(SeedSearcher.Mode.Star35);
            search.RegisterLSB(1); // Deliberately wrong: keep searching until cancellation.
            search.RegisterPokemon1(2,31,5,26,19,31,2,1,16,1,1,0,0,false,false);
            search.RegisterPokemon2(5,31,31,26,19,31,3,1,16,1,1,0,0,false,false);
            search.RegisterPokemon3(31,17,29,31,19,4,2,1,5,0,2,0,0,false,false);
            search.RegisterPokemon4(15,25,22,31,6,31,2,1,8,3,3,0,0,false,false);
            var target = new[] { 2,5,26,19,-1,-1 };
            var listener = new CompilationListener();
            Trace.Listeners.Add(listener);
            Task work = null;
            try
            {
                work = Task.Run(() => search.Calculate(0, 0, 0, target, null, null));
                Assert.IsTrue(listener.Ready.Wait(30000), "Kernel did not finish compilation.");
                Thread.Sleep(100);
                var watch = Stopwatch.StartNew();
                SeedSearcher.StopSearch();
                Assert.IsTrue(work.Wait(1000), "Real search cancellation exceeded one second.");
                Assert.AreEqual(SeedSearcher.SearchOutcome.Cancelled, search.Outcome);
                Console.WriteLine("Real search cancellation ms: " + watch.ElapsedMilliseconds);
            }
            finally
            {
                SeedSearcher.StopSearch();
                work?.Wait();
                SeedSearcherGPU.StopSearchCommand = false;
                Trace.Listeners.Remove(listener);
                listener.Ready.Dispose();
                listener.Dispose();
            }
            search.RegisterLSB(0);
            search.Calculate(0, 0, 0, target, null, null);
            Assert.AreEqual(SeedSearcher.SearchOutcome.Found, search.Outcome);
            Assert.AreEqual(0x87e8145f67d83f11UL, search.Result[0]);
        }
        [TestMethod]
        public void HostPreparation_HonorsCancellation()
        {
            var matrix = typeof(SeedSearcherGPU).GetNestedType("MatrixStruct", System.Reflection.BindingFlags.NonPublic);
            SeedSearcherGPU.StopSearchCommand = true;
            try
            {
                var error = Assert.ThrowsException<System.Reflection.TargetInvocationException>(() =>
                    matrix.GetMethod("CalculateCoefficientData").Invoke(null, new object[] { 40 }));
                Assert.IsInstanceOfType(error.InnerException, typeof(OperationCanceledException));
            }
            finally { SeedSearcherGPU.StopSearchCommand = false; }
        }
        [TestMethod]
        public void MissingDevice_ReportsFailure()
        {
            var searcher = new SeedSearcher(SeedSearcher.Mode.Star12);
            Assert.ThrowsException<InvalidOperationException>(() => searcher.Calculate(int.MaxValue, 0, 0, new int[6], null, null));
            Assert.AreEqual(SeedSearcher.SearchOutcome.Failed, searcher.Outcome);
        }
    }
}
