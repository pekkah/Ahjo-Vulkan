namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDiagnosticsConfigFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint diagnosticsConfig;
}
