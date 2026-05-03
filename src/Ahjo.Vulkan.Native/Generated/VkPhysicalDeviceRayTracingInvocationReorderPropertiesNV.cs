namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceRayTracingInvocationReorderPropertiesNV
{
    public VkStructureType sType;

    public void* pNext;

    public VkRayTracingInvocationReorderModeEXT rayTracingInvocationReorderReorderingHint;
}
