namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceRayTracingInvocationReorderFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint rayTracingInvocationReorder;
}
