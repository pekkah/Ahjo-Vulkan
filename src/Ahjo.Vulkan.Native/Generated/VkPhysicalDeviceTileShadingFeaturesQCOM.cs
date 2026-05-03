namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceTileShadingFeaturesQCOM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint tileShading;

    [NativeTypeName("VkBool32")]
    public uint tileShadingFragmentStage;

    [NativeTypeName("VkBool32")]
    public uint tileShadingColorAttachments;

    [NativeTypeName("VkBool32")]
    public uint tileShadingDepthAttachments;

    [NativeTypeName("VkBool32")]
    public uint tileShadingStencilAttachments;

    [NativeTypeName("VkBool32")]
    public uint tileShadingInputAttachments;

    [NativeTypeName("VkBool32")]
    public uint tileShadingSampledAttachments;

    [NativeTypeName("VkBool32")]
    public uint tileShadingPerTileDraw;

    [NativeTypeName("VkBool32")]
    public uint tileShadingPerTileDispatch;

    [NativeTypeName("VkBool32")]
    public uint tileShadingDispatchTile;

    [NativeTypeName("VkBool32")]
    public uint tileShadingApron;

    [NativeTypeName("VkBool32")]
    public uint tileShadingAnisotropicApron;

    [NativeTypeName("VkBool32")]
    public uint tileShadingAtomicOps;

    [NativeTypeName("VkBool32")]
    public uint tileShadingImageProcessing;
}
