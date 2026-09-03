# Ahjo.Vulkan.Ngx.Native

Raw P/Invoke bindings over NVIDIA's NGX (DLSS) Vulkan C API, for .NET 10.

## This package does not contain DLSS

It contains **no feature DLL**. `nvngx_dlss.dll` (Windows) and
`libnvidia-ngx-dlss.so.<version>` (Linux) are NVIDIA's, are covered by the
NVIDIA RTX SDKs licence, and are **yours to ship**: download them from
[NVIDIA/DLSS](https://github.com/NVIDIA/DLSS) and place the file beside your
application's executable. Without it, feature creation fails at runtime with
`NVSDK_NGX_Result_FAIL_UnableToInitializeFeature` — the bindings load and the
shim runs, but there is nothing for NGX to load.

Three things follow from that, and they are the consumer's responsibility, not
this package's:

- **Ship the `rel/` build, never the `dev/` build.** The `dev/` feature DLL
  carries an on-screen debug watermark and must never be redistributed.
- **The obligations in `NGX-LICENSE.txt` are yours.** That file is packed at
  the root of this nupkg so it travels with the code, but accepting it is a
  decision you make when you ship the feature DLL.
- **NVIDIA hardware and driver required.** DLSS runs inside the display
  driver. There is no fallback path and no software implementation.

## What the package does contain

| | |
|---|---|
| Managed bindings | 27 `DllImport`s + the NGX enums, structs and 204 UTF-8 parameter-name constants |
| `runtimes/win-x64/native/ahjo_ngx.dll` | the shim, built from `native/ngx/src/` |
| `runtimes/linux-x64/native/libahjo_ngx.so` | same, for Linux |
| `NGX-LICENSE.txt` | NVIDIA RTX SDKs licence text |

The shim exists because NVIDIA ships NGX as a **static** client library with
no DLL to P/Invoke. `ahjo_ngx` links that archive and re-exports 20 of its
symbols verbatim, plus 7 `ahjo_ngx_*` additions that replace the entry points
taking `wchar_t` — whose width differs between Windows and Linux — with UTF-8
equivalents. Nothing wide crosses into managed code.

Only `win-x64` and `linux-x64` exist: those are the only platforms NVIDIA
publishes NGX libraries for.

## Status

This is the raw binding layer. The idiomatic wrapper (`NgxContext`,
`DlssFeature`, and the render-loop integration) is a later phase; see
[issue #214](https://github.com/pekkah/Ahjo-Vulkan/issues/214).

Part of [Ahjo.Vulkan](https://github.com/pekkah/Ahjo-Vulkan). MIT licensed —
which covers this package's own code, not NVIDIA's binaries.
