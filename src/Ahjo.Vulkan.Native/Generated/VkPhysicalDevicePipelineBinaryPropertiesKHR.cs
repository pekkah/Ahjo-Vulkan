namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePipelineBinaryPropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint pipelineBinaryInternalCache;

    [NativeTypeName("VkBool32")]
    public uint pipelineBinaryInternalCacheControl;

    [NativeTypeName("VkBool32")]
    public uint pipelineBinaryPrefersInternalCache;

    [NativeTypeName("VkBool32")]
    public uint pipelineBinaryPrecompiledInternalCache;

    [NativeTypeName("VkBool32")]
    public uint pipelineBinaryCompressedData;
}
