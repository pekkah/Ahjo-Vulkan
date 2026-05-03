namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMaintenance7PropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint robustFragmentShadingRateAttachmentAccess;

    [NativeTypeName("VkBool32")]
    public uint separateDepthStencilAttachmentAccess;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetTotalUniformBuffersDynamic;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetTotalStorageBuffersDynamic;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetTotalBuffersDynamic;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetUpdateAfterBindTotalUniformBuffersDynamic;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetUpdateAfterBindTotalStorageBuffersDynamic;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetUpdateAfterBindTotalBuffersDynamic;
}
