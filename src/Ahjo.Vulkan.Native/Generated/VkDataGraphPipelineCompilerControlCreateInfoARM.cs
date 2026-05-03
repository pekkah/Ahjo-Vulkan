namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphPipelineCompilerControlCreateInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const char *")]
    public sbyte* pVendorOptions;
}
