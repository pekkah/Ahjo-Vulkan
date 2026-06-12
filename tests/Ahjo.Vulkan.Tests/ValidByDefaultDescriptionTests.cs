using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Issue #119 — every description struct is valid-by-default:
/// <c>new Description { … }</c> maps to a valid <c>VkCreateInfo</c> with the
/// "obvious" defaults filled in by field initializers, no call-site
/// normalization required. Subsumes #113 (invalid zero-defaults on creation
/// paths) and #105 (present-mode zero-conflation).
/// </summary>
public sealed class ValidByDefaultDescriptionTests
{
    // ---- Pure default-value round-trips (no GPU) ----

    [Fact]
    public void ImageDescription_Defaults_AreValidImageBaseline()
    {
        var d = new ImageDescription();

        Assert.Equal(VkImageType.VK_IMAGE_TYPE_2D, d.ImageType);
        Assert.Equal(1u, d.Depth);
        Assert.Equal(1u, d.MipLevels);
        Assert.Equal(1u, d.ArrayLayers);
        Assert.Equal(VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT, d.Samples);

        // Object-initializer syntax keeps the defaults for unset fields.
        var partial = new ImageDescription { Format = VkFormat.VK_FORMAT_R8G8B8A8_UNORM, Width = 4, Height = 4 };
        Assert.Equal(1u, partial.MipLevels);
        Assert.Equal(1u, partial.ArrayLayers);
        Assert.Equal(1u, partial.Depth);
        Assert.Equal(VkImageType.VK_IMAGE_TYPE_2D, partial.ImageType);
        Assert.Equal(VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT, partial.Samples);
    }

    [Fact]
    public void ImageViewDescription_Defaults_CoverWholeImageAs2D()
    {
        var d = new ImageViewDescription();

        Assert.Equal(VkImageViewType.VK_IMAGE_VIEW_TYPE_2D, d.ViewType);
        Assert.Equal(Vk.VK_REMAINING_MIP_LEVELS, d.LevelCount);
        Assert.Equal(Vk.VK_REMAINING_ARRAY_LAYERS, d.LayerCount);

        // The old zero-default produced levelCount = 0 (invalid); prove a
        // minimal view description no longer carries a zero count.
        var minimal = new ImageViewDescription { Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT };
        Assert.NotEqual(0u, minimal.LevelCount);
        Assert.NotEqual(0u, minimal.LayerCount);
    }

    [Fact]
    public void DescriptorBinding_Default_CountIsOne()
    {
        Assert.Equal(1u, new DescriptorBinding().Count);
        Assert.Equal(1u, new DescriptorBinding { Slot = 3, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLER }.Count);
    }

    [Fact]
    public void SwapchainDescription_Default_PreferredPresentModeIsFifo()
    {
        // The field initializer (not the zero enum value) supplies FIFO, so
        // an unset present mode is FIFO and an explicit IMMEDIATE (which is the
        // zero enum value) survives — the crux of #105.
        Assert.Equal(VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR, new SwapchainDescription().PreferredPresentMode);

        var immediate = new SwapchainDescription
        {
            PreferredPresentMode = VkPresentModeKHR.VK_PRESENT_MODE_IMMEDIATE_KHR,
        };
        Assert.Equal(VkPresentModeKHR.VK_PRESENT_MODE_IMMEDIATE_KHR, immediate.PreferredPresentMode);
    }

    [Fact]
    public void ImageFromRaw_ReportsUnitSubresourceCounts()
    {
        // FromRaw is valid-by-default too: a wrapped raw handle reports 1 mip,
        // 1 layer, depth 1 (correct for any single image), so the whole-image
        // subresource helpers never see a zero count.
        Image i = Image.FromRaw(0x1234);
        Assert.Equal(1u, i.MipLevels);
        Assert.Equal(1u, i.ArrayLayers);
        Assert.Equal(1u, i.Depth);
    }

    // ---- Creation-path round-trips to valid VkCreateInfo (GPU-gated) ----

    [Fact]
    public void CreateImage_MinimalDescription_Succeeds()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipWhen(VulkanDriverProbe.IsSoftwareDriver,
            "Software ICD (Mesa lavapipe): hangs inside the driver during image-view creation.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        // Only Format / Width / Height / Usage / Tiling set — the field
        // initializers supply a valid mipLevels/arrayLayers/samples/imageType.
        // Pre-#119 this produced mipLevels = 0 (VUID reject).
        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                Format = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width  = 128,
                Height = 128,
                Tiling = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage  = ImageUsage.Sampled | ImageUsage.TransferDst,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        Assert.False(image.IsNull);
        Assert.Equal(1u, image.MipLevels);
        Assert.Equal(1u, image.ArrayLayers);

        // Minimal view description: only Aspect set. Default LevelCount /
        // LayerCount = VK_REMAINING_* cover the whole image — a valid view
        // where the old zero-default would have rejected with levelCount = 0.
        using var view = image.CreateView(device, new ImageViewDescription
        {
            Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
        });
        Assert.False(view.IsNull);
    }

    [Fact]
    public void CreateDescriptorSetLayout_DefaultBindingElement_NormalizesCount()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        // A default(DescriptorBinding) element bypasses the Count = 1 field
        // initializer (it is zeroed, not constructed). The layout build keeps a
        // belt-and-braces Count == 0 ? 1 guard for exactly this case; prove the
        // layout still builds rather than rejecting on descriptorCount = 0.
        var bindings = new DescriptorBinding[1];
        bindings[0] = bindings[0] with
        {
            Slot   = 0,
            Type   = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
            Stages = ShaderStages.Vertex,
        };
        Assert.Equal(0u, bindings[0].Count); // `with` on a default element keeps the zeroed Count.

        using var layout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings = bindings,
        });
        Assert.False(layout.IsNull);
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
