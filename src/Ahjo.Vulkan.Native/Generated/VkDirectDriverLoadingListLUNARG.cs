namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDirectDriverLoadingListLUNARG
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkDirectDriverLoadingModeLUNARG mode;

    [NativeTypeName("uint32_t")]
    public uint driverCount;

    [NativeTypeName("const VkDirectDriverLoadingInfoLUNARG *")]
    public VkDirectDriverLoadingInfoLUNARG* pDrivers;
}
