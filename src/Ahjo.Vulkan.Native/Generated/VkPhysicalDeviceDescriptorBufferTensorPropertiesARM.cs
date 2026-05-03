namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDescriptorBufferTensorPropertiesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("size_t")]
    public nuint tensorCaptureReplayDescriptorDataSize;

    [NativeTypeName("size_t")]
    public nuint tensorViewCaptureReplayDescriptorDataSize;

    [NativeTypeName("size_t")]
    public nuint tensorDescriptorSize;
}
