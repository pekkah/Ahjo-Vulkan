namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceASTCDecodeFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint decodeModeSharedExponent;
}
