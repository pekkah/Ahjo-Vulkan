namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSamplerYcbcrConversionInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkSamplerYcbcrConversion")]
    public VkSamplerYcbcrConversion_T* conversion;
}
