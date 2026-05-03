namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkClusterAccelerationStructureMoveObjectsInputNV
{
    public VkStructureType sType;

    public void* pNext;

    public VkClusterAccelerationStructureTypeNV type;

    [NativeTypeName("VkBool32")]
    public uint noMoveOverlap;

    [NativeTypeName("VkDeviceSize")]
    public ulong maxMovedBytes;
}
