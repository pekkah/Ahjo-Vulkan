namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShaderAtomicFloatFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shaderBufferFloat32Atomics;

    [NativeTypeName("VkBool32")]
    public uint shaderBufferFloat32AtomicAdd;

    [NativeTypeName("VkBool32")]
    public uint shaderBufferFloat64Atomics;

    [NativeTypeName("VkBool32")]
    public uint shaderBufferFloat64AtomicAdd;

    [NativeTypeName("VkBool32")]
    public uint shaderSharedFloat32Atomics;

    [NativeTypeName("VkBool32")]
    public uint shaderSharedFloat32AtomicAdd;

    [NativeTypeName("VkBool32")]
    public uint shaderSharedFloat64Atomics;

    [NativeTypeName("VkBool32")]
    public uint shaderSharedFloat64AtomicAdd;

    [NativeTypeName("VkBool32")]
    public uint shaderImageFloat32Atomics;

    [NativeTypeName("VkBool32")]
    public uint shaderImageFloat32AtomicAdd;

    [NativeTypeName("VkBool32")]
    public uint sparseImageFloat32Atomics;

    [NativeTypeName("VkBool32")]
    public uint sparseImageFloat32AtomicAdd;
}
