using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
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
        TestGate.RequireDriver();
        TestGate.RequireHardwareDriver(
            "Software ICD (Mesa lavapipe): hangs inside the driver during image-view creation.");

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
        TestGate.RequireDriver();
        TestGate.RequireHardwareDriver(
            "Software ICD (Mesa lavapipe): hangs inside the driver during image-view creation.");

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

    [Fact]
    public void CreateImage_CubeCompatible_BuildsCubeAndPerFace2DViews()
    {
        TestGate.RequireDriver();
        TestGate.RequireHardwareDriver(
            "Software ICD (Mesa lavapipe): hangs inside the driver during image-view creation.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        // 64×64 RGBA cube with a mip chain: 6 array layers + CubeCompatible
        // → matches the engine's EnvironmentMap (skybox + irradiance +
        // prefiltered specular) image shape.
        using var cube = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType     = VkImageType.VK_IMAGE_TYPE_2D,
                Format        = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width         = 64, Height = 64, Depth = 1,
                MipLevels     = 7, // log2(64) + 1
                ArrayLayers   = 6,
                Samples       = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage         = ImageUsage.Sampled | ImageUsage.TransferDst,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                Flags         = VkImageCreateFlagBits.VK_IMAGE_CREATE_CUBE_COMPATIBLE_BIT,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        Assert.False(cube.IsNull);
        Assert.Equal(6u, cube.ArrayLayers);
        Assert.Equal(7u, cube.MipLevels);

        // Cube view spanning all 6 layers — only valid because
        // CubeCompatible is set on the underlying image.
        using var cubeView = cube.CreateView(device, new ImageViewDescription
        {
            ViewType       = VkImageViewType.VK_IMAGE_VIEW_TYPE_CUBE,
            Aspect         = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            BaseMipLevel   = 0, LevelCount = 7,
            BaseArrayLayer = 0, LayerCount = 6,
        });
        Assert.False(cubeView.IsNull);

        // Per-face 2D views — each face is a single layer slice. The engine
        // renders into these one at a time when prefiltering specular mips.
        for (uint face = 0; face < 6; face++)
        {
            using var faceView = cube.CreateView(device, new ImageViewDescription
            {
                ViewType       = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
                Aspect         = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                BaseMipLevel   = 0, LevelCount = 1,
                BaseArrayLayer = face, LayerCount = 1,
            });
            Assert.False(faceView.IsNull);
        }
    }

    [Fact]
    public void CreateImage_DefaultFlags_StaysExclusive()
    {
        TestGate.RequireDriver();
        TestGate.RequireHardwareDriver(
            "Software ICD (Mesa lavapipe): hangs inside the driver during image-view creation.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        // Sanity: a regular 2D image with Flags unset (default 0) still
        // creates without issue — the Flags addition didn't regress the
        // common path.
        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType     = VkImageType.VK_IMAGE_TYPE_2D,
                Format        = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width         = 32, Height = 32, Depth = 1,
                MipLevels     = 1, ArrayLayers = 1,
                Samples       = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage         = ImageUsage.Sampled,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });
        Assert.False(image.IsNull);
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
