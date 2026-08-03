# DRAFT — upstream report for shader-slang/slang

> **Not filed.** Tracked in this repo as
> [#181](https://github.com/pekkah/Ahjo-Vulkan/issues/181). The repro source is
> `slang-getbindingrangeimageformat-crash.cpp` beside this file — identical to
> the listing inlined below, kept separately so it can be built and re-run
> against a newer Slang without copy-pasting out of Markdown.
>
> Paste the section below (from "Summary" onward) into a new issue at
> https://github.com/shader-slang/slang/issues/new. Suggested title:
>
> **`spReflectionTypeLayout_getBindingRangeImageFormat` dereferences a null `leafVariable` and crashes the process on an `EXISTENTIAL_VALUE` binding range**
>
> Duplicate check (2026-08-03): searched shader-slang/slang issues and PRs for
> `getBindingRangeImageFormat`, `image format reflection`, `leafVariable`,
> `reflection crash` / `reflection segfault` / `access violation reflection`,
> `existential`, `ParameterBlock crash`, `interface ParameterBlock reflection`,
> `binding range crash`. No issue matching. The only existential-plus-reflection
> issue open is [#12092](https://github.com/shader-slang/slang/issues/12092)
> (existential element *size* under-reported), which is a different defect in a
> different accessor.
>
> **There is, however, an open PR that fixes this incidentally — see
> "Prior art upstream" below. Read that section before filing: it changes the
> ask from "here is a one-line fix" to "please land #11344, or apply the
> one-liner meanwhile, and add a regression test either way."**

---

## Summary

`spReflectionTypeLayout_getBindingRangeImageFormat()` crashes the calling process
when it is asked for the image format of a binding range whose type is
`SLANG_BINDING_TYPE_EXISTENTIAL_VALUE`. There is no error code and no way for a
caller to see it coming: the call simply does not return.

- Windows x64: exit code `0xC0000005` (`STATUS_ACCESS_VIOLATION`)
- Linux x64: `SIGSEGV` (exit 139)

The range in question is the one that the *element scope* of a
`ParameterBlock<ISurface>` reports — i.e. the value buffer of any interface-typed
parameter block. Every other accessor on that same range returns normally
(`getBindingRangeType`, `getBindingRangeBindingCount`,
`getBindingRangeDescriptorSetIndex`,
`getBindingRangeFirstDescriptorRangeIndex`, `isBindingRangeSpecializable`,
`getBindingRangeLeafTypeLayout`, `getBindingRangeLeafVariable`), so a caller that
walks binding ranges generically has no signal that this one accessor is
different.

This is a hard problem for host-side tooling because it presents as a vanished
process rather than as a failure: a test host or an asset pipeline that walks
reflection to build descriptor-set layouts just disappears, with no exception, no
result code, and nothing on stderr.

## Root cause (hypothesis, but the code is unambiguous)

`source/slang/slang-reflection-api.cpp`, `v2026.14.1`, lines 2796–2817 —
unchanged on `master` as of `53b76e6d` (2026-08-01):

```cpp
SLANG_API SlangImageFormat spReflectionTypeLayout_getBindingRangeImageFormat(
    SlangReflectionTypeLayout* typeLayout,
    SlangInt index)
{
    auto typeLayout_ = convert(typeLayout);
    if (!typeLayout_)
        return SLANG_IMAGE_FORMAT_unknown;

    auto extTypeLayout = Slang::getExtendedTypeLayout(typeLayout_);
    if (index < 0)
        return SLANG_IMAGE_FORMAT_unknown;
    if (index >= extTypeLayout->m_bindingRanges.getCount())
        return SLANG_IMAGE_FORMAT_unknown;
    auto& bindingRange = extTypeLayout->m_bindingRanges[index];

    auto leafVar = bindingRange.leafVariable;
    if (auto formatAttrib = leafVar->findModifier<FormatAttribute>())   // <-- leafVar is null here
    {
        return (SlangImageFormat)formatAttrib->format;
    }
    return SLANG_IMAGE_FORMAT_unknown;
}
```

`BindingRangeInfo::leafVariable` (`source/slang/slang-type-layout.h`) is a raw
`VarDeclBase*`, and it is null for an `EXISTENTIAL_VALUE` range — that range
describes a synthesized value buffer, not a declared variable. The repro prints
this directly: `getBindingRangeLeafVariable(element, 0)` returns `nullptr`, and
the very next call on the same `(scope, index)` pair dies.

The neighbouring accessor, `spReflectionTypeLayout_getBindingRangeLeafVariable`
(same file, line 2778), reads the same field and is null-safe by construction
because it just converts and returns it. The format getter is the only one in the
family that dereferences it.

A one-line guard would fix it:

```cpp
    auto leafVar = bindingRange.leafVariable;
    if (!leafVar)
        return SLANG_IMAGE_FORMAT_unknown;
```

That matches the "unknown format" contract the function already uses for every
other early-out, and it is what the function returns for every non-image binding
type anyway.

A regression test would fit naturally alongside
`tools/slang-unit-test/unit-test-image-format-reflection.cpp`, which currently
only exercises ranges that do have a leaf variable.

## Prior art upstream

Searched 2026-08-03. **No issue reports this crash**, but an open PR already
fixes it as a side effect of unrelated work.

[**shader-slang/slang#11344**](https://github.com/shader-slang/slang/pull/11344),
"Add texture format descriptor types" (`csyonghe`, opened 2026-05-28), rewrites
the accessor to delegate to a new helper:

```cpp
    auto leafVar = bindingRange.leafVariable;
    return _getImageFormat(leafVar, bindingRange.leafTypeLayout);
```

and the helper null-checks the variable, which is precisely the guard proposed
above:

```cpp
static SlangImageFormat _getImageFormat(VarDeclBase* varDecl, TypeLayout* typeLayout)
{
    if (typeLayout)
    {
        auto format = _getImageFormatFromType(typeLayout->getType());
        if (format != SLANG_IMAGE_FORMAT_unknown)
            return format;
    }

    if (!varDecl)                                // <-- the fix, arrived at incidentally
        return SLANG_IMAGE_FORMAT_unknown;
    ...
}
```

Both paths are safe for the range in this report: `leafTypeLayout` is non-null
but is an `INTERFACE` layout, so `_getImageFormatFromType`'s
`as<TextureTypeBase>` fails and returns unknown, and control reaches the null
check. The same helper appeared in the closed predecessor PR
[#11163](https://github.com/shader-slang/slang/pull/11163), so it has survived
one respin — it reads as part of the design rather than an artifact of a single
draft.

### Why this does not retire our guard

1. **It has not landed.** As of 2026-08-03 #11344 is `CONFLICTING` with
   `REVIEW_REQUIRED` and was last updated 2026-06-22. Its predecessor #11163 was
   closed unmerged. Nothing about it is imminent.
2. **`master` is still unfixed.** Verified at `master` HEAD on 2026-08-03: the
   unguarded dereference quoted under "Root cause" is present verbatim.
3. **The fix is incidental and unprotected.** Upstream does not know the current
   code crashes. `unit-test-image-format-reflection.cpp` has no existential
   coverage on `master`, and #11344 adds none, so a later refactor of the format
   path could reintroduce the dereference with no test to catch it. This is the
   strongest argument for filing the report even though a fix exists in flight:
   the *test* is what is missing, not the guard.

The guard in `SlangReflection.ImageFormatOf` comes out when a Slang release
containing the fix is **pinned** in `Directory.Build.props` and this repro stops
crashing against it — not when #11344 merges. Re-run the repro to decide; that
is what it is kept for.

## Reproducer

Compiled as C++ only because `slang.h` is not C-includable in this release
(`enum class`, `constexpr`, `namespace`, default arguments). Every Slang call in
the file is a plain C entry point from `slang-deprecated.h` — no C++ interfaces,
no COM, no `slang-rhi`.

`repro_min.cpp`:

```cpp
#include <slang.h>
#include <stdio.h>

static const char* kSource =
    "interface ISurface { float4 shade(float3 n); };\n"
    "struct Glossy : ISurface { float4 tint; float4 shade(float3 n) { return tint; } };\n"
    "ISurface makeGlossy() { Glossy g; g.tint = float4(1.0); return g; }\n"
    "ParameterBlock<ISurface> gSurface;\n"
    "[shader(\"fragment\")]\n"
    "float4 fragmentMain(float3 n : NORMAL) : SV_Target\n"
    "{ return gSurface.shade(n) + makeGlossy().shade(n); }\n";

int main(void)
{
    setvbuf(stdout, NULL, _IONBF, 0);

    SlangSession* session = spCreateSession(NULL);
    SlangCompileRequest* request = spCreateCompileRequest(session);
    int target = spAddCodeGenTarget(request, SLANG_SPIRV);
    spSetTargetProfile(request, target, spFindProfile(session, "spirv_1_5"));
    int tu = spAddTranslationUnit(request, SLANG_SOURCE_LANGUAGE_SLANG, "surface");
    spAddTranslationUnitSourceString(request, tu, "surface.slang", kSource);
    printf("spCompile -> 0x%08X\n%s", (unsigned)spCompile(request), spGetDiagnosticOutput(request));

    // Global scope -> the ParameterBlock binding range -> the block's element scope.
    SlangReflectionTypeLayout* global =
        spReflection_getGlobalParamsTypeLayout(spGetReflection(request));
    SlangReflectionTypeLayout* element = spReflectionTypeLayout_GetElementTypeLayout(
        spReflectionTypeLayout_getBindingRangeLeafTypeLayout(global, 0));

    // The element scope of ParameterBlock<ISurface> reports one binding range,
    // of type SLANG_BINDING_TYPE_EXISTENTIAL_VALUE, whose leaf variable is null.
    printf("element scope kind          = %d (SLANG_TYPE_KIND_INTERFACE = %d)\n",
           (int)spReflectionTypeLayout_getKind(element), (int)SLANG_TYPE_KIND_INTERFACE);
    printf("binding range count         = %d\n",
           (int)spReflectionTypeLayout_getBindingRangeCount(element));
    printf("getBindingRangeType(0)      = %d (SLANG_BINDING_TYPE_EXISTENTIAL_VALUE = %d)\n",
           (int)spReflectionTypeLayout_getBindingRangeType(element, 0),
           (int)SLANG_BINDING_TYPE_EXISTENTIAL_VALUE);
    printf("getBindingRangeBindingCount = %d\n",
           (int)spReflectionTypeLayout_getBindingRangeBindingCount(element, 0));
    printf("getBindingRangeLeafVariable = %p   <-- null\n",
           (void*)spReflectionTypeLayout_getBindingRangeLeafVariable(element, 0));
    printf("getBindingRangeLeafTypeLayout = %p\n",
           (void*)spReflectionTypeLayout_getBindingRangeLeafTypeLayout(element, 0));

    printf("calling getBindingRangeImageFormat ...\n");
    SlangImageFormat format = spReflectionTypeLayout_getBindingRangeImageFormat(element, 0);
    printf("returned %d (not reached on v2026.14.1)\n", (int)format);
    return 0;
}
```

`makeGlossy()` only exists to put a conformance in the linkage so the program
compiles cleanly. Deleting it leaves the crash completely unchanged; it only adds
`error[E50100]: no type conformances found` to the output. The crash is in
reflection, not in code generation.

### Build and run

Against the official `v2026.14.1` release binaries, unpacked to `$SLANG`:

```sh
# Windows (VS 2022 x64 developer prompt), slang-2026.14.1-windows-x86_64.zip
cl /nologo /EHsc /std:c++17 /I %SLANG%\include repro_min.cpp ^
   /link %SLANG%\lib\slang.lib /out:repro_min.exe
set PATH=%SLANG%\bin;%PATH%
repro_min.exe
echo %ERRORLEVEL%

# Linux, slang-2026.14.1-linux-x86_64.tar.gz
g++ -std=c++17 -I $SLANG/include repro_min.cpp -L $SLANG/lib -lslang -o repro_min
LD_LIBRARY_PATH=$SLANG/lib ./repro_min
echo $?
```

`/std:c++17` is required on MSVC — `slang.h` uses inline variables.

## Expected behaviour

`getBindingRangeImageFormat` returns a `SlangImageFormat` for any binding range
index that is in bounds. A range with no `[[vk::image_format]]` /
`[format(...)]` attribute — which includes every `EXISTENTIAL_VALUE` range —
should report `SLANG_IMAGE_FORMAT_unknown` (0), the same as it does for
`CONSTANT_BUFFER`, `SAMPLER`, `PARAMETER_BLOCK`, and every other non-image type.

## Actual behaviour

The process dies inside `slang`. Observed output, Windows x64 (stdout is
unbuffered in the repro, so this is everything that was produced):

```
spCompile -> 0x00000000
element scope kind          = 13 (SLANG_TYPE_KIND_INTERFACE = 13)
binding range count         = 1
getBindingRangeType(0)      = 13 (SLANG_BINDING_TYPE_EXISTENTIAL_VALUE = 13)
getBindingRangeBindingCount = 1
getBindingRangeLeafVariable = 0000000000000000   <-- null
getBindingRangeLeafTypeLayout = 0000049877D82570
calling getBindingRangeImageFormat ...
<process exits, code 0xC0000005>
```

Linux x64, identical up to the pointer formatting, exit code 139 (`SIGSEGV`).
`gdb` puts the fault in the accessor itself, frame 0 — `findModifier` is inlined:

```
Program received signal SIGSEGV, Segmentation fault.
0x00007ffff6d99e36 in spReflectionTypeLayout_getBindingRangeImageFormat ()
   from .../lib/libslang-compiler.so.0.2026.14.1
#0  0x00007ffff6d99e36 in spReflectionTypeLayout_getBindingRangeImageFormat ()
   from .../lib/libslang-compiler.so.0.2026.14.1
#1  0x0000555555555dbc in main (argc=1, argv=0x7fffffffdd98) at repro_min.cpp:...
```

### Contrast: every other call on the same range is fine

A longer instrumented version of the repro dumps the whole walk. On the exact
range that kills the process:

| call | result |
|---|---|
| `getBindingRangeType(element, 0)` | `13` (`EXISTENTIAL_VALUE`) |
| `getBindingRangeBindingCount(element, 0)` | `1` |
| `getBindingRangeDescriptorSetIndex(element, 0)` | `0` |
| `getBindingRangeFirstDescriptorRangeIndex(element, 0)` | `0` |
| `isBindingRangeSpecializable(element, 0)` | `1` |
| `getBindingRangeLeafTypeLayout(element, 0)` | non-null, kind `INTERFACE` |
| `getBindingRangeLeafVariable(element, 0)` | **null** |
| `getBindingRangeImageFormat(element, 0)` | **process dies** |

And the same getter on a range that *does* have a leaf variable — binding range 0
of the global scope, the `PARAMETER_BLOCK` range for `gSurface` — returns `0`
normally in the same run, immediately before the fatal call.

## Environment

| | |
|---|---|
| Slang | `v2026.14.1`, official release binaries (`slang-2026.14.1-windows-x86_64.zip`, `slang-2026.14.1-linux-x86_64.tar.gz`), not a local build |
| `slangc -v` | `2026.14.1` |
| Windows | Windows 11 Pro 26200, x64; MSVC 19.44.35224 (VS Build Tools 2022) |
| Linux | Ubuntu 24.04.2 LTS x86_64 (WSL2), g++ 13.3.0 |
| API used | C API only (`spCreateSession` / `spCreateCompileRequest` / `spReflection*`) |
| Target | `SLANG_SPIRV`, profile `spirv_1_5` |

## Platform specificity

**Not platform-specific — reproduces identically on win-x64 and linux-x64.**
Both were measured, not inferred:

| platform | outcome |
|---|---|
| Windows 11 x64, `slang-2026.14.1-windows-x86_64` | exit `0xC0000005` |
| Ubuntu 24.04 x86_64 (WSL2), `slang-2026.14.1-linux-x86_64` | `SIGSEGV`, exit 139 |

Both runs used the official release archives; the linux tarball's SHA-256 is
`21f2d7847385a770e569fb61b1507a7794d742d97850bce0432bff0032ca005f`. This is
consistent with the root cause: an unconditional null dereference, with no
platform-dependent code on the path.

## Impact / workaround

Any host-side tool that walks binding ranges generically and asks each one for
its image format will die the first time it meets an interface-typed
`ParameterBlock`. The only workaround available to a caller is to never ask:
guard the call on the binding type (`TEXTURE` / `TYPED_BUFFER`, ± the mutable
flag), since those are the only declarations `[[vk::image_format]]` applies to.
That is what we ship today. It is a guard against a crash, though, not against a
wrong answer — there is no result code to check and no way to recover.
