namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDescriptorBufferFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint descriptorBuffer;

    [NativeTypeName("VkBool32")]
    public uint descriptorBufferCaptureReplay;

    [NativeTypeName("VkBool32")]
    public uint descriptorBufferImageLayoutIgnored;

    [NativeTypeName("VkBool32")]
    public uint descriptorBufferPushDescriptors;
}
