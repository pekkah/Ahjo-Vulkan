namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDescriptorHeapTensorPropertiesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkDeviceSize")]
    public ulong tensorDescriptorSize;

    [NativeTypeName("VkDeviceSize")]
    public ulong tensorDescriptorAlignment;

    [NativeTypeName("size_t")]
    public nuint tensorCaptureReplayOpaqueDataSize;
}
