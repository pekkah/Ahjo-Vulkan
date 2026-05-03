namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkShaderModuleValidationCacheCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkValidationCacheEXT")]
    public VkValidationCacheEXT_T* validationCache;
}
