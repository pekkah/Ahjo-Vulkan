using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class QueueFamilyInfoTests
{
    [Fact]
    public void Flags_GraphicsBitSet_SupportsGraphicsTrue()
    {
        var info = new QueueFamilyInfo(
            index: 0,
            flags: VkQueueFlagBits.VK_QUEUE_GRAPHICS_BIT,
            queueCount: 1,
            timestampValidBits: 0,
            minImageTransferGranularity: default);

        Assert.True (info.SupportsGraphics);
        Assert.False(info.SupportsCompute);
        Assert.False(info.SupportsTransfer);
        Assert.False(info.SupportsSparseBinding);
    }

    [Fact]
    public void Flags_AllZero_AllSupportsFalse()
    {
        var info = new QueueFamilyInfo(0, 0, 0, 0, default);

        Assert.False(info.SupportsGraphics);
        Assert.False(info.SupportsCompute);
        Assert.False(info.SupportsTransfer);
        Assert.False(info.SupportsSparseBinding);
    }

    [Fact]
    public void Flags_GraphicsAndCompute_BothBitsRead()
    {
        var both = VkQueueFlagBits.VK_QUEUE_GRAPHICS_BIT | VkQueueFlagBits.VK_QUEUE_COMPUTE_BIT;
        var info = new QueueFamilyInfo(0, both, 1, 0, default);

        Assert.True (info.SupportsGraphics);
        Assert.True (info.SupportsCompute);
        Assert.False(info.SupportsTransfer);
    }
}
