using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Wrapper handle for a <c>VkQueue</c>. Owned by a <see cref="Device"/>
/// and produced exclusively by <see cref="PhysicalDevice.CreateDevice"/>;
/// the device caches one instance per <c>(family, index)</c> requested in
/// <see cref="DeviceDescription.Queues"/>.
/// </summary>
/// <remarks>
/// Owner-class shape (sealed class) for the same reason as
/// <see cref="PhysicalDevice"/> and <see cref="Device"/>: queues are
/// created 1–4 times per device and never debug-named or pooled.
/// Construction is internal — outside-the-wrapper construction is
/// meaningless because <c>vkGetDeviceQueue</c> requires a device.
/// </remarks>
public sealed unsafe class Queue
{
    internal readonly VkQueue_T* Handle;
    public   readonly uint       FamilyIndex;
    public   readonly uint       QueueIndex;
    public   readonly Device     Device;

    internal Queue(Device device, VkQueue_T* handle, uint familyIndex, uint queueIndex)
    {
        Device      = device;
        Handle      = handle;
        FamilyIndex = familyIndex;
        QueueIndex  = queueIndex;
    }

    public ulong RawHandle => (ulong)(nint)Handle;
    public bool  IsNull    => Handle == null;
    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_QUEUE;
}
