namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkValidationFlagsEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint disabledValidationCheckCount;

    [NativeTypeName("const VkValidationCheckEXT *")]
    public VkValidationCheckEXT* pDisabledValidationChecks;
}
