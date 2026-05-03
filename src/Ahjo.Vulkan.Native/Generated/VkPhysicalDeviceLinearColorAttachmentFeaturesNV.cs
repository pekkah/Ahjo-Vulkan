namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceLinearColorAttachmentFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint linearColorAttachment;
}
