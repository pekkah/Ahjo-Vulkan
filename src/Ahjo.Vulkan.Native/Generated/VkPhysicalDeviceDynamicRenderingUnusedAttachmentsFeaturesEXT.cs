namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDynamicRenderingUnusedAttachmentsFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint dynamicRenderingUnusedAttachments;
}
