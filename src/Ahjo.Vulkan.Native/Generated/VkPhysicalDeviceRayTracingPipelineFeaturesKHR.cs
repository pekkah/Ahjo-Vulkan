namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceRayTracingPipelineFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint rayTracingPipeline;

    [NativeTypeName("VkBool32")]
    public uint rayTracingPipelineShaderGroupHandleCaptureReplay;

    [NativeTypeName("VkBool32")]
    public uint rayTracingPipelineShaderGroupHandleCaptureReplayMixed;

    [NativeTypeName("VkBool32")]
    public uint rayTracingPipelineTraceRaysIndirect;

    [NativeTypeName("VkBool32")]
    public uint rayTraversalPrimitiveCulling;
}
