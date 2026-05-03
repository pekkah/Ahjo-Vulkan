namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSpecializationInfo
{
    [NativeTypeName("uint32_t")]
    public uint mapEntryCount;

    [NativeTypeName("const VkSpecializationMapEntry *")]
    public VkSpecializationMapEntry* pMapEntries;

    [NativeTypeName("size_t")]
    public nuint dataSize;

    [NativeTypeName("const void *")]
    public void* pData;
}
