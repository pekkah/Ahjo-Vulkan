namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderPassCreationFeedbackCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkRenderPassCreationFeedbackInfoEXT* pRenderPassFeedback;
}
