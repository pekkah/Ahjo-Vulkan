# Native AOT notes

Tracked under issue 28. The wrapper publishes cleanly under `PublishAot=true` and the smoke binary at `samples/AotSmoke/` runs the full HeadlessTriangle workload (allocator + image + buffer + pipeline + cmd recorder + fence wait + PNG dump) when a Vulkan driver is present.

Since issue 166 the smoke binary also **compiles its own shader at startup** with `Ahjo.Vulkan.Slang`, so the ILC run covers the Slang compiler wrapper and its C++ vtable binding rather than only the Vulkan surface. That part needs no ICD, so it executes on a driverless host too — verified on linux-x64: `dotnet publish -c Release -r linux-x64` reports zero trim/AOT warnings, and the published native binary loads `libslang.so`, compiles `Shaders/triangle.slang` and reports both entry points before the driver probe.

## Why the wrapper is AOT-safe today

The wrapper's interop surface was designed AOT-first. The patterns that block AOT — runtime codegen, marshaller-by-attribute, reflection-driven dispatch — are absent or replaced.

- **No `Marshal.GetFunctionPointerForDelegate`.** All managed callbacks crossing into Vulkan are `[UnmanagedCallersOnly]` static methods (`Instance.DefaultCallback`, `Instance.ManagedCallbackThunk`). The unmanaged ABI is fixed at compile time; ILC emits a real function pointer with no thunking.
- **No `[DllImport]` with managed marshalling.** `Ahjo.Vulkan.Native` and `Ahjo.Vulkan.Vma.Native` use unmanaged-only signatures (raw pointers, `nint`, `VkResult`-style enums). The Vulkan loader is wired through `NativeLibrary.SetDllImportResolver` at module init (a static ctor pattern AOT supports natively).
- **Static abstract dispatch via `IVulkanHandle<T>`.** `T.ObjectType` (debug naming, pool keys) and `T.FromRaw` are static abstracts on a generic interface. The JIT (and ILC) devirtualizes and inlines through the constrained generic — no runtime type lookup, no reflection.
- **No reflection-based serialization or DI.** The wrapper has no JSON, no `Activator.CreateInstance`, no `Type.GetType`, no `MakeGenericType`. The only `System.Reflection` references in the source tree are MSBuild-generated assembly attributes.
- **Delegate `GCHandle.Alloc` + `GCHandle.ToIntPtr` for callback userdata.** `Instance.Create` pins a managed `Action<DebugMessage>` for the lifetime of the messenger so the unmanaged callback thunk can recover it. AOT supports `GCHandle` in full.

## The Slang binding's C++ vtables are AOT-clean too

`Ahjo.Vulkan.Slang.Native` is the first binding in the repo over a C++ interface hierarchy rather than a flat C API, so it is worth stating why that does not reopen the question.

Interface methods dispatch through `delegate* unmanaged[MemberFunction]<…>` loaded out of a `void** lpVtbl` field — a raw function-pointer call the compiler emits directly. There is **no `ComWrappers`, no `[ComImport]`, no `Marshal`, and no runtime type lookup** anywhere in the generated tree; Slang explicitly does not require COM. `CallConvMemberFunction` is the CLR's own modelling of a C++ instance method, and on x64 (the only architecture either shipped RID has) it is ABI-identical to the platform default.

The `[VtblIndex]` and `[NativeTypeName]` attributes ClangSharp emits are `[Conditional("DEBUG")]`, so they are not present in a Release build at all and nothing reads them at runtime — they are review aids for spotting slot drift in a regen diff, not metadata.

`Ahjo.Vulkan.Slang`, the wrapper on top, adds nothing AOT-hostile either: no reflection, no dynamic codegen, no generic instantiation over a runtime `Type`. Its interop is `stackalloc`/`ArrayPool` buffers pinned with `fixed` for the duration of a call, one `NativeMemory` block for the session's search-path array, and `Marshal.PtrToStringUTF8` on the way back — all AOT-supported outright.

`SlangExportDriftTests` deliberately resolves its symbol list through `NativeLibrary.Load` + `NativeLibrary.TryGetExport` against a literal `string[]`, rather than enumerating the binding's `DllImport`s with `Assembly.GetTypes()`. The reflective version would be shorter and would be exactly the pattern this document forbids.

## Local AOT publish

```pwsh
# Set up MSVC link.exe on PATH (or use a Developer PowerShell).
& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"

dotnet publish samples/AotSmoke/AotSmoke.csproj -c Release -r win-x64 -p:IlcUseEnvironmentalTools=true

samples/AotSmoke/bin/x64/Release/net10.0/win-x64/publish/AotSmoke.exe
```

`-p:IlcUseEnvironmentalTools=true` skips the AOT toolchain's `findvcvarsall.bat` lookup and uses the linker on `PATH` instead. Without it (or without the action that imports the dev environment in CI), the bat's `vswhere.exe`-based discovery can fail on some Visual Studio Build Tools layouts and the publish dies in the native-link stage with `MSB3073` / exit code 123. The bat is fragile around paths containing `(x86)`; the env-var path is the documented escape hatch.

## CI

`build-test` (windows-latest) publishes the AOT smoke after the test step. Trim warnings and ILC errors fail the publish — that's the load-bearing regression check. The published exe runs too; on a runner without a Vulkan ICD it prints "no Vulkan driver detected; AOT publish verified, skipping smoke run" and exits 0. A driver-equipped runner would render the triangle and dump `aot-smoke.png` next to the binary.

## Things that would break AOT

If a future PR introduces any of the following, the AOT publish step in CI will start failing — fix the underlying pattern rather than suppress the warning:

- A managed delegate handed to a `DllImport` parameter that takes a function pointer (use `[UnmanagedCallersOnly]` instead).
- `Marshal.GetFunctionPointerForDelegate`, `Marshal.GetDelegateForFunctionPointer`, `Activator.CreateInstance(Type)`, `Type.GetType(string)`.
- `MakeGenericType` / `MakeGenericMethod` runtime instantiation (static-abstract generic dispatch is fine; reflection-driven generic instantiation is not).
- `[DllImport]` with `MarshalAs` on a complex type (string conversion, struct layout transforms). Native interop should stay raw.
- Any package reference that uses runtime IL emit (`System.Reflection.Emit`, dynamic proxy generators).
