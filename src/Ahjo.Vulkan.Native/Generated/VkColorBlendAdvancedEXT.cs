namespace Ahjo.Vulkan.Native;

public partial struct VkColorBlendAdvancedEXT
{
    public VkBlendOp advancedBlendOp;

    [NativeTypeName("VkBool32")]
    public uint srcPremultiplied;

    [NativeTypeName("VkBool32")]
    public uint dstPremultiplied;

    public VkBlendOverlapEXT blendOverlap;

    [NativeTypeName("VkBool32")]
    public uint clampResults;
}
