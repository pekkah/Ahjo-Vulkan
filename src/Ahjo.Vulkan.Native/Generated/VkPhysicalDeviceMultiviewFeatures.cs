namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMultiviewFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint multiview;

    [NativeTypeName("VkBool32")]
    public uint multiviewGeometryShader;

    [NativeTypeName("VkBool32")]
    public uint multiviewTessellationShader;
}
