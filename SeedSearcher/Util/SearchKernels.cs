using ILGPU;
using ILGPU.Runtime;
namespace SeedSearcherGui
{
    // Only value types and device views may cross the kernel boundary.
    internal struct SearchKernelData
    {
        public long InputStart;
        public int CoefficientStart, CoefficientEnd;
        public ArrayView<int> Found;
        public ArrayView<ulong> Result;
        public ArrayView<ulong> g_IvsRef;
        public ArrayView<int> allIVs;
        public ArrayView<int> fixedIVs;
        public ArrayView<int> abilitys;
        public ArrayView<byte> noGender;
        public ArrayView<byte> HA;
        public ArrayView<int> natures;
        public ArrayView<int> characteristics;
        public ArrayView<int> characteristicorder;
        public ArrayView<int> species;
        public ArrayView<int> alt;
        public ArrayView<ulong> add_const;
        public ArrayView<byte> g_FreeBit;
        public ArrayView<ulong> g_AnswerFlag;
        public ArrayView<ulong> g_CoefficientData;
        public ArrayView<ulong> g_SearchPattern;
        public ArrayView<int> ToxtricityAmplifiedNatures;
        public ArrayView<int> ToxtricityLowKeyNatures;
        public ulong g_ConstantTermVector;
        public int l;
        public int numElems;
        public int g_FixedIndex;
        public ulong g_ulongIndex;
        public ulong ability;
        public ulong iv0;
        public ulong iv1;
        public ulong iv2;
        public ulong iv3;
        public ulong iv4;
        public ulong iv5;
        public int shift;
        public ulong fixedPos;
    }
    partial class SeedSearcherGPU
    {
        private static ulong GetSignature(ulong value)
			{
				uint a = (uint)(value ^ (value >> 32));
				a ^= a >> 16;
				a ^= a >> 8;
				a ^= a >> 4;
				a ^= a >> 2;
				return (a ^ (a >> 1)) & 1;
			}
        internal static void KernelOne(Index1D index, SearchKernelData data)
        {
            long input = data.InputStart + index.X;

		ulong target = data.ability;
		ulong input_ivs = (ulong)input;
		target |= (input_ivs & 0xE000000ul) << 30;
		target |= (input_ivs & 0x1F00000ul) << 27;
		target |= (input_ivs & 0xF8000ul) << 22;
		target |= (input_ivs & 0x7C00ul) << 17;
		target |= (input_ivs & 0x3E0ul) << 12;
		target |= (input_ivs & 0x1Ful) << 7;

		target |= ((8ul + data.g_ulongIndex - ((input_ivs & 0xE000000ul) >> 25)) & 7) << 52;
		target |= ((32ul + data.g_IvsRef[data.g_FixedIndex] - ((input_ivs & 0x1F00000ul) >> 20)) & 0x1F) << 42;
		target |= ((32ul + data.g_IvsRef[data.g_FixedIndex + 1] - ((input_ivs & 0xF8000ul) >> 15)) & 0x1F) << 32;
		target |= ((32ul + data.g_IvsRef[data.g_FixedIndex + 2] - ((input_ivs & 0x7C00ul) >> 10)) & 0x1F) << 22;
		target |= ((32ul + data.g_IvsRef[data.g_FixedIndex + 3] - ((input_ivs & 0x3E0ul) >> 5)) & 0x1F) << 12;
		target |= ((32ul + data.g_IvsRef[data.g_FixedIndex + 4] - (input_ivs & 0x1Ful)) & 0x1F) << 2;

		target ^= data.g_ConstantTermVector;

		ulong processedTarget = 0;
		int offset = 0;
		for (int i = 0; i < data.l; ++i)
		{
			while ((data.g_FreeBit[i + offset] != 0))
			{
				++offset;
			}
			processedTarget |= GetSignature(data.g_AnswerFlag[i] & target) << (63 - (i + offset));
		}

		ulong s0;
		ulong s1;
		ulong s0tmp;
		ulong s1tmp;
		uint ec;
		uint skip;
		int ivs;
		int g_FixedIvs;
		int fixedIndex;
		int tmp;
		ulong seed = 0;
		if (Atomic.CompareExchange(ref data.Found[0], 0, 0) == 0)
			for (int search = data.CoefficientStart; search < data.CoefficientEnd; ++search)
			{
				seed = (processedTarget ^ data.g_CoefficientData[search]) | data.g_SearchPattern[search];
				int val = 2;
				while (val >= 0)
				{
					s0 = seed + data.add_const[val];
					s1 = 0x82a2b175229d6a5b;
					// EC
					do
					{
						ec = (uint)(s0 + s1);
						s1 = s0 ^ s1;
						s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
						s1 = RotateLeft(s1, 37);
					} while (ec == 0xFFFFFFFF);

					if (data.characteristics[val] >= 0)
					{
						int characteristic = data.characteristicorder[val * 6 + ec % 6];
						if (characteristic != data.characteristics[val])
						{
							break;
						}
					}

					// SIDTID
					do
					{
						skip = (uint)(s0 + s1);
						s1 = s0 ^ s1;
						s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
						s1 = RotateLeft(s1, 37);
					} while (skip == 0xFFFFFFFF);

					// TID
					do
					{
						skip = (uint)(s0 + s1);
						s1 = s0 ^ s1;
						s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
						s1 = RotateLeft(s1, 37);
					} while (skip == 0xFFFFFFFF);

					ivs = 0xC0;
					g_FixedIvs = data.fixedIVs[val];
					fixedIndex = 0;
					while (g_FixedIvs > 0)
					{
						do
						{
							fixedIndex = (int)((s0 + s1) & 7);
							s1 = s0 ^ s1;
							s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
							s1 = RotateLeft(s1, 37);
						} while (((1 << fixedIndex) & ivs) != 0);
						ivs |= 1 << fixedIndex;
						if (data.allIVs[val * 6 + fixedIndex] != 31)
						{
							goto end;
						}
						g_FixedIvs--;
					}

					for (int i = 0; i < 6; ++i)
					{
						if (((1 << i) & ivs) == 0)
						{
							if (data.allIVs[val * 6 + i] != (int)((s0 + s1) & 31))
							{
								goto end;
							}
							s1 = s0 ^ s1;
							s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
							s1 = RotateLeft(s1, 37);
						}
					}
					tmp = 0;
					// special case
					if (data.abilitys[val] == -2)
					{
						s0tmp = s0;
						s1tmp = s1;
						if ((data.HA[val] != 0))
						{
							do
							{
								tmp = (int)((s0 + s1) & 3);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 3);
						}
						else
						{
							tmp = (int)((s0 + s1) & 1);
							s1 = s0 ^ s1;
							s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
							s1 = RotateLeft(s1, 37);
						}
						if (!(data.noGender[val] != 0))
						{
							do
							{
								tmp = (int)((s0 + s1) & 255);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 253);
						}
						tmp = 0;
						if (data.species[val] == ToxtricityID)
						{
							if (data.alt[val] == 0)
							{
								do
								{
									tmp = (int)((s0 + s1) & 15);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 13);
								tmp = data.ToxtricityAmplifiedNatures[tmp];
							}
							else
							{
								do
								{
									tmp = (int)((s0 + s1) & 15);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 12);
								tmp = data.ToxtricityLowKeyNatures[tmp];
							}
						}
						else
						{
							do
							{
								tmp = (int)((s0 + s1) & 31);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 25);
						}
						if (tmp != data.natures[val])
						{
							s0 = s0tmp;
							s1 = s1tmp;
							if (!(data.noGender[val] != 0))
							{
								do
								{
									tmp = (int)((s0 + s1) & 255);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 253);
							}
							tmp = 0;
							if (data.species[val] == ToxtricityID)
							{
								if (data.alt[val] == 0)
								{
									do
									{
										tmp = (int)((s0 + s1) & 15);
										s1 = s0 ^ s1;
										s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
										s1 = RotateLeft(s1, 37);
									} while (tmp >= 13);
									tmp = data.ToxtricityAmplifiedNatures[tmp];
								}
								else
								{
									do
									{
										tmp = (int)((s0 + s1) & 15);
										s1 = s0 ^ s1;
										s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
										s1 = RotateLeft(s1, 37);
									} while (tmp >= 12);
									tmp = data.ToxtricityLowKeyNatures[tmp];
								}
							}
							else
							{
								do
								{
									tmp = (int)((s0 + s1) & 31);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 25);
							}
							if (tmp != data.natures[val])
							{
								break;
							}
						}

					}
					else
					{
						if ((data.HA[val] != 0))
						{
							do
							{
								tmp = (int)((s0 + s1) & 3);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 3);
							if (data.abilitys[val] != -1 && data.abilitys[val] != tmp) break;
						}
						else
						{
							tmp = (int)((s0 + s1) & 1);
							s1 = s0 ^ s1;
							s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
							s1 = RotateLeft(s1, 37);
							if (data.abilitys[val] != -1 && data.abilitys[val] != tmp) break;
						}

						if (!(data.noGender[val] != 0))
						{
							do
							{
								tmp = (int)((s0 + s1) & 255);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 253);
						}

						tmp = 0;
						if (data.species[val] == ToxtricityID)
						{
							if (data.alt[val] == 0)
							{
								do
								{
									tmp = (int)((s0 + s1) & 15);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 13);
								tmp = data.ToxtricityAmplifiedNatures[tmp];
							}
							else
							{
								do
								{
									tmp = (int)((s0 + s1) & 15);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 12);
								tmp = data.ToxtricityLowKeyNatures[tmp];
							}
						}
						else
						{
							do
							{
								tmp = (int)((s0 + s1) & 31);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 25);
						}
						if (tmp != data.natures[val])
						{
							break;
						}
					}
					if (val == 0)
					{
						if (Atomic.CompareExchange(ref data.Found[0], 0, 1) == 0)
                                    data.Result[0] = seed;
					}
					val--;
					continue;
				end:
					break;
				}
			}
        }
        internal static void KernelSix(Index1D index, SearchKernelData data)
        {
            long input = data.InputStart + index.X;

		ulong target = data.ability;
		ulong input_ivs = (ulong)input;
		target |= (input_ivs & 0x3E000000ul) << (30 + data.shift);
		target |= (input_ivs & 0x1F00000ul) << (25 + data.shift);
		target |= (input_ivs & 0xF8000ul) << (20 + data.shift);
		target |= (input_ivs & 0x7C00ul) << (15 + data.shift);
		target |= (input_ivs & 0x3E0ul) << (10 + data.shift);
		target |= (input_ivs & 0x1Ful) << (5 + data.shift);

		target |= ((32ul + data.iv0 - ((input_ivs & 0x3E000000ul) >> 25)) & 0x1F) << (50 + data.shift);
		target |= ((32ul + data.iv1 - ((input_ivs & 0x1F00000ul) >> 20)) & 0x1F) << (40 + data.shift);
		target |= ((32ul + data.iv2 - ((input_ivs & 0xF8000ul) >> 15)) & 0x1F) << (30 + data.shift);
		target |= ((32ul + data.iv3 - ((input_ivs & 0x7C00ul) >> 10)) & 0x1F) << (20 + data.shift);
		target |= ((32ul + data.iv4 - ((input_ivs & 0x3E0ul) >> 5)) & 0x1F) << (10 + data.shift);
		target |= ((32ul + data.iv5 - (input_ivs & 0x1Ful)) & 0x1F) << (0 + data.shift);

		target ^= data.g_ConstantTermVector;

		ulong processedTarget = 0;
		int offset = 0;
		for (int i = 0; i < data.l; ++i)
		{
			while ((data.g_FreeBit[i + offset] != 0))
			{
				++offset;
			}
			processedTarget |= GetSignature(data.g_AnswerFlag[i] & target) << (63 - (i + offset));
		}

		ulong s0;
		ulong s1;
		ulong s0tmp;
		ulong s1tmp;
		uint ec;
		uint skip;
		int ivs;
		int g_FixedIvs;
		int fixedIndex;
		int tmp;
		ulong seed = 0;
		if (Atomic.CompareExchange(ref data.Found[0], 0, 0) == 0)
			for (int search = data.CoefficientStart; search < data.CoefficientEnd; ++search)
			{
				seed = (processedTarget ^ data.g_CoefficientData[search]) | data.g_SearchPattern[search];
				int val = 3;
				while (val >= 0)
				{
					s0 = seed + data.add_const[val];
					s1 = 0x82a2b175229d6a5b;
					// EC
					do
					{
						ec = (uint)(s0 + s1);
						s1 = s0 ^ s1;
						s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
						s1 = RotateLeft(s1, 37);
					} while (ec == 0xFFFFFFFF);

					if (data.characteristics[val] >= 0)
					{
						int characteristic = data.characteristicorder[val * 6 + ec % 6];
						if (characteristic != data.characteristics[val])
						{
							break;
						}
					}

					// SIDTID
					do
					{
						skip = (uint)(s0 + s1);
						s1 = s0 ^ s1;
						s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
						s1 = RotateLeft(s1, 37);
					} while (skip == 0xFFFFFFFF);

					// TID
					do
					{
						skip = (uint)(s0 + s1);
						s1 = s0 ^ s1;
						s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
						s1 = RotateLeft(s1, 37);
					} while (skip == 0xFFFFFFFF);

					ivs = 0xC0;
					g_FixedIvs = data.fixedIVs[val];
					fixedIndex = 0;
					while (g_FixedIvs > 0)
					{
						do
						{
							fixedIndex = (int)((s0 + s1) & 7);
							s1 = s0 ^ s1;
							s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
							s1 = RotateLeft(s1, 37);
						} while (((1 << fixedIndex) & ivs) != 0);
						ivs |= 1 << fixedIndex;
						if (data.allIVs[val * 6 + fixedIndex] != 31)
						{
							goto end;
						}
						g_FixedIvs--;
					}

					for (int i = 0; i < 6; ++i)
					{
						if (((1 << i) & ivs) == 0)
						{
							if (data.allIVs[val * 6 + i] != (int)((s0 + s1) & 31))
							{
								goto end;
							}
							s1 = s0 ^ s1;
							s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
							s1 = RotateLeft(s1, 37);
						}
					}

					tmp = 0;
					// special case
					if (data.abilitys[val] == -2)
					{
						s0tmp = s0;
						s1tmp = s1;
						if ((data.HA[val] != 0))
						{
							do
							{
								tmp = (int)((s0 + s1) & 3);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 3);
						}
						else
						{
							tmp = (int)((s0 + s1) & 1);
							s1 = s0 ^ s1;
							s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
							s1 = RotateLeft(s1, 37);
						}
						if (!(data.noGender[val] != 0))
						{
							do
							{
								tmp = (int)((s0 + s1) & 255);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 253);
						}
						tmp = 0;
						if (data.species[val] == ToxtricityID)
						{
							if (data.alt[val] == 0)
							{
								do
								{
									tmp = (int)((s0 + s1) & 15);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 13);
								tmp = data.ToxtricityAmplifiedNatures[tmp];
							}
							else
							{
								do
								{
									tmp = (int)((s0 + s1) & 15);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 12);
								tmp = data.ToxtricityLowKeyNatures[tmp];
							}
						}
						else
						{
							do
							{
								tmp = (int)((s0 + s1) & 31);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 25);
						}
						if (tmp != data.natures[val])
						{
							s0 = s0tmp;
							s1 = s1tmp;
							if (!(data.noGender[val] != 0))
							{
								do
								{
									tmp = (int)((s0 + s1) & 255);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 253);
							}
							tmp = 0;
							if (data.species[val] == ToxtricityID)
							{
								if (data.alt[val] == 0)
								{
									do
									{
										tmp = (int)((s0 + s1) & 15);
										s1 = s0 ^ s1;
										s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
										s1 = RotateLeft(s1, 37);
									} while (tmp >= 13);
									tmp = data.ToxtricityAmplifiedNatures[tmp];
								}
								else
								{
									do
									{
										tmp = (int)((s0 + s1) & 15);
										s1 = s0 ^ s1;
										s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
										s1 = RotateLeft(s1, 37);
									} while (tmp >= 12);
									tmp = data.ToxtricityLowKeyNatures[tmp];
								}
							}
							else
							{
								do
								{
									tmp = (int)((s0 + s1) & 31);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 25);
							}
							if (tmp != data.natures[val])
							{
								break;
							}
						}

					}
					else
					{
						if ((data.HA[val] != 0))
						{
							do
							{
								tmp = (int)((s0 + s1) & 3);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 3);
							if (data.abilitys[val] != -1 && data.abilitys[val] != tmp) break;
						}
						else
						{
							tmp = (int)((s0 + s1) & 1);
							s1 = s0 ^ s1;
							s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
							s1 = RotateLeft(s1, 37);
							if (data.abilitys[val] != -1 && data.abilitys[val] != tmp) break;
						}

						if (!(data.noGender[val] != 0))
						{
							do
							{
								tmp = (int)((s0 + s1) & 255);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 253);
						}

						tmp = 0;
						if (data.species[val] == ToxtricityID)
						{
							if (data.alt[val] == 0)
							{
								do
								{
									tmp = (int)((s0 + s1) & 15);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 13);
								tmp = data.ToxtricityAmplifiedNatures[tmp];
							}
							else
							{
								do
								{
									tmp = (int)((s0 + s1) & 15);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 12);
								tmp = data.ToxtricityLowKeyNatures[tmp];
							}
						}
						else
						{
							do
							{
								tmp = (int)((s0 + s1) & 31);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 25);
						}
						if (tmp != data.natures[val])
						{
							break;
						}
					}
					if (val == 0)
					{
						if (Atomic.CompareExchange(ref data.Found[0], 0, 1) == 0)
                                    data.Result[0] = seed;
					}
					val--;
					continue;
				end:
					break;
				}
			}
        }
        internal static void KernelFive(Index1D index, SearchKernelData data)
        {
            long input = data.InputStart + index.X;

		ulong target = data.ability;
		ulong input_ivs = (ulong)input;

		target |= (input_ivs & 0x1F00000ul) << 27;
		target |= (input_ivs & 0xF8000ul) << 22;
		target |= (input_ivs & 0x7C00ul) << 17;
		target |= (input_ivs & 0x3E0ul) << 12;
		target |= (input_ivs & 0x1Ful) << 7;

		target |= ((32ul + data.iv0 - ((input_ivs & 0x1F00000ul) >> 20)) & 0x1F) << 42;
		target |= ((32ul + data.iv1 - ((input_ivs & 0xF8000ul) >> 15)) & 0x1F) << 32;
		target |= ((32ul + data.iv2 - ((input_ivs & 0x7C00ul) >> 10)) & 0x1F) << 22;
		target |= ((32ul + data.iv3 - ((input_ivs & 0x3E0ul) >> 5)) & 0x1F) << 12;
		target |= ((32ul + data.iv4 - (input_ivs & 0x1Ful)) & 0x1F) << 2;

		target |= (input_ivs & 0xE000000ul) << 30;
		target |= ((8ul + data.fixedPos - ((input_ivs & 0xE000000ul) >> 25)) & 7) << 52;

		target ^= data.g_ConstantTermVector;

		ulong processedTarget = 0;
		int offset = 0;
		for (int i = 0; i < data.l; ++i)
		{
			while ((data.g_FreeBit[i + offset] != 0))
			{
				++offset;
			}
			processedTarget |= GetSignature(data.g_AnswerFlag[i] & target) << (63 - (i + offset));
		}

		ulong s0;
		ulong s1;
		ulong s0tmp;
		ulong s1tmp;
		uint ec;
		uint skip;
		int ivs;
		int g_FixedIvs;
		int fixedIndex;
		int tmp;
		ulong seed = 0;
		if (Atomic.CompareExchange(ref data.Found[0], 0, 0) == 0)
			for (int search = data.CoefficientStart; search < data.CoefficientEnd; ++search)
			{
				seed = (processedTarget ^ data.g_CoefficientData[search]) | data.g_SearchPattern[search];
				int val = 3;
				while (val >= 0)
				{
					s0 = seed + data.add_const[val];
					s1 = 0x82a2b175229d6a5b;
					// EC
					do
					{
						ec = (uint)(s0 + s1);
						s1 = s0 ^ s1;
						s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
						s1 = RotateLeft(s1, 37);
					} while (ec == 0xFFFFFFFF);

					if (data.characteristics[val] >= 0)
					{
						int characteristic = data.characteristicorder[val * 6 + ec % 6];
						if (characteristic != data.characteristics[val])
						{
							break;
						}
					}

					// SIDTID
					do
					{
						skip = (uint)(s0 + s1);
						s1 = s0 ^ s1;
						s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
						s1 = RotateLeft(s1, 37);
					} while (skip == 0xFFFFFFFF);

					// TID
					do
					{
						skip = (uint)(s0 + s1);
						s1 = s0 ^ s1;
						s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
						s1 = RotateLeft(s1, 37);
					} while (skip == 0xFFFFFFFF);

					ivs = 0xC0;
					g_FixedIvs = data.fixedIVs[val];
					fixedIndex = 0;
					while (g_FixedIvs > 0)
					{
						do
						{
							fixedIndex = (int)((s0 + s1) & 7);
							s1 = s0 ^ s1;
							s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
							s1 = RotateLeft(s1, 37);
						} while (((1 << fixedIndex) & ivs) != 0);
						ivs |= 1 << fixedIndex;
						if (data.allIVs[val * 6 + fixedIndex] != 31)
						{
							goto end;
						}
						g_FixedIvs--;
					}

					for (int i = 0; i < 6; ++i)
					{
						if (((1 << i) & ivs) == 0)
						{
							if (data.allIVs[val * 6 + i] != (int)((s0 + s1) & 31))
							{
								goto end;
							}
							s1 = s0 ^ s1;
							s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
							s1 = RotateLeft(s1, 37);
						}
					}
					tmp = 0;
					// special case
					if (data.abilitys[val] == -2)
					{
						s0tmp = s0;
						s1tmp = s1;
						if ((data.HA[val] != 0))
						{
							do
							{
								tmp = (int)((s0 + s1) & 3);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 3);
						}
						else
						{
							tmp = (int)((s0 + s1) & 1);
							s1 = s0 ^ s1;
							s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
							s1 = RotateLeft(s1, 37);
						}
						if (!(data.noGender[val] != 0))
						{
							do
							{
								tmp = (int)((s0 + s1) & 255);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 253);
						}
						tmp = 0;
						if (data.species[val] == ToxtricityID)
						{
							if (data.alt[val] == 0)
							{
								do
								{
									tmp = (int)((s0 + s1) & 15);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 13);
								tmp = data.ToxtricityAmplifiedNatures[tmp];
							}
							else
							{
								do
								{
									tmp = (int)((s0 + s1) & 15);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 12);
								tmp = data.ToxtricityLowKeyNatures[tmp];
							}
						}
						else
						{
							do
							{
								tmp = (int)((s0 + s1) & 31);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 25);
						}
						if (tmp != data.natures[val])
						{
							s0 = s0tmp;
							s1 = s1tmp;
							if (!(data.noGender[val] != 0))
							{
								do
								{
									tmp = (int)((s0 + s1) & 255);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 253);
							}
							tmp = 0;
							if (data.species[val] == ToxtricityID)
							{
								if (data.alt[val] == 0)
								{
									do
									{
										tmp = (int)((s0 + s1) & 15);
										s1 = s0 ^ s1;
										s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
										s1 = RotateLeft(s1, 37);
									} while (tmp >= 13);
									tmp = data.ToxtricityAmplifiedNatures[tmp];
								}
								else
								{
									do
									{
										tmp = (int)((s0 + s1) & 15);
										s1 = s0 ^ s1;
										s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
										s1 = RotateLeft(s1, 37);
									} while (tmp >= 12);
									tmp = data.ToxtricityLowKeyNatures[tmp];
								}
							}
							else
							{
								do
								{
									tmp = (int)((s0 + s1) & 31);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 25);
							}
							if (tmp != data.natures[val])
							{
								break;
							}
						}

					}
					else
					{
						if ((data.HA[val] != 0))
						{
							do
							{
								tmp = (int)((s0 + s1) & 3);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 3);
							if (data.abilitys[val] != -1 && data.abilitys[val] != tmp) break;
						}
						else
						{
							tmp = (int)((s0 + s1) & 1);
							s1 = s0 ^ s1;
							s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
							s1 = RotateLeft(s1, 37);
							if (data.abilitys[val] != -1 && data.abilitys[val] != tmp) break;
						}

						if (!(data.noGender[val] != 0))
						{
							do
							{
								tmp = (int)((s0 + s1) & 255);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 253);
						}

						tmp = 0;
						if (data.species[val] == ToxtricityID)
						{
							if (data.alt[val] == 0)
							{
								do
								{
									tmp = (int)((s0 + s1) & 15);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 13);
								tmp = data.ToxtricityAmplifiedNatures[tmp];
							}
							else
							{
								do
								{
									tmp = (int)((s0 + s1) & 15);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 12);
								tmp = data.ToxtricityLowKeyNatures[tmp];
							}
						}
						else
						{
							do
							{
								tmp = (int)((s0 + s1) & 31);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 25);
						}
						if (tmp != data.natures[val])
						{
							break;
						}
					}
					if (val == 0)
					{
						if (Atomic.CompareExchange(ref data.Found[0], 0, 1) == 0)
                                    data.Result[0] = seed;
					}
					val--;
					continue;
				end:
					break;
				}
			}
        }
        internal static void KernelFour(Index1D index, SearchKernelData data)
        {
            long input = data.InputStart + index.X;

		ulong target = data.ability;
		ulong input_ivs = (ulong)input;
		target |= (input_ivs & 0xF8000ul) << 22;
		target |= (input_ivs & 0x7C00ul) << 17;
		target |= (input_ivs & 0x3E0ul) << 12;
		target |= (input_ivs & 0x1Ful) << 7;

		target |= ((32ul + data.iv0 - ((input_ivs & 0xF8000ul) >> 15)) & 0x1F) << 32;
		target |= ((32ul + data.iv1 - ((input_ivs & 0x7C00ul) >> 10)) & 0x1F) << 22;
		target |= ((32ul + data.iv2 - ((input_ivs & 0x3E0ul) >> 5)) & 0x1F) << 12;
		target |= ((32ul + data.iv3 - (input_ivs & 0x1Ful)) & 0x1F) << 2;

		target |= (input_ivs & 0x700000ul) << 25;
		target |= ((8ul + data.fixedPos - ((input_ivs & 0x700000ul) >> 20)) & 7) << 42;

		target ^= data.g_ConstantTermVector;

		ulong processedTarget = 0;
		int offset = 0;
		for (int i = 0; i < data.l; ++i)
		{
			while ((data.g_FreeBit[i + offset] != 0))
			{
				++offset;
			}
			processedTarget |= GetSignature(data.g_AnswerFlag[i] & target) << (63 - (i + offset));
		}

		ulong s0;
		ulong s1;
		ulong s0tmp;
		ulong s1tmp;
		uint ec;
		uint skip;
		int ivs;
		int g_FixedIvs;
		int fixedIndex;
		int tmp;
		ulong seed = 0;
		if (Atomic.CompareExchange(ref data.Found[0], 0, 0) == 0)
			for (int search = data.CoefficientStart; search < data.CoefficientEnd; ++search)
			{
				seed = (processedTarget ^ data.g_CoefficientData[search]) | data.g_SearchPattern[search];
				int val = 3;
				while (val >= 0)
				{
					s0 = seed + data.add_const[val];
					s1 = 0x82a2b175229d6a5b;
					// EC
					do
					{
						ec = (uint)(s0 + s1);
						s1 = s0 ^ s1;
						s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
						s1 = RotateLeft(s1, 37);
					} while (ec == 0xFFFFFFFF);

					if (data.characteristics[val] >= 0)
					{
						int characteristic = data.characteristicorder[val * 6 + ec % 6];
						if (characteristic != data.characteristics[val])
						{
							break;
						}
					}

					// SIDTID
					do
					{
						skip = (uint)(s0 + s1);
						s1 = s0 ^ s1;
						s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
						s1 = RotateLeft(s1, 37);
					} while (skip == 0xFFFFFFFF);

					// TID
					do
					{
						skip = (uint)(s0 + s1);
						s1 = s0 ^ s1;
						s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
						s1 = RotateLeft(s1, 37);
					} while (skip == 0xFFFFFFFF);

					ivs = 0xC0;
					g_FixedIvs = data.fixedIVs[val];
					fixedIndex = 0;
					while (g_FixedIvs > 0)
					{
						do
						{
							fixedIndex = (int)((s0 + s1) & 7);
							s1 = s0 ^ s1;
							s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
							s1 = RotateLeft(s1, 37);
						} while (((1 << fixedIndex) & ivs) != 0);
						ivs |= 1 << fixedIndex;
						if (data.allIVs[val * 6 + fixedIndex] != 31)
						{
							goto end;
						}
						g_FixedIvs--;
					}

					for (int i = 0; i < 6; ++i)
					{
						if (((1 << i) & ivs) == 0)
						{
							if (data.allIVs[val * 6 + i] != (int)((s0 + s1) & 31))
							{
								goto end;
							}
							s1 = s0 ^ s1;
							s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
							s1 = RotateLeft(s1, 37);
						}
					}
					tmp = 0;
					// special case
					if (data.abilitys[val] == -2)
					{
						s0tmp = s0;
						s1tmp = s1;
						if ((data.HA[val] != 0))
						{
							do
							{
								tmp = (int)((s0 + s1) & 3);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 3);
						}
						else
						{
							tmp = (int)((s0 + s1) & 1);
							s1 = s0 ^ s1;
							s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
							s1 = RotateLeft(s1, 37);
						}
						if (!(data.noGender[val] != 0))
						{
							do
							{
								tmp = (int)((s0 + s1) & 255);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 253);
						}
						tmp = 0;
						if (data.species[val] == ToxtricityID)
						{
							if (data.alt[val] == 0)
							{
								do
								{
									tmp = (int)((s0 + s1) & 15);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 13);
								tmp = data.ToxtricityAmplifiedNatures[tmp];
							}
							else
							{
								do
								{
									tmp = (int)((s0 + s1) & 15);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 12);
								tmp = data.ToxtricityLowKeyNatures[tmp];
							}
						}
						else
						{
							do
							{
								tmp = (int)((s0 + s1) & 31);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 25);
						}
						if (tmp != data.natures[val])
						{
							s0 = s0tmp;
							s1 = s1tmp;
							if (!(data.noGender[val] != 0))
							{
								do
								{
									tmp = (int)((s0 + s1) & 255);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 253);
							}
							tmp = 0;
							if (data.species[val] == ToxtricityID)
							{
								if (data.alt[val] == 0)
								{
									do
									{
										tmp = (int)((s0 + s1) & 15);
										s1 = s0 ^ s1;
										s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
										s1 = RotateLeft(s1, 37);
									} while (tmp >= 13);
									tmp = data.ToxtricityAmplifiedNatures[tmp];
								}
								else
								{
									do
									{
										tmp = (int)((s0 + s1) & 15);
										s1 = s0 ^ s1;
										s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
										s1 = RotateLeft(s1, 37);
									} while (tmp >= 12);
									tmp = data.ToxtricityLowKeyNatures[tmp];
								}
							}
							else
							{
								do
								{
									tmp = (int)((s0 + s1) & 31);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 25);
							}
							if (tmp != data.natures[val])
							{
								break;
							}
						}

					}
					else
					{
						if ((data.HA[val] != 0))
						{
							do
							{
								tmp = (int)((s0 + s1) & 3);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 3);
							if (data.abilitys[val] != -1 && data.abilitys[val] != tmp) break;
						}
						else
						{
							tmp = (int)((s0 + s1) & 1);
							s1 = s0 ^ s1;
							s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
							s1 = RotateLeft(s1, 37);
							if (data.abilitys[val] != -1 && data.abilitys[val] != tmp) break;
						}

						if (!(data.noGender[val] != 0))
						{
							do
							{
								tmp = (int)((s0 + s1) & 255);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 253);
						}

						tmp = 0;
						if (data.species[val] == ToxtricityID)
						{
							if (data.alt[val] == 0)
							{
								do
								{
									tmp = (int)((s0 + s1) & 15);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 13);
								tmp = data.ToxtricityAmplifiedNatures[tmp];
							}
							else
							{
								do
								{
									tmp = (int)((s0 + s1) & 15);
									s1 = s0 ^ s1;
									s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
									s1 = RotateLeft(s1, 37);
								} while (tmp >= 12);
								tmp = data.ToxtricityLowKeyNatures[tmp];
							}
						}
						else
						{
							do
							{
								tmp = (int)((s0 + s1) & 31);
								s1 = s0 ^ s1;
								s0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
								s1 = RotateLeft(s1, 37);
							} while (tmp >= 25);
						}
						if (tmp != data.natures[val])
						{
							break;
						}
					}
					if (val == 0)
					{
						if (Atomic.CompareExchange(ref data.Found[0], 0, 1) == 0)
                                    data.Result[0] = seed;
					}
					val--;
					continue;
				end:
					break;
				}
			}
        }
    }
}
