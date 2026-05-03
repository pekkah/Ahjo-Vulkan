namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceRayTracingMotionBlurFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint rayTracingMotionBlur;

    [NativeTypeName("VkBool32")]
    public uint rayTracingMotionBlurPipelineTraceRaysIndirect;
}
