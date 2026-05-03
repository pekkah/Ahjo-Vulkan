namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCuFunctionCreateInfoNVX
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkCuModuleNVX")]
    public VkCuModuleNVX_T* module;

    [NativeTypeName("const char *")]
    public sbyte* pName;
}
