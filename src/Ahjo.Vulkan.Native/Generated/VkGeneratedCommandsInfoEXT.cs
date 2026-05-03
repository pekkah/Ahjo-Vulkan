namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkGeneratedCommandsInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkShaderStageFlags")]
    public uint shaderStages;

    [NativeTypeName("VkIndirectExecutionSetEXT")]
    public VkIndirectExecutionSetEXT_T* indirectExecutionSet;

    [NativeTypeName("VkIndirectCommandsLayoutEXT")]
    public VkIndirectCommandsLayoutEXT_T* indirectCommandsLayout;

    [NativeTypeName("VkDeviceAddress")]
    public ulong indirectAddress;

    [NativeTypeName("VkDeviceSize")]
    public ulong indirectAddressSize;

    [NativeTypeName("VkDeviceAddress")]
    public ulong preprocessAddress;

    [NativeTypeName("VkDeviceSize")]
    public ulong preprocessSize;

    [NativeTypeName("uint32_t")]
    public uint maxSequenceCount;

    [NativeTypeName("VkDeviceAddress")]
    public ulong sequenceCountAddress;

    [NativeTypeName("uint32_t")]
    public uint maxDrawCount;
}
