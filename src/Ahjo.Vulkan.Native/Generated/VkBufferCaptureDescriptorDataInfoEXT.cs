namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBufferCaptureDescriptorDataInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* buffer;
}
