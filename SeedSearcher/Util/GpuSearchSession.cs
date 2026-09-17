using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;

namespace SeedSearcherGui
{
    internal static class GpuDeviceCatalog
    {
        private static readonly Lazy<Context> cuda = new Lazy<Context>(() => Context.Create(b => b.Cuda()));
        private static readonly Lazy<Context> opencl = new Lazy<Context>(() => Context.Create(b => b.OpenCL()));
        private static readonly Lazy<Device[]> devices = new Lazy<Device[]>(Discover);
        internal static Device[] Devices => (Device[])devices.Value.Clone();
        internal static string DiscoveryError { get; private set; }
        internal static Context ContextFor(Device device) => device.AcceleratorType == AcceleratorType.OpenCL ? opencl.Value : cuda.Value;
        internal static bool IncludeOpenCL(CLDevice device) =>
            (device.DeviceType & CLDeviceType.CL_DEVICE_TYPE_GPU) != 0 &&
            device.VendorName.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) < 0;
        private static Device[] Discover()
        {
            var list = new List<Device>();
            var errors = new List<string>();
            // Discover independently: a broken vendor runtime must not hide another backend.
            try { foreach (var device in cuda.Value.GetCudaDevices()) list.Add(device); }
            catch (Exception ex) { errors.Add("CUDA: " + ex.Message); }
            try
            {
                foreach (var device in opencl.Value.GetCLDevices())
                    if (IncludeOpenCL(device)) list.Add(device);
            }
            catch (Exception ex) { errors.Add("OpenCL: " + ex.Message); }
            DiscoveryError = string.Join(Environment.NewLine, errors);
            if (errors.Count > 0) Trace.WriteLine(DiscoveryError);
            return list.ToArray();
        }
        internal static void Shutdown()
        {
            try { if (opencl.IsValueCreated) opencl.Value.Dispose(); }
            finally { if (cuda.IsValueCreated) cuda.Value.Dispose(); }
        }
    }

    // Owns all input buffers for one search. Immutable arrays transfer once;
    // changed matrix arrays reuse device storage when their length is unchanged.
    internal sealed class SearchBuffers : IDisposable
    {
        private readonly Accelerator accelerator;
        private readonly Dictionary<string, IDisposable> buffers = new Dictionary<string, IDisposable>();
        private readonly Dictionary<string, object> sources = new Dictionary<string, object>();
        internal SearchBuffers(Accelerator accelerator) { this.accelerator = accelerator; }
        internal ArrayView<T> Upload<T>(string key, T[] values) where T : unmanaged
        {
            if (sources.TryGetValue(key, out var previous) && ReferenceEquals(previous, values))
                return ((MemoryBuffer1D<T, Stride1D.Dense>)buffers[key]).View.BaseView;
            MemoryBuffer1D<T, Stride1D.Dense> buffer;
            if (buffers.TryGetValue(key, out var old) && ((MemoryBuffer1D<T, Stride1D.Dense>)old).Length == values.Length)
            {
                buffer = (MemoryBuffer1D<T, Stride1D.Dense>)old;
                buffer.CopyFromCPU(values);
            }
            else
            {
                old?.Dispose();
                buffers.Remove(key);
                sources.Remove(key);
                buffer = accelerator.Allocate1D(values);
            }
            buffers[key] = buffer;
            sources[key] = values;
            return buffer.View.BaseView;
        }
        internal ArrayView<byte> Upload(string key, bool[] values)
        {
            // CLR bool marshalling differs from device bool layout; transfer bytes explicitly.
            if (sources.TryGetValue(key, out var previous) && ReferenceEquals(previous, values))
                return ((MemoryBuffer1D<byte, Stride1D.Dense>)buffers[key]).View.BaseView;
            var bytes = Array.ConvertAll(values, value => (byte)(value ? 1 : 0));
            var view = Upload(key, bytes);
            sources[key] = values;
            return view;
        }
        public void Dispose() { foreach (var buffer in buffers.Values) buffer.Dispose(); }
    }

    internal sealed class GpuSearchSession : IDisposable
    {
        internal Accelerator Accelerator { get; }
        private readonly MemoryBuffer1D<int, Stride1D.Dense> found;
        private readonly MemoryBuffer1D<ulong, Stride1D.Dense> result;
        internal double CompilationMilliseconds { get; private set; }
        private int batchSize = 4096;
        internal int BatchSize { get => batchSize; set => batchSize = Math.Max(1, value); }
        internal bool AdaptiveBatches { get; set; } = true;
        internal long CompletedBatches { get; private set; }
        internal double MaximumBatchMilliseconds { get; private set; }
        internal double ExecutionMilliseconds { get; private set; }
        internal GpuSearchSession(Device device, Context context = null)
        {
            Accelerator = device.CreateAccelerator(context ?? GpuDeviceCatalog.ContextFor(device));
            try
            {
                found = Accelerator.Allocate1D<int>(1);
                result = Accelerator.Allocate1D<ulong>(1);
            }
            catch { Dispose(); throw; }
        }
        internal Action<Index1D, SearchKernelData> Compile(Action<Index1D, SearchKernelData> kernel)
        {
            var watch = Stopwatch.StartNew();
            var launcher = Accelerator.LoadAutoGroupedStreamKernel(kernel);
            CompilationMilliseconds = watch.Elapsed.TotalMilliseconds;
            Trace.WriteLine($"ILGPU compilation: {CompilationMilliseconds:F1} ms");
            return launcher;
        }
        internal ulong? Run(Action<Index1D, SearchKernelData> kernel, long inputCount, int coefficientCount, SearchKernelData data)
        {
            data.Found = found.View.BaseView;
            data.Result = result.View.BaseView;
            found.MemSetToZero();
            // Bound both dimensions: a candidate may otherwise execute millions of coefficients.
            const int coefficientBatch = 256;
            for (int coefficient = 0; coefficient < coefficientCount; coefficient += coefficientBatch)
            {
                data.CoefficientStart = coefficient;
                data.CoefficientEnd = Math.Min(coefficientCount, coefficient + coefficientBatch);
                for (long offset = 0; offset < inputCount;)
                {
                    if (SeedSearcherGPU.StopSearchCommand) throw new OperationCanceledException();
                    int count = (int)Math.Min(batchSize, inputCount - offset);
                    data.InputStart = offset;
                    var watch = Stopwatch.StartNew();
                    kernel(count, data);
                    Accelerator.Synchronize();
                    CompletedBatches++;
                    double elapsed = watch.Elapsed.TotalMilliseconds;
                    MaximumBatchMilliseconds = Math.Max(MaximumBatchMilliseconds, elapsed);
                    ExecutionMilliseconds += elapsed;
                    if (found.GetAsArray1D()[0] != 0) return result.GetAsArray1D()[0];
                    offset += count;
                    double ratio = 150.0 / Math.Max(1.0, watch.Elapsed.TotalMilliseconds);
                    if (AdaptiveBatches) batchSize = (int)Math.Max(256, Math.Min(1048576, batchSize * Math.Min(2.0, Math.Max(0.5, ratio))));
                }
            }
            return null;
        }
        internal static void Update(ToolStripItem item, Action update)
        {
            var owner = item?.Owner;
            if (owner == null || owner.IsDisposed) return;
            if (owner.InvokeRequired) owner.BeginInvoke(new Action(() => { if (!owner.IsDisposed) update(); }));
            else update();
        }
        internal static void SetText(ToolStripItem item, string value) => Update(item, () => item.Text = value);
        public void Dispose()
        {
            Trace.WriteLine($"ILGPU execution: {ExecutionMilliseconds:F1} ms; batches: {CompletedBatches}; max batch: {MaximumBatchMilliseconds:F1} ms");
            result?.Dispose(); found?.Dispose(); Accelerator?.Dispose();
        }
    }
}
