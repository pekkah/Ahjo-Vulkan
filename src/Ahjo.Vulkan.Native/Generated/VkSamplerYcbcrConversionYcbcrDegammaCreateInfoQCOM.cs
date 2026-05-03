namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSamplerYcbcrConversionYcbcrDegammaCreateInfoQCOM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint enableYDegamma;

    [NativeTypeName("VkBool32")]
    public uint enableCbCrDegamma;
}
