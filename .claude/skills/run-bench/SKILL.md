---
name: run-bench
description: Run one or more BenchmarkDotNet benchmarks from tests/Ahjo.Vulkan.Benchmarks with the right Release config and a sensible filter. Use when the user asks to "run the benchmarks", "check allocations for X", "bench Y", or wants to verify the zero-alloc invariant after a hot-path change. Reports where the artifacts landed.
---

# run-bench

Wraps the BenchmarkDotNet harness so a target name turns into the exact `dotnet run` invocation, with Release config and the right filter syntax. The harness's purpose is the **Allocated** column — every `-` is good, anything else is a regression.

## When to use

- After changing a hot-path file (`src/Ahjo.Vulkan/{Recording,Sync,Pools,Memory,Pipelines,Resources}/`).
- When the user says "bench X", "run the X benchmarks", or "check allocations for X".
- To confirm `docs/benchmarks.md` baseline still holds.

Don't use this for one-shot timing curiosity questions — BDN warmup + JIT tiering means a single run takes ~20-60 s per benchmark class. If the user just wants a back-of-envelope number, suggest a `Stopwatch` snippet instead.

## How to translate a target into a filter

The benchmark project is a normal exe; `--filter` accepts BDN glob syntax matched against the fully qualified `Namespace.Class.Method`.

| User says…                    | Filter                                           |
|-------------------------------|--------------------------------------------------|
| `chain builder`               | `*ChainBuilder*`                                 |
| `command recorder`            | `*CommandRecorder*`                              |
| `frame ring`                  | `*FrameRing*`                                    |
| `pipeline barrier`            | `*PipelineBarrier*`                              |
| `push descriptors`            | `*PushDescriptors*`                              |
| `staging`                     | `*StagingUploader*`                              |
| `sync pool`                   | `*SyncPool*`                                     |
| `command buffer pool`         | `*CommandBufferPool*`                            |
| `buffer`                      | `*Buffer*`                                       |
| `graphics pipeline builder`   | `*GraphicsPipelineBuilder*`                      |
| `push constants`              | `*PushConstants*`                                |
| `specialization info`         | `*SpecializationInfo*`                           |
| `result policy`               | `*ResultPolicy*`                                 |
| `physical device picker`      | `*PhysicalDevicePicker*`                         |
| `everything` / `all`          | `*`                                              |
| `host-only` / `no driver`     | `*ChainBuilder*\|*ResultPolicy*`                  |
| `driver-bound`                | `*FrameRing*\|*PushDescriptors*\|*PipelineBarrier*\|*CommandRecorder*\|*StagingUploader*\|*SyncPool*\|*Buffer*` |

When in doubt, look at `tests/Ahjo.Vulkan.Benchmarks/*.cs` for the class names — each file name follows `<Subsystem>Benchmarks.cs` (except `PhysicalDevicePickerBenchmark.cs`, which is singular).

## How to run

Always pass `-c Release`. Debug builds short-circuit JIT tiering and produce noisy + meaningless numbers.

```bash
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "<filter>"
```

Example invocations:

```bash
# After changing ChainBuilder.cs
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*ChainBuilder*"

# Host-only subset (no Vulkan ICD required) — useful in environments without a driver
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*ChainBuilder*|*ResultPolicy*"

# Full sweep — refreshes the docs/benchmarks.md baseline
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*"
```

## Driver dependency

Most benchmarks call into a real Vulkan device at `[GlobalSetup]` and fail there if no ICD is available:

- **Host-only** (no driver needed): `ChainBuilder`, `ResultPolicy`, `SpecializationInfo`, `PushConstants` (partially).
- **Driver-bound** (need `vulkan-1.dll` + an ICD on path): `FrameRing`, `BufferBenchmarks`, `CommandRecorder`, `PipelineBarrier`, `PushDescriptors`, `StagingUploader`, `SyncPool`, `CommandBufferPool`, `GraphicsPipelineBuilder`, `PhysicalDevicePicker`.

On a host with no Vulkan driver, point the user at one of the host-only benchmarks instead, or provision SwiftShader the way CI does (`.github/workflows/ci.yml` is the reference recipe).

## Where artifacts land

BDN writes per-class reports under:

```
BenchmarkDotNet.Artifacts/results/<Namespace.Class>-report-github.md   (best for diffs)
BenchmarkDotNet.Artifacts/results/<Namespace.Class>-report.csv         (machine-readable)
BenchmarkDotNet.Artifacts/results/<Namespace.Class>-report.html
```

The folder is gitignored. After the run, point the user at the `*-report-github.md` file — it's the markdown table BDN produces, which is the same shape as the baseline in `docs/benchmarks.md`.

## Reading the result

The single load-bearing column is **Allocated**. A row that reads `-` is zero managed bytes per op — that's the invariant holding. **Any** number larger than `-` on a hot-path benchmark means a wrapper change leaked an allocation through to the managed heap. Investigate before merging.

Per-iteration Mean is informational. Treat changes <20 % as noise (desktop variance). Investigate larger swings.

## What not to do

- **Don't run BDN in Debug.** Numbers are meaningless. Always `-c Release`.
- **Don't capture timings on CI runners.** Hosted-runner variance makes them unusable; `docs/benchmarks.md` is intentionally a manual capture from a known host. CI does not run benchmarks.
- **Don't claim a regression from a single run** if Mean moved <20 %. Re-run; check allocations; only escalate if allocations changed or the timing swing is large.
- **Don't paste the full HTML report.** Surface the `-report-github.md` table; that's what humans read.
