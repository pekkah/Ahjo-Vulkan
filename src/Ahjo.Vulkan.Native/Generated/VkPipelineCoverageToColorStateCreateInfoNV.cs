namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineCoverageToColorStateCreateInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineCoverageToColorStateCreateFlagsNV")]
    public uint flags;

    [NativeTypeName("VkBool32")]
    public uint coverageToColorEnable;

    [NativeTypeName("uint32_t")]
    public uint coverageToColorLocation;
}
