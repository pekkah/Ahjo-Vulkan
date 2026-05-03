namespace Ahjo.Vulkan.Vma.Native;

public unsafe partial struct VmaAllocatorInfo
{
    [NativeTypeName("VkInstance _Nonnull")]
    public Ahjo.Vulkan.Native.VkInstance_T* instance;

    [NativeTypeName("VkPhysicalDevice _Nonnull")]
    public Ahjo.Vulkan.Native.VkPhysicalDevice_T* physicalDevice;

    [NativeTypeName("VkDevice _Nonnull")]
    public Ahjo.Vulkan.Native.VkDevice_T* device;
}
