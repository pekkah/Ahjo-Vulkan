namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineRasterizationLineStateCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkLineRasterizationMode lineRasterizationMode;

    [NativeTypeName("VkBool32")]
    public uint stippledLineEnable;

    [NativeTypeName("uint32_t")]
    public uint lineStippleFactor;

    [NativeTypeName("uint16_t")]
    public ushort lineStipplePattern;
}
