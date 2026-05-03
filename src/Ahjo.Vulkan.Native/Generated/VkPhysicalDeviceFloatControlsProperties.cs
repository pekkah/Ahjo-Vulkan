namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceFloatControlsProperties
{
    public VkStructureType sType;

    public void* pNext;

    public VkShaderFloatControlsIndependence denormBehaviorIndependence;

    public VkShaderFloatControlsIndependence roundingModeIndependence;

    [NativeTypeName("VkBool32")]
    public uint shaderSignedZeroInfNanPreserveFloat16;

    [NativeTypeName("VkBool32")]
    public uint shaderSignedZeroInfNanPreserveFloat32;

    [NativeTypeName("VkBool32")]
    public uint shaderSignedZeroInfNanPreserveFloat64;

    [NativeTypeName("VkBool32")]
    public uint shaderDenormPreserveFloat16;

    [NativeTypeName("VkBool32")]
    public uint shaderDenormPreserveFloat32;

    [NativeTypeName("VkBool32")]
    public uint shaderDenormPreserveFloat64;

    [NativeTypeName("VkBool32")]
    public uint shaderDenormFlushToZeroFloat16;

    [NativeTypeName("VkBool32")]
    public uint shaderDenormFlushToZeroFloat32;

    [NativeTypeName("VkBool32")]
    public uint shaderDenormFlushToZeroFloat64;

    [NativeTypeName("VkBool32")]
    public uint shaderRoundingModeRTEFloat16;

    [NativeTypeName("VkBool32")]
    public uint shaderRoundingModeRTEFloat32;

    [NativeTypeName("VkBool32")]
    public uint shaderRoundingModeRTEFloat64;

    [NativeTypeName("VkBool32")]
    public uint shaderRoundingModeRTZFloat16;

    [NativeTypeName("VkBool32")]
    public uint shaderRoundingModeRTZFloat32;

    [NativeTypeName("VkBool32")]
    public uint shaderRoundingModeRTZFloat64;
}
