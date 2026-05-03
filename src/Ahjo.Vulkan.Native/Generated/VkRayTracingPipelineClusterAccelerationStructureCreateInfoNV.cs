namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRayTracingPipelineClusterAccelerationStructureCreateInfoNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint allowClusterAccelerationStructure;
}
