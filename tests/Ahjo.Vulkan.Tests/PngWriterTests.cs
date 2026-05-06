using Ahjo.Vulkan.Utilities;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class PngWriterTests
{
    [Fact]
    public void Writes_2x2_Rgba_With_Valid_Magic_And_IHDR()
    {
        // 2x2 image: red, green / blue, white
        byte[] pixels =
        [
            0xFF, 0x00, 0x00, 0xFF,  0x00, 0xFF, 0x00, 0xFF,
            0x00, 0x00, 0xFF, 0xFF,  0xFF, 0xFF, 0xFF, 0xFF,
        ];

        using var ms = new MemoryStream();
        PngWriter.Write(ms, pixels, width: 2, height: 2);
        byte[] bytes = ms.ToArray();

        // PNG magic
        Assert.Equal((byte)0x89, bytes[0]);
        Assert.Equal((byte)'P',  bytes[1]);
        Assert.Equal((byte)'N',  bytes[2]);
        Assert.Equal((byte)'G',  bytes[3]);

        // IHDR chunk type at offset 12 (after magic + length)
        Assert.Equal((byte)'I', bytes[12]);
        Assert.Equal((byte)'H', bytes[13]);
        Assert.Equal((byte)'D', bytes[14]);
        Assert.Equal((byte)'R', bytes[15]);

        // Width / height fields are big-endian uint32 at 16..20 / 20..24
        uint width  = (uint)((bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19]);
        uint height = (uint)((bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23]);
        Assert.Equal(2u, width);
        Assert.Equal(2u, height);
        Assert.Equal((byte)8, bytes[24]); // bit depth
        Assert.Equal((byte)6, bytes[25]); // color type RGBA
    }

    [Fact]
    public void Length_Mismatch_Throws()
    {
        Assert.Throws<ArgumentException>(() => PngWriter.Write(new MemoryStream(), new byte[15], 2, 2));
    }
}
