namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderAtomicFloat2FeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderBufferFloat16Atomics;

    [NativeTypeName("VkBool32")]
    public uint shaderBufferFloat16AtomicAdd;

    [NativeTypeName("VkBool32")]
    public uint shaderBufferFloat16AtomicMinMax;

    [NativeTypeName("VkBool32")]
    public uint shaderBufferFloat32AtomicMinMax;

    [NativeTypeName("VkBool32")]
    public uint shaderBufferFloat64AtomicMinMax;

    [NativeTypeName("VkBool32")]
    public uint shaderSharedFloat16Atomics;

    [NativeTypeName("VkBool32")]
    public uint shaderSharedFloat16AtomicAdd;

    [NativeTypeName("VkBool32")]
    public uint shaderSharedFloat16AtomicMinMax;

    [NativeTypeName("VkBool32")]
    public uint shaderSharedFloat32AtomicMinMax;

    [NativeTypeName("VkBool32")]
    public uint shaderSharedFloat64AtomicMinMax;

    [NativeTypeName("VkBool32")]
    public uint shaderImageFloat32AtomicMinMax;

    [NativeTypeName("VkBool32")]
    public uint sparseImageFloat32AtomicMinMax;
}
