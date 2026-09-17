# ILGPU migration validation

## Environment

- Windows x64; .NET Framework 4.8; Visual Studio 2022 MSBuild with native C++ v142 tools.
- NVIDIA GeForce RTX 4060 Laptop GPU, 8 GB, compute capability 8.9, driver 610.62.
- ILGPU 1.5.3 CUDA, with the native CPU fallback retained.
- RTX 50-series support is expected through PTX forward compatibility, but **not hardware-verified**. Run the same CUDA acceptance suite on a Blackwell card before expanding the verified-hardware list.

## Completed checks

- All four ILGPU search modes returned their expected seeds on actual CUDA hardware.
- **CUDA acceptance: 118/118 passed** in 6.20 minutes (`TestResults/cuda-acceptance.trx`), comprising 106 original fixtures and 12 focused/runtime/native-witness checks. This includes every original one-IV and six-IV fixture and positive/negative representatives of both fixed-IV variants of the four- and five-IV modes.
- **Final focused rebuild: 13/13 passed** (`TestResults/cuda-tests.trx`), including the added host-preparation cancellation check. The current acceptance command consequently selects 119 tests.
- Focused checks passed input/coefficient batch boundaries, final partial batches, atomic multiple-winner handling, zero seed publication, cancellation/restart, buffer reuse, missing-device errors, bounded ILGPU CPU-accelerator execution, Toxtricity forms and day offsets.
- Native CPU full searches passed one-IV, five-IV, and six-IV positive cases and a six-IV negative case. The native four-IV check passed using an independently forward-generated candidate and an incorrect-LSB candidate, avoiding an hours-long scan.
- WinForms initialization and a six-IV CUDA search passed from a fresh release directory. Its embedded resources and filesystem contain no Alea runtime or timeout-registry script.
- A lexical comparison of all four old/new kernel bodies confirmed RNG logic was preserved after accounting for explicit parameter views, bounded coefficient loops and atomic result publication.

## Measured behavior

- Real four-IV search cancellation after compilation completed in 47 ms; restarting the same searcher returned the expected seed.
- Largest measured GPU batch across the acceptance run: 23.6 ms. The adaptive target is 150 ms with conservative initial batches and bounded coefficient work.
- The full native CPU representative run passed 4/4 tests (one-IV, five-IV and positive/negative six-IV) in 2.46 minutes (`cpu-representative.trx`). Native four-IV candidate parity passed separately in 175–195 ms and is also included in CUDA acceptance.

### Clean-package six-IV comparison

The same `Test_Fixed2_6_Deviation0` fixture returned `1FA0517D9F60FC44` through both backends in separate, serial release-smoke processes:

| Backend | Search wall time | Kernel compilation | GPU execution |
| --- | ---: | ---: | ---: |
| ILGPU CUDA | 2.225 s | 1.111 s | 0.729 s |
| Native CPU (100%) | 26.586 s | n/a | n/a |

This is one local fixture, not a general performance guarantee. The GPU time includes cold kernel compilation; GUI startup is outside the search timer. Logs: `artifacts/packaged-cuda-smoke.log` and `artifacts/packaged-cpu-smoke.log`.

## Limits

All 384 original fixtures remain runnable with explicit CPU/CUDA selection. The complete exhaustive suite was not completed: some four-IV negative cases take minutes each on this GPU and substantially longer on the native CPU. Representative searches and the bounded native witness are not evidence that all 384 exhaustive cases passed.

The existing Newtonsoft.Json 12/13 dependency-resolution build warning from the pinned raid plugin remains. The clean-package WinForms initialization and search smoke test pass with the existing dependency set.

## Reproduce

```powershell
.\tools\Test.ps1 -Backend CUDA -Acceptance
.\tools\Test.ps1 -Backend CPU -Acceptance
# Exhaustive original fixtures; allow a long run, especially on CPU.
.\tools\Test.ps1 -Backend CUDA -Filter 'FullyQualifiedName~UnitTest1'
.\tools\Build-Release.ps1
```

TRX reports are written under `TestResults/`. ILGPU trace output separates compilation time, execution time, completed batches and maximum launch duration. Run CPU/GPU benchmarks serially: simultaneous CPU load changes these timings.

# Building and testing

Install Visual Studio 2022 with the C++ v142 tools, a Windows SDK, and the .NET Framework 4.8 targeting pack. The application remains .NET Framework 4.8/x64; the modern .NET SDK alone does not build the native DLL.

```powershell
# Restore packages and create a fresh, zipped release under artifacts/.
.\tools\Build-Release.ps1

# Representative native CPU regressions, including a bounded four-IV witness.
.\tools\Test.ps1 -Backend CPU -Acceptance

# CUDA acceptance across all four modes (several minutes on the RTX 4060).
.\tools\Test.ps1 -Backend CUDA -Acceptance

# GPU runtime/encounter checks, including cancellation and batch boundaries.
.\tools\Test.ps1 -Backend CUDA -Filter 'FullyQualifiedName~GpuRuntimeTests|FullyQualifiedName~GpuEncounterTests'

# All 384 original encounter fixtures on CUDA. Four-IV negative cases can take minutes each.
.\tools\Test.ps1 -Backend CUDA -Filter 'FullyQualifiedName~UnitTest1'
```

`SEEDSEARCHER_TEST_BACKEND` explicitly selects `CPU` or `CUDA` for the original fixtures. Direct test-runner invocation defaults to native CPU. Test processes must be x64. The PowerShell test script copies the freshly built native DLL to the test output and writes TRX reports to `TestResults/`. Run suites serially for meaningful timing comparisons.

The native six-IV matrix preparation was corrected to use the same 62 encoded constraints as the GPU path; the old preparation included two extra constraints without corresponding candidate bits. Native state is reset before every CPU search.
