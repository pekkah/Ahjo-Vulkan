namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceExtendedDynamicState2FeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint extendedDynamicState2;

    [NativeTypeName("VkBool32")]
    public uint extendedDynamicState2LogicOp;

    [NativeTypeName("VkBool32")]
    public uint extendedDynamicState2PatchControlPoints;
}
