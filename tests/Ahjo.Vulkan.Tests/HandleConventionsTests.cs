using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Locks down the <see cref="IVulkanHandle{TSelf}"/> contract: handles are
/// constructible from a raw pointer through constrained generic dispatch,
/// round-trip without loss, and report the right Vulkan object type.
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
    public void FromRawHandles_Dispose_IsNoOp()
    {
        // A FromRaw'd (borrowed) handle carries no owning device/allocator,
        // so Dispose must short-circuit rather than dispatch
        // vkDestroy*/vmaDestroy* through a null device/allocator (issue #106).
        // A missing guard would segfault the loader trampoline here rather
        // than fail an assertion.
        nint raw = 0x1234_5678;
        Buffer.FromRaw(raw).Dispose();
        Image.FromRaw(raw).Dispose();
        ImageView.FromRaw(raw).Dispose();
        Sampler.FromRaw(raw).Dispose();
        ShaderModule.FromRaw(raw).Dispose();
        DescriptorSetLayout.FromRaw(raw).Dispose();
        PipelineLayout.FromRaw(raw).Dispose();
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

    private static T MakeFromRaw<T>(nint raw) where T : unmanaged, IVulkanHandle<T>
        => T.FromRaw(raw);

    private static VkObjectType ObjectTypeOf<T>() where T : unmanaged, IVulkanHandle<T>
        => T.ObjectType;
}
