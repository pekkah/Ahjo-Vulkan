using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Vma.Native;
using VmaApi = Ahjo.Vulkan.Vma.Native.Vma;

namespace Ahjo.Vulkan;

/// <summary>
/// A <c>VkBuffer</c> paired with the VMA allocation that backs it and a
/// reference to the <see cref="Allocator"/> that produced both. Always
/// VMA-allocated; the wrapper has no raw <c>VkBuffer</c> path. Pairing all
/// three on one struct keeps disposal and mapping local — the caller never
/// has to thread the allocator separately.
/// </summary>
/// <remarks>
/// <para>Three pointers + cached metadata; passes by value through registers
/// on x64. <c>default(Buffer)</c> is a legal null handle: <see cref="IsNull"/>
/// returns <see langword="true"/> and <see cref="Dispose"/> is a no-op.
/// Double-dispose is undefined behavior — the struct can't null its own
/// fields (they're <c>readonly</c>).</para>
/// <para>Equality is intentionally not implemented via
/// <see cref="IEquatable{Buffer}"/>. The handle pointer is the identity, but
/// adding equality would imply that two structs sharing the same handle are
/// "the same buffer," which is misleading on a copy-by-value type that can
/// outlive its owning allocator.</para>
/// </remarks>
public readonly unsafe struct Buffer : IVulkanHandle<Buffer>, IDisposable
{
    public readonly VkBuffer_T*      Handle;
    public readonly VmaAllocation_T* AllocationHandle;
    public readonly Allocator        Owner;
    public readonly ulong            Size;
    public readonly BufferUsage      Usage;
    public readonly bool             IsHostVisible;

    internal Buffer(
        VkBuffer_T*      handle,
        VmaAllocation_T* allocation,
        Allocator        owner,
        ulong            size,
        BufferUsage      usage,
        bool             isHostVisible)
    {
        Handle           = handle;
        AllocationHandle = allocation;
        Owner            = owner;
        Size             = size;
        Usage            = usage;
        IsHostVisible    = isHostVisible;
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_BUFFER;

    public static Buffer FromRaw(nint handle) =>
        new((VkBuffer_T*)handle, null, default, 0, BufferUsage.None, false);

    public ulong RawHandle => (ulong)Handle;

    public bool IsNull => Handle == null;

    /// <summary>
    /// GPU virtual address for use with <c>bufferDeviceAddress</c> features.
    /// Caller must have created the buffer with <see cref="BufferUsage.ShaderDeviceAddress"/>.
    /// </summary>
    public ulong GetDeviceAddress(Device device)
    {
        var info = new VkBufferDeviceAddressInfo
        {
            sType  = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_DEVICE_ADDRESS_INFO,
            buffer = Handle,
        };
        return Vk.vkGetBufferDeviceAddress(device.Handle, &info);
    }

    public void Dispose()
    {
        if (Handle == null) return;
        VmaApi.vmaDestroyBuffer(Owner.Handle, Handle, AllocationHandle);
    }
}
