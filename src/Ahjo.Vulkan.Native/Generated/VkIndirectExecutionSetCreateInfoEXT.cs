namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkIndirectExecutionSetCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkIndirectExecutionSetInfoTypeEXT type;

    public VkIndirectExecutionSetInfoEXT info;
}
