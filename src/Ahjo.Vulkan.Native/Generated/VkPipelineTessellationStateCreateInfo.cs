namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineTessellationStateCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineTessellationStateCreateFlags")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint patchControlPoints;
}
