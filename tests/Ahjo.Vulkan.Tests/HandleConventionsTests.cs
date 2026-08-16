using System.Reflection;
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Locks down the <see cref="IVulkanHandle{TSelf}"/> contract: handles are
/// constructible from a raw pointer through constrained generic dispatch,
/// round-trip without loss, report the right Vulkan object type, and state
/// their ownership (<see cref="IVulkanHandle{TSelf}.OwnsHandle"/>) — a
/// borrowed (<c>FromRaw</c>/<c>default</c>) handle never reaches a
/// <c>vkDestroy*</c>/<c>vmaDestroy*</c> call (issues #118, #106).
/// </summary>
public sealed class HandleConventionsTests
{
    [Fact]
    public void DefaultHandleIsNull()
    {
        Buffer empty = default;
        Assert.True(empty.IsNull);
        Assert.Equal(0UL, empty.RawHandle);
    }

    [Fact]
    public void FromRaw_RoundTripsThroughGenericDispatch()
    {
        nint raw = 0x1234_5678;
        Buffer buffer = MakeFromRaw<Buffer>(raw);

        Assert.False(buffer.IsNull);
        Assert.Equal((ulong)raw, buffer.RawHandle);
    }

    [Fact]
    public void ObjectType_DispatchesStatically()
    {
        Assert.Equal(VkObjectType.VK_OBJECT_TYPE_BUFFER, ObjectTypeOf<Buffer>());
    }

    [Fact]
    public void PhysicalDevice_ObjectType_IsPhysicalDevice()
    {
        Assert.Equal(VkObjectType.VK_OBJECT_TYPE_PHYSICAL_DEVICE, PhysicalDevice.ObjectType);
    }

    [Fact]
    public void Device_ObjectType_IsDevice()
    {
        Assert.Equal(VkObjectType.VK_OBJECT_TYPE_DEVICE, Device.ObjectType);
    }

    [Fact]
    public void Queue_ObjectType_IsQueue()
    {
        Assert.Equal(VkObjectType.VK_OBJECT_TYPE_QUEUE, Queue.ObjectType);
    }

    [Fact]
    public void BorrowContract_HoldsForEveryHandleType()
    {
        // The full matrix (#118): for each of the seventeen IVulkanHandle
        // struct types, a FromRaw'd (borrowed) handle and default(T) report
        // OwnsHandle == false, and — for the IDisposable types — Dispose is
        // a no-op rather than a vkDestroy*/vmaDestroy* dispatched through a
        // null device/allocator (issue #106's crash matrix). A missing
        // guard would segfault the loader trampoline here rather than fail
        // an assertion. Enumerated explicitly (not reflection-discovered)
        // so adding a handle type forces a conscious entry.
        AssertBorrowContract<Buffer>();
        AssertBorrowContract<Image>();
        AssertBorrowContract<ImageView>();
        AssertBorrowContract<Sampler>();
        AssertBorrowContract<ShaderModule>();
        AssertBorrowContract<DescriptorSetLayout>();
        AssertBorrowContract<PipelineLayout>();
        AssertBorrowContract<PipelineCache>();
        AssertBorrowContract<GraphicsPipeline>();
        AssertBorrowContract<ComputePipeline>();
        AssertBorrowContract<Surface>();
        AssertBorrowContract<Fence>();
        AssertBorrowContract<BinarySemaphore>();
        AssertBorrowContract<TimelineSemaphore>();
        AssertBorrowContract<DescriptorSet>();
        AssertBorrowContract<Event>();
        AssertBorrowContract<QueryPool>();
    }

    [Fact]
    public unsafe void OwningHandles_ReportOwnsHandle()
    {
        // Owner pointers are sentinels — these handles must NOT be
        // disposed; the test only asserts the ownership predicate.
        var device   = (VkDevice_T*)0x1000;
        var instance = (VkInstance_T*)0x1000;

        Assert.True(new ImageView((VkImageView_T*)0x2000, device).OwnsHandle);
        Assert.True(new Sampler((VkSampler_T*)0x2000, device).OwnsHandle);
        Assert.True(new ShaderModule((VkShaderModule_T*)0x2000, device).OwnsHandle);
        Assert.True(new DescriptorSetLayout((VkDescriptorSetLayout_T*)0x2000, device).OwnsHandle);
        Assert.True(new PipelineLayout((VkPipelineLayout_T*)0x2000, device).OwnsHandle);
        Assert.True(new PipelineCache((VkPipelineCache_T*)0x2000, device).OwnsHandle);
        Assert.True(new GraphicsPipeline((VkPipeline_T*)0x2000, null, device).OwnsHandle);
        Assert.True(new ComputePipeline((VkPipeline_T*)0x2000, null, device).OwnsHandle);
        Assert.True(new Surface((VkSurfaceKHR_T*)0x2000, instance).OwnsHandle);
        // Event is the first owning handle in Sync/ (#155): its neighbours
        // Fence/BinarySemaphore/TimelineSemaphore are pool-owned, Event is
        // caller-owned and destroys on Dispose.
        Assert.True(new Event((VkEvent_T*)0x2000, device, EventCreateFlags.DeviceOnly).OwnsHandle);
        // QueryPool is caller-owned like Event (#198), not pool-owned.
        Assert.True(new QueryPool((VkQueryPool_T*)0x2000, device, queryCount: 4).OwnsHandle);

        // Pool-owned types never own, even when device-bound.
        Assert.False(new Fence((VkFence_T*)0x2000, device).OwnsHandle);
        Assert.False(new TimelineSemaphore((VkSemaphore_T*)0x2000, device).OwnsHandle);
        Assert.False(new BinarySemaphore((VkSemaphore_T*)0x2000).OwnsHandle);
        Assert.False(new DescriptorSet((VkDescriptorSet_T*)0x2000, (VkDescriptorSetLayout_T*)0x3000).OwnsHandle);
    }

    [Fact]
    public void Event_ObjectType_IsEvent()
    {
        Assert.Equal(VkObjectType.VK_OBJECT_TYPE_EVENT, Event.ObjectType);
    }

    [Fact]
    public void Event_FromRaw_ReportsDeviceOnlyUnknown()
    {
        // false means *unknown* for a borrowed handle — the wrapper never
        // learns a FromRaw'd event's create flags. It must not be read as
        // "this event is host-capable".
        Assert.False(Event.FromRaw(0x1234_5678).IsDeviceOnly);
    }

    [Fact]
    public void QueryPool_ObjectType_IsQueryPool()
    {
        Assert.Equal(VkObjectType.VK_OBJECT_TYPE_QUERY_POOL, QueryPool.ObjectType);
    }

    [Fact]
    public void QueryPool_FromRaw_ReportsQueryCountUnknown()
    {
        // 0 means *unknown* for a borrowed handle, not "empty" — an empty
        // pool cannot be created (VUID-VkQueryPoolCreateInfo-queryCount-02763)
        // and the wrapper never learns a FromRaw'd pool's size.
        Assert.Equal(0u, QueryPool.FromRaw(0x1234_5678).QueryCount);
    }

    [Fact]
    public unsafe void PipelineLayoutMetadata_RidesTheStruct()
    {
        // #118: the declared push ranges / set layouts ride the handle as
        // one managed reference field instead of process-global side
        // tables — copies share the same metadata object, borrowed/default
        // handles carry none, and nothing needs unregistration on dispose.
        var metadata = new PipelineLayoutMetadata
        {
            PushRanges = [new PushConstantRange { Stages = ShaderStages.Vertex, Offset = 0, Size = 16 }],
            SetLayouts = [0x4000],
        };
        var layout = new PipelineLayout((VkPipelineLayout_T*)0x2000, (VkDevice_T*)0x1000, metadata);

        PipelineLayout copy = layout;
        Assert.Same(metadata, copy.Metadata);
        Assert.Null(PipelineLayout.FromRaw(0x2000).Metadata);
        Assert.Null(default(PipelineLayout).Metadata);
    }

    [Fact]
    public void PipelineLayout_HasNoStaticSideTables()
    {
        // Reflection probe (test-side only; the wrapper itself stays
        // AOT-clean): lock the #118 fix in — the metadata must never move
        // back into static state keyed by raw pointer values.
        FieldInfo[] statics = typeof(PipelineLayout).GetFields(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.Empty(statics);
    }

    [Fact]
    public void FromRawDescriptorSetLayout_CreateUpdateTemplate_Throws()
    {
        // CreateUpdateTemplate would dispatch through the null device of a
        // borrowed layout; it must fail loudly instead of crashing (issue #106).
        DescriptorSetLayout layout = DescriptorSetLayout.FromRaw(0x1234_5678);
        Assert.Throws<InvalidOperationException>(
            () => { _ = layout.CreateUpdateTemplate<int>(default); });
    }

    [Fact]
    public void FromRawPipelineLayout_CreatePushDescriptorTemplate_Throws()
    {
        // Same null-device hazard as DescriptorSetLayout.CreateUpdateTemplate:
        // fail loudly on a borrowed layout instead of crashing (issue #106).
        PipelineLayout layout = PipelineLayout.FromRaw(0x1234_5678);
        Assert.Throws<InvalidOperationException>(
            () => { _ = layout.CreatePushDescriptorTemplate<int>(
                0, VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS, default); });
    }

    [Fact]
    public void FromRawFence_DeviceBoundCalls_Throw()
    {
        // A borrowed fence has no DeviceHandle; status/wait/reset would
        // dereference the loader's null dispatch table and AV the process.
        // They must throw a diagnosable managed error instead (issue #102).
        Fence fence = Fence.FromRaw(0x1234_5678);
        Assert.Throws<InvalidOperationException>(() => { _ = fence.IsSignaled; });
        Assert.Throws<InvalidOperationException>(() => { _ = fence.Wait(TimeSpan.Zero); });
        Assert.Throws<InvalidOperationException>(() => fence.Reset());
    }

    [Fact]
    public void FromRawTimelineSemaphore_DeviceBoundCalls_Throw()
    {
        // Same null-device hazard as Fence (issue #102).
        TimelineSemaphore semaphore = TimelineSemaphore.FromRaw(0x1234_5678);
        Assert.Throws<InvalidOperationException>(() => { _ = semaphore.Value; });
        Assert.Throws<InvalidOperationException>(() => semaphore.Signal(1));
        Assert.Throws<InvalidOperationException>(() => { _ = semaphore.WaitFor(1, TimeSpan.Zero); });
    }

    private static void AssertBorrowContract<T>() where T : struct, IVulkanHandle<T>
    {
        nint raw = unchecked((nint)0xDEADBEEF);

        T borrowed = T.FromRaw(raw);
        Assert.False(borrowed.IsNull);
        Assert.Equal((ulong)raw, borrowed.RawHandle);
        Assert.False(borrowed.OwnsHandle);
        Assert.False(default(T).OwnsHandle);

        // Borrowed Dispose is a no-op by contract — a regressed guard
        // would dispatch a destroy through a null owner and crash here.
        if (borrowed is IDisposable disposable) disposable.Dispose();
    }

    private static T MakeFromRaw<T>(nint raw) where T : struct, IVulkanHandle<T>
        => T.FromRaw(raw);

    private static VkObjectType ObjectTypeOf<T>() where T : struct, IVulkanHandle<T>
        => T.ObjectType;
}
