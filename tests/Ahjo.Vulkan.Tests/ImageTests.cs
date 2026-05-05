using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class ImageTests
{
    [Fact]
    public void Default_IsNull_DisposeIsNoOp()
    {
        Image i = default;
        Assert.True(i.IsNull);
        i.Dispose();

        ImageView v = default;
        Assert.True(v.IsNull);
        v.Dispose();
    }

    [Fact]
    public void CreateImage_2D_Rgba8_AndDefaultColorView_Roundtrips()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType     = VkImageType.VK_IMAGE_TYPE_2D,
                Format        = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width         = 256,
                Height        = 256,
                Depth         = 1,
                MipLevels     = 1,
                ArrayLayers   = 1,
                Samples       = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage         = ImageUsage.Sampled | ImageUsage.TransferDst,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        Assert.False(image.IsNull);
        Assert.Equal(256u, image.Width);
        Assert.Equal(256u, image.Height);
        Assert.Equal(VkFormat.VK_FORMAT_R8G8B8A8_UNORM, image.Format);

        using var view = image.CreateView(device, new ImageViewDescription
        {
            ViewType       = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
            Aspect         = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            BaseMipLevel   = 0,
            LevelCount     = 1,
            BaseArrayLayer = 0,
            LayerCount     = 1,
        });

        Assert.False(view.IsNull);
        unsafe { Assert.True(view.DeviceHandle == device.Handle); }
    }

    [Fact]
    public void CreateView_DefaultFormat_InheritsFromImage()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType     = VkImageType.VK_IMAGE_TYPE_2D,
                Format        = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width         = 64, Height = 64, Depth = 1,
                MipLevels     = 1, ArrayLayers = 1,
                Samples       = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage         = ImageUsage.Sampled,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        // Format omitted from the description → wrapper inherits the image format.
        using var view = image.CreateView(device, new ImageViewDescription
        {
            ViewType       = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
            Aspect         = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            BaseMipLevel   = 0, LevelCount = 1,
            BaseArrayLayer = 0, LayerCount = 1,
        });

        Assert.False(view.IsNull);
    }

    private static Device CreateGraphicsDevice(Instance instance)
    {
        uint family = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    family = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
    }
}
