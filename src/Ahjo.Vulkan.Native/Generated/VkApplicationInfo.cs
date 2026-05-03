namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkApplicationInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const char *")]
    public sbyte* pApplicationName;

    [NativeTypeName("uint32_t")]
    public uint applicationVersion;

    [NativeTypeName("const char *")]
    public sbyte* pEngineName;

    [NativeTypeName("uint32_t")]
    public uint engineVersion;

    [NativeTypeName("uint32_t")]
    public uint apiVersion;
}
