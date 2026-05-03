namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDepthStencilResolveProperties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkResolveModeFlags")]
    public uint supportedDepthResolveModes;

    [NativeTypeName("VkResolveModeFlags")]
    public uint supportedStencilResolveModes;

    [NativeTypeName("VkBool32")]
    public uint independentResolveNone;

    [NativeTypeName("VkBool32")]
    public uint independentResolve;
}
