namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkExternalTensorPropertiesARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkExternalMemoryProperties externalMemoryProperties;
}
