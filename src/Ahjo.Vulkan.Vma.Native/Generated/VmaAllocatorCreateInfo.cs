namespace Ahjo.Vulkan.Vma.Native;

public unsafe partial struct VmaAllocatorCreateInfo
{
    [NativeTypeName("VmaAllocatorCreateFlags")]
    public uint flags;

    [NativeTypeName("VkPhysicalDevice _Nonnull")]
    public Ahjo.Vulkan.Native.VkPhysicalDevice_T* physicalDevice;

    [NativeTypeName("VkDevice _Nonnull")]
    public Ahjo.Vulkan.Native.VkDevice_T* device;

    [NativeTypeName("VkDeviceSize")]
    public ulong preferredLargeHeapBlockSize;

    [NativeTypeName("const VkAllocationCallbacks * _Nullable")]
    public Ahjo.Vulkan.Native.VkAllocationCallbacks* pAllocationCallbacks;

    [NativeTypeName("const VmaDeviceMemoryCallbacks * _Nullable")]
    public VmaDeviceMemoryCallbacks* pDeviceMemoryCallbacks;

    [NativeTypeName("const VkDeviceSize * _Nullable")]
    public ulong* pHeapSizeLimit;

    [NativeTypeName("const VmaVulkanFunctions * _Nullable")]
    public VmaVulkanFunctions* pVulkanFunctions;

    [NativeTypeName("VkInstance _Nonnull")]
    public Ahjo.Vulkan.Native.VkInstance_T* instance;

    [NativeTypeName("uint32_t")]
    public uint vulkanApiVersion;

    [NativeTypeName("const VkExternalMemoryHandleTypeFlagsKHR * _Nullable")]
    public uint* pTypeExternalMemoryHandleTypes;
}
