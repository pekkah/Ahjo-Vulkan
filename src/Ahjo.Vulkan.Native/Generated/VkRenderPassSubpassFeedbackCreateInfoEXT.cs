namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderPassSubpassFeedbackCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkRenderPassSubpassFeedbackInfoEXT* pSubpassFeedback;
}
