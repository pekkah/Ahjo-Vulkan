namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderClockFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderSubgroupClock;

    [NativeTypeName("VkBool32")]
    public uint shaderDeviceClock;
}
