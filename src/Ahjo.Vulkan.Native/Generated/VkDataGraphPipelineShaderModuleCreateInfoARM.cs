namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphPipelineShaderModuleCreateInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkShaderModule")]
    public VkShaderModule_T* module;

    [NativeTypeName("const char *")]
    public sbyte* pName;

    [NativeTypeName("const VkSpecializationInfo *")]
    public VkSpecializationInfo* pSpecializationInfo;

    [NativeTypeName("uint32_t")]
    public uint constantCount;

    [NativeTypeName("const VkDataGraphPipelineConstantARM *")]
    public VkDataGraphPipelineConstantARM* pConstants;
}
