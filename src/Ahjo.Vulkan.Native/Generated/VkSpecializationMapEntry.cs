namespace Ahjo.Vulkan.Native;

public partial struct VkSpecializationMapEntry
{
    [NativeTypeName("uint32_t")]
    public uint constantID;

    [NativeTypeName("uint32_t")]
    public uint offset;

    [NativeTypeName("size_t")]
    public nuint size;
}
