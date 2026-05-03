namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceRayTracingLinearSweptSpheresFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint spheres;

    [NativeTypeName("VkBool32")]
    public uint linearSweptSpheres;
}
