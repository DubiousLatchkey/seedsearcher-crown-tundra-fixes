using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SeedSearcherGui;

namespace SeedSearcherTest
{
    [TestClass]
    public class GpuEncounterTests
    {
        private const ulong Step = 0x82a2b175229d6a5bUL;
        private struct Rng
        {
            public ulong A, B;
            public uint Next(uint mask)
            {
                uint value = (uint)unchecked(A + B) & mask;
                B ^= A;
                A = ((A << 24) | (A >> 40)) ^ B ^ (B << 16);
                B = (B << 37) | (B >> 27);
                return value;
            }
            public int Below(uint mask, int limit)
            {
                int value;
                do { value = (int)Next(mask); } while ((uint)value >= (uint)limit);
                return value;
            }
        }
        // Independent forward RNG fixture generator; search uses matrix inversion.
        private static PkmnStruct Encounter(ulong seed, int day, int fixedCount, int form, bool toxtricity)
        {
            var rng = new Rng { A = unchecked(seed + (ulong)(day - 1) * Step), B = Step };
            uint ec;
            do { ec = rng.Next(uint.MaxValue); } while (ec == uint.MaxValue);
            while (rng.Next(uint.MaxValue) == uint.MaxValue) { }
            while (rng.Next(uint.MaxValue) == uint.MaxValue) { }
            var ivs = new[] { -1, -1, -1, -1, -1, -1 };
            int position = 0;
            for (int n = 0; n < fixedCount;)
            {
                position = rng.Below(7, 6);
                if (ivs[position] == -1) { ivs[position] = 31; n++; }
            }
            for (int i = 0; i < 6; i++) if (ivs[i] == -1) ivs[i] = (int)rng.Next(31);
            int ability = (int)rng.Next(1);
            rng.Below(255, 253);
            int[] amplified = { 3, 4, 2, 8, 9, 19, 22, 11, 13, 14, 0, 6, 24 };
            int[] lowKey = { 1, 5, 7, 10, 12, 15, 16, 17, 18, 20, 21, 23 };
            int nature = toxtricity ? (form == 0 ? amplified[rng.Below(15, 13)] : lowKey[rng.Below(15, 12)]) : rng.Below(31, 25);
            int[] order = { 0, 1, 2, 5, 3, 4 };
            int characteristic = 0;
            for (int i = 0; i < 6; i++)
            {
                int index = ((int)(ec % 6) + i) % 6;
                if (ivs[order[index]] == 31) { characteristic = index; break; }
            }
            return new PkmnStruct(ivs[0], ivs[1], ivs[2], ivs[3], ivs[4], ivs[5], fixedCount, ability, nature,
                characteristic, day, toxtricity ? 849 : 0, form, false, false, position);
        }
        private static SeedSearcherGPU Configure(ulong seed, int firstDay, int form, bool toxtricity)
        {
            var search = new SeedSearcherGPU();
            search.SetSixLSB((int)(unchecked(seed + (ulong)firstDay * Step) & 1));
            search.SetSixFirstCondition(Encounter(seed, firstDay, 2, form, toxtricity));
            search.SetSixSecondCondition(Encounter(seed, firstDay, 3, form, toxtricity));
            search.SetSixThirdCondition(Encounter(seed, firstDay + 1, 2, form, toxtricity));
            search.SetSixFourthCondition(Encounter(seed, firstDay + 2, 2, form, toxtricity));
            search.SetTargetCondition(new[] { 7, 14, 16, 17, 29, 29 });
            return search;
        }
        private static void Check(int form, int day)
        {
            ulong seed = unchecked(0x1fa0517d9f60fc44UL - (ulong)(day - 1) * Step);
            var search = Configure(seed, day, form, true);
            Assert.AreEqual(5U, search.TestSeed(seed));
            var devices = SeedSearcherGPU.UseableGPU();
            if (devices.Length == 0) Assert.Inconclusive("CUDA hardware required.");
            ulong? result = search.SearchSix(devices[0], 0, 0, new List<ulong> { 3 }, null, null);
            Assert.AreEqual(seed, result.Value);
        }
        private static object Native(string method, params object[] args) => typeof(SeedSearcher)
            .GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, args);

        [TestMethod]
        public void NativeFour_ForwardGeneratedCandidate()
        {
            // Bound the native four-IV regression to a candidate independently obtained by
            // advancing the known seed. Exhaustive four-IV CPU scans take hours.
            Native("Reset");
            Native("SetSixFirstCondition", 2,31,5,26,19,31,2,1,16,1,1,0,0,false,false);
            Native("SetSixSecondCondition", 5,31,31,26,19,31,3,1,16,1,1,0,0,false,false);
            Native("SetSixThirdCondition", 31,17,29,31,19,4,2,1,5,0,2,0,0,false,false);
            Native("SetSixFourthCondition", 15,25,22,31,6,31,2,1,8,3,3,0,0,false,false);
            Native("SetTargetCondition6", 2,5,26,19,-1,-1);
            Native("PrepareFive", 0);
            Assert.AreEqual(5U, (uint)Native("TestSixSeed", 0x87e8145f67d83f11UL));
            var rng = new Rng { A = 0x87e8145f67d83f11UL, B = Step };
            for (int i = 0; i < 4; i++) rng.Next(uint.MaxValue);
            ulong input = (rng.B & 7) << 20;
            ulong fixedPosition = unchecked(rng.A + rng.B) & 7;
            rng.Next(uint.MaxValue);
            for (int i = 0; i < 4; i++)
            {
                input |= (rng.B & 31) << (15 - i * 5);
                rng.Next(uint.MaxValue);
            }
            Assert.AreEqual(0x87e8145f67d83f11UL, (ulong)Native("SearchFour", input, 2UL, fixedPosition));
            Assert.AreEqual(0UL, (ulong)Native("SearchFour", input, 3UL, fixedPosition));
        }
        [TestMethod] public void Cuda_ToxtricityAmplified() => Check(0, 1);
        [TestMethod] public void Cuda_ToxtricityLowKeyAndDayOffset() => Check(1, 7);
        [TestMethod]
        public void Cuda_ZeroSeedThroughPublicSearch()
        {
            if (SeedSearcherGPU.UseableGPU().Length == 0) Assert.Inconclusive("CUDA hardware required.");
            var search = new SeedSearcher(SeedSearcher.Mode.Star35);
            search.RegisterLSB(1);
            var encounters = new[] { Encounter(0,1,2,0,false), Encounter(0,1,3,0,false), Encounter(0,2,2,0,false), Encounter(0,3,2,0,false) };
            for (int i = 0; i < encounters.Length; i++)
            {
                var p = encounters[i];
                typeof(SeedSearcher).GetMethod("RegisterPokemon" + (i + 1)).Invoke(search, new object[] {
                    p.ivs0,p.ivs1,p.ivs2,p.ivs3,p.ivs4,p.ivs5,p.fixedIV,p.ability,p.nature,p.characteristic,p.day,p.ID,p.altForm,p.isNoGender,p.isEnableDream,p.fixedIVPos });
            }
            search.Calculate(0, 0, 0, new[] {7,14,16,17,29,29}, null, null);
            Assert.AreEqual(SeedSearcher.SearchOutcome.Found, search.Outcome);
            Assert.AreEqual(0UL, search.Result[0]);
        }
        [TestMethod]
        public void HostValidation_ZeroSeed()
        {
            var search = Configure(0, 1, 0, false);
            Assert.AreEqual(5U, search.TestSeed(0));
        }
    }
}
