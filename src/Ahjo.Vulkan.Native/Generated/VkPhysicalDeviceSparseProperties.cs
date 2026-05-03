namespace Ahjo.Vulkan.Native;

public partial struct VkPhysicalDeviceSparseProperties
{
    [NativeTypeName("VkBool32")]
    public uint residencyStandard2DBlockShape;

    [NativeTypeName("VkBool32")]
    public uint residencyStandard2DMultisampleBlockShape;

    [NativeTypeName("VkBool32")]
    public uint residencyStandard3DBlockShape;

    [NativeTypeName("VkBool32")]
    public uint residencyAlignedMipSize;

    [NativeTypeName("VkBool32")]
    public uint residencyNonResidentStrict;
}
