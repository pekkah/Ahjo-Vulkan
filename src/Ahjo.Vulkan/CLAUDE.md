# Ahjo.Vulkan wrapper — invariants

The three invariants below are hard constraints on every change in this project. Violating them either breaks CI (AOT, warnings) or ships a silent runtime bug (dangling pointers, per-frame GC pressure).

## UTF-8 string literals for Vulkan `const char*`

Vulkan APIs that take `const char*` (extension names, layer names, application name, debug labels) require a UTF-8, null-terminated, non-GC-movable pointer. The convention is:

```csharp
Utf8Name.FromLiteral("VK_KHR_surface"u8)
```

`"…"u8` literals live in the assembly's read-only data segment — process lifetime, null-terminated, no GC pinning. **Never** round-trip through `string` + `Encoding.UTF8.GetBytes(...)`: the resulting `byte[]` is GC-movable and not null-terminated, so the pointer Vulkan sees will dangle.

`VulkanExtensions.KhrSurface` / `KhrSwapchain` / etc. expose the names the wrapper actively wraps as ready-made `Utf8Name` values — prefer those over re-quoting the literal at each call site.

## Native AOT must stay clean

`samples/AotSmoke/` is published with `PublishAot=true` in CI and the produced exe runs the full render→PNG round-trip. Trim warnings, ILC errors, or runtime trim-related crashes will fail the build.

Forbidden on any code path reachable from the wrapper:

- `Type.MakeGenericType`, `MethodInfo.MakeGenericMethod`
- Reflection-based discovery (`Assembly.GetTypes()`, attribute scans)
- Dynamic code generation (`System.Reflection.Emit`, `DynamicMethod`, expression trees compiled at runtime)
- Anything that triggers `RequiresUnreferencedCodeAttribute` / `RequiresDynamicCodeAttribute`

See `docs/aot-notes.md` for the full inventory of patterns and the trim-attribute approach.

## Zero per-frame allocations on hot paths

Stated explicitly in `README.md`: "Low allocation, raw-pointer friendly, minimal ceremony… perf and zero per-frame allocations take precedence." This is a hard constraint on:

- `Recording/**` — every command-recording call
- `Sync/**` — fence/semaphore operations
- `Pools/**` — `FrameRing`, `CommandBufferPool`, descriptor pools
- `Memory/**` — `StagingUploader`, `MappedRegion`, `ChainBuilder`
- Any other API expected to run inside a per-frame loop

Setup-time allocations (constructors, builder finalization, one-shot config) are fine. The constraint is per-frame, not lifetime.

Watch for the usual leaks on hot paths: LINQ, string interpolation, closures capturing locals, `params T[]` where a `ReadOnlySpan<T>` overload would do, boxing through interface casts.

**Span parameters on `CommandRecorder` must be `scoped`.** The recorder is a `ref struct`, so an un-`scoped` span parameter cannot receive a `stackalloc` — the call site fails with CS9080/CS8350 and the caller is forced into a heap array on exactly the path that is supposed to allocate nothing. Nothing in `Recording/` stores a caller span past the call, so `scoped` is always accurate there. `params ReadOnlySpan<T>` is implicitly scoped and needs no modifier. `tests/Ahjo.Vulkan.Tests/ScopedSpanProbe.cs` is the compile-time guard: every span entry point is called there with a stack span, so dropping the modifier breaks the build.

`tests/Ahjo.Vulkan.Benchmarks/` has a `[MemoryDiagnoser]` benchmark per hot-path subsystem and `docs/benchmarks.md` records the baseline (every `Allocated` cell should read `-`). When changing a hot path, run the matching benchmark (`/run-bench`) or use the `bench-coverage-checker` agent to confirm coverage hasn't slipped. Before a PR that touches Recording/, Sync/, Pools/, Memory/, Resources/, or Pipelines/, also run the `vulkan-validation-reviewer` agent.
