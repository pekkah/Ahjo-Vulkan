using System.IO;
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Covers the non-templated descriptor-write path (issue 59):
/// <see cref="DescriptorWrite"/> + <see cref="DescriptorSetExtensions.Update"/>
/// + <see cref="CommandRecorder.PushDescriptorSet(VkPipelineBindPoint, in PipelineLayout, uint, ReadOnlySpan{DescriptorWrite})"/>.
/// Two patterns exercised: bindless single-element write at a non-zero
/// <c>dstArrayElement</c>, and a non-templated push-descriptor flow on
/// the compute fill pipeline.
/// </summary>
public sealed unsafe class DescriptorWriteTests
{
    [Fact]
    public void DescriptorWrite_BufferFactory_PopulatesBufferKind()
    {
        var bufWrite = new BufferDescriptorWrite((VkBuffer_T*)0x1234, offset: 16, range: 64);
        DescriptorWrite w = DescriptorWrite.Buffer(
            binding: 3, arrayElement: 7,
            VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER, in bufWrite);

        Assert.Equal(3u, w._binding);
        Assert.Equal(7u, w._arrayElement);
        Assert.Equal(VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER, w._type);
        Assert.Equal(DescriptorWrite.Kind.Buffer, w._kind);
        Assert.Equal((nint)0x1234, (nint)w._buffer.Buffer);
        Assert.Equal(16ul, w._buffer.Offset);
        Assert.Equal(64ul, w._buffer.Range);
    }

    [Fact]
    public void DescriptorWrite_CombinedImageSampler_HoldsSamplerAndView()
    {
        // Synthetic non-null pointers; no driver call here, just the
        // factory's plumbing.
        var img = new ImageDescriptorWrite(
            (VkSampler_T*)0x1000,
            (VkImageView_T*)0x2000,
            VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL);
        DescriptorWrite w = DescriptorWrite.CombinedImageSampler(
            binding: 0, arrayElement: 0, in img);

        Assert.Equal(DescriptorWrite.Kind.Image, w._kind);
        Assert.Equal(VkDescriptorType.VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER, w._type);
        Assert.Equal((nint)0x1000, (nint)w._image.Sampler);
        Assert.Equal((nint)0x2000, (nint)w._image.View);
    }

    /// <summary>
    /// End-to-end: the same compute fill shader the templated tests
    /// exercise, driven through <see cref="CommandRecorder.PushDescriptorSet"/>
    /// with a non-templated <see cref="DescriptorWrite"/> span.
    /// </summary>
    [Fact]
    public void PushDescriptorSet_FillBuffer_NonTemplated_RoundTrips()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipWhen(VulkanDriverProbe.IsSoftwareDriver,
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");
        Assert.SkipUnless(File.Exists(FillSpvPath), $"fill.comp.spv missing at {FillSpvPath}.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        using var blob   = SpirvBlob.Load(FillSpvPath);
        using var module = device.CreateShaderModule(blob.Words);

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
            Bindings       = bindings,
            PushDescriptor = true,
        });

        DescriptorSetLayout[] layouts = [setLayout];
        PushConstantRange[]   ranges  = [PushConstantRange.For<PushBlock>(ShaderStages.Compute)];
        using var pipelineLayout = device.CreatePipelineLayout(new PipelineLayoutDescription
        {
            SetLayouts         = layouts,
            PushConstantRanges = ranges,
        });

        using var pipeline = device.BuildComputePipeline()
            .WithShader(in module)
            .WithLayout(in pipelineLayout)
            .Build();

        const uint Count = 256;
        using var buffer = device.Allocator.CreateBuffer(
            new BufferDescription { Size = Count * sizeof(uint), Usage = BufferUsage.StorageBuffer },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
            });

        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        var fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                rec.BindPipeline(in pipeline);

                // Non-templated push descriptor: one storage-buffer write.
                var bufferInfo = BufferDescriptorWrite.Of(in buffer);
                DescriptorWrite[] writes =
                [
                    DescriptorWrite.Buffer(
                        binding: 0, arrayElement: 0,
                        VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                        in bufferInfo),
                ];
                rec.PushDescriptorSet(
                    VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_COMPUTE,
                    in pipelineLayout, set: 0, writes);

                var pc = new PushBlock { Count = Count };
                rec.PushConstants(in pipelineLayout, ShaderStages.Compute, in pc);
                rec.Dispatch(groupCountX: (Count + 63) / 64);

                var queue = device.GetQueue(family, 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally { fencePool.Release(fence); }

        ReadOnlySpan<uint> data = buffer.AsReadOnlySpan<uint>();
        for (int i = 0; i < (int)Count; i++)
            Assert.Equal((uint)i, data[i]);
    }

    /// <summary>
    /// Bindless-style write: allocate a 32-element storage-buffer array,
    /// update element 5 only via <see cref="DescriptorSetExtensions.Update"/>
    /// at <c>dstArrayElement = 5</c>. The update must succeed without
    /// touching the other 31 entries (which the validation layer would
    /// flag if descriptorCount were treated as the whole array).
    /// </summary>
    [Fact]
    public void DescriptorSet_Update_BindlessSingleElement_AtArrayElement5()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.SupportsBindlessStorageBuffer,
            "Device does not advertise descriptorBindingPartiallyBound + " +
            "descriptorBindingStorageBufferUpdateAfterBind; this bindless storage-buffer test requires both.");

        using var instance = Instance.Create(default);
        using var device   = CreateBindlessGraphicsDevice(instance, out uint _);

        // 32-element array binding mirroring the engine's bindless table.
        DescriptorBinding[] bindings =
        [
            new DescriptorBinding
            {
                Slot   = 0,
                Type   = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                Count  = 32,
                Stages = ShaderStages.Compute,
                BindingFlags = DescriptorBindingFlags.PartiallyBound | DescriptorBindingFlags.UpdateAfterBind,
            },
        ];
        using var setLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            UpdateAfterBindPool = true,
            Bindings            = bindings,
        });

        ReadOnlySpan<VkDescriptorPoolSize> sizes =
        [
            new VkDescriptorPoolSize
            {
                type            = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                descriptorCount = 32,
            },
        ];
        using var pool = new DescriptorSetPool(device, maxSets: 1, sizes, updateAfterBind: true);

        using var buffer = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 256, Usage = BufferUsage.StorageBuffer },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        DescriptorSet set = pool.Acquire(setLayout.Handle);
        try
        {
            var info = BufferDescriptorWrite.Of(in buffer);
            ReadOnlySpan<DescriptorWrite> writes =
            [
                DescriptorWrite.Buffer(
                    binding: 0, arrayElement: 5,
                    VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                    in info),
            ];
            // Update succeeds — validates the dstArrayElement plumbing
            // and the single-write path for bindless tables.
            set.Update(device, writes);
        }
        finally { pool.Release(setLayout.Handle, set); }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Size = 16)]
    private struct PushBlock
    {
        public uint Count;
    }

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
            Queues = [new QueueRequest(f, count: 1, priority: 1.0f)],
        });
    }

    // Same picker as CreateGraphicsDevice but also enables the
    // descriptor-indexing bits the bindless storage-buffer test needs.
    // The wrapper does not turn these on by default — callers that ask
    // for UPDATE_AFTER_BIND_POOL + PARTIALLY_BOUND must opt in explicitly,
    // otherwise vkCreateDescriptorSetLayout / driver paths SIGSEGV (seen
    // on SwiftShader Linux) when the binding flags reference features
    // the device was never told to grant.
    private static Device CreateBindlessGraphicsDevice(Instance instance, out uint family)
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
            Queues            = [new QueueRequest(f, count: 1, priority: 1.0f)],
            ConfigureFeatures = static (
                ref ChainBuilder<VkDeviceCreateInfo> _,
                ref VkPhysicalDeviceFeatures2 _,
                ref VkPhysicalDeviceVulkan12Features f12,
                ref VkPhysicalDeviceVulkan13Features _,
                ref VkPhysicalDeviceVulkan14Features _) =>
            {
                f12.descriptorBindingPartiallyBound              = 1;
                f12.descriptorBindingStorageBufferUpdateAfterBind = 1;
            },
        });
    }
}

