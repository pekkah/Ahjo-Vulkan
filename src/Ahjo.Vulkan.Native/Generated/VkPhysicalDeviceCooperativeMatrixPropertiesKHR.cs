namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceCooperativeMatrixPropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkShaderStageFlags")]
    public uint cooperativeMatrixSupportedStages;
}
