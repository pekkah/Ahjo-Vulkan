namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDevicePrivateDataCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint privateDataSlotRequestCount;
}
