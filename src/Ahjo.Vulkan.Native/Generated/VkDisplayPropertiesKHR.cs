namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDisplayPropertiesKHR
{
    [NativeTypeName("VkDisplayKHR")]
    public VkDisplayKHR_T* display;

    [NativeTypeName("const char *")]
    public sbyte* displayName;

    public VkExtent2D physicalDimensions;

    public VkExtent2D physicalResolution;

    [NativeTypeName("VkSurfaceTransformFlagsKHR")]
    public uint supportedTransforms;

    [NativeTypeName("VkBool32")]
    public uint planeReorderPossible;

    [NativeTypeName("VkBool32")]
    public uint persistentContent;
}
