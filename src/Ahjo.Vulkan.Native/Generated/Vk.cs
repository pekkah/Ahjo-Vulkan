using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Native;

public static unsafe partial class Vk
{
    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateInstance([NativeTypeName("const VkInstanceCreateInfo *")] VkInstanceCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkInstance *")] VkInstance_T** pInstance);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyInstance([NativeTypeName("VkInstance")] VkInstance_T* instance, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkEnumeratePhysicalDevices([NativeTypeName("VkInstance")] VkInstance_T* instance, [NativeTypeName("uint32_t *")] uint* pPhysicalDeviceCount, [NativeTypeName("VkPhysicalDevice *")] VkPhysicalDevice_T** pPhysicalDevices);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceFeatures([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, VkPhysicalDeviceFeatures* pFeatures);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceFormatProperties([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, VkFormat format, VkFormatProperties* pFormatProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceImageFormatProperties([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, VkFormat format, VkImageType type, VkImageTiling tiling, [NativeTypeName("VkImageUsageFlags")] uint usage, [NativeTypeName("VkImageCreateFlags")] uint flags, VkImageFormatProperties* pImageFormatProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceProperties([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, VkPhysicalDeviceProperties* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceQueueFamilyProperties([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t *")] uint* pQueueFamilyPropertyCount, VkQueueFamilyProperties* pQueueFamilyProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceMemoryProperties([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, VkPhysicalDeviceMemoryProperties* pMemoryProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("PFN_vkVoidFunction")]
    public static extern delegate* unmanaged[Stdcall]<void> vkGetInstanceProcAddr([NativeTypeName("VkInstance")] VkInstance_T* instance, [NativeTypeName("const char *")] sbyte* pName);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("PFN_vkVoidFunction")]
    public static extern delegate* unmanaged[Stdcall]<void> vkGetDeviceProcAddr([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const char *")] sbyte* pName);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateDevice([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkDeviceCreateInfo *")] VkDeviceCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkDevice *")] VkDevice_T** pDevice);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyDevice([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkEnumerateInstanceExtensionProperties([NativeTypeName("const char *")] sbyte* pLayerName, [NativeTypeName("uint32_t *")] uint* pPropertyCount, VkExtensionProperties* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkEnumerateDeviceExtensionProperties([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const char *")] sbyte* pLayerName, [NativeTypeName("uint32_t *")] uint* pPropertyCount, VkExtensionProperties* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkEnumerateInstanceLayerProperties([NativeTypeName("uint32_t *")] uint* pPropertyCount, VkLayerProperties* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkEnumerateDeviceLayerProperties([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t *")] uint* pPropertyCount, VkLayerProperties* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDeviceQueue([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint queueFamilyIndex, [NativeTypeName("uint32_t")] uint queueIndex, [NativeTypeName("VkQueue *")] VkQueue_T** pQueue);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkQueueSubmit([NativeTypeName("VkQueue")] VkQueue_T* queue, [NativeTypeName("uint32_t")] uint submitCount, [NativeTypeName("const VkSubmitInfo *")] VkSubmitInfo* pSubmits, [NativeTypeName("VkFence")] VkFence_T* fence);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkQueueWaitIdle([NativeTypeName("VkQueue")] VkQueue_T* queue);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkDeviceWaitIdle([NativeTypeName("VkDevice")] VkDevice_T* device);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkAllocateMemory([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkMemoryAllocateInfo *")] VkMemoryAllocateInfo* pAllocateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkDeviceMemory *")] VkDeviceMemory_T** pMemory);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkFreeMemory([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDeviceMemory")] VkDeviceMemory_T* memory, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkMapMemory([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDeviceMemory")] VkDeviceMemory_T* memory, [NativeTypeName("VkDeviceSize")] ulong offset, [NativeTypeName("VkDeviceSize")] ulong size, [NativeTypeName("VkMemoryMapFlags")] uint flags, void** ppData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkUnmapMemory([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDeviceMemory")] VkDeviceMemory_T* memory);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkFlushMappedMemoryRanges([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint memoryRangeCount, [NativeTypeName("const VkMappedMemoryRange *")] VkMappedMemoryRange* pMemoryRanges);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkInvalidateMappedMemoryRanges([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint memoryRangeCount, [NativeTypeName("const VkMappedMemoryRange *")] VkMappedMemoryRange* pMemoryRanges);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDeviceMemoryCommitment([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDeviceMemory")] VkDeviceMemory_T* memory, [NativeTypeName("VkDeviceSize *")] ulong* pCommittedMemoryInBytes);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkBindBufferMemory([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, [NativeTypeName("VkDeviceMemory")] VkDeviceMemory_T* memory, [NativeTypeName("VkDeviceSize")] ulong memoryOffset);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkBindImageMemory([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkImage")] VkImage_T* image, [NativeTypeName("VkDeviceMemory")] VkDeviceMemory_T* memory, [NativeTypeName("VkDeviceSize")] ulong memoryOffset);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetBufferMemoryRequirements([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, VkMemoryRequirements* pMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetImageMemoryRequirements([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkImage")] VkImage_T* image, VkMemoryRequirements* pMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetImageSparseMemoryRequirements([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkImage")] VkImage_T* image, [NativeTypeName("uint32_t *")] uint* pSparseMemoryRequirementCount, VkSparseImageMemoryRequirements* pSparseMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceSparseImageFormatProperties([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, VkFormat format, VkImageType type, VkSampleCountFlagBits samples, [NativeTypeName("VkImageUsageFlags")] uint usage, VkImageTiling tiling, [NativeTypeName("uint32_t *")] uint* pPropertyCount, VkSparseImageFormatProperties* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkQueueBindSparse([NativeTypeName("VkQueue")] VkQueue_T* queue, [NativeTypeName("uint32_t")] uint bindInfoCount, [NativeTypeName("const VkBindSparseInfo *")] VkBindSparseInfo* pBindInfo, [NativeTypeName("VkFence")] VkFence_T* fence);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateFence([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkFenceCreateInfo *")] VkFenceCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkFence *")] VkFence_T** pFence);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyFence([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkFence")] VkFence_T* fence, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkResetFences([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint fenceCount, [NativeTypeName("const VkFence *")] VkFence_T** pFences);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetFenceStatus([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkFence")] VkFence_T* fence);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkWaitForFences([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint fenceCount, [NativeTypeName("const VkFence *")] VkFence_T** pFences, [NativeTypeName("VkBool32")] uint waitAll, [NativeTypeName("uint64_t")] ulong timeout);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateSemaphore([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkSemaphoreCreateInfo *")] VkSemaphoreCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkSemaphore *")] VkSemaphore_T** pSemaphore);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroySemaphore([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSemaphore")] VkSemaphore_T* semaphore, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateQueryPool([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkQueryPoolCreateInfo *")] VkQueryPoolCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkQueryPool *")] VkQueryPool_T** pQueryPool);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyQueryPool([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkQueryPool")] VkQueryPool_T* queryPool, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetQueryPoolResults([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkQueryPool")] VkQueryPool_T* queryPool, [NativeTypeName("uint32_t")] uint firstQuery, [NativeTypeName("uint32_t")] uint queryCount, [NativeTypeName("size_t")] nuint dataSize, void* pData, [NativeTypeName("VkDeviceSize")] ulong stride, [NativeTypeName("VkQueryResultFlags")] uint flags);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateBuffer([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkBufferCreateInfo *")] VkBufferCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkBuffer *")] VkBuffer_T** pBuffer);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyBuffer([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateImage([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkImageCreateInfo *")] VkImageCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkImage *")] VkImage_T** pImage);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyImage([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkImage")] VkImage_T* image, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetImageSubresourceLayout([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkImage")] VkImage_T* image, [NativeTypeName("const VkImageSubresource *")] VkImageSubresource* pSubresource, VkSubresourceLayout* pLayout);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateImageView([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkImageViewCreateInfo *")] VkImageViewCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkImageView *")] VkImageView_T** pView);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyImageView([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkImageView")] VkImageView_T* imageView, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateCommandPool([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkCommandPoolCreateInfo *")] VkCommandPoolCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkCommandPool *")] VkCommandPool_T** pCommandPool);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyCommandPool([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkCommandPool")] VkCommandPool_T* commandPool, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkResetCommandPool([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkCommandPool")] VkCommandPool_T* commandPool, [NativeTypeName("VkCommandPoolResetFlags")] uint flags);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkAllocateCommandBuffers([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkCommandBufferAllocateInfo *")] VkCommandBufferAllocateInfo* pAllocateInfo, [NativeTypeName("VkCommandBuffer *")] VkCommandBuffer_T** pCommandBuffers);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkFreeCommandBuffers([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkCommandPool")] VkCommandPool_T* commandPool, [NativeTypeName("uint32_t")] uint commandBufferCount, [NativeTypeName("const VkCommandBuffer *")] VkCommandBuffer_T** pCommandBuffers);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkBeginCommandBuffer([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkCommandBufferBeginInfo *")] VkCommandBufferBeginInfo* pBeginInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkEndCommandBuffer([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkResetCommandBuffer([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkCommandBufferResetFlags")] uint flags);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyBuffer([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* srcBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* dstBuffer, [NativeTypeName("uint32_t")] uint regionCount, [NativeTypeName("const VkBufferCopy *")] VkBufferCopy* pRegions);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyImage([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkImage")] VkImage_T* srcImage, VkImageLayout srcImageLayout, [NativeTypeName("VkImage")] VkImage_T* dstImage, VkImageLayout dstImageLayout, [NativeTypeName("uint32_t")] uint regionCount, [NativeTypeName("const VkImageCopy *")] VkImageCopy* pRegions);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyBufferToImage([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* srcBuffer, [NativeTypeName("VkImage")] VkImage_T* dstImage, VkImageLayout dstImageLayout, [NativeTypeName("uint32_t")] uint regionCount, [NativeTypeName("const VkBufferImageCopy *")] VkBufferImageCopy* pRegions);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyImageToBuffer([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkImage")] VkImage_T* srcImage, VkImageLayout srcImageLayout, [NativeTypeName("VkBuffer")] VkBuffer_T* dstBuffer, [NativeTypeName("uint32_t")] uint regionCount, [NativeTypeName("const VkBufferImageCopy *")] VkBufferImageCopy* pRegions);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdUpdateBuffer([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* dstBuffer, [NativeTypeName("VkDeviceSize")] ulong dstOffset, [NativeTypeName("VkDeviceSize")] ulong dataSize, [NativeTypeName("const void *")] void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdFillBuffer([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* dstBuffer, [NativeTypeName("VkDeviceSize")] ulong dstOffset, [NativeTypeName("VkDeviceSize")] ulong size, [NativeTypeName("uint32_t")] uint data);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdPipelineBarrier([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkPipelineStageFlags")] uint srcStageMask, [NativeTypeName("VkPipelineStageFlags")] uint dstStageMask, [NativeTypeName("VkDependencyFlags")] uint dependencyFlags, [NativeTypeName("uint32_t")] uint memoryBarrierCount, [NativeTypeName("const VkMemoryBarrier *")] VkMemoryBarrier* pMemoryBarriers, [NativeTypeName("uint32_t")] uint bufferMemoryBarrierCount, [NativeTypeName("const VkBufferMemoryBarrier *")] VkBufferMemoryBarrier* pBufferMemoryBarriers, [NativeTypeName("uint32_t")] uint imageMemoryBarrierCount, [NativeTypeName("const VkImageMemoryBarrier *")] VkImageMemoryBarrier* pImageMemoryBarriers);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBeginQuery([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkQueryPool")] VkQueryPool_T* queryPool, [NativeTypeName("uint32_t")] uint query, [NativeTypeName("VkQueryControlFlags")] uint flags);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdEndQuery([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkQueryPool")] VkQueryPool_T* queryPool, [NativeTypeName("uint32_t")] uint query);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdResetQueryPool([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkQueryPool")] VkQueryPool_T* queryPool, [NativeTypeName("uint32_t")] uint firstQuery, [NativeTypeName("uint32_t")] uint queryCount);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdWriteTimestamp([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkPipelineStageFlagBits pipelineStage, [NativeTypeName("VkQueryPool")] VkQueryPool_T* queryPool, [NativeTypeName("uint32_t")] uint query);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyQueryPoolResults([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkQueryPool")] VkQueryPool_T* queryPool, [NativeTypeName("uint32_t")] uint firstQuery, [NativeTypeName("uint32_t")] uint queryCount, [NativeTypeName("VkBuffer")] VkBuffer_T* dstBuffer, [NativeTypeName("VkDeviceSize")] ulong dstOffset, [NativeTypeName("VkDeviceSize")] ulong stride, [NativeTypeName("VkQueryResultFlags")] uint flags);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdExecuteCommands([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint commandBufferCount, [NativeTypeName("const VkCommandBuffer *")] VkCommandBuffer_T** pCommandBuffers);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateEvent([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkEventCreateInfo *")] VkEventCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkEvent *")] VkEvent_T** pEvent);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyEvent([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkEvent")] VkEvent_T* @event, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetEventStatus([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkEvent")] VkEvent_T* @event);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkSetEvent([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkEvent")] VkEvent_T* @event);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkResetEvent([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkEvent")] VkEvent_T* @event);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateBufferView([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkBufferViewCreateInfo *")] VkBufferViewCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkBufferView *")] VkBufferView_T** pView);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyBufferView([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkBufferView")] VkBufferView_T* bufferView, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateShaderModule([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkShaderModuleCreateInfo *")] VkShaderModuleCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkShaderModule *")] VkShaderModule_T** pShaderModule);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyShaderModule([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkShaderModule")] VkShaderModule_T* shaderModule, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreatePipelineCache([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkPipelineCacheCreateInfo *")] VkPipelineCacheCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkPipelineCache *")] VkPipelineCache_T** pPipelineCache);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyPipelineCache([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkPipelineCache")] VkPipelineCache_T* pipelineCache, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPipelineCacheData([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkPipelineCache")] VkPipelineCache_T* pipelineCache, [NativeTypeName("size_t *")] nuint* pDataSize, void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkMergePipelineCaches([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkPipelineCache")] VkPipelineCache_T* dstCache, [NativeTypeName("uint32_t")] uint srcCacheCount, [NativeTypeName("const VkPipelineCache *")] VkPipelineCache_T** pSrcCaches);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateComputePipelines([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkPipelineCache")] VkPipelineCache_T* pipelineCache, [NativeTypeName("uint32_t")] uint createInfoCount, [NativeTypeName("const VkComputePipelineCreateInfo *")] VkComputePipelineCreateInfo* pCreateInfos, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkPipeline *")] VkPipeline_T** pPipelines);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyPipeline([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkPipeline")] VkPipeline_T* pipeline, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreatePipelineLayout([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkPipelineLayoutCreateInfo *")] VkPipelineLayoutCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkPipelineLayout *")] VkPipelineLayout_T** pPipelineLayout);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyPipelineLayout([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkPipelineLayout")] VkPipelineLayout_T* pipelineLayout, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateSampler([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkSamplerCreateInfo *")] VkSamplerCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkSampler *")] VkSampler_T** pSampler);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroySampler([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSampler")] VkSampler_T* sampler, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateDescriptorSetLayout([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDescriptorSetLayoutCreateInfo *")] VkDescriptorSetLayoutCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkDescriptorSetLayout *")] VkDescriptorSetLayout_T** pSetLayout);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyDescriptorSetLayout([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDescriptorSetLayout")] VkDescriptorSetLayout_T* descriptorSetLayout, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateDescriptorPool([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDescriptorPoolCreateInfo *")] VkDescriptorPoolCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkDescriptorPool *")] VkDescriptorPool_T** pDescriptorPool);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyDescriptorPool([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDescriptorPool")] VkDescriptorPool_T* descriptorPool, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkResetDescriptorPool([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDescriptorPool")] VkDescriptorPool_T* descriptorPool, [NativeTypeName("VkDescriptorPoolResetFlags")] uint flags);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkAllocateDescriptorSets([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDescriptorSetAllocateInfo *")] VkDescriptorSetAllocateInfo* pAllocateInfo, [NativeTypeName("VkDescriptorSet *")] VkDescriptorSet_T** pDescriptorSets);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkFreeDescriptorSets([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDescriptorPool")] VkDescriptorPool_T* descriptorPool, [NativeTypeName("uint32_t")] uint descriptorSetCount, [NativeTypeName("const VkDescriptorSet *")] VkDescriptorSet_T** pDescriptorSets);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkUpdateDescriptorSets([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint descriptorWriteCount, [NativeTypeName("const VkWriteDescriptorSet *")] VkWriteDescriptorSet* pDescriptorWrites, [NativeTypeName("uint32_t")] uint descriptorCopyCount, [NativeTypeName("const VkCopyDescriptorSet *")] VkCopyDescriptorSet* pDescriptorCopies);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindPipeline([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkPipelineBindPoint pipelineBindPoint, [NativeTypeName("VkPipeline")] VkPipeline_T* pipeline);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindDescriptorSets([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkPipelineBindPoint pipelineBindPoint, [NativeTypeName("VkPipelineLayout")] VkPipelineLayout_T* layout, [NativeTypeName("uint32_t")] uint firstSet, [NativeTypeName("uint32_t")] uint descriptorSetCount, [NativeTypeName("const VkDescriptorSet *")] VkDescriptorSet_T** pDescriptorSets, [NativeTypeName("uint32_t")] uint dynamicOffsetCount, [NativeTypeName("const uint32_t *")] uint* pDynamicOffsets);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdClearColorImage([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkImage")] VkImage_T* image, VkImageLayout imageLayout, [NativeTypeName("const VkClearColorValue *")] VkClearColorValue* pColor, [NativeTypeName("uint32_t")] uint rangeCount, [NativeTypeName("const VkImageSubresourceRange *")] VkImageSubresourceRange* pRanges);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDispatch([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint groupCountX, [NativeTypeName("uint32_t")] uint groupCountY, [NativeTypeName("uint32_t")] uint groupCountZ);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDispatchIndirect([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, [NativeTypeName("VkDeviceSize")] ulong offset);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetEvent([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkEvent")] VkEvent_T* @event, [NativeTypeName("VkPipelineStageFlags")] uint stageMask);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdResetEvent([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkEvent")] VkEvent_T* @event, [NativeTypeName("VkPipelineStageFlags")] uint stageMask);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdWaitEvents([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint eventCount, [NativeTypeName("const VkEvent *")] VkEvent_T** pEvents, [NativeTypeName("VkPipelineStageFlags")] uint srcStageMask, [NativeTypeName("VkPipelineStageFlags")] uint dstStageMask, [NativeTypeName("uint32_t")] uint memoryBarrierCount, [NativeTypeName("const VkMemoryBarrier *")] VkMemoryBarrier* pMemoryBarriers, [NativeTypeName("uint32_t")] uint bufferMemoryBarrierCount, [NativeTypeName("const VkBufferMemoryBarrier *")] VkBufferMemoryBarrier* pBufferMemoryBarriers, [NativeTypeName("uint32_t")] uint imageMemoryBarrierCount, [NativeTypeName("const VkImageMemoryBarrier *")] VkImageMemoryBarrier* pImageMemoryBarriers);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdPushConstants([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkPipelineLayout")] VkPipelineLayout_T* layout, [NativeTypeName("VkShaderStageFlags")] uint stageFlags, [NativeTypeName("uint32_t")] uint offset, [NativeTypeName("uint32_t")] uint size, [NativeTypeName("const void *")] void* pValues);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateGraphicsPipelines([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkPipelineCache")] VkPipelineCache_T* pipelineCache, [NativeTypeName("uint32_t")] uint createInfoCount, [NativeTypeName("const VkGraphicsPipelineCreateInfo *")] VkGraphicsPipelineCreateInfo* pCreateInfos, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkPipeline *")] VkPipeline_T** pPipelines);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateFramebuffer([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkFramebufferCreateInfo *")] VkFramebufferCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkFramebuffer *")] VkFramebuffer_T** pFramebuffer);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyFramebuffer([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkFramebuffer")] VkFramebuffer_T* framebuffer, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateRenderPass([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkRenderPassCreateInfo *")] VkRenderPassCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkRenderPass *")] VkRenderPass_T** pRenderPass);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyRenderPass([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkRenderPass")] VkRenderPass_T* renderPass, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetRenderAreaGranularity([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkRenderPass")] VkRenderPass_T* renderPass, VkExtent2D* pGranularity);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetViewport([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint firstViewport, [NativeTypeName("uint32_t")] uint viewportCount, [NativeTypeName("const VkViewport *")] VkViewport* pViewports);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetScissor([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint firstScissor, [NativeTypeName("uint32_t")] uint scissorCount, [NativeTypeName("const VkRect2D *")] VkRect2D* pScissors);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetLineWidth([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, float lineWidth);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDepthBias([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, float depthBiasConstantFactor, float depthBiasClamp, float depthBiasSlopeFactor);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetBlendConstants([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const float[4]")] float* blendConstants);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDepthBounds([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, float minDepthBounds, float maxDepthBounds);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetStencilCompareMask([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkStencilFaceFlags")] uint faceMask, [NativeTypeName("uint32_t")] uint compareMask);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetStencilWriteMask([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkStencilFaceFlags")] uint faceMask, [NativeTypeName("uint32_t")] uint writeMask);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetStencilReference([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkStencilFaceFlags")] uint faceMask, [NativeTypeName("uint32_t")] uint reference);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindIndexBuffer([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, [NativeTypeName("VkDeviceSize")] ulong offset, VkIndexType indexType);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindVertexBuffers([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint firstBinding, [NativeTypeName("uint32_t")] uint bindingCount, [NativeTypeName("const VkBuffer *")] VkBuffer_T** pBuffers, [NativeTypeName("const VkDeviceSize *")] ulong* pOffsets);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDraw([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint vertexCount, [NativeTypeName("uint32_t")] uint instanceCount, [NativeTypeName("uint32_t")] uint firstVertex, [NativeTypeName("uint32_t")] uint firstInstance);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawIndexed([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint indexCount, [NativeTypeName("uint32_t")] uint instanceCount, [NativeTypeName("uint32_t")] uint firstIndex, [NativeTypeName("int32_t")] int vertexOffset, [NativeTypeName("uint32_t")] uint firstInstance);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawIndirect([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, [NativeTypeName("VkDeviceSize")] ulong offset, [NativeTypeName("uint32_t")] uint drawCount, [NativeTypeName("uint32_t")] uint stride);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawIndexedIndirect([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, [NativeTypeName("VkDeviceSize")] ulong offset, [NativeTypeName("uint32_t")] uint drawCount, [NativeTypeName("uint32_t")] uint stride);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBlitImage([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkImage")] VkImage_T* srcImage, VkImageLayout srcImageLayout, [NativeTypeName("VkImage")] VkImage_T* dstImage, VkImageLayout dstImageLayout, [NativeTypeName("uint32_t")] uint regionCount, [NativeTypeName("const VkImageBlit *")] VkImageBlit* pRegions, VkFilter filter);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdClearDepthStencilImage([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkImage")] VkImage_T* image, VkImageLayout imageLayout, [NativeTypeName("const VkClearDepthStencilValue *")] VkClearDepthStencilValue* pDepthStencil, [NativeTypeName("uint32_t")] uint rangeCount, [NativeTypeName("const VkImageSubresourceRange *")] VkImageSubresourceRange* pRanges);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdClearAttachments([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint attachmentCount, [NativeTypeName("const VkClearAttachment *")] VkClearAttachment* pAttachments, [NativeTypeName("uint32_t")] uint rectCount, [NativeTypeName("const VkClearRect *")] VkClearRect* pRects);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdResolveImage([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkImage")] VkImage_T* srcImage, VkImageLayout srcImageLayout, [NativeTypeName("VkImage")] VkImage_T* dstImage, VkImageLayout dstImageLayout, [NativeTypeName("uint32_t")] uint regionCount, [NativeTypeName("const VkImageResolve *")] VkImageResolve* pRegions);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBeginRenderPass([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkRenderPassBeginInfo *")] VkRenderPassBeginInfo* pRenderPassBegin, VkSubpassContents contents);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdNextSubpass([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkSubpassContents contents);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdEndRenderPass([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkEnumerateInstanceVersion([NativeTypeName("uint32_t *")] uint* pApiVersion);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkBindBufferMemory2([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint bindInfoCount, [NativeTypeName("const VkBindBufferMemoryInfo *")] VkBindBufferMemoryInfo* pBindInfos);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkBindImageMemory2([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint bindInfoCount, [NativeTypeName("const VkBindImageMemoryInfo *")] VkBindImageMemoryInfo* pBindInfos);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDeviceGroupPeerMemoryFeatures([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint heapIndex, [NativeTypeName("uint32_t")] uint localDeviceIndex, [NativeTypeName("uint32_t")] uint remoteDeviceIndex, [NativeTypeName("VkPeerMemoryFeatureFlags *")] uint* pPeerMemoryFeatures);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDeviceMask([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint deviceMask);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkEnumeratePhysicalDeviceGroups([NativeTypeName("VkInstance")] VkInstance_T* instance, [NativeTypeName("uint32_t *")] uint* pPhysicalDeviceGroupCount, VkPhysicalDeviceGroupProperties* pPhysicalDeviceGroupProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetImageMemoryRequirements2([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkImageMemoryRequirementsInfo2 *")] VkImageMemoryRequirementsInfo2* pInfo, VkMemoryRequirements2* pMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetBufferMemoryRequirements2([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkBufferMemoryRequirementsInfo2 *")] VkBufferMemoryRequirementsInfo2* pInfo, VkMemoryRequirements2* pMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetImageSparseMemoryRequirements2([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkImageSparseMemoryRequirementsInfo2 *")] VkImageSparseMemoryRequirementsInfo2* pInfo, [NativeTypeName("uint32_t *")] uint* pSparseMemoryRequirementCount, VkSparseImageMemoryRequirements2* pSparseMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceFeatures2([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, VkPhysicalDeviceFeatures2* pFeatures);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceProperties2([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, VkPhysicalDeviceProperties2* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceFormatProperties2([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, VkFormat format, VkFormatProperties2* pFormatProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceImageFormatProperties2([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkPhysicalDeviceImageFormatInfo2 *")] VkPhysicalDeviceImageFormatInfo2* pImageFormatInfo, VkImageFormatProperties2* pImageFormatProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceQueueFamilyProperties2([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t *")] uint* pQueueFamilyPropertyCount, VkQueueFamilyProperties2* pQueueFamilyProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceMemoryProperties2([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, VkPhysicalDeviceMemoryProperties2* pMemoryProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceSparseImageFormatProperties2([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkPhysicalDeviceSparseImageFormatInfo2 *")] VkPhysicalDeviceSparseImageFormatInfo2* pFormatInfo, [NativeTypeName("uint32_t *")] uint* pPropertyCount, VkSparseImageFormatProperties2* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkTrimCommandPool([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkCommandPool")] VkCommandPool_T* commandPool, [NativeTypeName("VkCommandPoolTrimFlags")] uint flags);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDeviceQueue2([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDeviceQueueInfo2 *")] VkDeviceQueueInfo2* pQueueInfo, [NativeTypeName("VkQueue *")] VkQueue_T** pQueue);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceExternalBufferProperties([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkPhysicalDeviceExternalBufferInfo *")] VkPhysicalDeviceExternalBufferInfo* pExternalBufferInfo, VkExternalBufferProperties* pExternalBufferProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceExternalFenceProperties([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkPhysicalDeviceExternalFenceInfo *")] VkPhysicalDeviceExternalFenceInfo* pExternalFenceInfo, VkExternalFenceProperties* pExternalFenceProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceExternalSemaphoreProperties([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkPhysicalDeviceExternalSemaphoreInfo *")] VkPhysicalDeviceExternalSemaphoreInfo* pExternalSemaphoreInfo, VkExternalSemaphoreProperties* pExternalSemaphoreProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDispatchBase([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint baseGroupX, [NativeTypeName("uint32_t")] uint baseGroupY, [NativeTypeName("uint32_t")] uint baseGroupZ, [NativeTypeName("uint32_t")] uint groupCountX, [NativeTypeName("uint32_t")] uint groupCountY, [NativeTypeName("uint32_t")] uint groupCountZ);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateDescriptorUpdateTemplate([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDescriptorUpdateTemplateCreateInfo *")] VkDescriptorUpdateTemplateCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkDescriptorUpdateTemplate *")] VkDescriptorUpdateTemplate_T** pDescriptorUpdateTemplate);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyDescriptorUpdateTemplate([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDescriptorUpdateTemplate")] VkDescriptorUpdateTemplate_T* descriptorUpdateTemplate, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkUpdateDescriptorSetWithTemplate([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDescriptorSet")] VkDescriptorSet_T* descriptorSet, [NativeTypeName("VkDescriptorUpdateTemplate")] VkDescriptorUpdateTemplate_T* descriptorUpdateTemplate, [NativeTypeName("const void *")] void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDescriptorSetLayoutSupport([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDescriptorSetLayoutCreateInfo *")] VkDescriptorSetLayoutCreateInfo* pCreateInfo, VkDescriptorSetLayoutSupport* pSupport);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateSamplerYcbcrConversion([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkSamplerYcbcrConversionCreateInfo *")] VkSamplerYcbcrConversionCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkSamplerYcbcrConversion *")] VkSamplerYcbcrConversion_T** pYcbcrConversion);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroySamplerYcbcrConversion([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSamplerYcbcrConversion")] VkSamplerYcbcrConversion_T* ycbcrConversion, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkResetQueryPool([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkQueryPool")] VkQueryPool_T* queryPool, [NativeTypeName("uint32_t")] uint firstQuery, [NativeTypeName("uint32_t")] uint queryCount);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetSemaphoreCounterValue([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSemaphore")] VkSemaphore_T* semaphore, [NativeTypeName("uint64_t *")] ulong* pValue);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkWaitSemaphores([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkSemaphoreWaitInfo *")] VkSemaphoreWaitInfo* pWaitInfo, [NativeTypeName("uint64_t")] ulong timeout);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkSignalSemaphore([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkSemaphoreSignalInfo *")] VkSemaphoreSignalInfo* pSignalInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("VkDeviceAddress")]
    public static extern ulong vkGetBufferDeviceAddress([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkBufferDeviceAddressInfo *")] VkBufferDeviceAddressInfo* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("uint64_t")]
    public static extern ulong vkGetBufferOpaqueCaptureAddress([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkBufferDeviceAddressInfo *")] VkBufferDeviceAddressInfo* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("uint64_t")]
    public static extern ulong vkGetDeviceMemoryOpaqueCaptureAddress([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDeviceMemoryOpaqueCaptureAddressInfo *")] VkDeviceMemoryOpaqueCaptureAddressInfo* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawIndirectCount([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, [NativeTypeName("VkDeviceSize")] ulong offset, [NativeTypeName("VkBuffer")] VkBuffer_T* countBuffer, [NativeTypeName("VkDeviceSize")] ulong countBufferOffset, [NativeTypeName("uint32_t")] uint maxDrawCount, [NativeTypeName("uint32_t")] uint stride);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawIndexedIndirectCount([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, [NativeTypeName("VkDeviceSize")] ulong offset, [NativeTypeName("VkBuffer")] VkBuffer_T* countBuffer, [NativeTypeName("VkDeviceSize")] ulong countBufferOffset, [NativeTypeName("uint32_t")] uint maxDrawCount, [NativeTypeName("uint32_t")] uint stride);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateRenderPass2([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkRenderPassCreateInfo2 *")] VkRenderPassCreateInfo2* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkRenderPass *")] VkRenderPass_T** pRenderPass);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBeginRenderPass2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkRenderPassBeginInfo *")] VkRenderPassBeginInfo* pRenderPassBegin, [NativeTypeName("const VkSubpassBeginInfo *")] VkSubpassBeginInfo* pSubpassBeginInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdNextSubpass2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkSubpassBeginInfo *")] VkSubpassBeginInfo* pSubpassBeginInfo, [NativeTypeName("const VkSubpassEndInfo *")] VkSubpassEndInfo* pSubpassEndInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdEndRenderPass2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkSubpassEndInfo *")] VkSubpassEndInfo* pSubpassEndInfo);

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_NONE = 0UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_TOP_OF_PIPE_BIT = 0x00000001UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_DRAW_INDIRECT_BIT = 0x00000002UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_VERTEX_INPUT_BIT = 0x00000004UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_VERTEX_SHADER_BIT = 0x00000008UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_TESSELLATION_CONTROL_SHADER_BIT = 0x00000010UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_TESSELLATION_EVALUATION_SHADER_BIT = 0x00000020UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_GEOMETRY_SHADER_BIT = 0x00000040UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_FRAGMENT_SHADER_BIT = 0x00000080UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_EARLY_FRAGMENT_TESTS_BIT = 0x00000100UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_LATE_FRAGMENT_TESTS_BIT = 0x00000200UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_COLOR_ATTACHMENT_OUTPUT_BIT = 0x00000400UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_COMPUTE_SHADER_BIT = 0x00000800UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_ALL_TRANSFER_BIT = 0x00001000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_TRANSFER_BIT = 0x00001000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_BOTTOM_OF_PIPE_BIT = 0x00002000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_HOST_BIT = 0x00004000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_ALL_GRAPHICS_BIT = 0x00008000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_ALL_COMMANDS_BIT = 0x00010000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_COPY_BIT = 0x100000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_RESOLVE_BIT = 0x200000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_BLIT_BIT = 0x400000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_CLEAR_BIT = 0x800000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_INDEX_INPUT_BIT = 0x1000000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_VERTEX_ATTRIBUTE_INPUT_BIT = 0x2000000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_PRE_RASTERIZATION_SHADERS_BIT = 0x4000000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_VIDEO_DECODE_BIT_KHR = 0x04000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_VIDEO_ENCODE_BIT_KHR = 0x08000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_NONE_KHR = 0UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_TOP_OF_PIPE_BIT_KHR = 0x00000001UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_DRAW_INDIRECT_BIT_KHR = 0x00000002UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_VERTEX_INPUT_BIT_KHR = 0x00000004UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_VERTEX_SHADER_BIT_KHR = 0x00000008UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_TESSELLATION_CONTROL_SHADER_BIT_KHR = 0x00000010UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_TESSELLATION_EVALUATION_SHADER_BIT_KHR = 0x00000020UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_GEOMETRY_SHADER_BIT_KHR = 0x00000040UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_FRAGMENT_SHADER_BIT_KHR = 0x00000080UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_EARLY_FRAGMENT_TESTS_BIT_KHR = 0x00000100UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_LATE_FRAGMENT_TESTS_BIT_KHR = 0x00000200UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_COLOR_ATTACHMENT_OUTPUT_BIT_KHR = 0x00000400UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_COMPUTE_SHADER_BIT_KHR = 0x00000800UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_ALL_TRANSFER_BIT_KHR = 0x00001000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_TRANSFER_BIT_KHR = 0x00001000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_BOTTOM_OF_PIPE_BIT_KHR = 0x00002000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_HOST_BIT_KHR = 0x00004000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_ALL_GRAPHICS_BIT_KHR = 0x00008000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_ALL_COMMANDS_BIT_KHR = 0x00010000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_COPY_BIT_KHR = 0x100000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_RESOLVE_BIT_KHR = 0x200000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_BLIT_BIT_KHR = 0x400000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_CLEAR_BIT_KHR = 0x800000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_INDEX_INPUT_BIT_KHR = 0x1000000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_VERTEX_ATTRIBUTE_INPUT_BIT_KHR = 0x2000000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_PRE_RASTERIZATION_SHADERS_BIT_KHR = 0x4000000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_TRANSFORM_FEEDBACK_BIT_EXT = 0x01000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_CONDITIONAL_RENDERING_BIT_EXT = 0x00040000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_COMMAND_PREPROCESS_BIT_NV = 0x00020000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_COMMAND_PREPROCESS_BIT_EXT = 0x00020000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_FRAGMENT_SHADING_RATE_ATTACHMENT_BIT_KHR = 0x00400000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_SHADING_RATE_IMAGE_BIT_NV = 0x00400000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_ACCELERATION_STRUCTURE_BUILD_BIT_KHR = 0x02000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_RAY_TRACING_SHADER_BIT_KHR = 0x00200000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_RAY_TRACING_SHADER_BIT_NV = 0x00200000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_ACCELERATION_STRUCTURE_BUILD_BIT_NV = 0x02000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_FRAGMENT_DENSITY_PROCESS_BIT_EXT = 0x00800000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_TASK_SHADER_BIT_NV = 0x00080000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_MESH_SHADER_BIT_NV = 0x00100000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_TASK_SHADER_BIT_EXT = 0x00080000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_MESH_SHADER_BIT_EXT = 0x00100000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_SUBPASS_SHADER_BIT_HUAWEI = 0x8000000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_SUBPASS_SHADING_BIT_HUAWEI = 0x8000000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_INVOCATION_MASK_BIT_HUAWEI = 0x10000000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_ACCELERATION_STRUCTURE_COPY_BIT_KHR = 0x10000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_MICROMAP_BUILD_BIT_EXT = 0x40000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_CLUSTER_CULLING_SHADER_BIT_HUAWEI = 0x20000000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_OPTICAL_FLOW_BIT_NV = 0x20000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_CONVERT_COOPERATIVE_VECTOR_MATRIX_BIT_NV = 0x100000000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_DATA_GRAPH_BIT_ARM = 0x40000000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_COPY_INDIRECT_BIT_KHR = 0x400000000000UL;

    [NativeTypeName("const VkPipelineStageFlagBits2")]
    public const ulong VK_PIPELINE_STAGE_2_MEMORY_DECOMPRESSION_BIT_EXT = 0x200000000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_NONE = 0UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_INDIRECT_COMMAND_READ_BIT = 0x00000001UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_INDEX_READ_BIT = 0x00000002UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_VERTEX_ATTRIBUTE_READ_BIT = 0x00000004UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_UNIFORM_READ_BIT = 0x00000008UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_INPUT_ATTACHMENT_READ_BIT = 0x00000010UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_SHADER_READ_BIT = 0x00000020UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_SHADER_WRITE_BIT = 0x00000040UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_COLOR_ATTACHMENT_READ_BIT = 0x00000080UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_COLOR_ATTACHMENT_WRITE_BIT = 0x00000100UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_DEPTH_STENCIL_ATTACHMENT_READ_BIT = 0x00000200UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT = 0x00000400UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_TRANSFER_READ_BIT = 0x00000800UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_TRANSFER_WRITE_BIT = 0x00001000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_HOST_READ_BIT = 0x00002000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_HOST_WRITE_BIT = 0x00004000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_MEMORY_READ_BIT = 0x00008000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_MEMORY_WRITE_BIT = 0x00010000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_SHADER_SAMPLED_READ_BIT = 0x100000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_SHADER_STORAGE_READ_BIT = 0x200000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_SHADER_STORAGE_WRITE_BIT = 0x400000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_VIDEO_DECODE_READ_BIT_KHR = 0x800000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_VIDEO_DECODE_WRITE_BIT_KHR = 0x1000000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_SAMPLER_HEAP_READ_BIT_EXT = 0x200000000000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_RESOURCE_HEAP_READ_BIT_EXT = 0x400000000000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_VIDEO_ENCODE_READ_BIT_KHR = 0x2000000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_VIDEO_ENCODE_WRITE_BIT_KHR = 0x4000000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_SHADER_TILE_ATTACHMENT_READ_BIT_QCOM = 0x8000000000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_SHADER_TILE_ATTACHMENT_WRITE_BIT_QCOM = 0x10000000000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_NONE_KHR = 0UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_INDIRECT_COMMAND_READ_BIT_KHR = 0x00000001UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_INDEX_READ_BIT_KHR = 0x00000002UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_VERTEX_ATTRIBUTE_READ_BIT_KHR = 0x00000004UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_UNIFORM_READ_BIT_KHR = 0x00000008UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_INPUT_ATTACHMENT_READ_BIT_KHR = 0x00000010UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_SHADER_READ_BIT_KHR = 0x00000020UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_SHADER_WRITE_BIT_KHR = 0x00000040UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_COLOR_ATTACHMENT_READ_BIT_KHR = 0x00000080UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_COLOR_ATTACHMENT_WRITE_BIT_KHR = 0x00000100UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_DEPTH_STENCIL_ATTACHMENT_READ_BIT_KHR = 0x00000200UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT_KHR = 0x00000400UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_TRANSFER_READ_BIT_KHR = 0x00000800UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_TRANSFER_WRITE_BIT_KHR = 0x00001000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_HOST_READ_BIT_KHR = 0x00002000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_HOST_WRITE_BIT_KHR = 0x00004000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_MEMORY_READ_BIT_KHR = 0x00008000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_MEMORY_WRITE_BIT_KHR = 0x00010000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_SHADER_SAMPLED_READ_BIT_KHR = 0x100000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_SHADER_STORAGE_READ_BIT_KHR = 0x200000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_SHADER_STORAGE_WRITE_BIT_KHR = 0x400000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_TRANSFORM_FEEDBACK_WRITE_BIT_EXT = 0x02000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_TRANSFORM_FEEDBACK_COUNTER_READ_BIT_EXT = 0x04000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_TRANSFORM_FEEDBACK_COUNTER_WRITE_BIT_EXT = 0x08000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_CONDITIONAL_RENDERING_READ_BIT_EXT = 0x00100000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_COMMAND_PREPROCESS_READ_BIT_NV = 0x00020000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_COMMAND_PREPROCESS_WRITE_BIT_NV = 0x00040000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_COMMAND_PREPROCESS_READ_BIT_EXT = 0x00020000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_COMMAND_PREPROCESS_WRITE_BIT_EXT = 0x00040000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_FRAGMENT_SHADING_RATE_ATTACHMENT_READ_BIT_KHR = 0x00800000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_SHADING_RATE_IMAGE_READ_BIT_NV = 0x00800000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_ACCELERATION_STRUCTURE_READ_BIT_KHR = 0x00200000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_ACCELERATION_STRUCTURE_WRITE_BIT_KHR = 0x00400000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_ACCELERATION_STRUCTURE_READ_BIT_NV = 0x00200000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_ACCELERATION_STRUCTURE_WRITE_BIT_NV = 0x00400000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_FRAGMENT_DENSITY_MAP_READ_BIT_EXT = 0x01000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_COLOR_ATTACHMENT_READ_NONCOHERENT_BIT_EXT = 0x00080000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_DESCRIPTOR_BUFFER_READ_BIT_EXT = 0x20000000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_INVOCATION_MASK_READ_BIT_HUAWEI = 0x8000000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_SHADER_BINDING_TABLE_READ_BIT_KHR = 0x10000000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_MICROMAP_READ_BIT_EXT = 0x100000000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_MICROMAP_WRITE_BIT_EXT = 0x200000000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_OPTICAL_FLOW_READ_BIT_NV = 0x40000000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_OPTICAL_FLOW_WRITE_BIT_NV = 0x80000000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_DATA_GRAPH_READ_BIT_ARM = 0x800000000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_DATA_GRAPH_WRITE_BIT_ARM = 0x1000000000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_MEMORY_DECOMPRESSION_READ_BIT_EXT = 0x80000000000000UL;

    [NativeTypeName("const VkAccessFlagBits2")]
    public const ulong VK_ACCESS_2_MEMORY_DECOMPRESSION_WRITE_BIT_EXT = 0x100000000000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_BIT = 0x00000001UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_STORAGE_IMAGE_BIT = 0x00000002UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_STORAGE_IMAGE_ATOMIC_BIT = 0x00000004UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_UNIFORM_TEXEL_BUFFER_BIT = 0x00000008UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_STORAGE_TEXEL_BUFFER_BIT = 0x00000010UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_STORAGE_TEXEL_BUFFER_ATOMIC_BIT = 0x00000020UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_VERTEX_BUFFER_BIT = 0x00000040UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_COLOR_ATTACHMENT_BIT = 0x00000080UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_COLOR_ATTACHMENT_BLEND_BIT = 0x00000100UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_DEPTH_STENCIL_ATTACHMENT_BIT = 0x00000200UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_BLIT_SRC_BIT = 0x00000400UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_BLIT_DST_BIT = 0x00000800UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_FILTER_LINEAR_BIT = 0x00001000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_TRANSFER_SRC_BIT = 0x00004000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_TRANSFER_DST_BIT = 0x00008000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_FILTER_MINMAX_BIT = 0x00010000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_MIDPOINT_CHROMA_SAMPLES_BIT = 0x00020000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_YCBCR_CONVERSION_LINEAR_FILTER_BIT = 0x00040000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_YCBCR_CONVERSION_SEPARATE_RECONSTRUCTION_FILTER_BIT = 0x00080000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_YCBCR_CONVERSION_CHROMA_RECONSTRUCTION_EXPLICIT_BIT = 0x00100000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_YCBCR_CONVERSION_CHROMA_RECONSTRUCTION_EXPLICIT_FORCEABLE_BIT = 0x00200000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_DISJOINT_BIT = 0x00400000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_COSITED_CHROMA_SAMPLES_BIT = 0x00800000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_STORAGE_READ_WITHOUT_FORMAT_BIT = 0x80000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_STORAGE_WRITE_WITHOUT_FORMAT_BIT = 0x100000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_DEPTH_COMPARISON_BIT = 0x200000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_FILTER_CUBIC_BIT = 0x00002000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_HOST_IMAGE_TRANSFER_BIT = 0x400000000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_VIDEO_DECODE_OUTPUT_BIT_KHR = 0x02000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_VIDEO_DECODE_DPB_BIT_KHR = 0x04000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_ACCELERATION_STRUCTURE_VERTEX_BUFFER_BIT_KHR = 0x20000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_FRAGMENT_DENSITY_MAP_BIT_EXT = 0x01000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_FRAGMENT_SHADING_RATE_ATTACHMENT_BIT_KHR = 0x40000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_HOST_IMAGE_TRANSFER_BIT_EXT = 0x400000000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_VIDEO_ENCODE_INPUT_BIT_KHR = 0x08000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_VIDEO_ENCODE_DPB_BIT_KHR = 0x10000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_BIT_KHR = 0x00000001UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_STORAGE_IMAGE_BIT_KHR = 0x00000002UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_STORAGE_IMAGE_ATOMIC_BIT_KHR = 0x00000004UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_UNIFORM_TEXEL_BUFFER_BIT_KHR = 0x00000008UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_STORAGE_TEXEL_BUFFER_BIT_KHR = 0x00000010UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_STORAGE_TEXEL_BUFFER_ATOMIC_BIT_KHR = 0x00000020UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_VERTEX_BUFFER_BIT_KHR = 0x00000040UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_COLOR_ATTACHMENT_BIT_KHR = 0x00000080UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_COLOR_ATTACHMENT_BLEND_BIT_KHR = 0x00000100UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_DEPTH_STENCIL_ATTACHMENT_BIT_KHR = 0x00000200UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_BLIT_SRC_BIT_KHR = 0x00000400UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_BLIT_DST_BIT_KHR = 0x00000800UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_FILTER_LINEAR_BIT_KHR = 0x00001000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_TRANSFER_SRC_BIT_KHR = 0x00004000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_TRANSFER_DST_BIT_KHR = 0x00008000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_MIDPOINT_CHROMA_SAMPLES_BIT_KHR = 0x00020000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_YCBCR_CONVERSION_LINEAR_FILTER_BIT_KHR = 0x00040000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_YCBCR_CONVERSION_SEPARATE_RECONSTRUCTION_FILTER_BIT_KHR = 0x00080000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_YCBCR_CONVERSION_CHROMA_RECONSTRUCTION_EXPLICIT_BIT_KHR = 0x00100000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_YCBCR_CONVERSION_CHROMA_RECONSTRUCTION_EXPLICIT_FORCEABLE_BIT_KHR = 0x00200000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_DISJOINT_BIT_KHR = 0x00400000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_COSITED_CHROMA_SAMPLES_BIT_KHR = 0x00800000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_STORAGE_READ_WITHOUT_FORMAT_BIT_KHR = 0x80000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_STORAGE_WRITE_WITHOUT_FORMAT_BIT_KHR = 0x100000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_DEPTH_COMPARISON_BIT_KHR = 0x200000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_FILTER_MINMAX_BIT_KHR = 0x00010000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_FILTER_CUBIC_BIT_EXT = 0x00002000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_ACCELERATION_STRUCTURE_RADIUS_BUFFER_BIT_NV = 0x8000000000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_LINEAR_COLOR_ATTACHMENT_BIT_NV = 0x4000000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_WEIGHT_IMAGE_BIT_QCOM = 0x400000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_WEIGHT_SAMPLED_IMAGE_BIT_QCOM = 0x800000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_BLOCK_MATCHING_BIT_QCOM = 0x1000000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_BOX_FILTER_SAMPLED_BIT_QCOM = 0x2000000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_TENSOR_SHADER_BIT_ARM = 0x8000000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_TENSOR_IMAGE_ALIASING_BIT_ARM = 0x80000000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_OPTICAL_FLOW_IMAGE_BIT_NV = 0x10000000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_OPTICAL_FLOW_VECTOR_BIT_NV = 0x20000000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_OPTICAL_FLOW_COST_BIT_NV = 0x40000000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_TENSOR_DATA_GRAPH_BIT_ARM = 0x1000000000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_COPY_IMAGE_INDIRECT_DST_BIT_KHR = 0x800000000000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_VIDEO_ENCODE_QUANTIZATION_DELTA_MAP_BIT_KHR = 0x2000000000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_VIDEO_ENCODE_EMPHASIS_MAP_BIT_KHR = 0x4000000000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_DEPTH_COPY_ON_COMPUTE_QUEUE_BIT_KHR = 0x10000000000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_DEPTH_COPY_ON_TRANSFER_QUEUE_BIT_KHR = 0x20000000000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_STENCIL_COPY_ON_COMPUTE_QUEUE_BIT_KHR = 0x40000000000000UL;

    [NativeTypeName("const VkFormatFeatureFlagBits2")]
    public const ulong VK_FORMAT_FEATURE_2_STENCIL_COPY_ON_TRANSFER_QUEUE_BIT_KHR = 0x80000000000000UL;

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceToolProperties([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t *")] uint* pToolCount, VkPhysicalDeviceToolProperties* pToolProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreatePrivateDataSlot([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkPrivateDataSlotCreateInfo *")] VkPrivateDataSlotCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkPrivateDataSlot *")] VkPrivateDataSlot_T** pPrivateDataSlot);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyPrivateDataSlot([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkPrivateDataSlot")] VkPrivateDataSlot_T* privateDataSlot, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkSetPrivateData([NativeTypeName("VkDevice")] VkDevice_T* device, VkObjectType objectType, [NativeTypeName("uint64_t")] ulong objectHandle, [NativeTypeName("VkPrivateDataSlot")] VkPrivateDataSlot_T* privateDataSlot, [NativeTypeName("uint64_t")] ulong data);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPrivateData([NativeTypeName("VkDevice")] VkDevice_T* device, VkObjectType objectType, [NativeTypeName("uint64_t")] ulong objectHandle, [NativeTypeName("VkPrivateDataSlot")] VkPrivateDataSlot_T* privateDataSlot, [NativeTypeName("uint64_t *")] ulong* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdPipelineBarrier2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkDependencyInfo *")] VkDependencyInfo* pDependencyInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdWriteTimestamp2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkPipelineStageFlags2")] ulong stage, [NativeTypeName("VkQueryPool")] VkQueryPool_T* queryPool, [NativeTypeName("uint32_t")] uint query);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkQueueSubmit2([NativeTypeName("VkQueue")] VkQueue_T* queue, [NativeTypeName("uint32_t")] uint submitCount, [NativeTypeName("const VkSubmitInfo2 *")] VkSubmitInfo2* pSubmits, [NativeTypeName("VkFence")] VkFence_T* fence);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyBuffer2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkCopyBufferInfo2 *")] VkCopyBufferInfo2* pCopyBufferInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyImage2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkCopyImageInfo2 *")] VkCopyImageInfo2* pCopyImageInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyBufferToImage2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkCopyBufferToImageInfo2 *")] VkCopyBufferToImageInfo2* pCopyBufferToImageInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyImageToBuffer2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkCopyImageToBufferInfo2 *")] VkCopyImageToBufferInfo2* pCopyImageToBufferInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDeviceBufferMemoryRequirements([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDeviceBufferMemoryRequirements *")] VkDeviceBufferMemoryRequirements* pInfo, VkMemoryRequirements2* pMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDeviceImageMemoryRequirements([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDeviceImageMemoryRequirements *")] VkDeviceImageMemoryRequirements* pInfo, VkMemoryRequirements2* pMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDeviceImageSparseMemoryRequirements([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDeviceImageMemoryRequirements *")] VkDeviceImageMemoryRequirements* pInfo, [NativeTypeName("uint32_t *")] uint* pSparseMemoryRequirementCount, VkSparseImageMemoryRequirements2* pSparseMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetEvent2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkEvent")] VkEvent_T* @event, [NativeTypeName("const VkDependencyInfo *")] VkDependencyInfo* pDependencyInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdResetEvent2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkEvent")] VkEvent_T* @event, [NativeTypeName("VkPipelineStageFlags2")] ulong stageMask);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdWaitEvents2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint eventCount, [NativeTypeName("const VkEvent *")] VkEvent_T** pEvents, [NativeTypeName("const VkDependencyInfo *")] VkDependencyInfo* pDependencyInfos);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBlitImage2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkBlitImageInfo2 *")] VkBlitImageInfo2* pBlitImageInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdResolveImage2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkResolveImageInfo2 *")] VkResolveImageInfo2* pResolveImageInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBeginRendering([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkRenderingInfo *")] VkRenderingInfo* pRenderingInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdEndRendering([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetCullMode([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkCullModeFlags")] uint cullMode);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetFrontFace([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkFrontFace frontFace);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetPrimitiveTopology([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkPrimitiveTopology primitiveTopology);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetViewportWithCount([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint viewportCount, [NativeTypeName("const VkViewport *")] VkViewport* pViewports);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetScissorWithCount([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint scissorCount, [NativeTypeName("const VkRect2D *")] VkRect2D* pScissors);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindVertexBuffers2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint firstBinding, [NativeTypeName("uint32_t")] uint bindingCount, [NativeTypeName("const VkBuffer *")] VkBuffer_T** pBuffers, [NativeTypeName("const VkDeviceSize *")] ulong* pOffsets, [NativeTypeName("const VkDeviceSize *")] ulong* pSizes, [NativeTypeName("const VkDeviceSize *")] ulong* pStrides);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDepthTestEnable([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint depthTestEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDepthWriteEnable([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint depthWriteEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDepthCompareOp([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkCompareOp depthCompareOp);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDepthBoundsTestEnable([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint depthBoundsTestEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetStencilTestEnable([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint stencilTestEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetStencilOp([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkStencilFaceFlags")] uint faceMask, VkStencilOp failOp, VkStencilOp passOp, VkStencilOp depthFailOp, VkCompareOp compareOp);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetRasterizerDiscardEnable([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint rasterizerDiscardEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDepthBiasEnable([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint depthBiasEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetPrimitiveRestartEnable([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint primitiveRestartEnable);

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_TRANSFER_SRC_BIT = 0x00000001UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_TRANSFER_DST_BIT = 0x00000002UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_UNIFORM_TEXEL_BUFFER_BIT = 0x00000004UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_STORAGE_TEXEL_BUFFER_BIT = 0x00000008UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_UNIFORM_BUFFER_BIT = 0x00000010UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_STORAGE_BUFFER_BIT = 0x00000020UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_INDEX_BUFFER_BIT = 0x00000040UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_VERTEX_BUFFER_BIT = 0x00000080UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_INDIRECT_BUFFER_BIT = 0x00000100UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_SHADER_DEVICE_ADDRESS_BIT = 0x00020000UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_DESCRIPTOR_HEAP_BIT_EXT = 0x10000000UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_TRANSFER_SRC_BIT_KHR = 0x00000001UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_TRANSFER_DST_BIT_KHR = 0x00000002UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_UNIFORM_TEXEL_BUFFER_BIT_KHR = 0x00000004UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_STORAGE_TEXEL_BUFFER_BIT_KHR = 0x00000008UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_UNIFORM_BUFFER_BIT_KHR = 0x00000010UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_STORAGE_BUFFER_BIT_KHR = 0x00000020UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_INDEX_BUFFER_BIT_KHR = 0x00000040UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_VERTEX_BUFFER_BIT_KHR = 0x00000080UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_INDIRECT_BUFFER_BIT_KHR = 0x00000100UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_CONDITIONAL_RENDERING_BIT_EXT = 0x00000200UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_SHADER_BINDING_TABLE_BIT_KHR = 0x00000400UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_RAY_TRACING_BIT_NV = 0x00000400UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_TRANSFORM_FEEDBACK_BUFFER_BIT_EXT = 0x00000800UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_TRANSFORM_FEEDBACK_COUNTER_BUFFER_BIT_EXT = 0x00001000UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_VIDEO_DECODE_SRC_BIT_KHR = 0x00002000UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_VIDEO_DECODE_DST_BIT_KHR = 0x00004000UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_VIDEO_ENCODE_DST_BIT_KHR = 0x00008000UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_VIDEO_ENCODE_SRC_BIT_KHR = 0x00010000UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_SHADER_DEVICE_ADDRESS_BIT_KHR = 0x00020000UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_ACCELERATION_STRUCTURE_BUILD_INPUT_READ_ONLY_BIT_KHR = 0x00080000UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_ACCELERATION_STRUCTURE_STORAGE_BIT_KHR = 0x00100000UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_SAMPLER_DESCRIPTOR_BUFFER_BIT_EXT = 0x00200000UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_RESOURCE_DESCRIPTOR_BUFFER_BIT_EXT = 0x00400000UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_PUSH_DESCRIPTORS_DESCRIPTOR_BUFFER_BIT_EXT = 0x04000000UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_MICROMAP_BUILD_INPUT_READ_ONLY_BIT_EXT = 0x00800000UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_MICROMAP_STORAGE_BIT_EXT = 0x01000000UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_DATA_GRAPH_FOREIGN_DESCRIPTOR_BIT_ARM = 0x20000000UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_TILE_MEMORY_BIT_QCOM = 0x08000000UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_MEMORY_DECOMPRESSION_BIT_EXT = 0x100000000UL;

    [NativeTypeName("const VkBufferUsageFlagBits2")]
    public const ulong VK_BUFFER_USAGE_2_PREPROCESS_BUFFER_BIT_EXT = 0x80000000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_DISABLE_OPTIMIZATION_BIT = 0x00000001UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_ALLOW_DERIVATIVES_BIT = 0x00000002UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_DERIVATIVE_BIT = 0x00000004UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_VIEW_INDEX_FROM_DEVICE_INDEX_BIT = 0x00000008UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_DISPATCH_BASE_BIT = 0x00000010UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_FAIL_ON_PIPELINE_COMPILE_REQUIRED_BIT = 0x00000100UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_EARLY_RETURN_ON_FAILURE_BIT = 0x00000200UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_NO_PROTECTED_ACCESS_BIT = 0x08000000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_PROTECTED_ACCESS_ONLY_BIT = 0x40000000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_DESCRIPTOR_HEAP_BIT_EXT = 0x1000000000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_RAY_TRACING_SKIP_BUILT_IN_PRIMITIVES_BIT_KHR = 0x00001000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_RAY_TRACING_ALLOW_SPHERES_AND_LINEAR_SWEPT_SPHERES_BIT_NV = 0x200000000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_ENABLE_LEGACY_DITHERING_BIT_EXT = 0x400000000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_DISABLE_OPTIMIZATION_BIT_KHR = 0x00000001UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_ALLOW_DERIVATIVES_BIT_KHR = 0x00000002UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_DERIVATIVE_BIT_KHR = 0x00000004UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_VIEW_INDEX_FROM_DEVICE_INDEX_BIT_KHR = 0x00000008UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_DISPATCH_BASE_BIT_KHR = 0x00000010UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_DEFER_COMPILE_BIT_NV = 0x00000020UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_CAPTURE_STATISTICS_BIT_KHR = 0x00000040UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_CAPTURE_INTERNAL_REPRESENTATIONS_BIT_KHR = 0x00000080UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_FAIL_ON_PIPELINE_COMPILE_REQUIRED_BIT_KHR = 0x00000100UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_EARLY_RETURN_ON_FAILURE_BIT_KHR = 0x00000200UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_LINK_TIME_OPTIMIZATION_BIT_EXT = 0x00000400UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_RETAIN_LINK_TIME_OPTIMIZATION_INFO_BIT_EXT = 0x00800000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_LIBRARY_BIT_KHR = 0x00000800UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_RAY_TRACING_SKIP_TRIANGLES_BIT_KHR = 0x00001000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_RAY_TRACING_SKIP_AABBS_BIT_KHR = 0x00002000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_RAY_TRACING_NO_NULL_ANY_HIT_SHADERS_BIT_KHR = 0x00004000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_RAY_TRACING_NO_NULL_CLOSEST_HIT_SHADERS_BIT_KHR = 0x00008000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_RAY_TRACING_NO_NULL_MISS_SHADERS_BIT_KHR = 0x00010000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_RAY_TRACING_NO_NULL_INTERSECTION_SHADERS_BIT_KHR = 0x00020000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_RAY_TRACING_SHADER_GROUP_HANDLE_CAPTURE_REPLAY_BIT_KHR = 0x00080000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_INDIRECT_BINDABLE_BIT_NV = 0x00040000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_RAY_TRACING_ALLOW_MOTION_BIT_NV = 0x00100000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_RENDERING_FRAGMENT_SHADING_RATE_ATTACHMENT_BIT_KHR = 0x00200000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_RENDERING_FRAGMENT_DENSITY_MAP_ATTACHMENT_BIT_EXT = 0x00400000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_RAY_TRACING_OPACITY_MICROMAP_BIT_EXT = 0x01000000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_COLOR_ATTACHMENT_FEEDBACK_LOOP_BIT_EXT = 0x02000000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_DEPTH_STENCIL_ATTACHMENT_FEEDBACK_LOOP_BIT_EXT = 0x04000000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_NO_PROTECTED_ACCESS_BIT_EXT = 0x08000000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_PROTECTED_ACCESS_ONLY_BIT_EXT = 0x40000000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_RAY_TRACING_DISPLACEMENT_MICROMAP_BIT_NV = 0x10000000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_DESCRIPTOR_BUFFER_BIT_EXT = 0x20000000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_DISALLOW_OPACITY_MICROMAP_BIT_ARM = 0x2000000000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_CAPTURE_DATA_BIT_KHR = 0x80000000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_INDIRECT_BINDABLE_BIT_EXT = 0x4000000000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_PER_LAYER_FRAGMENT_DENSITY_BIT_VALVE = 0x10000000000UL;

    [NativeTypeName("const VkPipelineCreateFlagBits2")]
    public const ulong VK_PIPELINE_CREATE_2_64_BIT_INDEXING_BIT_EXT = 0x80000000000UL;

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkMapMemory2([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkMemoryMapInfo *")] VkMemoryMapInfo* pMemoryMapInfo, void** ppData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkUnmapMemory2([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkMemoryUnmapInfo *")] VkMemoryUnmapInfo* pMemoryUnmapInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDeviceImageSubresourceLayout([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDeviceImageSubresourceInfo *")] VkDeviceImageSubresourceInfo* pInfo, VkSubresourceLayout2* pLayout);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetImageSubresourceLayout2([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkImage")] VkImage_T* image, [NativeTypeName("const VkImageSubresource2 *")] VkImageSubresource2* pSubresource, VkSubresourceLayout2* pLayout);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCopyMemoryToImage([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkCopyMemoryToImageInfo *")] VkCopyMemoryToImageInfo* pCopyMemoryToImageInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCopyImageToMemory([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkCopyImageToMemoryInfo *")] VkCopyImageToMemoryInfo* pCopyImageToMemoryInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCopyImageToImage([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkCopyImageToImageInfo *")] VkCopyImageToImageInfo* pCopyImageToImageInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkTransitionImageLayout([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint transitionCount, [NativeTypeName("const VkHostImageLayoutTransitionInfo *")] VkHostImageLayoutTransitionInfo* pTransitions);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdPushDescriptorSet([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkPipelineBindPoint pipelineBindPoint, [NativeTypeName("VkPipelineLayout")] VkPipelineLayout_T* layout, [NativeTypeName("uint32_t")] uint set, [NativeTypeName("uint32_t")] uint descriptorWriteCount, [NativeTypeName("const VkWriteDescriptorSet *")] VkWriteDescriptorSet* pDescriptorWrites);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdPushDescriptorSetWithTemplate([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkDescriptorUpdateTemplate")] VkDescriptorUpdateTemplate_T* descriptorUpdateTemplate, [NativeTypeName("VkPipelineLayout")] VkPipelineLayout_T* layout, [NativeTypeName("uint32_t")] uint set, [NativeTypeName("const void *")] void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindDescriptorSets2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkBindDescriptorSetsInfo *")] VkBindDescriptorSetsInfo* pBindDescriptorSetsInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdPushConstants2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkPushConstantsInfo *")] VkPushConstantsInfo* pPushConstantsInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdPushDescriptorSet2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkPushDescriptorSetInfo *")] VkPushDescriptorSetInfo* pPushDescriptorSetInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdPushDescriptorSetWithTemplate2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkPushDescriptorSetWithTemplateInfo *")] VkPushDescriptorSetWithTemplateInfo* pPushDescriptorSetWithTemplateInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetLineStipple([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint lineStippleFactor, [NativeTypeName("uint16_t")] ushort lineStipplePattern);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindIndexBuffer2([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, [NativeTypeName("VkDeviceSize")] ulong offset, [NativeTypeName("VkDeviceSize")] ulong size, VkIndexType indexType);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetRenderingAreaGranularity([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkRenderingAreaInfo *")] VkRenderingAreaInfo* pRenderingAreaInfo, VkExtent2D* pGranularity);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetRenderingAttachmentLocations([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkRenderingAttachmentLocationInfo *")] VkRenderingAttachmentLocationInfo* pLocationInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetRenderingInputAttachmentIndices([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkRenderingInputAttachmentIndexInfo *")] VkRenderingInputAttachmentIndexInfo* pInputAttachmentIndexInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroySurfaceKHR([NativeTypeName("VkInstance")] VkInstance_T* instance, [NativeTypeName("VkSurfaceKHR")] VkSurfaceKHR_T* surface, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceSurfaceSupportKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t")] uint queueFamilyIndex, [NativeTypeName("VkSurfaceKHR")] VkSurfaceKHR_T* surface, [NativeTypeName("VkBool32 *")] uint* pSupported);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceSurfaceCapabilitiesKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("VkSurfaceKHR")] VkSurfaceKHR_T* surface, VkSurfaceCapabilitiesKHR* pSurfaceCapabilities);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceSurfaceFormatsKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("VkSurfaceKHR")] VkSurfaceKHR_T* surface, [NativeTypeName("uint32_t *")] uint* pSurfaceFormatCount, VkSurfaceFormatKHR* pSurfaceFormats);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceSurfacePresentModesKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("VkSurfaceKHR")] VkSurfaceKHR_T* surface, [NativeTypeName("uint32_t *")] uint* pPresentModeCount, VkPresentModeKHR* pPresentModes);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateSwapchainKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkSwapchainCreateInfoKHR *")] VkSwapchainCreateInfoKHR* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkSwapchainKHR *")] VkSwapchainKHR_T** pSwapchain);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroySwapchainKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSwapchainKHR")] VkSwapchainKHR_T* swapchain, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetSwapchainImagesKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSwapchainKHR")] VkSwapchainKHR_T* swapchain, [NativeTypeName("uint32_t *")] uint* pSwapchainImageCount, [NativeTypeName("VkImage *")] VkImage_T** pSwapchainImages);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkAcquireNextImageKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSwapchainKHR")] VkSwapchainKHR_T* swapchain, [NativeTypeName("uint64_t")] ulong timeout, [NativeTypeName("VkSemaphore")] VkSemaphore_T* semaphore, [NativeTypeName("VkFence")] VkFence_T* fence, [NativeTypeName("uint32_t *")] uint* pImageIndex);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkQueuePresentKHR([NativeTypeName("VkQueue")] VkQueue_T* queue, [NativeTypeName("const VkPresentInfoKHR *")] VkPresentInfoKHR* pPresentInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetDeviceGroupPresentCapabilitiesKHR([NativeTypeName("VkDevice")] VkDevice_T* device, VkDeviceGroupPresentCapabilitiesKHR* pDeviceGroupPresentCapabilities);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetDeviceGroupSurfacePresentModesKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSurfaceKHR")] VkSurfaceKHR_T* surface, [NativeTypeName("VkDeviceGroupPresentModeFlagsKHR *")] uint* pModes);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDevicePresentRectanglesKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("VkSurfaceKHR")] VkSurfaceKHR_T* surface, [NativeTypeName("uint32_t *")] uint* pRectCount, VkRect2D* pRects);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkAcquireNextImage2KHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkAcquireNextImageInfoKHR *")] VkAcquireNextImageInfoKHR* pAcquireInfo, [NativeTypeName("uint32_t *")] uint* pImageIndex);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceDisplayPropertiesKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t *")] uint* pPropertyCount, VkDisplayPropertiesKHR* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceDisplayPlanePropertiesKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t *")] uint* pPropertyCount, VkDisplayPlanePropertiesKHR* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetDisplayPlaneSupportedDisplaysKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t")] uint planeIndex, [NativeTypeName("uint32_t *")] uint* pDisplayCount, [NativeTypeName("VkDisplayKHR *")] VkDisplayKHR_T** pDisplays);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetDisplayModePropertiesKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("VkDisplayKHR")] VkDisplayKHR_T* display, [NativeTypeName("uint32_t *")] uint* pPropertyCount, VkDisplayModePropertiesKHR* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateDisplayModeKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("VkDisplayKHR")] VkDisplayKHR_T* display, [NativeTypeName("const VkDisplayModeCreateInfoKHR *")] VkDisplayModeCreateInfoKHR* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkDisplayModeKHR *")] VkDisplayModeKHR_T** pMode);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetDisplayPlaneCapabilitiesKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("VkDisplayModeKHR")] VkDisplayModeKHR_T* mode, [NativeTypeName("uint32_t")] uint planeIndex, VkDisplayPlaneCapabilitiesKHR* pCapabilities);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateDisplayPlaneSurfaceKHR([NativeTypeName("VkInstance")] VkInstance_T* instance, [NativeTypeName("const VkDisplaySurfaceCreateInfoKHR *")] VkDisplaySurfaceCreateInfoKHR* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkSurfaceKHR *")] VkSurfaceKHR_T** pSurface);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateSharedSwapchainsKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint swapchainCount, [NativeTypeName("const VkSwapchainCreateInfoKHR *")] VkSwapchainCreateInfoKHR* pCreateInfos, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkSwapchainKHR *")] VkSwapchainKHR_T** pSwapchains);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceVideoCapabilitiesKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkVideoProfileInfoKHR *")] VkVideoProfileInfoKHR* pVideoProfile, VkVideoCapabilitiesKHR* pCapabilities);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceVideoFormatPropertiesKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkPhysicalDeviceVideoFormatInfoKHR *")] VkPhysicalDeviceVideoFormatInfoKHR* pVideoFormatInfo, [NativeTypeName("uint32_t *")] uint* pVideoFormatPropertyCount, VkVideoFormatPropertiesKHR* pVideoFormatProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateVideoSessionKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkVideoSessionCreateInfoKHR *")] VkVideoSessionCreateInfoKHR* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkVideoSessionKHR *")] VkVideoSessionKHR_T** pVideoSession);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyVideoSessionKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkVideoSessionKHR")] VkVideoSessionKHR_T* videoSession, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetVideoSessionMemoryRequirementsKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkVideoSessionKHR")] VkVideoSessionKHR_T* videoSession, [NativeTypeName("uint32_t *")] uint* pMemoryRequirementsCount, VkVideoSessionMemoryRequirementsKHR* pMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkBindVideoSessionMemoryKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkVideoSessionKHR")] VkVideoSessionKHR_T* videoSession, [NativeTypeName("uint32_t")] uint bindSessionMemoryInfoCount, [NativeTypeName("const VkBindVideoSessionMemoryInfoKHR *")] VkBindVideoSessionMemoryInfoKHR* pBindSessionMemoryInfos);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateVideoSessionParametersKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkVideoSessionParametersCreateInfoKHR *")] VkVideoSessionParametersCreateInfoKHR* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkVideoSessionParametersKHR *")] VkVideoSessionParametersKHR_T** pVideoSessionParameters);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkUpdateVideoSessionParametersKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkVideoSessionParametersKHR")] VkVideoSessionParametersKHR_T* videoSessionParameters, [NativeTypeName("const VkVideoSessionParametersUpdateInfoKHR *")] VkVideoSessionParametersUpdateInfoKHR* pUpdateInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyVideoSessionParametersKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkVideoSessionParametersKHR")] VkVideoSessionParametersKHR_T* videoSessionParameters, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBeginVideoCodingKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkVideoBeginCodingInfoKHR *")] VkVideoBeginCodingInfoKHR* pBeginInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdEndVideoCodingKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkVideoEndCodingInfoKHR *")] VkVideoEndCodingInfoKHR* pEndCodingInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdControlVideoCodingKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkVideoCodingControlInfoKHR *")] VkVideoCodingControlInfoKHR* pCodingControlInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDecodeVideoKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkVideoDecodeInfoKHR *")] VkVideoDecodeInfoKHR* pDecodeInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBeginRenderingKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkRenderingInfo *")] VkRenderingInfo* pRenderingInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdEndRenderingKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceFeatures2KHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, VkPhysicalDeviceFeatures2* pFeatures);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceProperties2KHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, VkPhysicalDeviceProperties2* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceFormatProperties2KHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, VkFormat format, VkFormatProperties2* pFormatProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceImageFormatProperties2KHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkPhysicalDeviceImageFormatInfo2 *")] VkPhysicalDeviceImageFormatInfo2* pImageFormatInfo, VkImageFormatProperties2* pImageFormatProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceQueueFamilyProperties2KHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t *")] uint* pQueueFamilyPropertyCount, VkQueueFamilyProperties2* pQueueFamilyProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceMemoryProperties2KHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, VkPhysicalDeviceMemoryProperties2* pMemoryProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceSparseImageFormatProperties2KHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkPhysicalDeviceSparseImageFormatInfo2 *")] VkPhysicalDeviceSparseImageFormatInfo2* pFormatInfo, [NativeTypeName("uint32_t *")] uint* pPropertyCount, VkSparseImageFormatProperties2* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDeviceGroupPeerMemoryFeaturesKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint heapIndex, [NativeTypeName("uint32_t")] uint localDeviceIndex, [NativeTypeName("uint32_t")] uint remoteDeviceIndex, [NativeTypeName("VkPeerMemoryFeatureFlags *")] uint* pPeerMemoryFeatures);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDeviceMaskKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint deviceMask);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDispatchBaseKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint baseGroupX, [NativeTypeName("uint32_t")] uint baseGroupY, [NativeTypeName("uint32_t")] uint baseGroupZ, [NativeTypeName("uint32_t")] uint groupCountX, [NativeTypeName("uint32_t")] uint groupCountY, [NativeTypeName("uint32_t")] uint groupCountZ);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkTrimCommandPoolKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkCommandPool")] VkCommandPool_T* commandPool, [NativeTypeName("VkCommandPoolTrimFlags")] uint flags);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkEnumeratePhysicalDeviceGroupsKHR([NativeTypeName("VkInstance")] VkInstance_T* instance, [NativeTypeName("uint32_t *")] uint* pPhysicalDeviceGroupCount, VkPhysicalDeviceGroupProperties* pPhysicalDeviceGroupProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceExternalBufferPropertiesKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkPhysicalDeviceExternalBufferInfo *")] VkPhysicalDeviceExternalBufferInfo* pExternalBufferInfo, VkExternalBufferProperties* pExternalBufferProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetMemoryFdKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkMemoryGetFdInfoKHR *")] VkMemoryGetFdInfoKHR* pGetFdInfo, int* pFd);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetMemoryFdPropertiesKHR([NativeTypeName("VkDevice")] VkDevice_T* device, VkExternalMemoryHandleTypeFlagBits handleType, int fd, VkMemoryFdPropertiesKHR* pMemoryFdProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceExternalSemaphorePropertiesKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkPhysicalDeviceExternalSemaphoreInfo *")] VkPhysicalDeviceExternalSemaphoreInfo* pExternalSemaphoreInfo, VkExternalSemaphoreProperties* pExternalSemaphoreProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkImportSemaphoreFdKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkImportSemaphoreFdInfoKHR *")] VkImportSemaphoreFdInfoKHR* pImportSemaphoreFdInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetSemaphoreFdKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkSemaphoreGetFdInfoKHR *")] VkSemaphoreGetFdInfoKHR* pGetFdInfo, int* pFd);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdPushDescriptorSetKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkPipelineBindPoint pipelineBindPoint, [NativeTypeName("VkPipelineLayout")] VkPipelineLayout_T* layout, [NativeTypeName("uint32_t")] uint set, [NativeTypeName("uint32_t")] uint descriptorWriteCount, [NativeTypeName("const VkWriteDescriptorSet *")] VkWriteDescriptorSet* pDescriptorWrites);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdPushDescriptorSetWithTemplateKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkDescriptorUpdateTemplate")] VkDescriptorUpdateTemplate_T* descriptorUpdateTemplate, [NativeTypeName("VkPipelineLayout")] VkPipelineLayout_T* layout, [NativeTypeName("uint32_t")] uint set, [NativeTypeName("const void *")] void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateDescriptorUpdateTemplateKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDescriptorUpdateTemplateCreateInfo *")] VkDescriptorUpdateTemplateCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkDescriptorUpdateTemplate *")] VkDescriptorUpdateTemplate_T** pDescriptorUpdateTemplate);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyDescriptorUpdateTemplateKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDescriptorUpdateTemplate")] VkDescriptorUpdateTemplate_T* descriptorUpdateTemplate, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkUpdateDescriptorSetWithTemplateKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDescriptorSet")] VkDescriptorSet_T* descriptorSet, [NativeTypeName("VkDescriptorUpdateTemplate")] VkDescriptorUpdateTemplate_T* descriptorUpdateTemplate, [NativeTypeName("const void *")] void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateRenderPass2KHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkRenderPassCreateInfo2 *")] VkRenderPassCreateInfo2* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkRenderPass *")] VkRenderPass_T** pRenderPass);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBeginRenderPass2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkRenderPassBeginInfo *")] VkRenderPassBeginInfo* pRenderPassBegin, [NativeTypeName("const VkSubpassBeginInfo *")] VkSubpassBeginInfo* pSubpassBeginInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdNextSubpass2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkSubpassBeginInfo *")] VkSubpassBeginInfo* pSubpassBeginInfo, [NativeTypeName("const VkSubpassEndInfo *")] VkSubpassEndInfo* pSubpassEndInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdEndRenderPass2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkSubpassEndInfo *")] VkSubpassEndInfo* pSubpassEndInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetSwapchainStatusKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSwapchainKHR")] VkSwapchainKHR_T* swapchain);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceExternalFencePropertiesKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkPhysicalDeviceExternalFenceInfo *")] VkPhysicalDeviceExternalFenceInfo* pExternalFenceInfo, VkExternalFenceProperties* pExternalFenceProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkImportFenceFdKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkImportFenceFdInfoKHR *")] VkImportFenceFdInfoKHR* pImportFenceFdInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetFenceFdKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkFenceGetFdInfoKHR *")] VkFenceGetFdInfoKHR* pGetFdInfo, int* pFd);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkEnumeratePhysicalDeviceQueueFamilyPerformanceQueryCountersKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t")] uint queueFamilyIndex, [NativeTypeName("uint32_t *")] uint* pCounterCount, VkPerformanceCounterKHR* pCounters, VkPerformanceCounterDescriptionKHR* pCounterDescriptions);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceQueueFamilyPerformanceQueryPassesKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkQueryPoolPerformanceCreateInfoKHR *")] VkQueryPoolPerformanceCreateInfoKHR* pPerformanceQueryCreateInfo, [NativeTypeName("uint32_t *")] uint* pNumPasses);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkAcquireProfilingLockKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkAcquireProfilingLockInfoKHR *")] VkAcquireProfilingLockInfoKHR* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkReleaseProfilingLockKHR([NativeTypeName("VkDevice")] VkDevice_T* device);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceSurfaceCapabilities2KHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkPhysicalDeviceSurfaceInfo2KHR *")] VkPhysicalDeviceSurfaceInfo2KHR* pSurfaceInfo, VkSurfaceCapabilities2KHR* pSurfaceCapabilities);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceSurfaceFormats2KHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkPhysicalDeviceSurfaceInfo2KHR *")] VkPhysicalDeviceSurfaceInfo2KHR* pSurfaceInfo, [NativeTypeName("uint32_t *")] uint* pSurfaceFormatCount, VkSurfaceFormat2KHR* pSurfaceFormats);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceDisplayProperties2KHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t *")] uint* pPropertyCount, VkDisplayProperties2KHR* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceDisplayPlaneProperties2KHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t *")] uint* pPropertyCount, VkDisplayPlaneProperties2KHR* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetDisplayModeProperties2KHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("VkDisplayKHR")] VkDisplayKHR_T* display, [NativeTypeName("uint32_t *")] uint* pPropertyCount, VkDisplayModeProperties2KHR* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetDisplayPlaneCapabilities2KHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkDisplayPlaneInfo2KHR *")] VkDisplayPlaneInfo2KHR* pDisplayPlaneInfo, VkDisplayPlaneCapabilities2KHR* pCapabilities);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetImageMemoryRequirements2KHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkImageMemoryRequirementsInfo2 *")] VkImageMemoryRequirementsInfo2* pInfo, VkMemoryRequirements2* pMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetBufferMemoryRequirements2KHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkBufferMemoryRequirementsInfo2 *")] VkBufferMemoryRequirementsInfo2* pInfo, VkMemoryRequirements2* pMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetImageSparseMemoryRequirements2KHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkImageSparseMemoryRequirementsInfo2 *")] VkImageSparseMemoryRequirementsInfo2* pInfo, [NativeTypeName("uint32_t *")] uint* pSparseMemoryRequirementCount, VkSparseImageMemoryRequirements2* pSparseMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateSamplerYcbcrConversionKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkSamplerYcbcrConversionCreateInfo *")] VkSamplerYcbcrConversionCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkSamplerYcbcrConversion *")] VkSamplerYcbcrConversion_T** pYcbcrConversion);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroySamplerYcbcrConversionKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSamplerYcbcrConversion")] VkSamplerYcbcrConversion_T* ycbcrConversion, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkBindBufferMemory2KHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint bindInfoCount, [NativeTypeName("const VkBindBufferMemoryInfo *")] VkBindBufferMemoryInfo* pBindInfos);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkBindImageMemory2KHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint bindInfoCount, [NativeTypeName("const VkBindImageMemoryInfo *")] VkBindImageMemoryInfo* pBindInfos);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDescriptorSetLayoutSupportKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDescriptorSetLayoutCreateInfo *")] VkDescriptorSetLayoutCreateInfo* pCreateInfo, VkDescriptorSetLayoutSupport* pSupport);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawIndirectCountKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, [NativeTypeName("VkDeviceSize")] ulong offset, [NativeTypeName("VkBuffer")] VkBuffer_T* countBuffer, [NativeTypeName("VkDeviceSize")] ulong countBufferOffset, [NativeTypeName("uint32_t")] uint maxDrawCount, [NativeTypeName("uint32_t")] uint stride);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawIndexedIndirectCountKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, [NativeTypeName("VkDeviceSize")] ulong offset, [NativeTypeName("VkBuffer")] VkBuffer_T* countBuffer, [NativeTypeName("VkDeviceSize")] ulong countBufferOffset, [NativeTypeName("uint32_t")] uint maxDrawCount, [NativeTypeName("uint32_t")] uint stride);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetSemaphoreCounterValueKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSemaphore")] VkSemaphore_T* semaphore, [NativeTypeName("uint64_t *")] ulong* pValue);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkWaitSemaphoresKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkSemaphoreWaitInfo *")] VkSemaphoreWaitInfo* pWaitInfo, [NativeTypeName("uint64_t")] ulong timeout);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkSignalSemaphoreKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkSemaphoreSignalInfo *")] VkSemaphoreSignalInfo* pSignalInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceFragmentShadingRatesKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t *")] uint* pFragmentShadingRateCount, VkPhysicalDeviceFragmentShadingRateKHR* pFragmentShadingRates);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetFragmentShadingRateKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkExtent2D *")] VkExtent2D* pFragmentSize, [NativeTypeName("const VkFragmentShadingRateCombinerOpKHR[2]")] VkFragmentShadingRateCombinerOpKHR* combinerOps);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetRenderingAttachmentLocationsKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkRenderingAttachmentLocationInfo *")] VkRenderingAttachmentLocationInfo* pLocationInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetRenderingInputAttachmentIndicesKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkRenderingInputAttachmentIndexInfo *")] VkRenderingInputAttachmentIndexInfo* pInputAttachmentIndexInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkWaitForPresentKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSwapchainKHR")] VkSwapchainKHR_T* swapchain, [NativeTypeName("uint64_t")] ulong presentId, [NativeTypeName("uint64_t")] ulong timeout);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("VkDeviceAddress")]
    public static extern ulong vkGetBufferDeviceAddressKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkBufferDeviceAddressInfo *")] VkBufferDeviceAddressInfo* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("uint64_t")]
    public static extern ulong vkGetBufferOpaqueCaptureAddressKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkBufferDeviceAddressInfo *")] VkBufferDeviceAddressInfo* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("uint64_t")]
    public static extern ulong vkGetDeviceMemoryOpaqueCaptureAddressKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDeviceMemoryOpaqueCaptureAddressInfo *")] VkDeviceMemoryOpaqueCaptureAddressInfo* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateDeferredOperationKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkDeferredOperationKHR *")] VkDeferredOperationKHR_T** pDeferredOperation);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyDeferredOperationKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDeferredOperationKHR")] VkDeferredOperationKHR_T* operation, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t")]
    public static extern uint vkGetDeferredOperationMaxConcurrencyKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDeferredOperationKHR")] VkDeferredOperationKHR_T* operation);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetDeferredOperationResultKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDeferredOperationKHR")] VkDeferredOperationKHR_T* operation);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkDeferredOperationJoinKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDeferredOperationKHR")] VkDeferredOperationKHR_T* operation);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPipelineExecutablePropertiesKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkPipelineInfoKHR *")] VkPipelineInfoKHR* pPipelineInfo, [NativeTypeName("uint32_t *")] uint* pExecutableCount, VkPipelineExecutablePropertiesKHR* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPipelineExecutableStatisticsKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkPipelineExecutableInfoKHR *")] VkPipelineExecutableInfoKHR* pExecutableInfo, [NativeTypeName("uint32_t *")] uint* pStatisticCount, VkPipelineExecutableStatisticKHR* pStatistics);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPipelineExecutableInternalRepresentationsKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkPipelineExecutableInfoKHR *")] VkPipelineExecutableInfoKHR* pExecutableInfo, [NativeTypeName("uint32_t *")] uint* pInternalRepresentationCount, VkPipelineExecutableInternalRepresentationKHR* pInternalRepresentations);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkMapMemory2KHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkMemoryMapInfo *")] VkMemoryMapInfo* pMemoryMapInfo, void** ppData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkUnmapMemory2KHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkMemoryUnmapInfo *")] VkMemoryUnmapInfo* pMemoryUnmapInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceVideoEncodeQualityLevelPropertiesKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkPhysicalDeviceVideoEncodeQualityLevelInfoKHR *")] VkPhysicalDeviceVideoEncodeQualityLevelInfoKHR* pQualityLevelInfo, VkVideoEncodeQualityLevelPropertiesKHR* pQualityLevelProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetEncodedVideoSessionParametersKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkVideoEncodeSessionParametersGetInfoKHR *")] VkVideoEncodeSessionParametersGetInfoKHR* pVideoSessionParametersInfo, VkVideoEncodeSessionParametersFeedbackInfoKHR* pFeedbackInfo, [NativeTypeName("size_t *")] nuint* pDataSize, void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdEncodeVideoKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkVideoEncodeInfoKHR *")] VkVideoEncodeInfoKHR* pEncodeInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetEvent2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkEvent")] VkEvent_T* @event, [NativeTypeName("const VkDependencyInfo *")] VkDependencyInfo* pDependencyInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdResetEvent2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkEvent")] VkEvent_T* @event, [NativeTypeName("VkPipelineStageFlags2")] ulong stageMask);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdWaitEvents2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint eventCount, [NativeTypeName("const VkEvent *")] VkEvent_T** pEvents, [NativeTypeName("const VkDependencyInfo *")] VkDependencyInfo* pDependencyInfos);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdPipelineBarrier2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkDependencyInfo *")] VkDependencyInfo* pDependencyInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdWriteTimestamp2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkPipelineStageFlags2")] ulong stage, [NativeTypeName("VkQueryPool")] VkQueryPool_T* queryPool, [NativeTypeName("uint32_t")] uint query);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkQueueSubmit2KHR([NativeTypeName("VkQueue")] VkQueue_T* queue, [NativeTypeName("uint32_t")] uint submitCount, [NativeTypeName("const VkSubmitInfo2 *")] VkSubmitInfo2* pSubmits, [NativeTypeName("VkFence")] VkFence_T* fence);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyBuffer2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkCopyBufferInfo2 *")] VkCopyBufferInfo2* pCopyBufferInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyImage2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkCopyImageInfo2 *")] VkCopyImageInfo2* pCopyImageInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyBufferToImage2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkCopyBufferToImageInfo2 *")] VkCopyBufferToImageInfo2* pCopyBufferToImageInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyImageToBuffer2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkCopyImageToBufferInfo2 *")] VkCopyImageToBufferInfo2* pCopyImageToBufferInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBlitImage2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkBlitImageInfo2 *")] VkBlitImageInfo2* pBlitImageInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdResolveImage2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkResolveImageInfo2 *")] VkResolveImageInfo2* pResolveImageInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdTraceRaysIndirect2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkDeviceAddress")] ulong indirectDeviceAddress);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDeviceBufferMemoryRequirementsKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDeviceBufferMemoryRequirements *")] VkDeviceBufferMemoryRequirements* pInfo, VkMemoryRequirements2* pMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDeviceImageMemoryRequirementsKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDeviceImageMemoryRequirements *")] VkDeviceImageMemoryRequirements* pInfo, VkMemoryRequirements2* pMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDeviceImageSparseMemoryRequirementsKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDeviceImageMemoryRequirements *")] VkDeviceImageMemoryRequirements* pInfo, [NativeTypeName("uint32_t *")] uint* pSparseMemoryRequirementCount, VkSparseImageMemoryRequirements2* pSparseMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindIndexBuffer2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, [NativeTypeName("VkDeviceSize")] ulong offset, [NativeTypeName("VkDeviceSize")] ulong size, VkIndexType indexType);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetRenderingAreaGranularityKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkRenderingAreaInfo *")] VkRenderingAreaInfo* pRenderingAreaInfo, VkExtent2D* pGranularity);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDeviceImageSubresourceLayoutKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDeviceImageSubresourceInfo *")] VkDeviceImageSubresourceInfo* pInfo, VkSubresourceLayout2* pLayout);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetImageSubresourceLayout2KHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkImage")] VkImage_T* image, [NativeTypeName("const VkImageSubresource2 *")] VkImageSubresource2* pSubresource, VkSubresourceLayout2* pLayout);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkWaitForPresent2KHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSwapchainKHR")] VkSwapchainKHR_T* swapchain, [NativeTypeName("const VkPresentWait2InfoKHR *")] VkPresentWait2InfoKHR* pPresentWait2Info);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreatePipelineBinariesKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkPipelineBinaryCreateInfoKHR *")] VkPipelineBinaryCreateInfoKHR* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, VkPipelineBinaryHandlesInfoKHR* pBinaries);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyPipelineBinaryKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkPipelineBinaryKHR")] VkPipelineBinaryKHR_T* pipelineBinary, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPipelineKeyKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkPipelineCreateInfoKHR *")] VkPipelineCreateInfoKHR* pPipelineCreateInfo, VkPipelineBinaryKeyKHR* pPipelineKey);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPipelineBinaryDataKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkPipelineBinaryDataInfoKHR *")] VkPipelineBinaryDataInfoKHR* pInfo, VkPipelineBinaryKeyKHR* pPipelineBinaryKey, [NativeTypeName("size_t *")] nuint* pPipelineBinaryDataSize, void* pPipelineBinaryData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkReleaseCapturedPipelineDataKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkReleaseCapturedPipelineDataInfoKHR *")] VkReleaseCapturedPipelineDataInfoKHR* pInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkReleaseSwapchainImagesKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkReleaseSwapchainImagesInfoKHR *")] VkReleaseSwapchainImagesInfoKHR* pReleaseInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceCooperativeMatrixPropertiesKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t *")] uint* pPropertyCount, VkCooperativeMatrixPropertiesKHR* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetLineStippleKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint lineStippleFactor, [NativeTypeName("uint16_t")] ushort lineStipplePattern);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceCalibrateableTimeDomainsKHR([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t *")] uint* pTimeDomainCount, VkTimeDomainKHR* pTimeDomains);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetCalibratedTimestampsKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint timestampCount, [NativeTypeName("const VkCalibratedTimestampInfoKHR *")] VkCalibratedTimestampInfoKHR* pTimestampInfos, [NativeTypeName("uint64_t *")] ulong* pTimestamps, [NativeTypeName("uint64_t *")] ulong* pMaxDeviation);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindDescriptorSets2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkBindDescriptorSetsInfo *")] VkBindDescriptorSetsInfo* pBindDescriptorSetsInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdPushConstants2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkPushConstantsInfo *")] VkPushConstantsInfo* pPushConstantsInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdPushDescriptorSet2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkPushDescriptorSetInfo *")] VkPushDescriptorSetInfo* pPushDescriptorSetInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdPushDescriptorSetWithTemplate2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkPushDescriptorSetWithTemplateInfo *")] VkPushDescriptorSetWithTemplateInfo* pPushDescriptorSetWithTemplateInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDescriptorBufferOffsets2EXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkSetDescriptorBufferOffsetsInfoEXT *")] VkSetDescriptorBufferOffsetsInfoEXT* pSetDescriptorBufferOffsetsInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindDescriptorBufferEmbeddedSamplers2EXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkBindDescriptorBufferEmbeddedSamplersInfoEXT *")] VkBindDescriptorBufferEmbeddedSamplersInfoEXT* pBindDescriptorBufferEmbeddedSamplersInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyMemoryIndirectKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkCopyMemoryIndirectInfoKHR *")] VkCopyMemoryIndirectInfoKHR* pCopyMemoryIndirectInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyMemoryToImageIndirectKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkCopyMemoryToImageIndirectInfoKHR *")] VkCopyMemoryToImageIndirectInfoKHR* pCopyMemoryToImageIndirectInfo);

    [NativeTypeName("const VkAccessFlagBits3KHR")]
    public const ulong VK_ACCESS_3_NONE_KHR = 0UL;

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdEndRendering2KHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkRenderingEndInfoKHR *")] VkRenderingEndInfoKHR* pRenderingEndInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateDebugReportCallbackEXT([NativeTypeName("VkInstance")] VkInstance_T* instance, [NativeTypeName("const VkDebugReportCallbackCreateInfoEXT *")] VkDebugReportCallbackCreateInfoEXT* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkDebugReportCallbackEXT *")] VkDebugReportCallbackEXT_T** pCallback);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyDebugReportCallbackEXT([NativeTypeName("VkInstance")] VkInstance_T* instance, [NativeTypeName("VkDebugReportCallbackEXT")] VkDebugReportCallbackEXT_T* callback, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDebugReportMessageEXT([NativeTypeName("VkInstance")] VkInstance_T* instance, [NativeTypeName("VkDebugReportFlagsEXT")] uint flags, VkDebugReportObjectTypeEXT objectType, [NativeTypeName("uint64_t")] ulong @object, [NativeTypeName("size_t")] nuint location, [NativeTypeName("int32_t")] int messageCode, [NativeTypeName("const char *")] sbyte* pLayerPrefix, [NativeTypeName("const char *")] sbyte* pMessage);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkDebugMarkerSetObjectTagEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDebugMarkerObjectTagInfoEXT *")] VkDebugMarkerObjectTagInfoEXT* pTagInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkDebugMarkerSetObjectNameEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDebugMarkerObjectNameInfoEXT *")] VkDebugMarkerObjectNameInfoEXT* pNameInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDebugMarkerBeginEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkDebugMarkerMarkerInfoEXT *")] VkDebugMarkerMarkerInfoEXT* pMarkerInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDebugMarkerEndEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDebugMarkerInsertEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkDebugMarkerMarkerInfoEXT *")] VkDebugMarkerMarkerInfoEXT* pMarkerInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindTransformFeedbackBuffersEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint firstBinding, [NativeTypeName("uint32_t")] uint bindingCount, [NativeTypeName("const VkBuffer *")] VkBuffer_T** pBuffers, [NativeTypeName("const VkDeviceSize *")] ulong* pOffsets, [NativeTypeName("const VkDeviceSize *")] ulong* pSizes);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBeginTransformFeedbackEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint firstCounterBuffer, [NativeTypeName("uint32_t")] uint counterBufferCount, [NativeTypeName("const VkBuffer *")] VkBuffer_T** pCounterBuffers, [NativeTypeName("const VkDeviceSize *")] ulong* pCounterBufferOffsets);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdEndTransformFeedbackEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint firstCounterBuffer, [NativeTypeName("uint32_t")] uint counterBufferCount, [NativeTypeName("const VkBuffer *")] VkBuffer_T** pCounterBuffers, [NativeTypeName("const VkDeviceSize *")] ulong* pCounterBufferOffsets);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBeginQueryIndexedEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkQueryPool")] VkQueryPool_T* queryPool, [NativeTypeName("uint32_t")] uint query, [NativeTypeName("VkQueryControlFlags")] uint flags, [NativeTypeName("uint32_t")] uint index);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdEndQueryIndexedEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkQueryPool")] VkQueryPool_T* queryPool, [NativeTypeName("uint32_t")] uint query, [NativeTypeName("uint32_t")] uint index);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawIndirectByteCountEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint instanceCount, [NativeTypeName("uint32_t")] uint firstInstance, [NativeTypeName("VkBuffer")] VkBuffer_T* counterBuffer, [NativeTypeName("VkDeviceSize")] ulong counterBufferOffset, [NativeTypeName("uint32_t")] uint counterOffset, [NativeTypeName("uint32_t")] uint vertexStride);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateCuModuleNVX([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkCuModuleCreateInfoNVX *")] VkCuModuleCreateInfoNVX* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkCuModuleNVX *")] VkCuModuleNVX_T** pModule);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateCuFunctionNVX([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkCuFunctionCreateInfoNVX *")] VkCuFunctionCreateInfoNVX* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkCuFunctionNVX *")] VkCuFunctionNVX_T** pFunction);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyCuModuleNVX([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkCuModuleNVX")] VkCuModuleNVX_T* module, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyCuFunctionNVX([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkCuFunctionNVX")] VkCuFunctionNVX_T* function, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCuLaunchKernelNVX([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkCuLaunchInfoNVX *")] VkCuLaunchInfoNVX* pLaunchInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t")]
    public static extern uint vkGetImageViewHandleNVX([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkImageViewHandleInfoNVX *")] VkImageViewHandleInfoNVX* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("uint64_t")]
    public static extern ulong vkGetImageViewHandle64NVX([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkImageViewHandleInfoNVX *")] VkImageViewHandleInfoNVX* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetImageViewAddressNVX([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkImageView")] VkImageView_T* imageView, VkImageViewAddressPropertiesNVX* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("uint64_t")]
    public static extern ulong vkGetDeviceCombinedImageSamplerIndexNVX([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint64_t")] ulong imageViewIndex, [NativeTypeName("uint64_t")] ulong samplerIndex);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawIndirectCountAMD([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, [NativeTypeName("VkDeviceSize")] ulong offset, [NativeTypeName("VkBuffer")] VkBuffer_T* countBuffer, [NativeTypeName("VkDeviceSize")] ulong countBufferOffset, [NativeTypeName("uint32_t")] uint maxDrawCount, [NativeTypeName("uint32_t")] uint stride);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawIndexedIndirectCountAMD([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, [NativeTypeName("VkDeviceSize")] ulong offset, [NativeTypeName("VkBuffer")] VkBuffer_T* countBuffer, [NativeTypeName("VkDeviceSize")] ulong countBufferOffset, [NativeTypeName("uint32_t")] uint maxDrawCount, [NativeTypeName("uint32_t")] uint stride);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetShaderInfoAMD([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkPipeline")] VkPipeline_T* pipeline, VkShaderStageFlagBits shaderStage, VkShaderInfoTypeAMD infoType, [NativeTypeName("size_t *")] nuint* pInfoSize, void* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceExternalImageFormatPropertiesNV([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, VkFormat format, VkImageType type, VkImageTiling tiling, [NativeTypeName("VkImageUsageFlags")] uint usage, [NativeTypeName("VkImageCreateFlags")] uint flags, [NativeTypeName("VkExternalMemoryHandleTypeFlagsNV")] uint externalHandleType, VkExternalImageFormatPropertiesNV* pExternalImageFormatProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBeginConditionalRenderingEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkConditionalRenderingBeginInfoEXT *")] VkConditionalRenderingBeginInfoEXT* pConditionalRenderingBegin);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdEndConditionalRenderingEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetViewportWScalingNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint firstViewport, [NativeTypeName("uint32_t")] uint viewportCount, [NativeTypeName("const VkViewportWScalingNV *")] VkViewportWScalingNV* pViewportWScalings);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkReleaseDisplayEXT([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("VkDisplayKHR")] VkDisplayKHR_T* display);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceSurfaceCapabilities2EXT([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("VkSurfaceKHR")] VkSurfaceKHR_T* surface, VkSurfaceCapabilities2EXT* pSurfaceCapabilities);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkDisplayPowerControlEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDisplayKHR")] VkDisplayKHR_T* display, [NativeTypeName("const VkDisplayPowerInfoEXT *")] VkDisplayPowerInfoEXT* pDisplayPowerInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkRegisterDeviceEventEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDeviceEventInfoEXT *")] VkDeviceEventInfoEXT* pDeviceEventInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkFence *")] VkFence_T** pFence);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkRegisterDisplayEventEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDisplayKHR")] VkDisplayKHR_T* display, [NativeTypeName("const VkDisplayEventInfoEXT *")] VkDisplayEventInfoEXT* pDisplayEventInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkFence *")] VkFence_T** pFence);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetSwapchainCounterEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSwapchainKHR")] VkSwapchainKHR_T* swapchain, VkSurfaceCounterFlagBitsEXT counter, [NativeTypeName("uint64_t *")] ulong* pCounterValue);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetRefreshCycleDurationGOOGLE([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSwapchainKHR")] VkSwapchainKHR_T* swapchain, VkRefreshCycleDurationGOOGLE* pDisplayTimingProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPastPresentationTimingGOOGLE([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSwapchainKHR")] VkSwapchainKHR_T* swapchain, [NativeTypeName("uint32_t *")] uint* pPresentationTimingCount, VkPastPresentationTimingGOOGLE* pPresentationTimings);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDiscardRectangleEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint firstDiscardRectangle, [NativeTypeName("uint32_t")] uint discardRectangleCount, [NativeTypeName("const VkRect2D *")] VkRect2D* pDiscardRectangles);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDiscardRectangleEnableEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint discardRectangleEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDiscardRectangleModeEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkDiscardRectangleModeEXT discardRectangleMode);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkSetHdrMetadataEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint swapchainCount, [NativeTypeName("const VkSwapchainKHR *")] VkSwapchainKHR_T** pSwapchains, [NativeTypeName("const VkHdrMetadataEXT *")] VkHdrMetadataEXT* pMetadata);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkSetDebugUtilsObjectNameEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDebugUtilsObjectNameInfoEXT *")] VkDebugUtilsObjectNameInfoEXT* pNameInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkSetDebugUtilsObjectTagEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDebugUtilsObjectTagInfoEXT *")] VkDebugUtilsObjectTagInfoEXT* pTagInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkQueueBeginDebugUtilsLabelEXT([NativeTypeName("VkQueue")] VkQueue_T* queue, [NativeTypeName("const VkDebugUtilsLabelEXT *")] VkDebugUtilsLabelEXT* pLabelInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkQueueEndDebugUtilsLabelEXT([NativeTypeName("VkQueue")] VkQueue_T* queue);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkQueueInsertDebugUtilsLabelEXT([NativeTypeName("VkQueue")] VkQueue_T* queue, [NativeTypeName("const VkDebugUtilsLabelEXT *")] VkDebugUtilsLabelEXT* pLabelInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBeginDebugUtilsLabelEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkDebugUtilsLabelEXT *")] VkDebugUtilsLabelEXT* pLabelInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdEndDebugUtilsLabelEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdInsertDebugUtilsLabelEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkDebugUtilsLabelEXT *")] VkDebugUtilsLabelEXT* pLabelInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateDebugUtilsMessengerEXT([NativeTypeName("VkInstance")] VkInstance_T* instance, [NativeTypeName("const VkDebugUtilsMessengerCreateInfoEXT *")] VkDebugUtilsMessengerCreateInfoEXT* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkDebugUtilsMessengerEXT *")] VkDebugUtilsMessengerEXT_T** pMessenger);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyDebugUtilsMessengerEXT([NativeTypeName("VkInstance")] VkInstance_T* instance, [NativeTypeName("VkDebugUtilsMessengerEXT")] VkDebugUtilsMessengerEXT_T* messenger, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkSubmitDebugUtilsMessageEXT([NativeTypeName("VkInstance")] VkInstance_T* instance, VkDebugUtilsMessageSeverityFlagBitsEXT messageSeverity, [NativeTypeName("VkDebugUtilsMessageTypeFlagsEXT")] uint messageTypes, [NativeTypeName("const VkDebugUtilsMessengerCallbackDataEXT *")] VkDebugUtilsMessengerCallbackDataEXT* pCallbackData);

    [NativeTypeName("const VkTensorViewCreateFlagBitsARM")]
    public const ulong VK_TENSOR_VIEW_CREATE_DESCRIPTOR_BUFFER_CAPTURE_REPLAY_BIT_ARM = 0x00000001UL;

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkWriteSamplerDescriptorsEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint samplerCount, [NativeTypeName("const VkSamplerCreateInfo *")] VkSamplerCreateInfo* pSamplers, [NativeTypeName("const VkHostAddressRangeEXT *")] VkHostAddressRangeEXT* pDescriptors);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkWriteResourceDescriptorsEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint resourceCount, [NativeTypeName("const VkResourceDescriptorInfoEXT *")] VkResourceDescriptorInfoEXT* pResources, [NativeTypeName("const VkHostAddressRangeEXT *")] VkHostAddressRangeEXT* pDescriptors);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindSamplerHeapEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkBindHeapInfoEXT *")] VkBindHeapInfoEXT* pBindInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindResourceHeapEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkBindHeapInfoEXT *")] VkBindHeapInfoEXT* pBindInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdPushDataEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkPushDataInfoEXT *")] VkPushDataInfoEXT* pPushDataInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetImageOpaqueCaptureDataEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint imageCount, [NativeTypeName("const VkImage *")] VkImage_T** pImages, VkHostAddressRangeEXT* pDatas);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("VkDeviceSize")]
    public static extern ulong vkGetPhysicalDeviceDescriptorSizeEXT([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, VkDescriptorType descriptorType);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkRegisterCustomBorderColorEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkSamplerCustomBorderColorCreateInfoEXT *")] VkSamplerCustomBorderColorCreateInfoEXT* pBorderColor, [NativeTypeName("VkBool32")] uint requestIndex, [NativeTypeName("uint32_t *")] uint* pIndex);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkUnregisterCustomBorderColorEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint index);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetTensorOpaqueCaptureDataARM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint tensorCount, [NativeTypeName("const VkTensorARM *")] VkTensorARM_T** pTensors, VkHostAddressRangeEXT* pDatas);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetSampleLocationsEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkSampleLocationsInfoEXT *")] VkSampleLocationsInfoEXT* pSampleLocationsInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceMultisamplePropertiesEXT([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, VkSampleCountFlagBits samples, VkMultisamplePropertiesEXT* pMultisampleProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetImageDrmFormatModifierPropertiesEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkImage")] VkImage_T* image, VkImageDrmFormatModifierPropertiesEXT* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateValidationCacheEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkValidationCacheCreateInfoEXT *")] VkValidationCacheCreateInfoEXT* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkValidationCacheEXT *")] VkValidationCacheEXT_T** pValidationCache);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyValidationCacheEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkValidationCacheEXT")] VkValidationCacheEXT_T* validationCache, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkMergeValidationCachesEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkValidationCacheEXT")] VkValidationCacheEXT_T* dstCache, [NativeTypeName("uint32_t")] uint srcCacheCount, [NativeTypeName("const VkValidationCacheEXT *")] VkValidationCacheEXT_T** pSrcCaches);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetValidationCacheDataEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkValidationCacheEXT")] VkValidationCacheEXT_T* validationCache, [NativeTypeName("size_t *")] nuint* pDataSize, void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindShadingRateImageNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkImageView")] VkImageView_T* imageView, VkImageLayout imageLayout);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetViewportShadingRatePaletteNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint firstViewport, [NativeTypeName("uint32_t")] uint viewportCount, [NativeTypeName("const VkShadingRatePaletteNV *")] VkShadingRatePaletteNV* pShadingRatePalettes);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetCoarseSampleOrderNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkCoarseSampleOrderTypeNV sampleOrderType, [NativeTypeName("uint32_t")] uint customSampleOrderCount, [NativeTypeName("const VkCoarseSampleOrderCustomNV *")] VkCoarseSampleOrderCustomNV* pCustomSampleOrders);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateAccelerationStructureNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkAccelerationStructureCreateInfoNV *")] VkAccelerationStructureCreateInfoNV* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkAccelerationStructureNV *")] VkAccelerationStructureNV_T** pAccelerationStructure);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyAccelerationStructureNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkAccelerationStructureNV")] VkAccelerationStructureNV_T* accelerationStructure, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetAccelerationStructureMemoryRequirementsNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkAccelerationStructureMemoryRequirementsInfoNV *")] VkAccelerationStructureMemoryRequirementsInfoNV* pInfo, [NativeTypeName("VkMemoryRequirements2KHR *")] VkMemoryRequirements2* pMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkBindAccelerationStructureMemoryNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint bindInfoCount, [NativeTypeName("const VkBindAccelerationStructureMemoryInfoNV *")] VkBindAccelerationStructureMemoryInfoNV* pBindInfos);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBuildAccelerationStructureNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkAccelerationStructureInfoNV *")] VkAccelerationStructureInfoNV* pInfo, [NativeTypeName("VkBuffer")] VkBuffer_T* instanceData, [NativeTypeName("VkDeviceSize")] ulong instanceOffset, [NativeTypeName("VkBool32")] uint update, [NativeTypeName("VkAccelerationStructureNV")] VkAccelerationStructureNV_T* dst, [NativeTypeName("VkAccelerationStructureNV")] VkAccelerationStructureNV_T* src, [NativeTypeName("VkBuffer")] VkBuffer_T* scratch, [NativeTypeName("VkDeviceSize")] ulong scratchOffset);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyAccelerationStructureNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkAccelerationStructureNV")] VkAccelerationStructureNV_T* dst, [NativeTypeName("VkAccelerationStructureNV")] VkAccelerationStructureNV_T* src, VkCopyAccelerationStructureModeKHR mode);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdTraceRaysNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* raygenShaderBindingTableBuffer, [NativeTypeName("VkDeviceSize")] ulong raygenShaderBindingOffset, [NativeTypeName("VkBuffer")] VkBuffer_T* missShaderBindingTableBuffer, [NativeTypeName("VkDeviceSize")] ulong missShaderBindingOffset, [NativeTypeName("VkDeviceSize")] ulong missShaderBindingStride, [NativeTypeName("VkBuffer")] VkBuffer_T* hitShaderBindingTableBuffer, [NativeTypeName("VkDeviceSize")] ulong hitShaderBindingOffset, [NativeTypeName("VkDeviceSize")] ulong hitShaderBindingStride, [NativeTypeName("VkBuffer")] VkBuffer_T* callableShaderBindingTableBuffer, [NativeTypeName("VkDeviceSize")] ulong callableShaderBindingOffset, [NativeTypeName("VkDeviceSize")] ulong callableShaderBindingStride, [NativeTypeName("uint32_t")] uint width, [NativeTypeName("uint32_t")] uint height, [NativeTypeName("uint32_t")] uint depth);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateRayTracingPipelinesNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkPipelineCache")] VkPipelineCache_T* pipelineCache, [NativeTypeName("uint32_t")] uint createInfoCount, [NativeTypeName("const VkRayTracingPipelineCreateInfoNV *")] VkRayTracingPipelineCreateInfoNV* pCreateInfos, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkPipeline *")] VkPipeline_T** pPipelines);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetRayTracingShaderGroupHandlesKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkPipeline")] VkPipeline_T* pipeline, [NativeTypeName("uint32_t")] uint firstGroup, [NativeTypeName("uint32_t")] uint groupCount, [NativeTypeName("size_t")] nuint dataSize, void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetRayTracingShaderGroupHandlesNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkPipeline")] VkPipeline_T* pipeline, [NativeTypeName("uint32_t")] uint firstGroup, [NativeTypeName("uint32_t")] uint groupCount, [NativeTypeName("size_t")] nuint dataSize, void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetAccelerationStructureHandleNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkAccelerationStructureNV")] VkAccelerationStructureNV_T* accelerationStructure, [NativeTypeName("size_t")] nuint dataSize, void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdWriteAccelerationStructuresPropertiesNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint accelerationStructureCount, [NativeTypeName("const VkAccelerationStructureNV *")] VkAccelerationStructureNV_T** pAccelerationStructures, VkQueryType queryType, [NativeTypeName("VkQueryPool")] VkQueryPool_T* queryPool, [NativeTypeName("uint32_t")] uint firstQuery);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCompileDeferredNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkPipeline")] VkPipeline_T* pipeline, [NativeTypeName("uint32_t")] uint shader);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetMemoryHostPointerPropertiesEXT([NativeTypeName("VkDevice")] VkDevice_T* device, VkExternalMemoryHandleTypeFlagBits handleType, [NativeTypeName("const void *")] void* pHostPointer, VkMemoryHostPointerPropertiesEXT* pMemoryHostPointerProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdWriteBufferMarkerAMD([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkPipelineStageFlagBits pipelineStage, [NativeTypeName("VkBuffer")] VkBuffer_T* dstBuffer, [NativeTypeName("VkDeviceSize")] ulong dstOffset, [NativeTypeName("uint32_t")] uint marker);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdWriteBufferMarker2AMD([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkPipelineStageFlags2")] ulong stage, [NativeTypeName("VkBuffer")] VkBuffer_T* dstBuffer, [NativeTypeName("VkDeviceSize")] ulong dstOffset, [NativeTypeName("uint32_t")] uint marker);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceCalibrateableTimeDomainsEXT([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t *")] uint* pTimeDomainCount, VkTimeDomainKHR* pTimeDomains);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetCalibratedTimestampsEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint timestampCount, [NativeTypeName("const VkCalibratedTimestampInfoKHR *")] VkCalibratedTimestampInfoKHR* pTimestampInfos, [NativeTypeName("uint64_t *")] ulong* pTimestamps, [NativeTypeName("uint64_t *")] ulong* pMaxDeviation);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawMeshTasksNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint taskCount, [NativeTypeName("uint32_t")] uint firstTask);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawMeshTasksIndirectNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, [NativeTypeName("VkDeviceSize")] ulong offset, [NativeTypeName("uint32_t")] uint drawCount, [NativeTypeName("uint32_t")] uint stride);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawMeshTasksIndirectCountNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, [NativeTypeName("VkDeviceSize")] ulong offset, [NativeTypeName("VkBuffer")] VkBuffer_T* countBuffer, [NativeTypeName("VkDeviceSize")] ulong countBufferOffset, [NativeTypeName("uint32_t")] uint maxDrawCount, [NativeTypeName("uint32_t")] uint stride);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetExclusiveScissorEnableNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint firstExclusiveScissor, [NativeTypeName("uint32_t")] uint exclusiveScissorCount, [NativeTypeName("const VkBool32 *")] uint* pExclusiveScissorEnables);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetExclusiveScissorNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint firstExclusiveScissor, [NativeTypeName("uint32_t")] uint exclusiveScissorCount, [NativeTypeName("const VkRect2D *")] VkRect2D* pExclusiveScissors);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetCheckpointNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const void *")] void* pCheckpointMarker);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetQueueCheckpointDataNV([NativeTypeName("VkQueue")] VkQueue_T* queue, [NativeTypeName("uint32_t *")] uint* pCheckpointDataCount, VkCheckpointDataNV* pCheckpointData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetQueueCheckpointData2NV([NativeTypeName("VkQueue")] VkQueue_T* queue, [NativeTypeName("uint32_t *")] uint* pCheckpointDataCount, VkCheckpointData2NV* pCheckpointData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkSetSwapchainPresentTimingQueueSizeEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSwapchainKHR")] VkSwapchainKHR_T* swapchain, [NativeTypeName("uint32_t")] uint size);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetSwapchainTimingPropertiesEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSwapchainKHR")] VkSwapchainKHR_T* swapchain, VkSwapchainTimingPropertiesEXT* pSwapchainTimingProperties, [NativeTypeName("uint64_t *")] ulong* pSwapchainTimingPropertiesCounter);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetSwapchainTimeDomainPropertiesEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSwapchainKHR")] VkSwapchainKHR_T* swapchain, VkSwapchainTimeDomainPropertiesEXT* pSwapchainTimeDomainProperties, [NativeTypeName("uint64_t *")] ulong* pTimeDomainsCounter);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPastPresentationTimingEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkPastPresentationTimingInfoEXT *")] VkPastPresentationTimingInfoEXT* pPastPresentationTimingInfo, VkPastPresentationTimingPropertiesEXT* pPastPresentationTimingProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkInitializePerformanceApiINTEL([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkInitializePerformanceApiInfoINTEL *")] VkInitializePerformanceApiInfoINTEL* pInitializeInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkUninitializePerformanceApiINTEL([NativeTypeName("VkDevice")] VkDevice_T* device);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCmdSetPerformanceMarkerINTEL([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkPerformanceMarkerInfoINTEL *")] VkPerformanceMarkerInfoINTEL* pMarkerInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCmdSetPerformanceStreamMarkerINTEL([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkPerformanceStreamMarkerInfoINTEL *")] VkPerformanceStreamMarkerInfoINTEL* pMarkerInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCmdSetPerformanceOverrideINTEL([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkPerformanceOverrideInfoINTEL *")] VkPerformanceOverrideInfoINTEL* pOverrideInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkAcquirePerformanceConfigurationINTEL([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkPerformanceConfigurationAcquireInfoINTEL *")] VkPerformanceConfigurationAcquireInfoINTEL* pAcquireInfo, [NativeTypeName("VkPerformanceConfigurationINTEL *")] VkPerformanceConfigurationINTEL_T** pConfiguration);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkReleasePerformanceConfigurationINTEL([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkPerformanceConfigurationINTEL")] VkPerformanceConfigurationINTEL_T* configuration);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkQueueSetPerformanceConfigurationINTEL([NativeTypeName("VkQueue")] VkQueue_T* queue, [NativeTypeName("VkPerformanceConfigurationINTEL")] VkPerformanceConfigurationINTEL_T* configuration);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPerformanceParameterINTEL([NativeTypeName("VkDevice")] VkDevice_T* device, VkPerformanceParameterTypeINTEL parameter, VkPerformanceValueINTEL* pValue);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkSetLocalDimmingAMD([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSwapchainKHR")] VkSwapchainKHR_T* swapChain, [NativeTypeName("VkBool32")] uint localDimmingEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("VkDeviceAddress")]
    public static extern ulong vkGetBufferDeviceAddressEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkBufferDeviceAddressInfo *")] VkBufferDeviceAddressInfo* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceToolPropertiesEXT([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t *")] uint* pToolCount, VkPhysicalDeviceToolProperties* pToolProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceCooperativeMatrixPropertiesNV([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t *")] uint* pPropertyCount, VkCooperativeMatrixPropertiesNV* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceSupportedFramebufferMixedSamplesCombinationsNV([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t *")] uint* pCombinationCount, VkFramebufferMixedSamplesCombinationNV* pCombinations);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateHeadlessSurfaceEXT([NativeTypeName("VkInstance")] VkInstance_T* instance, [NativeTypeName("const VkHeadlessSurfaceCreateInfoEXT *")] VkHeadlessSurfaceCreateInfoEXT* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkSurfaceKHR *")] VkSurfaceKHR_T** pSurface);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetLineStippleEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint lineStippleFactor, [NativeTypeName("uint16_t")] ushort lineStipplePattern);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkResetQueryPoolEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkQueryPool")] VkQueryPool_T* queryPool, [NativeTypeName("uint32_t")] uint firstQuery, [NativeTypeName("uint32_t")] uint queryCount);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetCullModeEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkCullModeFlags")] uint cullMode);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetFrontFaceEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkFrontFace frontFace);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetPrimitiveTopologyEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkPrimitiveTopology primitiveTopology);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetViewportWithCountEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint viewportCount, [NativeTypeName("const VkViewport *")] VkViewport* pViewports);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetScissorWithCountEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint scissorCount, [NativeTypeName("const VkRect2D *")] VkRect2D* pScissors);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindVertexBuffers2EXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint firstBinding, [NativeTypeName("uint32_t")] uint bindingCount, [NativeTypeName("const VkBuffer *")] VkBuffer_T** pBuffers, [NativeTypeName("const VkDeviceSize *")] ulong* pOffsets, [NativeTypeName("const VkDeviceSize *")] ulong* pSizes, [NativeTypeName("const VkDeviceSize *")] ulong* pStrides);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDepthTestEnableEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint depthTestEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDepthWriteEnableEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint depthWriteEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDepthCompareOpEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkCompareOp depthCompareOp);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDepthBoundsTestEnableEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint depthBoundsTestEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetStencilTestEnableEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint stencilTestEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetStencilOpEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkStencilFaceFlags")] uint faceMask, VkStencilOp failOp, VkStencilOp passOp, VkStencilOp depthFailOp, VkCompareOp compareOp);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCopyMemoryToImageEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkCopyMemoryToImageInfo *")] VkCopyMemoryToImageInfo* pCopyMemoryToImageInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCopyImageToMemoryEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkCopyImageToMemoryInfo *")] VkCopyImageToMemoryInfo* pCopyImageToMemoryInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCopyImageToImageEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkCopyImageToImageInfo *")] VkCopyImageToImageInfo* pCopyImageToImageInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkTransitionImageLayoutEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint transitionCount, [NativeTypeName("const VkHostImageLayoutTransitionInfo *")] VkHostImageLayoutTransitionInfo* pTransitions);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetImageSubresourceLayout2EXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkImage")] VkImage_T* image, [NativeTypeName("const VkImageSubresource2 *")] VkImageSubresource2* pSubresource, VkSubresourceLayout2* pLayout);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkReleaseSwapchainImagesEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkReleaseSwapchainImagesInfoKHR *")] VkReleaseSwapchainImagesInfoKHR* pReleaseInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetGeneratedCommandsMemoryRequirementsNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkGeneratedCommandsMemoryRequirementsInfoNV *")] VkGeneratedCommandsMemoryRequirementsInfoNV* pInfo, VkMemoryRequirements2* pMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdPreprocessGeneratedCommandsNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkGeneratedCommandsInfoNV *")] VkGeneratedCommandsInfoNV* pGeneratedCommandsInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdExecuteGeneratedCommandsNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint isPreprocessed, [NativeTypeName("const VkGeneratedCommandsInfoNV *")] VkGeneratedCommandsInfoNV* pGeneratedCommandsInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindPipelineShaderGroupNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkPipelineBindPoint pipelineBindPoint, [NativeTypeName("VkPipeline")] VkPipeline_T* pipeline, [NativeTypeName("uint32_t")] uint groupIndex);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateIndirectCommandsLayoutNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkIndirectCommandsLayoutCreateInfoNV *")] VkIndirectCommandsLayoutCreateInfoNV* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkIndirectCommandsLayoutNV *")] VkIndirectCommandsLayoutNV_T** pIndirectCommandsLayout);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyIndirectCommandsLayoutNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkIndirectCommandsLayoutNV")] VkIndirectCommandsLayoutNV_T* indirectCommandsLayout, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDepthBias2EXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkDepthBiasInfoEXT *")] VkDepthBiasInfoEXT* pDepthBiasInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkAcquireDrmDisplayEXT([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("int32_t")] int drmFd, [NativeTypeName("VkDisplayKHR")] VkDisplayKHR_T* display);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetDrmDisplayEXT([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("int32_t")] int drmFd, [NativeTypeName("uint32_t")] uint connectorId, [NativeTypeName("VkDisplayKHR *")] VkDisplayKHR_T** display);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreatePrivateDataSlotEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkPrivateDataSlotCreateInfo *")] VkPrivateDataSlotCreateInfo* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkPrivateDataSlot *")] VkPrivateDataSlot_T** pPrivateDataSlot);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyPrivateDataSlotEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkPrivateDataSlot")] VkPrivateDataSlot_T* privateDataSlot, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkSetPrivateDataEXT([NativeTypeName("VkDevice")] VkDevice_T* device, VkObjectType objectType, [NativeTypeName("uint64_t")] ulong objectHandle, [NativeTypeName("VkPrivateDataSlot")] VkPrivateDataSlot_T* privateDataSlot, [NativeTypeName("uint64_t")] ulong data);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPrivateDataEXT([NativeTypeName("VkDevice")] VkDevice_T* device, VkObjectType objectType, [NativeTypeName("uint64_t")] ulong objectHandle, [NativeTypeName("VkPrivateDataSlot")] VkPrivateDataSlot_T* privateDataSlot, [NativeTypeName("uint64_t *")] ulong* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDispatchTileQCOM([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkDispatchTileInfoQCOM *")] VkDispatchTileInfoQCOM* pDispatchTileInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBeginPerTileExecutionQCOM([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkPerTileBeginInfoQCOM *")] VkPerTileBeginInfoQCOM* pPerTileBeginInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdEndPerTileExecutionQCOM([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkPerTileEndInfoQCOM *")] VkPerTileEndInfoQCOM* pPerTileEndInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDescriptorSetLayoutSizeEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDescriptorSetLayout")] VkDescriptorSetLayout_T* layout, [NativeTypeName("VkDeviceSize *")] ulong* pLayoutSizeInBytes);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDescriptorSetLayoutBindingOffsetEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDescriptorSetLayout")] VkDescriptorSetLayout_T* layout, [NativeTypeName("uint32_t")] uint binding, [NativeTypeName("VkDeviceSize *")] ulong* pOffset);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDescriptorEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDescriptorGetInfoEXT *")] VkDescriptorGetInfoEXT* pDescriptorInfo, [NativeTypeName("size_t")] nuint dataSize, void* pDescriptor);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindDescriptorBuffersEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint bufferCount, [NativeTypeName("const VkDescriptorBufferBindingInfoEXT *")] VkDescriptorBufferBindingInfoEXT* pBindingInfos);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDescriptorBufferOffsetsEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkPipelineBindPoint pipelineBindPoint, [NativeTypeName("VkPipelineLayout")] VkPipelineLayout_T* layout, [NativeTypeName("uint32_t")] uint firstSet, [NativeTypeName("uint32_t")] uint setCount, [NativeTypeName("const uint32_t *")] uint* pBufferIndices, [NativeTypeName("const VkDeviceSize *")] ulong* pOffsets);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindDescriptorBufferEmbeddedSamplersEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkPipelineBindPoint pipelineBindPoint, [NativeTypeName("VkPipelineLayout")] VkPipelineLayout_T* layout, [NativeTypeName("uint32_t")] uint set);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetBufferOpaqueCaptureDescriptorDataEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkBufferCaptureDescriptorDataInfoEXT *")] VkBufferCaptureDescriptorDataInfoEXT* pInfo, void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetImageOpaqueCaptureDescriptorDataEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkImageCaptureDescriptorDataInfoEXT *")] VkImageCaptureDescriptorDataInfoEXT* pInfo, void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetImageViewOpaqueCaptureDescriptorDataEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkImageViewCaptureDescriptorDataInfoEXT *")] VkImageViewCaptureDescriptorDataInfoEXT* pInfo, void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetSamplerOpaqueCaptureDescriptorDataEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkSamplerCaptureDescriptorDataInfoEXT *")] VkSamplerCaptureDescriptorDataInfoEXT* pInfo, void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetAccelerationStructureOpaqueCaptureDescriptorDataEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkAccelerationStructureCaptureDescriptorDataInfoEXT *")] VkAccelerationStructureCaptureDescriptorDataInfoEXT* pInfo, void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetFragmentShadingRateEnumNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkFragmentShadingRateNV shadingRate, [NativeTypeName("const VkFragmentShadingRateCombinerOpKHR[2]")] VkFragmentShadingRateCombinerOpKHR* combinerOps);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetDeviceFaultInfoEXT([NativeTypeName("VkDevice")] VkDevice_T* device, VkDeviceFaultCountsEXT* pFaultCounts, VkDeviceFaultInfoEXT* pFaultInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetVertexInputEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint vertexBindingDescriptionCount, [NativeTypeName("const VkVertexInputBindingDescription2EXT *")] VkVertexInputBindingDescription2EXT* pVertexBindingDescriptions, [NativeTypeName("uint32_t")] uint vertexAttributeDescriptionCount, [NativeTypeName("const VkVertexInputAttributeDescription2EXT *")] VkVertexInputAttributeDescription2EXT* pVertexAttributeDescriptions);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetDeviceSubpassShadingMaxWorkgroupSizeHUAWEI([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkRenderPass")] VkRenderPass_T* renderpass, VkExtent2D* pMaxWorkgroupSize);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSubpassShadingHUAWEI([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindInvocationMaskHUAWEI([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkImageView")] VkImageView_T* imageView, VkImageLayout imageLayout);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetMemoryRemoteAddressNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkMemoryGetRemoteAddressInfoNV *")] VkMemoryGetRemoteAddressInfoNV* pMemoryGetRemoteAddressInfo, [NativeTypeName("VkRemoteAddressNV *")] void** pAddress);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPipelinePropertiesEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkPipelineInfoEXT *")] VkPipelineInfoKHR* pPipelineInfo, VkBaseOutStructure* pPipelineProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetPatchControlPointsEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint patchControlPoints);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetRasterizerDiscardEnableEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint rasterizerDiscardEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDepthBiasEnableEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint depthBiasEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetLogicOpEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkLogicOp logicOp);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetPrimitiveRestartEnableEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint primitiveRestartEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetColorWriteEnableEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint attachmentCount, [NativeTypeName("const VkBool32 *")] uint* pColorWriteEnables);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawMultiEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint drawCount, [NativeTypeName("const VkMultiDrawInfoEXT *")] VkMultiDrawInfoEXT* pVertexInfo, [NativeTypeName("uint32_t")] uint instanceCount, [NativeTypeName("uint32_t")] uint firstInstance, [NativeTypeName("uint32_t")] uint stride);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawMultiIndexedEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint drawCount, [NativeTypeName("const VkMultiDrawIndexedInfoEXT *")] VkMultiDrawIndexedInfoEXT* pIndexInfo, [NativeTypeName("uint32_t")] uint instanceCount, [NativeTypeName("uint32_t")] uint firstInstance, [NativeTypeName("uint32_t")] uint stride, [NativeTypeName("const int32_t *")] int* pVertexOffset);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateMicromapEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkMicromapCreateInfoEXT *")] VkMicromapCreateInfoEXT* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkMicromapEXT *")] VkMicromapEXT_T** pMicromap);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyMicromapEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkMicromapEXT")] VkMicromapEXT_T* micromap, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBuildMicromapsEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint infoCount, [NativeTypeName("const VkMicromapBuildInfoEXT *")] VkMicromapBuildInfoEXT* pInfos);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkBuildMicromapsEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDeferredOperationKHR")] VkDeferredOperationKHR_T* deferredOperation, [NativeTypeName("uint32_t")] uint infoCount, [NativeTypeName("const VkMicromapBuildInfoEXT *")] VkMicromapBuildInfoEXT* pInfos);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCopyMicromapEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDeferredOperationKHR")] VkDeferredOperationKHR_T* deferredOperation, [NativeTypeName("const VkCopyMicromapInfoEXT *")] VkCopyMicromapInfoEXT* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCopyMicromapToMemoryEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDeferredOperationKHR")] VkDeferredOperationKHR_T* deferredOperation, [NativeTypeName("const VkCopyMicromapToMemoryInfoEXT *")] VkCopyMicromapToMemoryInfoEXT* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCopyMemoryToMicromapEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDeferredOperationKHR")] VkDeferredOperationKHR_T* deferredOperation, [NativeTypeName("const VkCopyMemoryToMicromapInfoEXT *")] VkCopyMemoryToMicromapInfoEXT* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkWriteMicromapsPropertiesEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint micromapCount, [NativeTypeName("const VkMicromapEXT *")] VkMicromapEXT_T** pMicromaps, VkQueryType queryType, [NativeTypeName("size_t")] nuint dataSize, void* pData, [NativeTypeName("size_t")] nuint stride);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyMicromapEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkCopyMicromapInfoEXT *")] VkCopyMicromapInfoEXT* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyMicromapToMemoryEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkCopyMicromapToMemoryInfoEXT *")] VkCopyMicromapToMemoryInfoEXT* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyMemoryToMicromapEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkCopyMemoryToMicromapInfoEXT *")] VkCopyMemoryToMicromapInfoEXT* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdWriteMicromapsPropertiesEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint micromapCount, [NativeTypeName("const VkMicromapEXT *")] VkMicromapEXT_T** pMicromaps, VkQueryType queryType, [NativeTypeName("VkQueryPool")] VkQueryPool_T* queryPool, [NativeTypeName("uint32_t")] uint firstQuery);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDeviceMicromapCompatibilityEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkMicromapVersionInfoEXT *")] VkMicromapVersionInfoEXT* pVersionInfo, VkAccelerationStructureCompatibilityKHR* pCompatibility);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetMicromapBuildSizesEXT([NativeTypeName("VkDevice")] VkDevice_T* device, VkAccelerationStructureBuildTypeKHR buildType, [NativeTypeName("const VkMicromapBuildInfoEXT *")] VkMicromapBuildInfoEXT* pBuildInfo, VkMicromapBuildSizesInfoEXT* pSizeInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawClusterHUAWEI([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint groupCountX, [NativeTypeName("uint32_t")] uint groupCountY, [NativeTypeName("uint32_t")] uint groupCountZ);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawClusterIndirectHUAWEI([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, [NativeTypeName("VkDeviceSize")] ulong offset);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkSetDeviceMemoryPriorityEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDeviceMemory")] VkDeviceMemory_T* memory, float priority);

    [NativeTypeName("const VkPhysicalDeviceSchedulingControlsFlagBitsARM")]
    public const ulong VK_PHYSICAL_DEVICE_SCHEDULING_CONTROLS_SHADER_CORE_COUNT_ARM = 0x00000001UL;

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDescriptorSetLayoutHostMappingInfoVALVE([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDescriptorSetBindingReferenceVALVE *")] VkDescriptorSetBindingReferenceVALVE* pBindingReference, VkDescriptorSetLayoutHostMappingInfoVALVE* pHostMapping);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDescriptorSetHostMappingVALVE([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDescriptorSet")] VkDescriptorSet_T* descriptorSet, void** ppData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyMemoryIndirectNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkDeviceAddress")] ulong copyBufferAddress, [NativeTypeName("uint32_t")] uint copyCount, [NativeTypeName("uint32_t")] uint stride);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyMemoryToImageIndirectNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkDeviceAddress")] ulong copyBufferAddress, [NativeTypeName("uint32_t")] uint copyCount, [NativeTypeName("uint32_t")] uint stride, [NativeTypeName("VkImage")] VkImage_T* dstImage, VkImageLayout dstImageLayout, [NativeTypeName("const VkImageSubresourceLayers *")] VkImageSubresourceLayers* pImageSubresources);

    [NativeTypeName("const VkMemoryDecompressionMethodFlagBitsEXT")]
    public const ulong VK_MEMORY_DECOMPRESSION_METHOD_GDEFLATE_1_0_BIT_EXT = 0x00000001UL;

    [NativeTypeName("const VkMemoryDecompressionMethodFlagBitsEXT")]
    public const ulong VK_MEMORY_DECOMPRESSION_METHOD_GDEFLATE_1_0_BIT_NV = 0x00000001UL;

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDecompressMemoryNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint decompressRegionCount, [NativeTypeName("const VkDecompressMemoryRegionNV *")] VkDecompressMemoryRegionNV* pDecompressMemoryRegions);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDecompressMemoryIndirectCountNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkDeviceAddress")] ulong indirectCommandsAddress, [NativeTypeName("VkDeviceAddress")] ulong indirectCommandsCountAddress, [NativeTypeName("uint32_t")] uint stride);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPipelineIndirectMemoryRequirementsNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkComputePipelineCreateInfo *")] VkComputePipelineCreateInfo* pCreateInfo, VkMemoryRequirements2* pMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdUpdatePipelineIndirectBufferNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkPipelineBindPoint pipelineBindPoint, [NativeTypeName("VkPipeline")] VkPipeline_T* pipeline);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("VkDeviceAddress")]
    public static extern ulong vkGetPipelineIndirectDeviceAddressNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkPipelineIndirectDeviceAddressInfoNV *")] VkPipelineIndirectDeviceAddressInfoNV* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDepthClampEnableEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint depthClampEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetPolygonModeEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkPolygonMode polygonMode);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetRasterizationSamplesEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkSampleCountFlagBits rasterizationSamples);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetSampleMaskEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkSampleCountFlagBits samples, [NativeTypeName("const VkSampleMask *")] uint* pSampleMask);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetAlphaToCoverageEnableEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint alphaToCoverageEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetAlphaToOneEnableEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint alphaToOneEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetLogicOpEnableEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint logicOpEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetColorBlendEnableEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint firstAttachment, [NativeTypeName("uint32_t")] uint attachmentCount, [NativeTypeName("const VkBool32 *")] uint* pColorBlendEnables);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetColorBlendEquationEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint firstAttachment, [NativeTypeName("uint32_t")] uint attachmentCount, [NativeTypeName("const VkColorBlendEquationEXT *")] VkColorBlendEquationEXT* pColorBlendEquations);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetColorWriteMaskEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint firstAttachment, [NativeTypeName("uint32_t")] uint attachmentCount, [NativeTypeName("const VkColorComponentFlags *")] uint* pColorWriteMasks);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetTessellationDomainOriginEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkTessellationDomainOrigin domainOrigin);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetRasterizationStreamEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint rasterizationStream);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetConservativeRasterizationModeEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkConservativeRasterizationModeEXT conservativeRasterizationMode);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetExtraPrimitiveOverestimationSizeEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, float extraPrimitiveOverestimationSize);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDepthClipEnableEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint depthClipEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetSampleLocationsEnableEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint sampleLocationsEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetColorBlendAdvancedEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint firstAttachment, [NativeTypeName("uint32_t")] uint attachmentCount, [NativeTypeName("const VkColorBlendAdvancedEXT *")] VkColorBlendAdvancedEXT* pColorBlendAdvanced);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetProvokingVertexModeEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkProvokingVertexModeEXT provokingVertexMode);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetLineRasterizationModeEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkLineRasterizationModeEXT")] VkLineRasterizationMode lineRasterizationMode);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetLineStippleEnableEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint stippledLineEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDepthClipNegativeOneToOneEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint negativeOneToOne);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetViewportWScalingEnableNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint viewportWScalingEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetViewportSwizzleNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint firstViewport, [NativeTypeName("uint32_t")] uint viewportCount, [NativeTypeName("const VkViewportSwizzleNV *")] VkViewportSwizzleNV* pViewportSwizzles);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetCoverageToColorEnableNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint coverageToColorEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetCoverageToColorLocationNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint coverageToColorLocation);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetCoverageModulationModeNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkCoverageModulationModeNV coverageModulationMode);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetCoverageModulationTableEnableNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint coverageModulationTableEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetCoverageModulationTableNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint coverageModulationTableCount, [NativeTypeName("const float *")] float* pCoverageModulationTable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetShadingRateImageEnableNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint shadingRateImageEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetRepresentativeFragmentTestEnableNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint representativeFragmentTestEnable);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetCoverageReductionModeNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkCoverageReductionModeNV coverageReductionMode);

    [NativeTypeName("const VkTensorCreateFlagBitsARM")]
    public const ulong VK_TENSOR_CREATE_MUTABLE_FORMAT_BIT_ARM = 0x00000001UL;

    [NativeTypeName("const VkTensorCreateFlagBitsARM")]
    public const ulong VK_TENSOR_CREATE_PROTECTED_BIT_ARM = 0x00000002UL;

    [NativeTypeName("const VkTensorCreateFlagBitsARM")]
    public const ulong VK_TENSOR_CREATE_DESCRIPTOR_HEAP_CAPTURE_REPLAY_BIT_ARM = 0x00000008UL;

    [NativeTypeName("const VkTensorCreateFlagBitsARM")]
    public const ulong VK_TENSOR_CREATE_DESCRIPTOR_BUFFER_CAPTURE_REPLAY_BIT_ARM = 0x00000004UL;

    [NativeTypeName("const VkTensorUsageFlagBitsARM")]
    public const ulong VK_TENSOR_USAGE_SHADER_BIT_ARM = 0x00000002UL;

    [NativeTypeName("const VkTensorUsageFlagBitsARM")]
    public const ulong VK_TENSOR_USAGE_TRANSFER_SRC_BIT_ARM = 0x00000004UL;

    [NativeTypeName("const VkTensorUsageFlagBitsARM")]
    public const ulong VK_TENSOR_USAGE_TRANSFER_DST_BIT_ARM = 0x00000008UL;

    [NativeTypeName("const VkTensorUsageFlagBitsARM")]
    public const ulong VK_TENSOR_USAGE_IMAGE_ALIASING_BIT_ARM = 0x00000010UL;

    [NativeTypeName("const VkTensorUsageFlagBitsARM")]
    public const ulong VK_TENSOR_USAGE_DATA_GRAPH_BIT_ARM = 0x00000020UL;

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateTensorARM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkTensorCreateInfoARM *")] VkTensorCreateInfoARM* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkTensorARM *")] VkTensorARM_T** pTensor);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyTensorARM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkTensorARM")] VkTensorARM_T* tensor, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateTensorViewARM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkTensorViewCreateInfoARM *")] VkTensorViewCreateInfoARM* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkTensorViewARM *")] VkTensorViewARM_T** pView);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyTensorViewARM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkTensorViewARM")] VkTensorViewARM_T* tensorView, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetTensorMemoryRequirementsARM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkTensorMemoryRequirementsInfoARM *")] VkTensorMemoryRequirementsInfoARM* pInfo, VkMemoryRequirements2* pMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkBindTensorMemoryARM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint bindInfoCount, [NativeTypeName("const VkBindTensorMemoryInfoARM *")] VkBindTensorMemoryInfoARM* pBindInfos);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDeviceTensorMemoryRequirementsARM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDeviceTensorMemoryRequirementsARM *")] VkDeviceTensorMemoryRequirementsARM* pInfo, VkMemoryRequirements2* pMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyTensorARM([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkCopyTensorInfoARM *")] VkCopyTensorInfoARM* pCopyTensorInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceExternalTensorPropertiesARM([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkPhysicalDeviceExternalTensorInfoARM *")] VkPhysicalDeviceExternalTensorInfoARM* pExternalTensorInfo, VkExternalTensorPropertiesARM* pExternalTensorProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetTensorOpaqueCaptureDescriptorDataARM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkTensorCaptureDescriptorDataInfoARM *")] VkTensorCaptureDescriptorDataInfoARM* pInfo, void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetTensorViewOpaqueCaptureDescriptorDataARM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkTensorViewCaptureDescriptorDataInfoARM *")] VkTensorViewCaptureDescriptorDataInfoARM* pInfo, void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetShaderModuleIdentifierEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkShaderModule")] VkShaderModule_T* shaderModule, VkShaderModuleIdentifierEXT* pIdentifier);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetShaderModuleCreateInfoIdentifierEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkShaderModuleCreateInfo *")] VkShaderModuleCreateInfo* pCreateInfo, VkShaderModuleIdentifierEXT* pIdentifier);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceOpticalFlowImageFormatsNV([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkOpticalFlowImageFormatInfoNV *")] VkOpticalFlowImageFormatInfoNV* pOpticalFlowImageFormatInfo, [NativeTypeName("uint32_t *")] uint* pFormatCount, VkOpticalFlowImageFormatPropertiesNV* pImageFormatProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateOpticalFlowSessionNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkOpticalFlowSessionCreateInfoNV *")] VkOpticalFlowSessionCreateInfoNV* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkOpticalFlowSessionNV *")] VkOpticalFlowSessionNV_T** pSession);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyOpticalFlowSessionNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkOpticalFlowSessionNV")] VkOpticalFlowSessionNV_T* session, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkBindOpticalFlowSessionImageNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkOpticalFlowSessionNV")] VkOpticalFlowSessionNV_T* session, VkOpticalFlowSessionBindingPointNV bindingPoint, [NativeTypeName("VkImageView")] VkImageView_T* view, VkImageLayout layout);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdOpticalFlowExecuteNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkOpticalFlowSessionNV")] VkOpticalFlowSessionNV_T* session, [NativeTypeName("const VkOpticalFlowExecuteInfoNV *")] VkOpticalFlowExecuteInfoNV* pExecuteInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkAntiLagUpdateAMD([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkAntiLagDataAMD *")] VkAntiLagDataAMD* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateShadersEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint createInfoCount, [NativeTypeName("const VkShaderCreateInfoEXT *")] VkShaderCreateInfoEXT* pCreateInfos, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkShaderEXT *")] VkShaderEXT_T** pShaders);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyShaderEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkShaderEXT")] VkShaderEXT_T* shader, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetShaderBinaryDataEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkShaderEXT")] VkShaderEXT_T* shader, [NativeTypeName("size_t *")] nuint* pDataSize, void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindShadersEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint stageCount, [NativeTypeName("const VkShaderStageFlagBits *")] VkShaderStageFlagBits* pStages, [NativeTypeName("const VkShaderEXT *")] VkShaderEXT_T** pShaders);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetDepthClampRangeEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, VkDepthClampModeEXT depthClampMode, [NativeTypeName("const VkDepthClampRangeEXT *")] VkDepthClampRangeEXT* pDepthClampRange);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetFramebufferTilePropertiesQCOM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkFramebuffer")] VkFramebuffer_T* framebuffer, [NativeTypeName("uint32_t *")] uint* pPropertiesCount, VkTilePropertiesQCOM* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetDynamicRenderingTilePropertiesQCOM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkRenderingInfo *")] VkRenderingInfo* pRenderingInfo, VkTilePropertiesQCOM* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceCooperativeVectorPropertiesNV([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t *")] uint* pPropertyCount, VkCooperativeVectorPropertiesNV* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkConvertCooperativeVectorMatrixNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkConvertCooperativeVectorMatrixInfoNV *")] VkConvertCooperativeVectorMatrixInfoNV* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdConvertCooperativeVectorMatrixNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint infoCount, [NativeTypeName("const VkConvertCooperativeVectorMatrixInfoNV *")] VkConvertCooperativeVectorMatrixInfoNV* pInfos);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkSetLatencySleepModeNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSwapchainKHR")] VkSwapchainKHR_T* swapchain, [NativeTypeName("const VkLatencySleepModeInfoNV *")] VkLatencySleepModeInfoNV* pSleepModeInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkLatencySleepNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSwapchainKHR")] VkSwapchainKHR_T* swapchain, [NativeTypeName("const VkLatencySleepInfoNV *")] VkLatencySleepInfoNV* pSleepInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkSetLatencyMarkerNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSwapchainKHR")] VkSwapchainKHR_T* swapchain, [NativeTypeName("const VkSetLatencyMarkerInfoNV *")] VkSetLatencyMarkerInfoNV* pLatencyMarkerInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetLatencyTimingsNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkSwapchainKHR")] VkSwapchainKHR_T* swapchain, VkGetLatencyMarkerInfoNV* pLatencyMarkerInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkQueueNotifyOutOfBandNV([NativeTypeName("VkQueue")] VkQueue_T* queue, [NativeTypeName("const VkOutOfBandQueueTypeInfoNV *")] VkOutOfBandQueueTypeInfoNV* pQueueTypeInfo);

    [NativeTypeName("const VkDataGraphPipelineSessionCreateFlagBitsARM")]
    public const ulong VK_DATA_GRAPH_PIPELINE_SESSION_CREATE_PROTECTED_BIT_ARM = 0x00000001UL;

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateDataGraphPipelinesARM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDeferredOperationKHR")] VkDeferredOperationKHR_T* deferredOperation, [NativeTypeName("VkPipelineCache")] VkPipelineCache_T* pipelineCache, [NativeTypeName("uint32_t")] uint createInfoCount, [NativeTypeName("const VkDataGraphPipelineCreateInfoARM *")] VkDataGraphPipelineCreateInfoARM* pCreateInfos, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkPipeline *")] VkPipeline_T** pPipelines);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateDataGraphPipelineSessionARM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDataGraphPipelineSessionCreateInfoARM *")] VkDataGraphPipelineSessionCreateInfoARM* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkDataGraphPipelineSessionARM *")] VkDataGraphPipelineSessionARM_T** pSession);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetDataGraphPipelineSessionBindPointRequirementsARM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDataGraphPipelineSessionBindPointRequirementsInfoARM *")] VkDataGraphPipelineSessionBindPointRequirementsInfoARM* pInfo, [NativeTypeName("uint32_t *")] uint* pBindPointRequirementCount, VkDataGraphPipelineSessionBindPointRequirementARM* pBindPointRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDataGraphPipelineSessionMemoryRequirementsARM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDataGraphPipelineSessionMemoryRequirementsInfoARM *")] VkDataGraphPipelineSessionMemoryRequirementsInfoARM* pInfo, VkMemoryRequirements2* pMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkBindDataGraphPipelineSessionMemoryARM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint bindInfoCount, [NativeTypeName("const VkBindDataGraphPipelineSessionMemoryInfoARM *")] VkBindDataGraphPipelineSessionMemoryInfoARM* pBindInfos);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyDataGraphPipelineSessionARM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDataGraphPipelineSessionARM")] VkDataGraphPipelineSessionARM_T* session, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDispatchDataGraphARM([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkDataGraphPipelineSessionARM")] VkDataGraphPipelineSessionARM_T* session, [NativeTypeName("const VkDataGraphPipelineDispatchInfoARM *")] VkDataGraphPipelineDispatchInfoARM* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetDataGraphPipelineAvailablePropertiesARM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDataGraphPipelineInfoARM *")] VkDataGraphPipelineInfoARM* pPipelineInfo, [NativeTypeName("uint32_t *")] uint* pPropertiesCount, VkDataGraphPipelinePropertyARM* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetDataGraphPipelinePropertiesARM([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkDataGraphPipelineInfoARM *")] VkDataGraphPipelineInfoARM* pPipelineInfo, [NativeTypeName("uint32_t")] uint propertiesCount, VkDataGraphPipelinePropertyQueryResultARM* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceQueueFamilyDataGraphPropertiesARM([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t")] uint queueFamilyIndex, [NativeTypeName("uint32_t *")] uint* pQueueFamilyDataGraphPropertyCount, VkQueueFamilyDataGraphPropertiesARM* pQueueFamilyDataGraphProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPhysicalDeviceQueueFamilyDataGraphProcessingEnginePropertiesARM([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("const VkPhysicalDeviceQueueFamilyDataGraphProcessingEngineInfoARM *")] VkPhysicalDeviceQueueFamilyDataGraphProcessingEngineInfoARM* pQueueFamilyDataGraphProcessingEngineInfo, VkQueueFamilyDataGraphProcessingEnginePropertiesARM* pQueueFamilyDataGraphProcessingEngineProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetAttachmentFeedbackLoopEnableEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkImageAspectFlags")] uint aspectMask);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBindTileMemoryQCOM([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkTileMemoryBindInfoQCOM *")] VkTileMemoryBindInfoQCOM* pTileMemoryBindInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDecompressMemoryEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkDecompressMemoryInfoEXT *")] VkDecompressMemoryInfoEXT* pDecompressMemoryInfoEXT);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDecompressMemoryIndirectCountEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkMemoryDecompressionMethodFlagsEXT")] ulong decompressionMethod, [NativeTypeName("VkDeviceAddress")] ulong indirectCommandsAddress, [NativeTypeName("VkDeviceAddress")] ulong indirectCommandsCountAddress, [NativeTypeName("uint32_t")] uint maxDecompressionCount, [NativeTypeName("uint32_t")] uint stride);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateExternalComputeQueueNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkExternalComputeQueueCreateInfoNV *")] VkExternalComputeQueueCreateInfoNV* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkExternalComputeQueueNV *")] VkExternalComputeQueueNV_T** pExternalQueue);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyExternalComputeQueueNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkExternalComputeQueueNV")] VkExternalComputeQueueNV_T* externalQueue, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetExternalComputeQueueDataNV([NativeTypeName("VkExternalComputeQueueNV")] VkExternalComputeQueueNV_T* externalQueue, VkExternalComputeQueueDataParamsNV* @params, void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetClusterAccelerationStructureBuildSizesNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkClusterAccelerationStructureInputInfoNV *")] VkClusterAccelerationStructureInputInfoNV* pInfo, VkAccelerationStructureBuildSizesInfoKHR* pSizeInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBuildClusterAccelerationStructureIndirectNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkClusterAccelerationStructureCommandsInfoNV *")] VkClusterAccelerationStructureCommandsInfoNV* pCommandInfos);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetPartitionedAccelerationStructuresBuildSizesNV([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkPartitionedAccelerationStructureInstancesInputNV *")] VkPartitionedAccelerationStructureInstancesInputNV* pInfo, VkAccelerationStructureBuildSizesInfoKHR* pSizeInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBuildPartitionedAccelerationStructuresNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkBuildPartitionedAccelerationStructureInfoNV *")] VkBuildPartitionedAccelerationStructureInfoNV* pBuildInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetGeneratedCommandsMemoryRequirementsEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkGeneratedCommandsMemoryRequirementsInfoEXT *")] VkGeneratedCommandsMemoryRequirementsInfoEXT* pInfo, VkMemoryRequirements2* pMemoryRequirements);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdPreprocessGeneratedCommandsEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkGeneratedCommandsInfoEXT *")] VkGeneratedCommandsInfoEXT* pGeneratedCommandsInfo, [NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* stateCommandBuffer);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdExecuteGeneratedCommandsEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBool32")] uint isPreprocessed, [NativeTypeName("const VkGeneratedCommandsInfoEXT *")] VkGeneratedCommandsInfoEXT* pGeneratedCommandsInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateIndirectCommandsLayoutEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkIndirectCommandsLayoutCreateInfoEXT *")] VkIndirectCommandsLayoutCreateInfoEXT* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkIndirectCommandsLayoutEXT *")] VkIndirectCommandsLayoutEXT_T** pIndirectCommandsLayout);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyIndirectCommandsLayoutEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkIndirectCommandsLayoutEXT")] VkIndirectCommandsLayoutEXT_T* indirectCommandsLayout, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateIndirectExecutionSetEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkIndirectExecutionSetCreateInfoEXT *")] VkIndirectExecutionSetCreateInfoEXT* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkIndirectExecutionSetEXT *")] VkIndirectExecutionSetEXT_T** pIndirectExecutionSet);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyIndirectExecutionSetEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkIndirectExecutionSetEXT")] VkIndirectExecutionSetEXT_T* indirectExecutionSet, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkUpdateIndirectExecutionSetPipelineEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkIndirectExecutionSetEXT")] VkIndirectExecutionSetEXT_T* indirectExecutionSet, [NativeTypeName("uint32_t")] uint executionSetWriteCount, [NativeTypeName("const VkWriteIndirectExecutionSetPipelineEXT *")] VkWriteIndirectExecutionSetPipelineEXT* pExecutionSetWrites);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkUpdateIndirectExecutionSetShaderEXT([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkIndirectExecutionSetEXT")] VkIndirectExecutionSetEXT_T* indirectExecutionSet, [NativeTypeName("uint32_t")] uint executionSetWriteCount, [NativeTypeName("const VkWriteIndirectExecutionSetShaderEXT *")] VkWriteIndirectExecutionSetShaderEXT* pExecutionSetWrites);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetPhysicalDeviceCooperativeMatrixFlexibleDimensionsPropertiesNV([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t *")] uint* pPropertyCount, VkCooperativeMatrixFlexibleDimensionsPropertiesNV* pProperties);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkEnumeratePhysicalDeviceQueueFamilyPerformanceCountersByRegionARM([NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice, [NativeTypeName("uint32_t")] uint queueFamilyIndex, [NativeTypeName("uint32_t *")] uint* pCounterCount, VkPerformanceCounterARM* pCounters, VkPerformanceCounterDescriptionARM* pCounterDescriptions);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdEndRendering2EXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkRenderingEndInfoKHR *")] VkRenderingEndInfoKHR* pRenderingEndInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBeginCustomResolveEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkBeginCustomResolveInfoEXT *")] VkBeginCustomResolveInfoEXT* pBeginCustomResolveInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetComputeOccupancyPriorityNV([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkComputeOccupancyPriorityParametersNV *")] VkComputeOccupancyPriorityParametersNV* pParameters);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateAccelerationStructureKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkAccelerationStructureCreateInfoKHR *")] VkAccelerationStructureCreateInfoKHR* pCreateInfo, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkAccelerationStructureKHR *")] VkAccelerationStructureKHR_T** pAccelerationStructure);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkDestroyAccelerationStructureKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkAccelerationStructureKHR")] VkAccelerationStructureKHR_T* accelerationStructure, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBuildAccelerationStructuresKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint infoCount, [NativeTypeName("const VkAccelerationStructureBuildGeometryInfoKHR *")] VkAccelerationStructureBuildGeometryInfoKHR* pInfos, [NativeTypeName("const VkAccelerationStructureBuildRangeInfoKHR *const *")] VkAccelerationStructureBuildRangeInfoKHR** ppBuildRangeInfos);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdBuildAccelerationStructuresIndirectKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint infoCount, [NativeTypeName("const VkAccelerationStructureBuildGeometryInfoKHR *")] VkAccelerationStructureBuildGeometryInfoKHR* pInfos, [NativeTypeName("const VkDeviceAddress *")] ulong* pIndirectDeviceAddresses, [NativeTypeName("const uint32_t *")] uint* pIndirectStrides, [NativeTypeName("const uint32_t *const *")] uint** ppMaxPrimitiveCounts);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkBuildAccelerationStructuresKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDeferredOperationKHR")] VkDeferredOperationKHR_T* deferredOperation, [NativeTypeName("uint32_t")] uint infoCount, [NativeTypeName("const VkAccelerationStructureBuildGeometryInfoKHR *")] VkAccelerationStructureBuildGeometryInfoKHR* pInfos, [NativeTypeName("const VkAccelerationStructureBuildRangeInfoKHR *const *")] VkAccelerationStructureBuildRangeInfoKHR** ppBuildRangeInfos);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCopyAccelerationStructureKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDeferredOperationKHR")] VkDeferredOperationKHR_T* deferredOperation, [NativeTypeName("const VkCopyAccelerationStructureInfoKHR *")] VkCopyAccelerationStructureInfoKHR* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCopyAccelerationStructureToMemoryKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDeferredOperationKHR")] VkDeferredOperationKHR_T* deferredOperation, [NativeTypeName("const VkCopyAccelerationStructureToMemoryInfoKHR *")] VkCopyAccelerationStructureToMemoryInfoKHR* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCopyMemoryToAccelerationStructureKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDeferredOperationKHR")] VkDeferredOperationKHR_T* deferredOperation, [NativeTypeName("const VkCopyMemoryToAccelerationStructureInfoKHR *")] VkCopyMemoryToAccelerationStructureInfoKHR* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkWriteAccelerationStructuresPropertiesKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("uint32_t")] uint accelerationStructureCount, [NativeTypeName("const VkAccelerationStructureKHR *")] VkAccelerationStructureKHR_T** pAccelerationStructures, VkQueryType queryType, [NativeTypeName("size_t")] nuint dataSize, void* pData, [NativeTypeName("size_t")] nuint stride);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyAccelerationStructureKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkCopyAccelerationStructureInfoKHR *")] VkCopyAccelerationStructureInfoKHR* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyAccelerationStructureToMemoryKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkCopyAccelerationStructureToMemoryInfoKHR *")] VkCopyAccelerationStructureToMemoryInfoKHR* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdCopyMemoryToAccelerationStructureKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkCopyMemoryToAccelerationStructureInfoKHR *")] VkCopyMemoryToAccelerationStructureInfoKHR* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("VkDeviceAddress")]
    public static extern ulong vkGetAccelerationStructureDeviceAddressKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkAccelerationStructureDeviceAddressInfoKHR *")] VkAccelerationStructureDeviceAddressInfoKHR* pInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdWriteAccelerationStructuresPropertiesKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint accelerationStructureCount, [NativeTypeName("const VkAccelerationStructureKHR *")] VkAccelerationStructureKHR_T** pAccelerationStructures, VkQueryType queryType, [NativeTypeName("VkQueryPool")] VkQueryPool_T* queryPool, [NativeTypeName("uint32_t")] uint firstQuery);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetDeviceAccelerationStructureCompatibilityKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("const VkAccelerationStructureVersionInfoKHR *")] VkAccelerationStructureVersionInfoKHR* pVersionInfo, VkAccelerationStructureCompatibilityKHR* pCompatibility);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkGetAccelerationStructureBuildSizesKHR([NativeTypeName("VkDevice")] VkDevice_T* device, VkAccelerationStructureBuildTypeKHR buildType, [NativeTypeName("const VkAccelerationStructureBuildGeometryInfoKHR *")] VkAccelerationStructureBuildGeometryInfoKHR* pBuildInfo, [NativeTypeName("const uint32_t *")] uint* pMaxPrimitiveCounts, VkAccelerationStructureBuildSizesInfoKHR* pSizeInfo);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdTraceRaysKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkStridedDeviceAddressRegionKHR *")] VkStridedDeviceAddressRegionKHR* pRaygenShaderBindingTable, [NativeTypeName("const VkStridedDeviceAddressRegionKHR *")] VkStridedDeviceAddressRegionKHR* pMissShaderBindingTable, [NativeTypeName("const VkStridedDeviceAddressRegionKHR *")] VkStridedDeviceAddressRegionKHR* pHitShaderBindingTable, [NativeTypeName("const VkStridedDeviceAddressRegionKHR *")] VkStridedDeviceAddressRegionKHR* pCallableShaderBindingTable, [NativeTypeName("uint32_t")] uint width, [NativeTypeName("uint32_t")] uint height, [NativeTypeName("uint32_t")] uint depth);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateRayTracingPipelinesKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkDeferredOperationKHR")] VkDeferredOperationKHR_T* deferredOperation, [NativeTypeName("VkPipelineCache")] VkPipelineCache_T* pipelineCache, [NativeTypeName("uint32_t")] uint createInfoCount, [NativeTypeName("const VkRayTracingPipelineCreateInfoKHR *")] VkRayTracingPipelineCreateInfoKHR* pCreateInfos, [NativeTypeName("const VkAllocationCallbacks *")] VkAllocationCallbacks* pAllocator, [NativeTypeName("VkPipeline *")] VkPipeline_T** pPipelines);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkGetRayTracingCaptureReplayShaderGroupHandlesKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkPipeline")] VkPipeline_T* pipeline, [NativeTypeName("uint32_t")] uint firstGroup, [NativeTypeName("uint32_t")] uint groupCount, [NativeTypeName("size_t")] nuint dataSize, void* pData);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdTraceRaysIndirectKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("const VkStridedDeviceAddressRegionKHR *")] VkStridedDeviceAddressRegionKHR* pRaygenShaderBindingTable, [NativeTypeName("const VkStridedDeviceAddressRegionKHR *")] VkStridedDeviceAddressRegionKHR* pMissShaderBindingTable, [NativeTypeName("const VkStridedDeviceAddressRegionKHR *")] VkStridedDeviceAddressRegionKHR* pHitShaderBindingTable, [NativeTypeName("const VkStridedDeviceAddressRegionKHR *")] VkStridedDeviceAddressRegionKHR* pCallableShaderBindingTable, [NativeTypeName("VkDeviceAddress")] ulong indirectDeviceAddress);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("VkDeviceSize")]
    public static extern ulong vkGetRayTracingShaderGroupStackSizeKHR([NativeTypeName("VkDevice")] VkDevice_T* device, [NativeTypeName("VkPipeline")] VkPipeline_T* pipeline, [NativeTypeName("uint32_t")] uint group, VkShaderGroupShaderKHR groupShader);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdSetRayTracingPipelineStackSizeKHR([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint pipelineStackSize);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawMeshTasksEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("uint32_t")] uint groupCountX, [NativeTypeName("uint32_t")] uint groupCountY, [NativeTypeName("uint32_t")] uint groupCountZ);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawMeshTasksIndirectEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, [NativeTypeName("VkDeviceSize")] ulong offset, [NativeTypeName("uint32_t")] uint drawCount, [NativeTypeName("uint32_t")] uint stride);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void vkCmdDrawMeshTasksIndirectCountEXT([NativeTypeName("VkCommandBuffer")] VkCommandBuffer_T* commandBuffer, [NativeTypeName("VkBuffer")] VkBuffer_T* buffer, [NativeTypeName("VkDeviceSize")] ulong offset, [NativeTypeName("VkBuffer")] VkBuffer_T* countBuffer, [NativeTypeName("VkDeviceSize")] ulong countBufferOffset, [NativeTypeName("uint32_t")] uint maxDrawCount, [NativeTypeName("uint32_t")] uint stride);
}
