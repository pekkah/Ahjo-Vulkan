namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceExternalTensorInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkTensorCreateFlagsARM")]
    public ulong flags;

    [NativeTypeName("const VkTensorDescriptionARM *")]
    public VkTensorDescriptionARM* pDescription;

    public VkExternalMemoryHandleTypeFlagBits handleType;
}
