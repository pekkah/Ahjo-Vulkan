using System.IO;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class ComputePipelineTests
{
    [Fact]
    public void Default_ComputePipeline_IsNull_DisposeNoOp()
    {
        ComputePipeline p = default;
        Assert.True(p.IsNull);
        p.Dispose();
    }

    [Fact]
    public void Builder_MissingShader_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        Assert.Throws<InvalidOperationException>(() => device.BuildComputePipeline().Build());
    }

    [Fact]
    public void Builder_MissingLayout_Throws()
    {
        TestGate.RequireDriver();
        TestGate.RequireSpirv(FillSpvPath);

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var blob   = SpirvBlob.Load(FillSpvPath);
        using var module = device.CreateShaderModule(blob.Words);

        // Cannot capture ref-struct builder across a delegate, so build inside the lambda.
        Assert.Throws<InvalidOperationException>(() =>
            device.BuildComputePipeline().WithShader(in module).Build());
    }

    [Fact]
    public void Builder_FillShader_Build_RoundTrips()
    {
        TestGate.RequireDriver();
        TestGate.RequireSpirv(FillSpvPath);

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var blob   = SpirvBlob.Load(FillSpvPath);
        using var module = device.CreateShaderModule(blob.Words);

        using var setLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings =
            [
                new DescriptorBinding
                {
                    Slot   = 0,
                    Type   = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                    Count  = 1,
                    Stages = ShaderStages.Compute,
                },
            ],
        });

        DescriptorSetLayout[]   layouts = [setLayout];
        PushConstantRange[] ranges = [PushConstantRange.For<PushBlock>(ShaderStages.Compute)];

        using var pipelineLayout = device.CreatePipelineLayout(new PipelineLayoutDescription
        {
            SetLayouts         = layouts,
            PushConstantRanges = ranges,
        });

        using var pipeline = device.BuildComputePipeline()
            .WithShader(in module)
            .WithLayout(in pipelineLayout)
            .Build();

        Assert.False(pipeline.IsNull);
        unsafe { Assert.True(pipeline.Layout == pipelineLayout.Handle); }
    }

    [Fact]
    public void Builder_CustomEntryPoint_Build_FailsForNonexistent()
    {
        TestGate.RequireDriver();
        TestGate.RequireSpirv(FillSpvPath);

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var blob   = SpirvBlob.Load(FillSpvPath);
        using var module = device.CreateShaderModule(blob.Words);

        using var setLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings =
            [
                new DescriptorBinding
                {
                    Slot = 0, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                    Count = 1, Stages = ShaderStages.Compute,
                },
            ],
        });
        DescriptorSetLayout[] layouts = [setLayout];
        using var pipelineLayout = device.CreatePipelineLayout(new PipelineLayoutDescription { SetLayouts = layouts });

        // The shader's entry point is "main"; passing a different name yields
        // VK_ERROR_INITIALIZATION_FAILED on most drivers (or an internal
        // error in shaderc-validated SPIR-V). Capture either as a wrapper
        // VulkanException; we only care that the typo path is reported.
        Assert.ThrowsAny<VulkanException>(() =>
            device.BuildComputePipeline()
                .WithShader(in module)
                .WithLayout(in pipelineLayout)
                .WithEntryPoint("not_main"u8)
                .Build());
    }

    private static string FillSpvPath =>
        Path.Combine(AppContext.BaseDirectory, "Shaders", "fill.comp.spv");

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Size = 4)]
    private struct PushBlock { }

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
