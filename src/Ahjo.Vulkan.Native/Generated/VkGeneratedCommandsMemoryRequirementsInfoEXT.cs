namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkGeneratedCommandsMemoryRequirementsInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkIndirectExecutionSetEXT")]
    public VkIndirectExecutionSetEXT_T* indirectExecutionSet;

    [NativeTypeName("VkIndirectCommandsLayoutEXT")]
    public VkIndirectCommandsLayoutEXT_T* indirectCommandsLayout;

    [NativeTypeName("uint32_t")]
    public uint maxSequenceCount;

    [NativeTypeName("uint32_t")]
    public uint maxDrawCount;
}
