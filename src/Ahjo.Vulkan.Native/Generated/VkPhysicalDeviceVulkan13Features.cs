namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceVulkan13Features
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint robustImageAccess;

    [NativeTypeName("VkBool32")]
    public uint inlineUniformBlock;

    [NativeTypeName("VkBool32")]
    public uint descriptorBindingInlineUniformBlockUpdateAfterBind;

    [NativeTypeName("VkBool32")]
    public uint pipelineCreationCacheControl;

    [NativeTypeName("VkBool32")]
    public uint privateData;

    [NativeTypeName("VkBool32")]
    public uint shaderDemoteToHelperInvocation;

    [NativeTypeName("VkBool32")]
    public uint shaderTerminateInvocation;

    [NativeTypeName("VkBool32")]
    public uint subgroupSizeControl;

    [NativeTypeName("VkBool32")]
    public uint computeFullSubgroups;

    [NativeTypeName("VkBool32")]
    public uint synchronization2;

    [NativeTypeName("VkBool32")]
    public uint textureCompressionASTC_HDR;

    [NativeTypeName("VkBool32")]
    public uint shaderZeroInitializeWorkgroupMemory;

    [NativeTypeName("VkBool32")]
    public uint dynamicRendering;

    [NativeTypeName("VkBool32")]
    public uint shaderIntegerDotProduct;

    [NativeTypeName("VkBool32")]
    public uint maintenance4;
}
