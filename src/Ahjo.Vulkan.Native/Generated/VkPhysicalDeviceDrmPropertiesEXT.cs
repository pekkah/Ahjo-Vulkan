namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDrmPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint hasPrimary;

    [NativeTypeName("VkBool32")]
    public uint hasRender;

    [NativeTypeName("int64_t")]
    public long primaryMajor;

    [NativeTypeName("int64_t")]
    public long primaryMinor;

    [NativeTypeName("int64_t")]
    public long renderMajor;

    [NativeTypeName("int64_t")]
    public long renderMinor;
}
