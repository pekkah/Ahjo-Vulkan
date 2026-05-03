using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Vma.Native;

public static unsafe partial class Vma
{
    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaCreateAllocator([NativeTypeName("const VmaAllocatorCreateInfo * _Nonnull")] VmaAllocatorCreateInfo* pCreateInfo, [NativeTypeName("VmaAllocator  _Nullable * _Nonnull")] VmaAllocator_T** pAllocator);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaDestroyAllocator([NativeTypeName("VmaAllocator _Nullable")] VmaAllocator_T* allocator);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaGetAllocatorInfo([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocatorInfo * _Nonnull")] VmaAllocatorInfo* pAllocatorInfo);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaGetPhysicalDeviceProperties([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("const VkPhysicalDeviceProperties * _Nullable * _Nonnull")] Ahjo.Vulkan.Native.VkPhysicalDeviceProperties** ppPhysicalDeviceProperties);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaGetMemoryProperties([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("const VkPhysicalDeviceMemoryProperties * _Nullable * _Nonnull")] Ahjo.Vulkan.Native.VkPhysicalDeviceMemoryProperties** ppPhysicalDeviceMemoryProperties);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaGetMemoryTypeProperties([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("uint32_t")] uint memoryTypeIndex, [NativeTypeName("VkMemoryPropertyFlags * _Nonnull")] uint* pFlags);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaSetCurrentFrameIndex([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("uint32_t")] uint frameIndex);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaCalculateStatistics([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaTotalStatistics * _Nonnull")] VmaTotalStatistics* pStats);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaGetHeapBudgets([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaBudget * _Nonnull")] VmaBudget* pBudgets);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaFindMemoryTypeIndex([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("uint32_t")] uint memoryTypeBits, [NativeTypeName("const VmaAllocationCreateInfo * _Nonnull")] VmaAllocationCreateInfo* pAllocationCreateInfo, [NativeTypeName("uint32_t * _Nonnull")] uint* pMemoryTypeIndex);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaFindMemoryTypeIndexForBufferInfo([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("const VkBufferCreateInfo * _Nonnull")] Ahjo.Vulkan.Native.VkBufferCreateInfo* pBufferCreateInfo, [NativeTypeName("const VmaAllocationCreateInfo * _Nonnull")] VmaAllocationCreateInfo* pAllocationCreateInfo, [NativeTypeName("uint32_t * _Nonnull")] uint* pMemoryTypeIndex);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaFindMemoryTypeIndexForImageInfo([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("const VkImageCreateInfo * _Nonnull")] Ahjo.Vulkan.Native.VkImageCreateInfo* pImageCreateInfo, [NativeTypeName("const VmaAllocationCreateInfo * _Nonnull")] VmaAllocationCreateInfo* pAllocationCreateInfo, [NativeTypeName("uint32_t * _Nonnull")] uint* pMemoryTypeIndex);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaCreatePool([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("const VmaPoolCreateInfo * _Nonnull")] VmaPoolCreateInfo* pCreateInfo, [NativeTypeName("VmaPool  _Nullable * _Nonnull")] VmaPool_T** pPool);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaDestroyPool([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaPool _Nullable")] VmaPool_T* pool);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaGetPoolStatistics([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaPool _Nonnull")] VmaPool_T* pool, [NativeTypeName("VmaStatistics * _Nonnull")] VmaStatistics* pPoolStats);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaCalculatePoolStatistics([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaPool _Nonnull")] VmaPool_T* pool, [NativeTypeName("VmaDetailedStatistics * _Nonnull")] VmaDetailedStatistics* pPoolStats);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaCheckPoolCorruption([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaPool _Nonnull")] VmaPool_T* pool);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaGetPoolName([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaPool _Nonnull")] VmaPool_T* pool, [NativeTypeName("const char * _Nullable * _Nonnull")] sbyte** ppName);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaSetPoolName([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaPool _Nonnull")] VmaPool_T* pool, [NativeTypeName("const char * _Nullable")] sbyte* pName);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaAllocateMemory([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("const VkMemoryRequirements * _Nonnull")] Ahjo.Vulkan.Native.VkMemoryRequirements* pVkMemoryRequirements, [NativeTypeName("const VmaAllocationCreateInfo * _Nonnull")] VmaAllocationCreateInfo* pCreateInfo, [NativeTypeName("VmaAllocation  _Nullable * _Nonnull")] VmaAllocation_T** pAllocation, [NativeTypeName("VmaAllocationInfo * _Nullable")] VmaAllocationInfo* pAllocationInfo);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaAllocateMemoryPages([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("const VkMemoryRequirements * _Nonnull")] Ahjo.Vulkan.Native.VkMemoryRequirements* pVkMemoryRequirements, [NativeTypeName("const VmaAllocationCreateInfo * _Nonnull")] VmaAllocationCreateInfo* pCreateInfo, [NativeTypeName("size_t")] nuint allocationCount, [NativeTypeName("VmaAllocation  _Nullable * _Nonnull")] VmaAllocation_T** pAllocations, [NativeTypeName("VmaAllocationInfo * _Nullable")] VmaAllocationInfo* pAllocationInfo);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaAllocateMemoryForBuffer([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VkBuffer _Nonnull")] Ahjo.Vulkan.Native.VkBuffer_T* buffer, [NativeTypeName("const VmaAllocationCreateInfo * _Nonnull")] VmaAllocationCreateInfo* pCreateInfo, [NativeTypeName("VmaAllocation  _Nullable * _Nonnull")] VmaAllocation_T** pAllocation, [NativeTypeName("VmaAllocationInfo * _Nullable")] VmaAllocationInfo* pAllocationInfo);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaAllocateMemoryForImage([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VkImage _Nonnull")] Ahjo.Vulkan.Native.VkImage_T* image, [NativeTypeName("const VmaAllocationCreateInfo * _Nonnull")] VmaAllocationCreateInfo* pCreateInfo, [NativeTypeName("VmaAllocation  _Nullable * _Nonnull")] VmaAllocation_T** pAllocation, [NativeTypeName("VmaAllocationInfo * _Nullable")] VmaAllocationInfo* pAllocationInfo);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaFreeMemory([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocation _Nullable")] VmaAllocation_T* allocation);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaFreeMemoryPages([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("size_t")] nuint allocationCount, [NativeTypeName("VmaAllocation  _Nullable const * _Nonnull")] VmaAllocation_T** pAllocations);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaGetAllocationInfo([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocation _Nonnull")] VmaAllocation_T* allocation, [NativeTypeName("VmaAllocationInfo * _Nonnull")] VmaAllocationInfo* pAllocationInfo);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaGetAllocationInfo2([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocation _Nonnull")] VmaAllocation_T* allocation, [NativeTypeName("VmaAllocationInfo2 * _Nonnull")] VmaAllocationInfo2* pAllocationInfo);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaSetAllocationUserData([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocation _Nonnull")] VmaAllocation_T* allocation, [NativeTypeName("void * _Nullable")] void* pUserData);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaSetAllocationName([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocation _Nonnull")] VmaAllocation_T* allocation, [NativeTypeName("const char * _Nullable")] sbyte* pName);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaGetAllocationMemoryProperties([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocation _Nonnull")] VmaAllocation_T* allocation, [NativeTypeName("VkMemoryPropertyFlags * _Nonnull")] uint* pFlags);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaMapMemory([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocation _Nonnull")] VmaAllocation_T* allocation, [NativeTypeName("void * _Nullable * _Nonnull")] void** ppData);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaUnmapMemory([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocation _Nonnull")] VmaAllocation_T* allocation);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaFlushAllocation([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocation _Nonnull")] VmaAllocation_T* allocation, [NativeTypeName("VkDeviceSize")] ulong offset, [NativeTypeName("VkDeviceSize")] ulong size);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaInvalidateAllocation([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocation _Nonnull")] VmaAllocation_T* allocation, [NativeTypeName("VkDeviceSize")] ulong offset, [NativeTypeName("VkDeviceSize")] ulong size);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaFlushAllocations([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("uint32_t")] uint allocationCount, [NativeTypeName("VmaAllocation  _Nonnull const * _Nullable")] VmaAllocation_T** allocations, [NativeTypeName("const VkDeviceSize * _Nullable")] ulong* offsets, [NativeTypeName("const VkDeviceSize * _Nullable")] ulong* sizes);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaInvalidateAllocations([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("uint32_t")] uint allocationCount, [NativeTypeName("VmaAllocation  _Nonnull const * _Nullable")] VmaAllocation_T** allocations, [NativeTypeName("const VkDeviceSize * _Nullable")] ulong* offsets, [NativeTypeName("const VkDeviceSize * _Nullable")] ulong* sizes);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaCopyMemoryToAllocation([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("const void * _Nonnull")] void* pSrcHostPointer, [NativeTypeName("VmaAllocation _Nonnull")] VmaAllocation_T* dstAllocation, [NativeTypeName("VkDeviceSize")] ulong dstAllocationLocalOffset, [NativeTypeName("VkDeviceSize")] ulong size);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaCopyAllocationToMemory([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocation _Nonnull")] VmaAllocation_T* srcAllocation, [NativeTypeName("VkDeviceSize")] ulong srcAllocationLocalOffset, [NativeTypeName("void * _Nonnull")] void* pDstHostPointer, [NativeTypeName("VkDeviceSize")] ulong size);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaCheckCorruption([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("uint32_t")] uint memoryTypeBits);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaBeginDefragmentation([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("const VmaDefragmentationInfo * _Nonnull")] VmaDefragmentationInfo* pInfo, [NativeTypeName("VmaDefragmentationContext  _Nullable * _Nonnull")] VmaDefragmentationContext_T** pContext);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaEndDefragmentation([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaDefragmentationContext _Nonnull")] VmaDefragmentationContext_T* context, [NativeTypeName("VmaDefragmentationStats * _Nullable")] VmaDefragmentationStats* pStats);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaBeginDefragmentationPass([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaDefragmentationContext _Nonnull")] VmaDefragmentationContext_T* context, [NativeTypeName("VmaDefragmentationPassMoveInfo * _Nonnull")] VmaDefragmentationPassMoveInfo* pPassInfo);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaEndDefragmentationPass([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaDefragmentationContext _Nonnull")] VmaDefragmentationContext_T* context, [NativeTypeName("VmaDefragmentationPassMoveInfo * _Nonnull")] VmaDefragmentationPassMoveInfo* pPassInfo);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaBindBufferMemory([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocation _Nonnull")] VmaAllocation_T* allocation, [NativeTypeName("VkBuffer _Nonnull")] Ahjo.Vulkan.Native.VkBuffer_T* buffer);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaBindBufferMemory2([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocation _Nonnull")] VmaAllocation_T* allocation, [NativeTypeName("VkDeviceSize")] ulong allocationLocalOffset, [NativeTypeName("VkBuffer _Nonnull")] Ahjo.Vulkan.Native.VkBuffer_T* buffer, [NativeTypeName("const void * _Nullable")] void* pNext);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaBindImageMemory([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocation _Nonnull")] VmaAllocation_T* allocation, [NativeTypeName("VkImage _Nonnull")] Ahjo.Vulkan.Native.VkImage_T* image);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaBindImageMemory2([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocation _Nonnull")] VmaAllocation_T* allocation, [NativeTypeName("VkDeviceSize")] ulong allocationLocalOffset, [NativeTypeName("VkImage _Nonnull")] Ahjo.Vulkan.Native.VkImage_T* image, [NativeTypeName("const void * _Nullable")] void* pNext);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaCreateBuffer([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("const VkBufferCreateInfo * _Nonnull")] Ahjo.Vulkan.Native.VkBufferCreateInfo* pBufferCreateInfo, [NativeTypeName("const VmaAllocationCreateInfo * _Nonnull")] VmaAllocationCreateInfo* pAllocationCreateInfo, [NativeTypeName("VkBuffer  _Nullable * _Nonnull")] Ahjo.Vulkan.Native.VkBuffer_T** pBuffer, [NativeTypeName("VmaAllocation  _Nullable * _Nonnull")] VmaAllocation_T** pAllocation, [NativeTypeName("VmaAllocationInfo * _Nullable")] VmaAllocationInfo* pAllocationInfo);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaCreateBufferWithAlignment([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("const VkBufferCreateInfo * _Nonnull")] Ahjo.Vulkan.Native.VkBufferCreateInfo* pBufferCreateInfo, [NativeTypeName("const VmaAllocationCreateInfo * _Nonnull")] VmaAllocationCreateInfo* pAllocationCreateInfo, [NativeTypeName("VkDeviceSize")] ulong minAlignment, [NativeTypeName("VkBuffer  _Nullable * _Nonnull")] Ahjo.Vulkan.Native.VkBuffer_T** pBuffer, [NativeTypeName("VmaAllocation  _Nullable * _Nonnull")] VmaAllocation_T** pAllocation, [NativeTypeName("VmaAllocationInfo * _Nullable")] VmaAllocationInfo* pAllocationInfo);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaCreateAliasingBuffer([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocation _Nonnull")] VmaAllocation_T* allocation, [NativeTypeName("const VkBufferCreateInfo * _Nonnull")] Ahjo.Vulkan.Native.VkBufferCreateInfo* pBufferCreateInfo, [NativeTypeName("VkBuffer  _Nullable * _Nonnull")] Ahjo.Vulkan.Native.VkBuffer_T** pBuffer);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaCreateAliasingBuffer2([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocation _Nonnull")] VmaAllocation_T* allocation, [NativeTypeName("VkDeviceSize")] ulong allocationLocalOffset, [NativeTypeName("const VkBufferCreateInfo * _Nonnull")] Ahjo.Vulkan.Native.VkBufferCreateInfo* pBufferCreateInfo, [NativeTypeName("VkBuffer  _Nullable * _Nonnull")] Ahjo.Vulkan.Native.VkBuffer_T** pBuffer);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaDestroyBuffer([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VkBuffer _Nullable")] Ahjo.Vulkan.Native.VkBuffer_T* buffer, [NativeTypeName("VmaAllocation _Nullable")] VmaAllocation_T* allocation);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaCreateImage([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("const VkImageCreateInfo * _Nonnull")] Ahjo.Vulkan.Native.VkImageCreateInfo* pImageCreateInfo, [NativeTypeName("const VmaAllocationCreateInfo * _Nonnull")] VmaAllocationCreateInfo* pAllocationCreateInfo, [NativeTypeName("VkImage  _Nullable * _Nonnull")] Ahjo.Vulkan.Native.VkImage_T** pImage, [NativeTypeName("VmaAllocation  _Nullable * _Nonnull")] VmaAllocation_T** pAllocation, [NativeTypeName("VmaAllocationInfo * _Nullable")] VmaAllocationInfo* pAllocationInfo);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaCreateAliasingImage([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocation _Nonnull")] VmaAllocation_T* allocation, [NativeTypeName("const VkImageCreateInfo * _Nonnull")] Ahjo.Vulkan.Native.VkImageCreateInfo* pImageCreateInfo, [NativeTypeName("VkImage  _Nullable * _Nonnull")] Ahjo.Vulkan.Native.VkImage_T** pImage);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaCreateAliasingImage2([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VmaAllocation _Nonnull")] VmaAllocation_T* allocation, [NativeTypeName("VkDeviceSize")] ulong allocationLocalOffset, [NativeTypeName("const VkImageCreateInfo * _Nonnull")] Ahjo.Vulkan.Native.VkImageCreateInfo* pImageCreateInfo, [NativeTypeName("VkImage  _Nullable * _Nonnull")] Ahjo.Vulkan.Native.VkImage_T** pImage);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaDestroyImage([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("VkImage _Nullable")] Ahjo.Vulkan.Native.VkImage_T* image, [NativeTypeName("VmaAllocation _Nullable")] VmaAllocation_T* allocation);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaCreateVirtualBlock([NativeTypeName("const VmaVirtualBlockCreateInfo * _Nonnull")] VmaVirtualBlockCreateInfo* pCreateInfo, [NativeTypeName("VmaVirtualBlock  _Nullable * _Nonnull")] VmaVirtualBlock_T** pVirtualBlock);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaDestroyVirtualBlock([NativeTypeName("VmaVirtualBlock _Nullable")] VmaVirtualBlock_T* virtualBlock);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkBool32")]
    public static extern uint vmaIsVirtualBlockEmpty([NativeTypeName("VmaVirtualBlock _Nonnull")] VmaVirtualBlock_T* virtualBlock);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaGetVirtualAllocationInfo([NativeTypeName("VmaVirtualBlock _Nonnull")] VmaVirtualBlock_T* virtualBlock, [NativeTypeName("VmaVirtualAllocation _Nonnull")] VmaVirtualAllocation_T* allocation, [NativeTypeName("VmaVirtualAllocationInfo * _Nonnull")] VmaVirtualAllocationInfo* pVirtualAllocInfo);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("VkResult")]
    public static extern Ahjo.Vulkan.Native.VkResult vmaVirtualAllocate([NativeTypeName("VmaVirtualBlock _Nonnull")] VmaVirtualBlock_T* virtualBlock, [NativeTypeName("const VmaVirtualAllocationCreateInfo * _Nonnull")] VmaVirtualAllocationCreateInfo* pCreateInfo, [NativeTypeName("VmaVirtualAllocation  _Nullable * _Nonnull")] VmaVirtualAllocation_T** pAllocation, [NativeTypeName("VkDeviceSize * _Nullable")] ulong* pOffset);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaVirtualFree([NativeTypeName("VmaVirtualBlock _Nonnull")] VmaVirtualBlock_T* virtualBlock, [NativeTypeName("VmaVirtualAllocation _Nullable")] VmaVirtualAllocation_T* allocation);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaClearVirtualBlock([NativeTypeName("VmaVirtualBlock _Nonnull")] VmaVirtualBlock_T* virtualBlock);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaSetVirtualAllocationUserData([NativeTypeName("VmaVirtualBlock _Nonnull")] VmaVirtualBlock_T* virtualBlock, [NativeTypeName("VmaVirtualAllocation _Nonnull")] VmaVirtualAllocation_T* allocation, [NativeTypeName("void * _Nullable")] void* pUserData);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaGetVirtualBlockStatistics([NativeTypeName("VmaVirtualBlock _Nonnull")] VmaVirtualBlock_T* virtualBlock, [NativeTypeName("VmaStatistics * _Nonnull")] VmaStatistics* pStats);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaCalculateVirtualBlockStatistics([NativeTypeName("VmaVirtualBlock _Nonnull")] VmaVirtualBlock_T* virtualBlock, [NativeTypeName("VmaDetailedStatistics * _Nonnull")] VmaDetailedStatistics* pStats);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaBuildVirtualBlockStatsString([NativeTypeName("VmaVirtualBlock _Nonnull")] VmaVirtualBlock_T* virtualBlock, [NativeTypeName("char * _Nullable * _Nonnull")] sbyte** ppStatsString, [NativeTypeName("VkBool32")] uint detailedMap);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaFreeVirtualBlockStatsString([NativeTypeName("VmaVirtualBlock _Nonnull")] VmaVirtualBlock_T* virtualBlock, [NativeTypeName("char * _Nullable")] sbyte* pStatsString);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaBuildStatsString([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("char * _Nullable * _Nonnull")] sbyte** ppStatsString, [NativeTypeName("VkBool32")] uint detailedMap);

    [DllImport("vma", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void vmaFreeStatsString([NativeTypeName("VmaAllocator _Nonnull")] VmaAllocator_T* allocator, [NativeTypeName("char * _Nullable")] sbyte* pStatsString);
}
