namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMaintenance10PropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint rgba4OpaqueBlackSwizzled;

    [NativeTypeName("VkBool32")]
    public uint resolveSrgbFormatAppliesTransferFunction;

    [NativeTypeName("VkBool32")]
    public uint resolveSrgbFormatSupportsTransferFunctionControl;
}
