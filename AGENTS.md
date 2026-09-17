# SeedSearcher Crown Tundra fork

This nested directory is a separate Git repository. Work here without changing the parent raid-map app or its data. Preserve the Crown Tundra location fixes and the pinned PKHeX assemblies in `SeedSearcher/deps`.

## Build and runtime

- Windows x64, .NET Framework 4.8, WinForms, native C++ v142 CPU DLL.
- GPU backend is ILGPU 1.5.3 CUDA for NVIDIA and experimental OpenCL for AMD/Intel. Never reintroduce Alea or distribute timeout-registry scripts.
- Use `tools/Build-Release.ps1` for an isolated release and `tools/Test.ps1` for restore/build/test. Visual Studio MSBuild with C++ tools is required.
- Derive the main window title from the assembly version; verify it with `tools/ReleaseSmoke.cs` before publishing. Always specify the fork repository explicitly in GitHub CLI release commands.
- Keep packages, native intermediate files, test outputs and release artifacts untracked.
- SeedSearcher dependencies are intentionally pinned to PKHeX 20.10.10 plus the 1.8-pre raid plugin, with the 86 Crown Tundra den coordinates backported from the later plugin data. Do not replace only one of these assemblies with a newer PKHeX/plugin build.
- RTX 4060 Laptop / driver 610.62 is the local hardware validation target. RTX 50-series support is expected through PTX forward compatibility, not hardware-verified.

## Search invariants

- `SeedSearcherGPU.cs` prepares matrices on the host; `SearchKernels.cs` contains four static ILGPU kernels. Preserve unsigned 64-bit wrapping, exact RNG call order, characteristics, Toxtricity forms and day offsets.
- Kernel inputs contain scalar values and device views only. Transfer Boolean flags as bytes; CLR Boolean marshalling is not blittable in this kernel argument struct.
- `GpuSearchSession` owns the accelerator and buffers. Batch both input indices and coefficient ranges. Keep cancellation checks between launches and publish results with an atomic integer flag separate from the seed so zero remains valid.
- Device discovery uses one application-lifetime device catalog with independent CUDA/OpenCL discovery; UI and execution use its same index mapping. There is no maximum GPU architecture allowlist.
- Both native and managed matrix preparation use global scratch state. Public searches are serialized; tests must not run these concurrently inside one process.
- Native CPU six-IV preparation uses 62 equations, matching the encoded candidate. Reset native state on each search.
- UI progress must be posted to its owning WinForms control; distinguish cancellation, exhaustion and failure.

## Validation

The original 384 fixtures retain their expected values and explicitly choose CPU/CUDA/OpenCL through `SEEDSEARCHER_TEST_BACKEND`. Exhaustive four-IV searches are expensive, especially native CPU negative cases. Use the focused runtime tests, representative full searches and bounded native four-IV witness test for routine checks. Do not claim the full suite passed unless its complete TRX report exists. See `docs/ILGPU-validation.md` for actual results and limitations.

## OpenCL prerelease

Version 1.3.2 adds OpenCL GPU discovery (NVIDIA remains CUDA; OpenCL CPU devices are excluded). CUDA/OpenCL contexts are independent so one broken vendor runtime cannot hide other backends. Use `tools/Test.ps1 -Backend OpenCL` for explicit hardware selection. Radeon 780M / Windows driver 31.0.14003.43006 passed focused hardware checks; RX 580 and Intel remain unverified. See `docs/OpenCL-prerelease.md`.
