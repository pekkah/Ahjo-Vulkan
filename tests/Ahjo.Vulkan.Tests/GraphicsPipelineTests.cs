using System.IO;
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class GraphicsPipelineTests
{
    [Fact]
    public void Default_GraphicsPipeline_IsNull_DisposeNoOp()
    {
        GraphicsPipeline p = default;
        Assert.True(p.IsNull);
        p.Dispose();
    }

    [Fact]
    public void Builder_MissingStages_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        Assert.Throws<InvalidOperationException>(() => device.BuildGraphicsPipeline().Build());
    }

    [Fact]
    public void Builder_MissingDynamicRendering_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipUnless(File.Exists(VertSpvPath), $"triangle.vert.spv missing at {VertSpvPath}.");
        Assert.SkipUnless(File.Exists(FragSpvPath), $"triangle.frag.spv missing at {FragSpvPath}.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var vBlob = SpirvBlob.Load(VertSpvPath);
        using var fBlob = SpirvBlob.Load(FragSpvPath);
        using var vMod  = device.CreateShaderModule(vBlob.Words);
        using var fMod  = device.CreateShaderModule(fBlob.Words);

        Assert.Throws<InvalidOperationException>(() =>
            device.BuildGraphicsPipeline().WithStages(in vMod, in fMod).Build());
    }

    [Fact]
    public void Builder_TrianglePipeline_RoundTrips()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipUnless(File.Exists(VertSpvPath), $"triangle.vert.spv missing at {VertSpvPath}.");
        Assert.SkipUnless(File.Exists(FragSpvPath), $"triangle.frag.spv missing at {FragSpvPath}.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var vBlob = SpirvBlob.Load(VertSpvPath);
        using var fBlob = SpirvBlob.Load(FragSpvPath);
        using var vMod  = device.CreateShaderModule(vBlob.Words);
        using var fMod  = device.CreateShaderModule(fBlob.Words);

        // Empty pipeline layout — the triangle has no descriptors and no push
        // constants. Layout still has to be non-null for vkCreateGraphicsPipelines.
        using var layout = device.CreatePipelineLayout(default);

        ReadOnlySpan<VkFormat> colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];

        using var pipeline = device.BuildGraphicsPipeline()
            .WithStages(in vMod, in fMod)
            .WithDynamicRendering(colorFormats)
            .WithLayout(in layout)
            .Build();

        Assert.False(pipeline.IsNull);
        unsafe { Assert.True(pipeline.Layout == layout.Handle); }
    }

    [Fact]
    public void Builder_AlphaBlend_Msaa4x_DynamicLineWidth_Builds()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipUnless(File.Exists(VertSpvPath), $"triangle.vert.spv missing at {VertSpvPath}.");
        Assert.SkipUnless(File.Exists(FragSpvPath), $"triangle.frag.spv missing at {FragSpvPath}.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var vBlob = SpirvBlob.Load(VertSpvPath);
        using var fBlob = SpirvBlob.Load(FragSpvPath);
        using var vMod  = device.CreateShaderModule(vBlob.Words);
        using var fMod  = device.CreateShaderModule(fBlob.Words);
        using var layout = device.CreatePipelineLayout(default);

        ReadOnlySpan<VkFormat> colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];
        ColorBlendAttachment[] blends       = [ColorBlendAttachment.AlphaBlend];
        VkDynamicState[] dynamicStates =
        [
            VkDynamicState.VK_DYNAMIC_STATE_VIEWPORT,
            VkDynamicState.VK_DYNAMIC_STATE_SCISSOR,
            VkDynamicState.VK_DYNAMIC_STATE_LINE_WIDTH,
        ];

        using var pipeline = device.BuildGraphicsPipeline()
            .WithStages(in vMod, in fMod)
            .WithDynamicRendering(colorFormats)
            .WithLayout(in layout)
            .WithColorBlend(new ColorBlendDescription { Attachments = blends })
            .WithMultisample(VkSampleCountFlagBits.VK_SAMPLE_COUNT_4_BIT)
            .WithDynamicState(dynamicStates)
            .Build();

        Assert.False(pipeline.IsNull);
    }

    /// <summary>
    /// Regression: WithColorBlend used to silently truncate to the
    /// rendering color-format count — caller asks for 2 attachments
    /// against a 1-color-format pipeline, the second is dropped, the
    /// pipeline builds with wrong blend state. Build now throws so the
    /// mismatch surfaces before vkCreateGraphicsPipelines.
    /// </summary>
    [Fact]
    public void Builder_BlendAttachmentCount_MismatchedColorFormats_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipUnless(File.Exists(VertSpvPath), $"triangle.vert.spv missing at {VertSpvPath}.");
        Assert.SkipUnless(File.Exists(FragSpvPath), $"triangle.frag.spv missing at {FragSpvPath}.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var vBlob = SpirvBlob.Load(VertSpvPath);
        using var fBlob = SpirvBlob.Load(FragSpvPath);
        using var vMod  = device.CreateShaderModule(vBlob.Words);
        using var fMod  = device.CreateShaderModule(fBlob.Words);
        using var layout = device.CreatePipelineLayout(default);

        // Two blend attachments against one color format — build must reject.
        ColorBlendAttachment[] tooManyBlends =
        [
            ColorBlendAttachment.Opaque,
            ColorBlendAttachment.AlphaBlend,
        ];
        ReadOnlySpan<VkFormat> oneFormat = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];

        Exception? caught = null;
        try
        {
            device.BuildGraphicsPipeline()
                .WithStages(in vMod, in fMod)
                .WithDynamicRendering(oneFormat)
                .WithLayout(in layout)
                .WithColorBlend(new ColorBlendDescription { Attachments = tooManyBlends })
                .Build();
        }
        catch (Exception ex) { caught = ex; }
        Assert.IsType<InvalidOperationException>(caught);
    }

    /// <summary>
    /// Symmetric guard: WithColorBlend with one attachment against a
    /// pipeline that declared two color formats also fails — would
    /// previously have silently used Opaque for the second attachment,
    /// discarding the caller's intent for that slot.
    /// </summary>
    [Fact]
    public void Builder_BlendAttachmentCount_FewerThanColorFormats_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipUnless(File.Exists(VertSpvPath), $"triangle.vert.spv missing at {VertSpvPath}.");
        Assert.SkipUnless(File.Exists(FragSpvPath), $"triangle.frag.spv missing at {FragSpvPath}.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var vBlob = SpirvBlob.Load(VertSpvPath);
        using var fBlob = SpirvBlob.Load(FragSpvPath);
        using var vMod  = device.CreateShaderModule(vBlob.Words);
        using var fMod  = device.CreateShaderModule(fBlob.Words);
        using var layout = device.CreatePipelineLayout(default);

        ColorBlendAttachment[] oneBlend = [ColorBlendAttachment.AlphaBlend];
        ReadOnlySpan<VkFormat> twoFormats =
        [
            VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
            VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
        ];

        Exception? caught = null;
        try
        {
            device.BuildGraphicsPipeline()
                .WithStages(in vMod, in fMod)
                .WithDynamicRendering(twoFormats)
                .WithLayout(in layout)
                .WithColorBlend(new ColorBlendDescription { Attachments = oneBlend })
                .Build();
        }
        catch (Exception ex) { caught = ex; }
        Assert.IsType<InvalidOperationException>(caught);
    }

    [Fact]
    public void Builder_TessellationStages_RequireBoth()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipUnless(File.Exists(VertSpvPath), $"triangle.vert.spv missing at {VertSpvPath}.");
        Assert.SkipUnless(File.Exists(FragSpvPath), $"triangle.frag.spv missing at {FragSpvPath}.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var vBlob = SpirvBlob.Load(VertSpvPath);
        using var fBlob = SpirvBlob.Load(FragSpvPath);
        using var vMod  = device.CreateShaderModule(vBlob.Words);
        using var fMod  = device.CreateShaderModule(fBlob.Words);
        using var layout = device.CreatePipelineLayout(default);
        ReadOnlySpan<VkFormat> colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];

        // Hand only the tess-control side a non-null module by re-using the
        // vertex shader's handle. The build should fail the new symmetry
        // check before vkCreateGraphicsPipelines ever gets called. Lambdas
        // can't close over the ref-struct builder, so we catch directly.
        ShaderModule control = vMod;
        ShaderModule eval    = default;
        Exception? caught = null;
        try
        {
            device.BuildGraphicsPipeline()
                .WithStages(in vMod, in fMod)
                .WithDynamicRendering(colorFormats)
                .WithLayout(in layout)
                .WithTessellationStages(in control, in eval)
                .Build();
        }
        catch (Exception ex) { caught = ex; }
        Assert.IsType<InvalidOperationException>(caught);
    }

    private static string VertSpvPath =>
        Path.Combine(AppContext.BaseDirectory, "Shaders", "triangle.vert.spv");

    private static string FragSpvPath =>
        Path.Combine(AppContext.BaseDirectory, "Shaders", "triangle.frag.spv");

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
