namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkLayerSettingsCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint settingCount;

    [NativeTypeName("const VkLayerSettingEXT *")]
    public VkLayerSettingEXT* pSettings;
}
