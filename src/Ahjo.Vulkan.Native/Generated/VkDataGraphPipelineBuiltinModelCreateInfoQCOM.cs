namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphPipelineBuiltinModelCreateInfoQCOM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const VkPhysicalDeviceDataGraphOperationSupportARM *")]
    public VkPhysicalDeviceDataGraphOperationSupportARM* pOperation;
}
