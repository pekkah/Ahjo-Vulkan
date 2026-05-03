namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCommandBufferInheritanceInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkRenderPass")]
    public VkRenderPass_T* renderPass;

    [NativeTypeName("uint32_t")]
    public uint subpass;

    [NativeTypeName("VkFramebuffer")]
    public VkFramebuffer_T* framebuffer;

    [NativeTypeName("VkBool32")]
    public uint occlusionQueryEnable;

    [NativeTypeName("VkQueryControlFlags")]
    public uint queryFlags;

    [NativeTypeName("VkQueryPipelineStatisticFlags")]
    public uint pipelineStatistics;
}
