namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceAttachmentFeedbackLoopDynamicStateFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint attachmentFeedbackLoopDynamicState;
}
