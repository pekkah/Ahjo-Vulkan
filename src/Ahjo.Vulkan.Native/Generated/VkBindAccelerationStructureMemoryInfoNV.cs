namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBindAccelerationStructureMemoryInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkAccelerationStructureNV")]
    public VkAccelerationStructureNV_T* accelerationStructure;

    [NativeTypeName("VkDeviceMemory")]
    public VkDeviceMemory_T* memory;

    [NativeTypeName("VkDeviceSize")]
    public ulong memoryOffset;

    [NativeTypeName("uint32_t")]
    public uint deviceIndexCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pDeviceIndices;
}
