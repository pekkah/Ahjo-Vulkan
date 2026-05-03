namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkLayerSettingEXT
{
    [NativeTypeName("const char *")]
    public sbyte* pLayerName;

    [NativeTypeName("const char *")]
    public sbyte* pSettingName;

    public VkLayerSettingTypeEXT type;

    [NativeTypeName("uint32_t")]
    public uint valueCount;

    [NativeTypeName("const void *")]
    public void* pValues;
}
