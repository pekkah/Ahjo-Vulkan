namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDeviceGeneratedCommandsComputeFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint deviceGeneratedCompute;

    [NativeTypeName("VkBool32")]
    public uint deviceGeneratedComputePipelines;

    [NativeTypeName("VkBool32")]
    public uint deviceGeneratedComputeCaptureReplay;
}
