namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSamplerYcbcrConversionCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkFormat format;

    public VkSamplerYcbcrModelConversion ycbcrModel;

    public VkSamplerYcbcrRange ycbcrRange;

    public VkComponentMapping components;

    public VkChromaLocation xChromaOffset;

    public VkChromaLocation yChromaOffset;

    public VkFilter chromaFilter;

    [NativeTypeName("VkBool32")]
    public uint forceExplicitReconstruction;
}
