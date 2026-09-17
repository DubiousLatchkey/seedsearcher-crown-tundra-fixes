# 1.3.2 OpenCL prerelease

AMD and Intel GPUs are now discovered through ILGPU 1.5.3 OpenCL. NVIDIA continues to use CUDA; native CPU options remain available. No search algorithm or encounter data was changed. AMD Radeon 780M (OpenCL device name `gfx1103`, Windows driver 31.0.14003.43006) passed 17/17 focused tests, including all four search modes and cancellation/restart; NVIDIA RTX 4060 Laptop also passed 17/17 CUDA regression checks. RX 580 and Intel GPUs have not yet been hardware-verified.

Extract the entire ZIP and launch SeedSearcherGui.exe. Select your card's OpenCL entry in Accelerator before searching. Install the card manufacturer's compatible graphics driver if it is absent. No OpenCL SDK or timeout-registry modification is required. The RX 580 is a candidate test device, not yet a verified one.

Please test a known seed, all available search modes, Stop Search followed by another search, and Show Results. Record the GPU model, driver version, Windows version, selected accelerator, search inputs/export, expected/actual seed, elapsed time, and any error text. A seed found successfully intentionally locks the inputs until New Search.

For developers with Visual Studio and the repository, run:

```powershell
.\tools\Test.ps1 -Backend OpenCL -Acceptance
```

This selects OpenCL explicitly and includes known-seed/negative fixtures across all four modes, boundary/atomic/zero-seed checks, Toxtricity/day offsets, and cancellation/restart. Missing OpenCL hardware produces inconclusive GPU tests, not a successful hardware acceptance result. Run the suite serially; some searches take several minutes. A pass verifies that GPU/driver combination, not every AMD or Intel model.

Additional local checks: 4/4 OpenCL negative-result fixtures passed (one-IV, four-IV, five-IV and six-IV); the final package passed GUI/title initialization and the same known six-IV seed through OpenCL and native CPU. Logs are under ignored artifacts/opencl-prerelease-tests.log and artifacts/opencl-negative-tests.log. The full 384-case suite has not been run.
