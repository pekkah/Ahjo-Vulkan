namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkGeneratedCommandsShaderInfoEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint shaderCount;

    [NativeTypeName("const VkShaderEXT *")]
    public VkShaderEXT_T** pShaders;
}
