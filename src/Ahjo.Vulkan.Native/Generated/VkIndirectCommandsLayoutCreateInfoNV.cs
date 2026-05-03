namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkIndirectCommandsLayoutCreateInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkIndirectCommandsLayoutUsageFlagsNV")]
    public uint flags;

    public VkPipelineBindPoint pipelineBindPoint;

    [NativeTypeName("uint32_t")]
    public uint tokenCount;

    [NativeTypeName("const VkIndirectCommandsLayoutTokenNV *")]
    public VkIndirectCommandsLayoutTokenNV* pTokens;

    [NativeTypeName("uint32_t")]
    public uint streamCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pStreamStrides;
}
