namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMultiviewPerViewAttributesInfoNVX
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint perViewAttributes;

    [NativeTypeName("VkBool32")]
    public uint perViewAttributesPositionXOnly;
}
