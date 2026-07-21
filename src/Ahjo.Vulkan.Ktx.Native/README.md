# Ahjo.Vulkan.Ktx.Native

Raw P/Invoke bindings for [Khronos libktx](https://github.com/KhronosGroup/KTX-Software) —
KTX2 container reading and Basis Universal transcoding — with native runtime
binaries included for `win-x64` and `linux-x64`.

The managed surface is generated with
[ClangSharpPInvokeGenerator](https://github.com/dotnet/ClangSharp) from `ktx.h` at
a pinned upstream tag. Nothing is hand-written, so the bindings are a mechanical
function of that tag.

```csharp
using Ahjo.Vulkan.Ktx.Native;

unsafe
{
    ktxTexture2* texture;
    fixed (byte* bytes = ktx2File)
    {
        var rc = Ktx.ktxTexture2_CreateFromMemory(
            bytes, (nuint)ktx2File.Length,
            (uint)ktxTextureCreateFlagBits.KTX_TEXTURE_CREATE_LOAD_IMAGE_DATA_BIT,
            &texture);
        if (rc != ktx_error_code_e.KTX_SUCCESS) { /* handle */ }
    }

    if (Ktx.ktxTexture2_NeedsTranscoding(texture))
    {
        Ktx.ktxTexture2_TranscodeBasis(texture, ktx_transcode_fmt_e.KTX_TTF_BC7_RGBA, 0);
    }

    // texture->vkFormat, ->baseWidth/baseHeight, ->numLevels, ->pData, ->dataSize
    // are now the transcoded result. Upload them with your own resource path.
    Ktx.ktxTexture2_Destroy(texture);
}
```

## What is and is not in the native library

The shipped `ktx.dll` / `libktx.so` is built from the pinned upstream tag with
tools, tests, docs, the JNI and Python bindings, and **both texture uploaders**
disabled.

Dropping `KTX_FEATURE_VK_UPLOAD` is the load-bearing choice: this package has **no
Vulkan dependency at all**, and does not reference `Ahjo.Vulkan.Native`. libktx's
own uploader creates its own `VkImage` and records its own barriers, which is
precisely what a consumer that owns its resource pool and its synchronization does
not want. The binding's contract ends at "here are transcoded bytes, a `vkFormat`,
and mip offsets"; getting them onto the GPU is the caller's business.

The OpenGL entry points (`ktxLoadOpenGL`, `ktxTexture_GLUpload`) are therefore
absent from the binary, and are excluded from generation as well — a binding whose
only possible outcome is `EntryPointNotFoundException` is a trap, not a feature.

## Platform support

| RID | Shipped | Notes |
| --- | --- | --- |
| `win-x64` | yes | MSVC, x86-64 |
| `linux-x64` | yes | glibc; built on `ubuntu-latest` |
| `*-arm64`, `osx-*` | no | see below |

arm64 and macOS are deliberately absent rather than untested. libktx links
astc-encoder, whose SIMD backend is selected at compile time, so each RID needs a
lane that actually loads the binary and transcodes before it can ship.

**The x86-64 binaries require AVX2.** That is upstream's default ISA selection for
astc-encoder and matches the binaries Khronos publishes, so this package stays on
the configuration upstream tests. CPUs older than roughly 2013 are out of scope.

## Licensing

libktx is Apache-2.0 (Khronos Group); the bundled Basis Universal transcoder is
Apache-2.0 (Binomial LLC). This package's own binding code is MIT, like the rest
of the Ahjo.Vulkan repository.
