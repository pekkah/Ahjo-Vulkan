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

    private static T MakeFromRaw<T>(nint raw) where T : unmanaged, IVulkanHandle<T>
        => T.FromRaw(raw);

    private static VkObjectType ObjectTypeOf<T>() where T : unmanaged, IVulkanHandle<T>
        => T.ObjectType;
}
