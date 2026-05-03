namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceBufferMemoryRequirements
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const VkBufferCreateInfo *")]
    public VkBufferCreateInfo* pCreateInfo;
}
