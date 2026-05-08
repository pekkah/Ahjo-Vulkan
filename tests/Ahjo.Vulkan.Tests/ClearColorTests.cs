using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Covers <see cref="ClearColor"/> (issue 66): the typed helpers must
/// round-trip through their matching union member without bit-
/// reinterpreting the input.
/// </summary>
public sealed class ClearColorTests
{
    [Fact]
    public void Float_RoundTripsThroughFloat32Member()
    {
        VkClearColorValue v = ClearColor.Float(0.25f, 0.5f, 0.75f, 1.0f);
        Assert.Equal(0.25f, v.float32[0]);
        Assert.Equal(0.5f,  v.float32[1]);
        Assert.Equal(0.75f, v.float32[2]);
        Assert.Equal(1.0f,  v.float32[3]);
    }

    [Fact]
    public void UInt_RoundTripsThroughUint32Member()
    {
        VkClearColorValue v = ClearColor.UInt(1, 2, 3, 4);
        Assert.Equal(1u, v.uint32[0]);
        Assert.Equal(2u, v.uint32[1]);
        Assert.Equal(3u, v.uint32[2]);
        Assert.Equal(4u, v.uint32[3]);
    }

    [Fact]
    public void Int_RoundTripsThroughInt32Member()
    {
        VkClearColorValue v = ClearColor.Int(-1, -2, 3, int.MinValue);
        Assert.Equal(-1, v.int32[0]);
        Assert.Equal(-2, v.int32[1]);
        Assert.Equal(3,  v.int32[2]);
        Assert.Equal(int.MinValue, v.int32[3]);
    }

    /// <summary>
    /// The whole point of the typed helpers: a UINT clear value built
    /// through <see cref="ClearColor.UInt"/> reads back as the same
    /// integer through the <c>uint32</c> member — no bit-reinterpret.
    /// The <see cref="VkClearColorValue"/> float ctor on the same input
    /// would have populated the union as floats and the integer read
    /// would yield garbage; this test pins the safe path.
    /// </summary>
    [Fact]
    public void UInt_DoesNotReinterpretAsFloat()
    {
        VkClearColorValue v = ClearColor.UInt(1, 2, 3, 4);

        // The float view of those bits would be denormalised noise — but
        // the helper should never expose it on the read path. The only
        // contract: the uint slot reads back what we wrote.
        Assert.Equal(1u, v.uint32[0]);

        // Conversely a Float helper writing 1f through the float slot
        // produces a uint slot with float bit pattern 0x3F800000 — proves
        // the union overlaps at offset 0.
        VkClearColorValue f = ClearColor.Float(1f, 0f, 0f, 0f);
        Assert.Equal(0x3F800000u, f.uint32[0]);
    }
}
