namespace Ahjo.Vulkan.Vma.Native;

public unsafe partial struct VmaVulkanFunctions
{
    [NativeTypeName("PFN_vkGetInstanceProcAddr _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkInstance_T*, sbyte*, delegate* unmanaged[Stdcall]<void>> vkGetInstanceProcAddr;

    [NativeTypeName("PFN_vkGetDeviceProcAddr _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, sbyte*, delegate* unmanaged[Stdcall]<void>> vkGetDeviceProcAddr;

    [NativeTypeName("PFN_vkGetPhysicalDeviceProperties _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkPhysicalDevice_T*, Ahjo.Vulkan.Native.VkPhysicalDeviceProperties*, void> vkGetPhysicalDeviceProperties;

    [NativeTypeName("PFN_vkGetPhysicalDeviceMemoryProperties _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkPhysicalDevice_T*, Ahjo.Vulkan.Native.VkPhysicalDeviceMemoryProperties*, void> vkGetPhysicalDeviceMemoryProperties;

    [NativeTypeName("PFN_vkAllocateMemory _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, Ahjo.Vulkan.Native.VkMemoryAllocateInfo*, Ahjo.Vulkan.Native.VkAllocationCallbacks*, Ahjo.Vulkan.Native.VkDeviceMemory_T**, Ahjo.Vulkan.Native.VkResult> vkAllocateMemory;

    [NativeTypeName("PFN_vkFreeMemory _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, Ahjo.Vulkan.Native.VkDeviceMemory_T*, Ahjo.Vulkan.Native.VkAllocationCallbacks*, void> vkFreeMemory;

    [NativeTypeName("PFN_vkMapMemory _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, Ahjo.Vulkan.Native.VkDeviceMemory_T*, ulong, ulong, uint, void**, Ahjo.Vulkan.Native.VkResult> vkMapMemory;

    [NativeTypeName("PFN_vkUnmapMemory _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, Ahjo.Vulkan.Native.VkDeviceMemory_T*, void> vkUnmapMemory;

    [NativeTypeName("PFN_vkFlushMappedMemoryRanges _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, uint, Ahjo.Vulkan.Native.VkMappedMemoryRange*, Ahjo.Vulkan.Native.VkResult> vkFlushMappedMemoryRanges;

    [NativeTypeName("PFN_vkInvalidateMappedMemoryRanges _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, uint, Ahjo.Vulkan.Native.VkMappedMemoryRange*, Ahjo.Vulkan.Native.VkResult> vkInvalidateMappedMemoryRanges;

    [NativeTypeName("PFN_vkBindBufferMemory _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, Ahjo.Vulkan.Native.VkBuffer_T*, Ahjo.Vulkan.Native.VkDeviceMemory_T*, ulong, Ahjo.Vulkan.Native.VkResult> vkBindBufferMemory;

    [NativeTypeName("PFN_vkBindImageMemory _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, Ahjo.Vulkan.Native.VkImage_T*, Ahjo.Vulkan.Native.VkDeviceMemory_T*, ulong, Ahjo.Vulkan.Native.VkResult> vkBindImageMemory;

    [NativeTypeName("PFN_vkGetBufferMemoryRequirements _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, Ahjo.Vulkan.Native.VkBuffer_T*, Ahjo.Vulkan.Native.VkMemoryRequirements*, void> vkGetBufferMemoryRequirements;

    [NativeTypeName("PFN_vkGetImageMemoryRequirements _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, Ahjo.Vulkan.Native.VkImage_T*, Ahjo.Vulkan.Native.VkMemoryRequirements*, void> vkGetImageMemoryRequirements;

    [NativeTypeName("PFN_vkCreateBuffer _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, Ahjo.Vulkan.Native.VkBufferCreateInfo*, Ahjo.Vulkan.Native.VkAllocationCallbacks*, Ahjo.Vulkan.Native.VkBuffer_T**, Ahjo.Vulkan.Native.VkResult> vkCreateBuffer;

    [NativeTypeName("PFN_vkDestroyBuffer _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, Ahjo.Vulkan.Native.VkBuffer_T*, Ahjo.Vulkan.Native.VkAllocationCallbacks*, void> vkDestroyBuffer;

    [NativeTypeName("PFN_vkCreateImage _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, Ahjo.Vulkan.Native.VkImageCreateInfo*, Ahjo.Vulkan.Native.VkAllocationCallbacks*, Ahjo.Vulkan.Native.VkImage_T**, Ahjo.Vulkan.Native.VkResult> vkCreateImage;

    [NativeTypeName("PFN_vkDestroyImage _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, Ahjo.Vulkan.Native.VkImage_T*, Ahjo.Vulkan.Native.VkAllocationCallbacks*, void> vkDestroyImage;

    [NativeTypeName("PFN_vkCmdCopyBuffer _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkCommandBuffer_T*, Ahjo.Vulkan.Native.VkBuffer_T*, Ahjo.Vulkan.Native.VkBuffer_T*, uint, Ahjo.Vulkan.Native.VkBufferCopy*, void> vkCmdCopyBuffer;

    [NativeTypeName("PFN_vkGetBufferMemoryRequirements2KHR _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, Ahjo.Vulkan.Native.VkBufferMemoryRequirementsInfo2*, Ahjo.Vulkan.Native.VkMemoryRequirements2*, void> vkGetBufferMemoryRequirements2KHR;

    [NativeTypeName("PFN_vkGetImageMemoryRequirements2KHR _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, Ahjo.Vulkan.Native.VkImageMemoryRequirementsInfo2*, Ahjo.Vulkan.Native.VkMemoryRequirements2*, void> vkGetImageMemoryRequirements2KHR;

    [NativeTypeName("PFN_vkBindBufferMemory2KHR _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, uint, Ahjo.Vulkan.Native.VkBindBufferMemoryInfo*, Ahjo.Vulkan.Native.VkResult> vkBindBufferMemory2KHR;

    [NativeTypeName("PFN_vkBindImageMemory2KHR _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, uint, Ahjo.Vulkan.Native.VkBindImageMemoryInfo*, Ahjo.Vulkan.Native.VkResult> vkBindImageMemory2KHR;

    [NativeTypeName("PFN_vkGetPhysicalDeviceMemoryProperties2KHR _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkPhysicalDevice_T*, Ahjo.Vulkan.Native.VkPhysicalDeviceMemoryProperties2*, void> vkGetPhysicalDeviceMemoryProperties2KHR;

    [NativeTypeName("PFN_vkGetDeviceBufferMemoryRequirementsKHR _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, Ahjo.Vulkan.Native.VkDeviceBufferMemoryRequirements*, Ahjo.Vulkan.Native.VkMemoryRequirements2*, void> vkGetDeviceBufferMemoryRequirements;

    [NativeTypeName("PFN_vkGetDeviceImageMemoryRequirementsKHR _Nullable")]
    public delegate* unmanaged[Stdcall]<Ahjo.Vulkan.Native.VkDevice_T*, Ahjo.Vulkan.Native.VkDeviceImageMemoryRequirements*, Ahjo.Vulkan.Native.VkMemoryRequirements2*, void> vkGetDeviceImageMemoryRequirements;

    [NativeTypeName("void * _Nullable")]
    public void* vkGetMemoryWin32HandleKHR;
}
