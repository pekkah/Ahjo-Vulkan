using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Vma.Native;
using VmaApi = Ahjo.Vulkan.Vma.Native.Vma;

namespace Ahjo.Vulkan;

/// <summary>
/// Top-level VMA context. One per <see cref="Device"/>. Owns memory pools,
/// statistics, and the dynamic Vulkan function table VMA uses to call down
/// into the loader. All buffer/image creation in the wrapper goes through
/// this type — no raw <c>vkCreateBuffer</c> path is exposed.
/// </summary>
/// <remarks>
/// <para><c>readonly struct</c> handle — copy-by-value across the wrapper
/// boundary. <c>default(Allocator)</c> is a legal null handle. Dispose is
/// not idempotent (cannot mutate the handle on a <c>readonly struct</c>);
/// double-dispose is undefined behavior. Callers that go through
/// <see cref="Device.Allocator"/> should let <see cref="Device"/> own the
/// disposal — the <see cref="Allocator.Create"/> factory is the entry
/// point for tests that want to manage the allocator themselves.</para>
/// <para><b>Leak diagnostics.</b> <see cref="Dispose"/> calls
/// <c>vmaCalculateStatistics</c> first; if any allocation is still live,
/// it writes a one-line warning to <see cref="Console.Error"/> before
/// invoking <c>vmaDestroyAllocator</c>. VMA itself asserts on outstanding
/// allocations in debug builds — the wrapper warning makes the same
/// problem visible in release builds, where the C++ assert is compiled
/// out.</para>
/// </remarks>
public readonly unsafe struct Allocator : IDisposable
{
    internal readonly VmaAllocator_T* Handle;

    internal Allocator(VmaAllocator_T* handle) { Handle = handle; }

    public bool IsNull => Handle == null;

    /// <summary>
    /// Builds a VMA allocator over <paramref name="device"/>. Loads the
    /// platform Vulkan loader, threads <c>vkGetInstanceProcAddr</c> +
    /// <c>vkGetDeviceProcAddr</c> into <see cref="VmaVulkanFunctions"/>,
    /// and feeds VMA the physical/logical/instance handles plus
    /// <c>vmaApiVersion = 1.4</c>.
    /// </summary>
    public static Allocator Create(Device device)
    {
        ArgumentNullException.ThrowIfNull(device);

        nint loader = LoadVulkanLoader();
        var functions = new VmaVulkanFunctions
        {
            vkGetInstanceProcAddr =
                (delegate* unmanaged[Stdcall]<VkInstance_T*, sbyte*, delegate* unmanaged[Stdcall]<void>>)
                NativeLibrary.GetExport(loader, "vkGetInstanceProcAddr"),
            vkGetDeviceProcAddr =
                (delegate* unmanaged[Stdcall]<VkDevice_T*, sbyte*, delegate* unmanaged[Stdcall]<void>>)
                NativeLibrary.GetExport(loader, "vkGetDeviceProcAddr"),
        };

        var ci = new VmaAllocatorCreateInfo
        {
            // bufferDeviceAddress is on by default in the wrapper's 1.4 device
            // feature chain (PhysicalDevice.CreateDevice). VMA needs this flag
            // to allocate buffers carrying VK_BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
            // without it, vmaCreateBuffer returns VK_ERROR_INITIALIZATION_FAILED.
            flags            = (uint)VmaAllocatorCreateFlagBits.VMA_ALLOCATOR_CREATE_BUFFER_DEVICE_ADDRESS_BIT,
            physicalDevice   = device.PhysicalDevice.Handle,
            device           = device.Handle,
            instance         = device.PhysicalDevice.Instance.Handle,
            pVulkanFunctions = &functions,
            vulkanApiVersion = VulkanVersion.V1_4.Packed,
        };

        VmaAllocator_T* raw = null;
        VmaApi.vmaCreateAllocator(&ci, &raw).ThrowIfFailed();
        return new Allocator(raw);
    }

    /// <summary>
    /// Allocates a <c>VkBuffer</c> + backing memory in one VMA call.
    /// </summary>
    public Buffer CreateBuffer(in BufferDescription buffer, in AllocationDescription allocation)
    {
        var bci = new VkBufferCreateInfo
        {
            sType       = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO,
            size        = buffer.Size,
            usage       = (uint)buffer.Usage,
            sharingMode = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE,
        };
        var aci = new VmaAllocationCreateInfo
        {
            usage = (VmaMemoryUsage)allocation.Usage,
            flags = (uint)allocation.Flags,
        };

        VkBuffer_T*       rawBuffer = null;
        VmaAllocation_T*  rawAlloc  = null;
        VmaAllocationInfo info      = default;
        VmaApi.vmaCreateBuffer(Handle, &bci, &aci, &rawBuffer, &rawAlloc, &info).ThrowIfFailed();

        uint memProps = 0;
        VmaApi.vmaGetAllocationMemoryProperties(Handle, rawAlloc, &memProps);
        bool hostVisible  = (memProps & (uint)VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT)  != 0;
        // HOST_COHERENT decides whether host writes/reads need an explicit
        // vmaFlushAllocation/vmaInvalidateAllocation around the mapped span.
        // On most desktop discrete GPUs HOST_VISIBLE memory is also coherent
        // and the calls are no-ops; mobile/UMA targets and certain BAR-only
        // setups expose host-visible memory without coherency, where missing
        // flushes silently corrupt GPU reads of fresh CPU writes (and vice
        // versa). Buffer carries the bit so callers can branch and the
        // Flush/Invalidate helpers can skip the syscall when unnecessary.
        bool hostCoherent = (memProps & (uint)VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_COHERENT_BIT) != 0;

        // VMA returns pMappedData != null when the allocation was created
        // with VMA_ALLOCATION_CREATE_MAPPED_BIT (our AllocationFlags.Mapped),
        // even for AUTO_PREFER_DEVICE allocations that landed on a non-host
        // memory type (in which case it stays null). The buffer caches it
        // so AsSpan/Map can skip vmaMapMemory in the persistent-mapped case.
        return new Buffer(rawBuffer, rawAlloc, this, buffer.Size, buffer.Usage, hostVisible, hostCoherent, info.pMappedData);
    }

    /// <summary>
    /// Allocates a <c>VkImage</c> + backing memory in one VMA call.
    /// </summary>
    public Image CreateImage(in ImageDescription image, in AllocationDescription allocation)
    {
        var ici = new VkImageCreateInfo
        {
            sType         = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO,
            imageType     = image.ImageType,
            format        = image.Format,
            extent        = new VkExtent3D { width = image.Width, height = image.Height, depth = image.Depth },
            mipLevels     = image.MipLevels,
            arrayLayers   = image.ArrayLayers,
            samples       = image.Samples,
            tiling        = image.Tiling,
            usage         = (uint)image.Usage,
            sharingMode   = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE,
            initialLayout = image.InitialLayout,
        };
        var aci = new VmaAllocationCreateInfo
        {
            usage = (VmaMemoryUsage)allocation.Usage,
            flags = (uint)allocation.Flags,
        };

        VkImage_T*        rawImage = null;
        VmaAllocation_T*  rawAlloc = null;
        VmaAllocationInfo info     = default;
        VmaApi.vmaCreateImage(Handle, &ici, &aci, &rawImage, &rawAlloc, &info).ThrowIfFailed();

        // Mirrors Buffer's pMappedData propagation: linear-tiled host-visible
        // images allocated with AllocationFlags.Mapped expose a persistent
        // pointer through info.pMappedData, and the wrapper has to pipe it
        // onto the handle for AsSpan/Map to skip vmaMapMemory. Dropping the
        // pAllocationInfo parameter (the prior null) would silently strand
        // the pointer — Image had no field for it either, but adding the
        // field without populating it would have been a worse trap.
        return new Image(
            rawImage, rawAlloc, this,
            image.Format, image.Width, image.Height, image.Depth,
            image.MipLevels, image.ArrayLayers, image.Usage,
            info.pMappedData);
    }

    public void Dispose()
    {
        if (Handle == null) return;

        VmaTotalStatistics stats = default;
        VmaApi.vmaCalculateStatistics(Handle, &stats);
        if (stats.total.statistics.allocationCount > 0)
        {
            Console.Error.WriteLine(
                $"[VMA] Allocator disposed with {stats.total.statistics.allocationCount} live allocation(s) " +
                $"({stats.total.statistics.allocationBytes} bytes). Call DestroyBuffer/DestroyImage on every " +
                "resource before disposing the allocator.");
        }

        VmaApi.vmaDestroyAllocator(Handle);
    }

    private static nint LoadVulkanLoader()
    {
        // Mirrors the per-OS candidate list in Ahjo.Vulkan.Native's
        // VulkanLoaderResolver. VMA needs raw function pointers and
        // [DllImport] static methods don't expose theirs (CS8757), so
        // we re-load the same DLL and pull the exports directly. The
        // loader is reference-counted by the OS — both this handle and
        // the resolver's handle point at the same image.
        string[] candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ["vulkan-1.dll", "vulkan-1"]
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? ["libvulkan.dylib", "libvulkan.1.dylib", "libMoltenVK.dylib"]
                : ["libvulkan.so.1", "libvulkan.so"];

        foreach (string c in candidates)
        {
            if (NativeLibrary.TryLoad(c, out nint handle))
                return handle;
        }
        throw new VulkanException(VkResult.VK_ERROR_INITIALIZATION_FAILED,
            "Vulkan loader not present on this host.");
    }
}
