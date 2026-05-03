namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkShaderModuleCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkShaderModuleCreateFlags")]
    public uint flags;

    [NativeTypeName("size_t")]
    public nuint codeSize;

    [NativeTypeName("const uint32_t *")]
    public uint* pCode;
}
