namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDeviceGeneratedCommandsPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxIndirectPipelineCount;

    [NativeTypeName("uint32_t")]
    public uint maxIndirectShaderObjectCount;

    [NativeTypeName("uint32_t")]
    public uint maxIndirectSequenceCount;

    [NativeTypeName("uint32_t")]
    public uint maxIndirectCommandsTokenCount;

    [NativeTypeName("uint32_t")]
    public uint maxIndirectCommandsTokenOffset;

    [NativeTypeName("uint32_t")]
    public uint maxIndirectCommandsIndirectStride;

    [NativeTypeName("VkIndirectCommandsInputModeFlagsEXT")]
    public uint supportedIndirectCommandsInputModes;

    [NativeTypeName("VkShaderStageFlags")]
    public uint supportedIndirectCommandsShaderStages;

    [NativeTypeName("VkShaderStageFlags")]
    public uint supportedIndirectCommandsShaderStagesPipelineBinding;

    [NativeTypeName("VkShaderStageFlags")]
    public uint supportedIndirectCommandsShaderStagesShaderBinding;

    [NativeTypeName("VkBool32")]
    public uint deviceGeneratedCommandsTransformFeedback;

    [NativeTypeName("VkBool32")]
    public uint deviceGeneratedCommandsMultiDrawIndirectCount;
}
