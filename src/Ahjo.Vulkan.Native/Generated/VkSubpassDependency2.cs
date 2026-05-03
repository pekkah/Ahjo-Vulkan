namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSubpassDependency2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint srcSubpass;

    [NativeTypeName("uint32_t")]
    public uint dstSubpass;

    [NativeTypeName("VkPipelineStageFlags")]
    public uint srcStageMask;

    [NativeTypeName("VkPipelineStageFlags")]
    public uint dstStageMask;

    [NativeTypeName("VkAccessFlags")]
    public uint srcAccessMask;

    [NativeTypeName("VkAccessFlags")]
    public uint dstAccessMask;

    [NativeTypeName("VkDependencyFlags")]
    public uint dependencyFlags;

    [NativeTypeName("int32_t")]
    public int viewOffset;
}
