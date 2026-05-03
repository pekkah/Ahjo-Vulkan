using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkQueueFamilyGlobalPriorityProperties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint priorityCount;

    [NativeTypeName("VkQueueGlobalPriority[16]")]
    public _priorities_e__FixedBuffer priorities;

    [InlineArray(16)]
    public partial struct _priorities_e__FixedBuffer
    {
        public VkQueueGlobalPriority e0;
    }
}
