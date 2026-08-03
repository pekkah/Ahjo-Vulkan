using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed unsafe class DescriptorTemplateTests
{
    [Fact]
    public void Default_Template_IsNull_DisposeNoOp()
    {
        DescriptorTemplate<UniformWrites> tmpl = default;
        Assert.True(tmpl.IsNull);
        tmpl.Dispose();
    }

    [Fact]
    public void DescriptorWrite_Sizes_Match_VkDescriptorInfo()
    {
        // Update templates rely on the user struct laying out one
        // 24-byte entry per binding — same shape as the underlying
        // VkDescriptorBufferInfo / VkDescriptorImageInfo.
        Assert.Equal(24, sizeof(BufferDescriptorWrite));
        Assert.Equal(24, sizeof(ImageDescriptorWrite));
        Assert.Equal(24, sizeof(SamplerDescriptorWrite));

        Assert.Equal(sizeof(VkDescriptorBufferInfo), sizeof(BufferDescriptorWrite));
        Assert.Equal(sizeof(VkDescriptorImageInfo),  sizeof(ImageDescriptorWrite));
        Assert.Equal(sizeof(VkDescriptorImageInfo),  sizeof(SamplerDescriptorWrite));
    }

    [Fact]
    public void Layout_Push_UniformImageSampler_BuildsTemplate()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        // Acceptance shape from #39: {uniform buffer, sampled image, sampler}.
        DescriptorBinding[] bindings =
        [
            new DescriptorBinding { Slot = 0, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                                    Count = 1, Stages = ShaderStages.Fragment },
            new DescriptorBinding { Slot = 1, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE,
                                    Count = 1, Stages = ShaderStages.Fragment },
            new DescriptorBinding { Slot = 2, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLER,
                                    Count = 1, Stages = ShaderStages.Fragment },
        ];

        using var setLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings       = bindings,
            PushDescriptor = true,
        });
        DescriptorSetLayout[] layouts = [setLayout];
        using var pipelineLayout = device.CreatePipelineLayout(new PipelineLayoutDescription
        {
            SetLayouts = layouts,
        });

        using var template = pipelineLayout.CreatePushDescriptorTemplate<MaterialDescriptors>(
            set: 0, VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS, bindings);

        Assert.False(template.IsNull);
        // The set index round-trips through the template — push commands
        // forward it back to vkCmdPushDescriptorSetWithTemplate.
        Assert.Equal(0u, GetSet(in template));
    }

    [Fact]
    public void Template_Update_RoundTrips_StorageBufferDispatch()
    {
        TestGate.RequireDriver();
        TestGate.RequireSpirv(FillSpvPath);

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        DescriptorBinding[] bindings =
        [
            new DescriptorBinding
            {
                Slot = 0, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                Count = 1, Stages = ShaderStages.Compute,
            },
        ];

        using var setLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings = bindings,
        });
        DescriptorSetLayout[] layouts = [setLayout];
        PushConstantRange[]   ranges  = [PushConstantRange.For<PushBlock>(ShaderStages.Compute)];
        using var pipelineLayout = device.CreatePipelineLayout(new PipelineLayoutDescription
        {
            SetLayouts         = layouts,
            PushConstantRanges = ranges,
        });

        using var blob   = SpirvBlob.Load(FillSpvPath);
        using var module = device.CreateShaderModule(blob.Words);
        using var pipeline = device.BuildComputePipeline()
            .WithShader(in module).WithLayout(in pipelineLayout).Build();

        const uint Count = 128;
        using var buffer = device.Allocator.CreateBuffer(
            new BufferDescription { Size = Count * sizeof(uint), Usage = BufferUsage.StorageBuffer },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
            });

        ReadOnlySpan<VkDescriptorPoolSize> sizes =
        [
            new VkDescriptorPoolSize { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER, descriptorCount = 1 },
        ];
        using var dsPool = new DescriptorSetPool(device, maxSets: 1, sizes);

        using var template = setLayout.CreateUpdateTemplate<FillWrites>(bindings);

        var set    = dsPool.Acquire(setLayout.Handle);
        var writes = new FillWrites { Out = BufferDescriptorWrite.Of(in buffer) };
        template.Update(in set, in writes);

        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        var fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                rec.BindPipeline(in pipeline);
                DescriptorSet[] descSets = [set];
                rec.BindDescriptorSets(VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_COMPUTE,
                    in pipelineLayout, firstSet: 0, descSets);
                var pc = new PushBlock { Count = Count };
                rec.PushConstants(in pipelineLayout, ShaderStages.Compute, in pc);
                rec.Dispatch(groupCountX: (Count + 63) / 64);

                device.GetQueue(family, 0).Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally { fencePool.Release(fence); }

        ReadOnlySpan<uint> data = buffer.AsReadOnlySpan<uint>();
        for (int i = 0; i < (int)Count; i++)
            Assert.Equal((uint)i, data[i]);
    }

    [Fact]
    public void BuildEntries_FieldCount_Mismatch_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        DescriptorBinding[] bindings =
        [
            new DescriptorBinding { Slot = 0, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                                    Count = 1, Stages = ShaderStages.Vertex },
            new DescriptorBinding { Slot = 1, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE,
                                    Count = 1, Stages = ShaderStages.Fragment },
        ];

        using var setLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings = bindings,
        });

        // UniformWrites has 1 field; bindings has 2 — must throw.
        Assert.Throws<ArgumentException>(() => setLayout.CreateUpdateTemplate<UniformWrites>(bindings));
    }

    /// <summary>
    /// Issue #191 made a zero-binding descriptor set layout legal, but a
    /// template over it is not: Vulkan requires
    /// <c>descriptorUpdateEntryCount &gt; 0</c>. The guard stays, and its message
    /// names the VUID so nobody removes it by analogy with the layout guard.
    /// </summary>
    [Fact]
    public void DescriptorTemplate_EmptyBindings_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        using var setLayout = device.CreateDescriptorSetLayout(default);

        DescriptorBinding[] empty = [];
        var ex = Assert.Throws<ArgumentException>(
            () => setLayout.CreateUpdateTemplate<UniformWrites>(empty));
        Assert.Contains("descriptorUpdateEntryCount", ex.Message, StringComparison.Ordinal);
    }

    private static uint GetSet<T>(in DescriptorTemplate<T> tmpl) where T : unmanaged
    {
        // Reach the internal Set field via reflection-free struct copy +
        // unsafe read so the public surface stays intentionally narrow
        // (Set is meaningful only inside the wrapper).
        ref readonly var r = ref Unsafe.AsRef(in tmpl);
        return Unsafe.As<DescriptorTemplate<T>, TemplateLayout>(ref Unsafe.AsRef(in r)).Set;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TemplateLayout
    {
        public nint Handle;
        public nint DeviceHandle;
        public uint Set;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UniformWrites
    {
        public BufferDescriptorWrite Uniform;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FillWrites
    {
        public BufferDescriptorWrite Out;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MaterialDescriptors
    {
        public BufferDescriptorWrite  Uniform;
        public ImageDescriptorWrite   Texture;
        public SamplerDescriptorWrite Sampler;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PushBlock { public uint Count; }

    private static string FillSpvPath =>
        Path.Combine(AppContext.BaseDirectory, "Shaders", "fill.comp.spv");

    private static Device CreateGraphicsDevice(Instance instance, out uint family)
    {
        uint f = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    f = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });
        family = f;
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
    }
}
