using System.IO;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class SpecializationInfoTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SpecConstants
    {
        public uint LocalSizeX;
        public uint Tag;
    }

    [Fact]
    public void For_DerivesMapEntriesFromFieldLayout()
    {
        var values = new SpecConstants { LocalSizeX = 32, Tag = 0xDEADBEEF };
        var spec   = SpecializationInfo.For<SpecConstants>(in values);

        VkSpecializationMapEntry[] entries = spec.Entries;
        Assert.Equal(2, entries.Length);

        // Constant ID 0 → first field (LocalSizeX), offset 0, size 4.
        Assert.Equal(0u,        entries[0].constantID);
        Assert.Equal(0u,        entries[0].offset);
        Assert.Equal((nuint)4,  entries[0].size);

        // Constant ID 1 → second field (Tag), offset 4, size 4.
        Assert.Equal(1u,        entries[1].constantID);
        Assert.Equal(4u,        entries[1].offset);
        Assert.Equal((nuint)4,  entries[1].size);

        // DataSize is sizeof(T).
        Assert.Equal(8, spec.DataSize);
    }

    [Fact]
    public void For_CachesMapEntriesPerType()
    {
        // Repeated calls hand back the same entry array — the steady-state
        // path is allocation-free on the wrapper side.
        var values = new SpecConstants { LocalSizeX = 64, Tag = 1 };
        var a = SpecializationInfo.For<SpecConstants>(in values);
        var b = SpecializationInfo.For<SpecConstants>(in values);
        Assert.Same(a.Entries, b.Entries);
    }

    [Fact]
    public void Builder_SpecializesLocalSizeAtRuntime()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipUnless(File.Exists(SpecFillSpvPath), $"spec_fill.comp.spv missing at {SpecFillSpvPath}.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        using var blob   = SpirvBlob.Load(SpecFillSpvPath);
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
        using var pipelineLayout = device.CreatePipelineLayout(new PipelineLayoutDescription
        {
            SetLayouts = layouts,
        });

        // Run the pipeline twice with two different specializations and
        // observe that gl_WorkGroupSize.x (slot 0) tracks the spec value.
        Assert.Equal(32u,         RunWithLocalSize(device, in module, in pipelineLayout, family, 32u, tag: 0xC0FFEEu));
        Assert.Equal(0xC0FFEEu,   RunSecondSlot(device, in module, in pipelineLayout, family, 32u, tag: 0xC0FFEEu));
        Assert.Equal(8u,          RunWithLocalSize(device, in module, in pipelineLayout, family, 8u, tag: 0xABCDEF12u));
    }

    private static uint RunWithLocalSize(
        Device device, in ShaderModule module, in PipelineLayout pipelineLayout,
        uint family, uint localSizeX, uint tag)
        => RunAndRead(device, in module, in pipelineLayout, family, localSizeX, tag, slot: 0);

    private static uint RunSecondSlot(
        Device device, in ShaderModule module, in PipelineLayout pipelineLayout,
        uint family, uint localSizeX, uint tag)
        => RunAndRead(device, in module, in pipelineLayout, family, localSizeX, tag, slot: 1);

    private static uint RunAndRead(
        Device device, in ShaderModule module, in PipelineLayout pipelineLayout,
        uint family, uint localSizeX, uint tag, int slot)
    {
        var specValues = new SpecConstants { LocalSizeX = localSizeX, Tag = tag };
        var spec       = SpecializationInfo.For<SpecConstants>(in specValues);

        // SpecializationInfo<T> stores a raw pointer at specValues; specValues
        // must remain on this stack frame until Build() returns. The chained
        // expression below satisfies that on a single frame.
        using var pipeline = device.BuildComputePipeline()
            .WithShader(in module)
            .WithLayout(in pipelineLayout)
            .WithSpecialization(spec)
            .Build();

        const int Count = 16;
        using var buffer = device.Allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = Count * sizeof(uint),
                Usage = BufferUsage.StorageBuffer,
            },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
            });

        using var template = pipelineLayout.CreatePushDescriptorTemplate<FillDescriptors>(
            set: 0, VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_COMPUTE,
            [
                new DescriptorBinding
                {
                    Slot = 0, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                    Count = 1, Stages = ShaderStages.Compute,
                },
            ]);

        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        var fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                rec.BindPipeline(in pipeline);
                var writes = new FillDescriptors { Out = BufferDescriptorWrite.Of(in buffer) };
                rec.PushDescriptors(in template, in pipelineLayout, in writes);
                rec.Dispatch(groupCountX: 1);

                var queue = device.GetQueue(family, 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally { fencePool.Release(fence); }

        return buffer.AsReadOnlySpan<uint>()[slot];
    }

    private static string SpecFillSpvPath =>
        Path.Combine(AppContext.BaseDirectory, "Shaders", "spec_fill.comp.spv");

    [StructLayout(LayoutKind.Sequential)]
    private struct FillDescriptors { public BufferDescriptorWrite Out; }

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
}
