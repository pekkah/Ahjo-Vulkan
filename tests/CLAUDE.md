# Tests — suites and constraints

| Project | What it covers |
|---|---|
| `Ahjo.Vulkan.Tests/` | xUnit integration suite over the wrapper (needs a Vulkan device) |
| `Ahjo.Vulkan.Native.Tests/` | smoke suite over the raw `vulkan.h` bindings |
| `Ahjo.Vulkan.Vma.Native.Tests/` | VMA binding + native-binary checks (allocation-only on Linux/lavapipe) |
| `Ahjo.Vulkan.Ktx.Native.Tests/` | libktx binding checks — must pass with **no** Vulkan loader/ICD installed |
| `Ahjo.Vulkan.Slang.Native.Tests/` | Slang binding checks — must pass with **no** Vulkan loader/ICD installed |
| `Ahjo.Vulkan.Slang.Tests/` | Slang **wrapper** checks — compile to SPIR-V, diagnostics as exceptions; one driver-gated test feeds the result to `Device.CreateShaderModule` |
| `Ahjo.Vulkan.Benchmarks/` | BenchmarkDotNet — the zero-allocation regression canary |

## Rules

- **Benchmarks are the allocation canary.** Every hot-path subsystem has a `[MemoryDiagnoser]` class; `docs/benchmarks.md` is the baseline and every `Allocated` cell should read `-`. Use `/run-bench` to run them correctly (always `-c Release`). CI does not run benchmarks; the baseline is a manual capture.
- **`AHJO_VULKAN_TIER`** is what a run declares it has: `none` < `software` < `hardware` < `validation` (unset = `none`). `VulkanTierContractTests` fails when the host is below the declaration, so it's the one place a driverless lane goes red. Locally, leave it unset unless you're reproducing a CI lane — or set it deliberately to turn your run into evidence. Full ladder: `docs/ci-coverage.md`. `AHJO_REQUIRE_VULKAN_DEVICE` is retired and now throws if set.
- **New gates go through `Ahjo.Vulkan.Testing.TestGate`, never a bare `Assert.Skip`.** `TestGate.Require{Driver,HardwareDriver,ValidationLayer,Spirv,Platform,DeviceFeature}` / `Unsupported` prefix the reason with a `[gate:*]` class, and CI **fails the job on any unclassified skip** — that's what keeps a driver-gated hole distinguishable from a permanent platform skip.
- **If your feature's only oracle is the validation layer**, run `AHJO_VULKAN_TIER=validation dotnet test tests/Ahjo.Vulkan.Tests` and quote the contract test's `declared=… observed=…` line. "N passed locally" without a tier is indistinguishable from N skips.
- **Driver-dependent tests skip, not fail, without a device.** Don't convert skips into mocks — issue #32 established that software-rasterizer coverage isn't honest coverage.
- Ktx tests must not acquire a Vulkan device — the package contract ships with both uploaders off (`src/Ahjo.Vulkan.Ktx.Native/CLAUDE.md`).
- Slang **native** tests must not acquire a Vulkan device either — Slang compiles shader text to bytes and has no Vulkan surface at all, so the suite references only `Ahjo.Vulkan.Slang.Native`. `SlangExportDriftTests` is the guard on the deprecated reflection header: don't trim its list to make a version bump green, decide what replaces the missing symbol (`src/Ahjo.Vulkan.Slang.Native/CLAUDE.md`).
- `Ahjo.Vulkan.Slang.Tests` is **not** in that no-loader club: it wraps the compiler for wrapper consumers, so it carries `tests/Shared/*.cs` like the other Vulkan-touching suites and its one device test gates on `TestGate.RequireDriver`. Everything else in it still runs driverless — compiling shader text to bytes has no business needing a GPU, and no benchmark covers this project because nothing in it is on a per-frame path (`src/Ahjo.Vulkan.Slang/CLAUDE.md`).
