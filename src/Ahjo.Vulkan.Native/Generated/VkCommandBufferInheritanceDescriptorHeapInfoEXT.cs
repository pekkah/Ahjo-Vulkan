namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCommandBufferInheritanceDescriptorHeapInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const VkBindHeapInfoEXT *")]
    public VkBindHeapInfoEXT* pSamplerHeapBindInfo;

    [NativeTypeName("const VkBindHeapInfoEXT *")]
    public VkBindHeapInfoEXT* pResourceHeapBindInfo;
}
