using System.Runtime.InteropServices;

using Xunit;

namespace Ahjo.Vulkan.Ktx.Native.Tests;

/// <summary>
/// Executes the packaged native library. This suite is the reason the
/// per-RID build matrix exists: a binding that compiles proves nothing about
/// a binary nobody has ever loaded, and every failure mode that matters here
/// — a missing runtime dependency, a glibc mismatch, an ISA the host does not
/// implement, a struct layout that drifted from the header — shows up only
/// when real bytes go through the real transcoder.
/// </summary>
public unsafe class KtxSmokeTests
{
    // KTX-Software tests/testimages/color_grid_basis.ktx2 at the pinned tag:
    // 1024x1024, one level, one face, vkFormat VK_FORMAT_UNDEFINED with
    // BasisLZ supercompression — i.e. exactly the shape the container spec
    // requires of a Basis Universal payload before transcoding.
    private const uint FixtureWidth = 1024;
    private const uint FixtureHeight = 1024;

    private static byte[] Fixture() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "color_grid_basis.ktx2"));

    [Fact]
    public void NativeLibraryLoads()
    {
        // The narrowest possible call into the library: no allocation, no
        // file parsing, nothing but symbol resolution. When the native
        // dependency story is broken this is what fails, and it fails with a
        // DllNotFoundException that names the library instead of a
        // transcoder error twelve frames deep.
        var message = Marshal.PtrToStringUTF8((nint)Ktx.ktxErrorString(ktx_error_code_e.KTX_SUCCESS));

        Assert.False(string.IsNullOrEmpty(message));
    }

    [Fact]
    public void ReadsKtx2ContainerMetadata()
    {
        var bytes = Fixture();

        ktxTexture2* texture = null;
        fixed (byte* p = bytes)
        {
            var rc = Ktx.ktxTexture2_CreateFromMemory(p, (nuint)bytes.Length, 0, &texture);
            Assert.Equal(ktx_error_code_e.KTX_SUCCESS, rc);
        }

        try
        {
            Assert.Equal(FixtureWidth, texture->baseWidth);
            Assert.Equal(FixtureHeight, texture->baseHeight);
            Assert.Equal(1u, texture->numLevels);
            Assert.Equal(1u, texture->numFaces);
            Assert.Equal(ktxSupercmpScheme.KTX_SS_BASIS_LZ, texture->supercompressionScheme);

            // Reading the header alone must not be enough to make the payload
            // usable — a Basis payload is undefined-format until transcoded,
            // and that is what the whole D4 path turns on.
            Assert.Equal(0u, texture->vkFormat);
            Assert.True(Ktx.ktxTexture2_NeedsTranscoding(texture));
        }
        finally
        {
            Ktx.ktxTexture2_Destroy(texture);
        }
    }

    [Theory]
    // BC7 is the desktop target: 1024x1024 in 4x4 blocks at 16 B a block.
    [InlineData(ktx_transcode_fmt_e.KTX_TTF_BC7_RGBA, 1024 / 4 * (1024 / 4) * 16)]
    // Uncompressed RGBA is the format-independent control. If BC7 regresses
    // and this one still passes, the fault is in a block-compression path
    // rather than in loading or in the Basis decoder itself.
    [InlineData(ktx_transcode_fmt_e.KTX_TTF_RGBA32, 1024 * 1024 * 4)]
    public void TranscodesBasisPayload(ktx_transcode_fmt_e format, int expectedDataSize)
    {
        var bytes = Fixture();

        ktxTexture2* texture = null;
        fixed (byte* p = bytes)
        {
            var rc = Ktx.ktxTexture2_CreateFromMemory(
                p,
                (nuint)bytes.Length,
                (uint)ktxTextureCreateFlagBits.KTX_TEXTURE_CREATE_LOAD_IMAGE_DATA_BIT,
                &texture);
            Assert.Equal(ktx_error_code_e.KTX_SUCCESS, rc);
        }

        try
        {
            Assert.Equal(ktx_error_code_e.KTX_SUCCESS, Ktx.ktxTexture2_TranscodeBasis(texture, format, 0));

            // A transcode that reports success but leaves the texture asking
            // to be transcoded has not done anything.
            Assert.False(Ktx.ktxTexture2_NeedsTranscoding(texture));
            Assert.NotEqual(0u, texture->vkFormat);

            Assert.Equal((nuint)expectedDataSize, texture->dataSize);
            Assert.True(texture->pData != null);

            // The transcoder is allowed to produce any pixels it likes, but
            // an all-zero buffer means it wrote nothing and the size check
            // above is measuring an allocation rather than a result.
            Assert.True(new ReadOnlySpan<byte>(texture->pData, (int)texture->dataSize).ContainsAnyExcept((byte)0));
        }
        finally
        {
            Ktx.ktxTexture2_Destroy(texture);
        }
    }
}
