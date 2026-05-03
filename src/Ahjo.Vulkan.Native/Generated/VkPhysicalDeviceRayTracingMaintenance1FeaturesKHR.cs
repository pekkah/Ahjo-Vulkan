namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceRayTracingMaintenance1FeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint rayTracingMaintenance1;

    [NativeTypeName("VkBool32")]
    public uint rayTracingPipelineTraceRaysIndirect2;
}
