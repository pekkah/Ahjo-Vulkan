namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineDynamicStateCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineDynamicStateCreateFlags")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint dynamicStateCount;

    [NativeTypeName("const VkDynamicState *")]
    public VkDynamicState* pDynamicStates;
}
