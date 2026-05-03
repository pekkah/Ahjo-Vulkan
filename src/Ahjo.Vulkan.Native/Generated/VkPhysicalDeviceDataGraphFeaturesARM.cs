namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDataGraphFeaturesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint dataGraph;

    [NativeTypeName("VkBool32")]
    public uint dataGraphUpdateAfterBind;

    [NativeTypeName("VkBool32")]
    public uint dataGraphSpecializationConstants;

    [NativeTypeName("VkBool32")]
    public uint dataGraphDescriptorBuffer;

    [NativeTypeName("VkBool32")]
    public uint dataGraphShaderModule;
}
