namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceTensorMemoryRequirementsARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const VkTensorCreateInfoARM *")]
    public VkTensorCreateInfoARM* pCreateInfo;
}
