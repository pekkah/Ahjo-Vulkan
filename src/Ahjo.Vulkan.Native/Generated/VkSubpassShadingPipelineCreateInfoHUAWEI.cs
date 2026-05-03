namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSubpassShadingPipelineCreateInfoHUAWEI
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkRenderPass")]
    public VkRenderPass_T* renderPass;

    [NativeTypeName("uint32_t")]
    public uint subpass;
}
