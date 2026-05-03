namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceRayTracingValidationFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint rayTracingValidation;
}
