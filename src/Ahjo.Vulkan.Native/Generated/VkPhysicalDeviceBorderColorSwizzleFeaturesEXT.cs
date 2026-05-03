namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceBorderColorSwizzleFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint borderColorSwizzle;

    [NativeTypeName("VkBool32")]
    public uint borderColorSwizzleFromImage;
}
