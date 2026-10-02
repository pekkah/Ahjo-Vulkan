namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkGpaSessionCreateInfoAMD
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkGpaSessionAMD")]
    public VkGpaSessionAMD_T* secondaryCopySource;
}
