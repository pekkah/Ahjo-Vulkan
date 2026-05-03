namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkHostImageCopyDevicePerformanceQuery
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint optimalDeviceAccess;

    [NativeTypeName("VkBool32")]
    public uint identicalMemoryLayout;
}
