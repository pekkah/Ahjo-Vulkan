namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceSubpassMergeFeedbackFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint subpassMergeFeedback;
}
