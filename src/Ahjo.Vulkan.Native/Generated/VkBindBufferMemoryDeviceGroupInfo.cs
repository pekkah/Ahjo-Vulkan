namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBindBufferMemoryDeviceGroupInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint deviceIndexCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pDeviceIndices;
}
