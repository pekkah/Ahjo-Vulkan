namespace Ahjo.Vulkan.Native;

public partial struct VkPresentTimeGOOGLE
{
    [NativeTypeName("uint32_t")]
    public uint presentID;

    [NativeTypeName("uint64_t")]
    public ulong desiredPresentTime;
}
