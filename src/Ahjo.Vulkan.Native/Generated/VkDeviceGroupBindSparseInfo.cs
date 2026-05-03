namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceGroupBindSparseInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint resourceDeviceIndex;

    [NativeTypeName("uint32_t")]
    public uint memoryDeviceIndex;
}
