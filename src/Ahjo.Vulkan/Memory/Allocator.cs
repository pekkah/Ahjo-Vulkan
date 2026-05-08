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
    /// and feeds VMA the physical/logical/instance handles plus the
    /// device's reported <c>apiVersion</c> (capped at the wrapper's
    /// header ceiling of 1.4).
    /// </summary>
    public static Allocator Create(Device device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // The loader handle is only needed for the GetExport calls below — VMA
        // copies the function pointers into its internal state, and the OS
        // keeps the DLL loaded via the wrapper's other reference (the static
        // [DllImport] path in Ahjo.Vulkan.Native). Releasing the handle in
        // finally keeps repeated Create/Dispose cycles (tests, benchmarks)
        // from accumulating handles on the loader.
        nint loader = LoadVulkanLoader();
        try
        {
            var functions = new VmaVulkanFunctions
            {
                vkGetInstanceProcAddr =
                    (delegate* unmanaged[Stdcall]<VkInstance_T*, sbyte*, delegate* unmanaged[Stdcall]<void>>)
                    NativeLibrary.GetExport(loader, "vkGetInstanceProcAddr"),
                vkGetDeviceProcAddr =
                    (delegate* unmanaged[Stdcall]<VkDevice_T*, sbyte*, delegate* unmanaged[Stdcall]<void>>)
                    NativeLibrary.GetExport(loader, "vkGetDeviceProcAddr"),
            };

            // VMA uses vulkanApiVersion to gate which version-promoted entry
            // points it imports through vkGetDeviceProcAddr. Passing a higher
            // version than the device actually supports makes VMA import
            // entry points the loader returns null for, then SIGSEGV the
            // first time it dispatches through one. Symptom seen on Mesa
            // lavapipe (advertises 1.3) when the wrapper hardcoded 1.4 here;
            // see VMA issue 397 for the same shape on Android 14.
            //
            // Clamp at 1.2 (not the device's reported version) — the 1.3
            // promotions VMA optionally imports (vkGetDeviceImageMemoryRequirements,
            // vkGetDeviceBufferMemoryRequirements) are convenience APIs the
            // wrapper doesn't depend on, and lavapipe-1.3.275's exposure of
            // them appears unstable. 1.2 still gives VMA core BDA support
            // (vkGetBufferDeviceAddress was promoted in 1.2) which the
            // wrapper does need. On a sub-1.2 device we degrade to whatever
            // the device reports.
            VkPhysicalDeviceProperties props;
            Vk.vkGetPhysicalDeviceProperties(device.PhysicalDevice.Handle, &props);
            uint apiVersion = Math.Min(VulkanVersion.V1_2.Packed, props.apiVersion);

            // Explicit baseline for every native field — see CreateBuffer
            // for the rationale. The VMA struct exposes several optional
            // pointers and a heap-size override that we don't drive; pinning
            // each to its zero today keeps a future field reorder honest.
            VmaAllocatorCreateInfo ci = default;
            // bufferDeviceAddress is on by default in the wrapper's 1.4 device
            // feature chain (PhysicalDevice.CreateDevice). VMA needs this flag
            // to allocate buffers carrying VK_BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
            // without it, vmaCreateBuffer returns VK_ERROR_INITIALIZATION_FAILED.
            ci.flags                          = (uint)VmaAllocatorCreateFlagBits.VMA_ALLOCATOR_CREATE_BUFFER_DEVICE_ADDRESS_BIT;
            ci.physicalDevice                 = device.PhysicalDevice.Handle;
            ci.device                         = device.Handle;
            ci.preferredLargeHeapBlockSize    = 0;
            ci.pAllocationCallbacks           = null;
            ci.pDeviceMemoryCallbacks         = null;
            ci.pHeapSizeLimit                 = null;
            ci.pVulkanFunctions               = &functions;
            ci.instance                       = device.PhysicalDevice.Instance.Handle;
            ci.vulkanApiVersion               = apiVersion;
            ci.pTypeExternalMemoryHandleTypes = null;

            VmaAllocator_T* raw = null;
            VmaApi.vmaCreateAllocator(&ci, &raw).ThrowIfFailed();
            return new Allocator(raw);
        }
        finally
        {
            NativeLibrary.Free(loader);
        }
    }

    /// <summary>
    /// Allocates a <c>VkBuffer</c> + backing memory in one VMA call.
    /// </summary>
    public Buffer CreateBuffer(in BufferDescription buffer, in AllocationDescription allocation)
    {
        // Every native field assigned explicitly so a future binding regen
        // that reorders / adds fields can't silently inherit a zero from
        // managed default-init. Costs nothing — the JIT folds the default
        // assignments — and the call site reads as the wrapper's actual
        // contract with VMA rather than "whatever zero means today".
        VkBufferCreateInfo bci = default;
        bci.sType                 = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO;
        bci.pNext                 = null;
        bci.flags                 = 0;
        bci.size                  = buffer.Size;
        bci.usage                 = (uint)buffer.Usage;
        bci.sharingMode           = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE;
        bci.queueFamilyIndexCount = 0;
        bci.pQueueFamilyIndices   = null;

        VmaAllocationCreateInfo aci = default;
        aci.flags          = (uint)allocation.Flags;
        aci.usage          = (VmaMemoryUsage)allocation.Usage;
        aci.requiredFlags  = 0;
        aci.preferredFlags = 0;
        aci.memoryTypeBits = 0;
        aci.pool           = null;
        aci.pUserData      = null;
        aci.priority       = 0f;

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
        // Same explicit-baseline reasoning as CreateBuffer — every native
        // field assigned, so a future struct shape change surfaces at
        // build time rather than as a silent zero on a renamed field.
        VkImageCreateInfo ici = default;
        ici.sType                 = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO;
        ici.pNext                 = null;
        ici.flags                 = (uint)image.Flags;
        ici.imageType             = image.ImageType;
        ici.format                = image.Format;
        ici.extent                = new VkExtent3D { width = image.Width, height = image.Height, depth = image.Depth };
        ici.mipLevels             = image.MipLevels;
        ici.arrayLayers           = image.ArrayLayers;
        ici.samples               = image.Samples;
        ici.tiling                = image.Tiling;
        ici.usage                 = (uint)image.Usage;
        ici.sharingMode           = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE;
        ici.queueFamilyIndexCount = 0;
        ici.pQueueFamilyIndices   = null;
        ici.initialLayout         = image.InitialLayout;

        VmaAllocationCreateInfo aci = default;
        aci.flags          = (uint)allocation.Flags;
        aci.usage          = (VmaMemoryUsage)allocation.Usage;
        aci.requiredFlags  = 0;
        aci.preferredFlags = 0;
        aci.memoryTypeBits = 0;
        aci.pool           = null;
        aci.pUserData      = null;
        aci.priority       = 0f;

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
