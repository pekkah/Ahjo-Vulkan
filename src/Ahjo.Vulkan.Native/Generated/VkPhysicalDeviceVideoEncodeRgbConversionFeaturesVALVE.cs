namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceVideoEncodeRgbConversionFeaturesVALVE
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint videoEncodeRgbConversion;
}
