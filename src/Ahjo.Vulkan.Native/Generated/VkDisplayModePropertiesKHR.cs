namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDisplayModePropertiesKHR
{
    [NativeTypeName("VkDisplayModeKHR")]
    public VkDisplayModeKHR_T* displayMode;

    public VkDisplayModeParametersKHR parameters;
}
