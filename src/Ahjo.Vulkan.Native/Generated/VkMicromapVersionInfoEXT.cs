namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMicromapVersionInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const uint8_t *")]
    public byte* pVersionData;
}
