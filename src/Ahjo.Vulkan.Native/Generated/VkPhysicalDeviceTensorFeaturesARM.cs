namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceTensorFeaturesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint tensorNonPacked;

    [NativeTypeName("VkBool32")]
    public uint shaderTensorAccess;

    [NativeTypeName("VkBool32")]
    public uint shaderStorageTensorArrayDynamicIndexing;

    [NativeTypeName("VkBool32")]
    public uint shaderStorageTensorArrayNonUniformIndexing;

    [NativeTypeName("VkBool32")]
    public uint descriptorBindingStorageTensorUpdateAfterBind;

    [NativeTypeName("VkBool32")]
    public uint tensors;
}
