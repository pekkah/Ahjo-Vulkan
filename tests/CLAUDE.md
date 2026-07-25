# Tests — suites and constraints

| Project | What it covers |
|---|---|
| `Ahjo.Vulkan.Tests/` | xUnit integration suite over the wrapper (needs a Vulkan device) |
| `Ahjo.Vulkan.Native.Tests/` | smoke suite over the raw `vulkan.h` bindings |
| `Ahjo.Vulkan.Vma.Native.Tests/` | VMA binding + native-binary checks (allocation-only on Linux/lavapipe) |
| `Ahjo.Vulkan.Ktx.Native.Tests/` | libktx binding checks — must pass with **no** Vulkan loader/ICD installed |
| `Ahjo.Vulkan.Benchmarks/` | BenchmarkDotNet — the zero-allocation regression canary |

## Rules

- **Benchmarks are the allocation canary.** Every hot-path subsystem has a `[MemoryDiagnoser]` class; `docs/benchmarks.md` is the baseline and every `Allocated` cell should read `-`. Use `/run-bench` to run them correctly (always `-c Release`). CI does not run benchmarks; the baseline is a manual capture.
- **`AHJO_REQUIRE_VULKAN_DEVICE=1`** turns the driverless skip into a hard failure. CI lanes that exist to prove a native binary executes set it so a broken ICD install can't report green while executing nothing. Locally, leave it unset unless you're reproducing a CI lane.
- **Driver-dependent tests skip, not fail, without a device.** Don't convert skips into mocks — issue #32 established that software-rasterizer coverage isn't honest coverage.
- Ktx tests must not acquire a Vulkan device — the package contract ships with both uploaders off (`src/Ahjo.Vulkan.Ktx.Native/CLAUDE.md`).
