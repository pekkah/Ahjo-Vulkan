namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceDiagnosticsConfigCreateInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDeviceDiagnosticsConfigFlagsNV")]
    public uint flags;
}
