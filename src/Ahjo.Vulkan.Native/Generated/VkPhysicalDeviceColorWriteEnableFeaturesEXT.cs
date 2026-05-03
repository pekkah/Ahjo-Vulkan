namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceColorWriteEnableFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint colorWriteEnable;
}
