namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineCompilerControlCreateInfoAMD
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineCompilerControlFlagsAMD")]
    public uint compilerControlFlags;
}
