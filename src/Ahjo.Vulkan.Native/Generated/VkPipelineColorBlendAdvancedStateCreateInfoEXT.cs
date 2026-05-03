namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineColorBlendAdvancedStateCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint srcPremultiplied;

    [NativeTypeName("VkBool32")]
    public uint dstPremultiplied;

    public VkBlendOverlapEXT blendOverlap;
}
