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
/// it writes a one-line warning through <see cref="AhjoDiagnostics.Sink"/>
/// (stderr by default) before
/// invoking <c>vmaDestroyAllocator</c>. VMA itself asserts on outstanding
/// allocations in debug builds — the wrapper warning makes the same
/// problem visible in release builds, where the C++ assert is compiled
/// out.</para>
/// </remarks>
public readonly unsafe struct Allocator : IDisposable
{
    internal readonly VmaAllocator_T* Handle;
    // OS handle on the Vulkan loader DLL. Owned for the allocator's
    // lifetime so VMA's captured function pointers can't dangle if some
    // other [DllImport] consumer is the only thing keeping the DLL
    // resident and gets unloaded. Zero on default(Allocator) and on
    // wrappers that didn't go through Create (none currently exist).
    internal readonly nint Loader;
    // Number of memory heaps the physical device reports, captured in Create.
    // GetHeapBudgets needs it because vmaGetHeapBudgets takes no count: it
    // writes memoryHeapCount entries (VMA_LEN_IF_NOT_NULL(memoryHeapCount) in
    // the header) and the caller is expected to already know that number.
    // Zero on default(Allocator).
    internal readonly uint HeapCount;

    /// <summary>
    /// Vulkan's <c>VK_MAX_MEMORY_HEAPS</c>. Not emitted as a constant by the
    /// generator (it is a plain <c>#define</c> the rsp does not bind), so it is
    /// spelled here; it is also the inline-array length of
    /// <c>VkPhysicalDeviceMemoryProperties.memoryHeaps</c>, which is what pins
    /// the value.
    /// </summary>
    private const int MaxMemoryHeaps = 16;

    internal Allocator(VmaAllocator_T* handle, nint loader, uint heapCount)
    {
        Handle    = handle;
        Loader    = loader;
        HeapCount = heapCount;
    }

    public bool IsNull => Handle == null;

    /// <summary>
    /// Builds a VMA allocator over <paramref name="device"/>. Loads the
    /// platform Vulkan loader, threads <c>vkGetInstanceProcAddr</c> +
    /// <c>vkGetDeviceProcAddr</c> into <see cref="VmaVulkanFunctions"/>,
    /// and feeds VMA the physical/logical/instance handles plus the
    /// device's reported <c>apiVersion</c> (capped at the wrapper's
    /// header ceiling of 1.4).
    /// </summary>
    public static Allocator Create(Device device) => Create(device, default);

    /// <summary>
    /// As <see cref="Create(Device)"/>, plus the allocator-level options in
    /// <paramref name="description"/>. <c>default(AllocatorDescription)</c>
    /// produces a byte-identical allocator to the single-argument overload.
    /// </summary>
    public static Allocator Create(Device device, in AllocatorDescription description)
    {
        ArgumentNullException.ThrowIfNull(device);

        // The last gate before the flag reaches VMA, and it asks about
        // ENABLEMENT, not support — those are different questions and only one
        // of them is the right one. Every desktop driver *supports*
        // VK_EXT_memory_budget, so a support test would wave through exactly the
        // caller this exists to stop: one who created the device without the
        // extension and then asks for a budget here. VMA would set the bit and
        // then chain VkPhysicalDeviceMemoryBudgetPropertiesEXT into a device
        // that never enabled it (VUID-VkPhysicalDeviceMemoryProperties2-pNext-pNext).
        //
        // NOT gated on AhjoValidation: this one throws in every configuration.
        // PhysicalDevice.CreateDevice's half is validation-gated because it is a
        // helpful early warning about a description; this is the point past
        // which a Release build would otherwise hand the driver an invalid
        // chain and read numbers that look plausible and are wrong.
        if (description.EnableMemoryBudget && !device.MemoryBudgetExtensionEnabled)
        {
            // No paramName: this is also reached from the Device.Allocator
            // property getter, where the description came from
            // DeviceDescription.Allocator and there is no parameter of that
            // name for the caller to go look at. The message names the real fix
            // on both paths, which is what a caller actually needs.
            throw new ArgumentException(
                "AllocatorDescription.EnableMemoryBudget is set but VK_EXT_memory_budget was not enabled on this device. " +
                "Add VulkanExtensions.ExtMemoryBudget to DeviceDescription.Extensions and recreate the device — support " +
                "is not enough, VMA chains VkPhysicalDeviceMemoryBudgetPropertiesEXT and the driver rejects it on a " +
                "device that did not enable the extension.");
        }

        // Load the loader DLL and keep the handle for the allocator's
        // lifetime — VMA captures vkGetInstanceProcAddr /
        // vkGetDeviceProcAddr below and dispatches through them on every
        // vmaCreateBuffer / vmaDestroyBuffer call. The static [DllImport]
        // path in Ahjo.Vulkan.Native usually pins the DLL too, but tying
        // the OS ref-count to the allocator (released in Dispose) keeps
        // VMA correct even in degenerate scenarios where this is the
        // only resident reference.
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
            //
            // EXT_MEMORY_BUDGET is opt-in through AllocatorDescription
            // (issue #218): it makes VMA read the driver's real heap usage via
            // VK_EXT_memory_budget instead of estimating from its own
            // bookkeeping, which is the only way GetHeapBudgets can see memory
            // VMA never allocated (DLSS's driver-side history/scratch, #214).
            // The device extension must be enabled too; CreateDevice checks the
            // pairing under AhjoValidation.
            ci.flags                          = (uint)VmaAllocatorCreateFlagBits.VMA_ALLOCATOR_CREATE_BUFFER_DEVICE_ADDRESS_BIT
                                              | (description.EnableMemoryBudget
                                                    ? (uint)VmaAllocatorCreateFlagBits.VMA_ALLOCATOR_CREATE_EXT_MEMORY_BUDGET_BIT
                                                    : 0u);
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

            // Captured once so GetHeapBudgets can trim vmaGetHeapBudgets'
            // fixed VK_MAX_MEMORY_HEAPS output down to the heaps that exist.
            VkPhysicalDeviceMemoryProperties memProps;
            Vk.vkGetPhysicalDeviceMemoryProperties(device.PhysicalDevice.Handle, &memProps);

            VmaAllocator_T* raw = null;
            VmaApi.vmaCreateAllocator(&ci, &raw).ThrowIfFailed();
            var allocator = new Allocator(raw, loader, memProps.memoryHeapCount);
            loader = 0; // ownership transferred — Dispose frees it now.
            return allocator;
        }
        finally
        {
            // Only the failure path runs Free here; the success path
            // moves the handle onto the returned Allocator (above).
            if (loader != 0) NativeLibrary.Free(loader);
        }
    }

    /// <summary>
    /// Allocates a <c>VkBuffer</c> + backing memory in one VMA call.
    /// </summary>
    public Buffer CreateBuffer(in BufferDescription buffer, in AllocationDescription allocation)
    {
        // The create-info mapping lives on the description (BufferDescription.ToNative) so
        // the aliasing creator and the requirements query cannot drift from this one.
        VkBufferCreateInfo bci = buffer.ToNative();
        VmaAllocationCreateInfo aci = ToNative(allocation);

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
        // The create-info mapping lives on the description (ImageDescription.ToNative) so
        // the aliasing creator and the requirements query cannot drift from this one.
        VkImageCreateInfo ici = image.ToNative();
        VmaAllocationCreateInfo aci = ToNative(allocation);

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

    /// <summary>
    /// Allocates device memory with NO resource bound to it, for a caller that will
    /// sub-allocate the block itself with <see cref="CreateAliasingImage"/> /
    /// <see cref="CreateAliasingBuffer"/>.
    /// </summary>
    /// <param name="requirements">
    /// What the block must satisfy. Fold every resource's
    /// <see cref="Device.GetImageMemoryRequirements"/> /
    /// <see cref="Device.GetBufferMemoryRequirements"/> answer together with
    /// <see cref="MemoryRequirements.CombineWith"/>, then raise
    /// <see cref="MemoryRequirements.Size"/> to whatever the packing actually needs — the
    /// combined size is only the largest single resource.
    /// </param>
    /// <param name="allocation">
    /// Pass <see cref="AllocationFlags.CanAlias"/> whenever more than one resource will be
    /// created into the block. VMA needs it to stop applying optimizations that assume one
    /// allocation backs one resource, and it is not inferred here — nothing at this point
    /// knows how many resources are coming.
    /// </param>
    public MemoryBlock AllocateMemory(in MemoryRequirements requirements, in AllocationDescription allocation)
    {
        // VMA derives an Auto* usage's memory type from the RESOURCE's usage flags, and a
        // block allocated before any resource exists has none. VMA answers that with an
        // internal assert and an opaque VK_ERROR_FEATURE_NOT_PRESENT, which reads like a
        // missing device feature; say what is actually wrong instead.
        if (allocation.Usage is MemoryUsage.Auto or MemoryUsage.AutoPreferDevice or MemoryUsage.AutoPreferHost)
        {
            throw new ArgumentException(
                "MemoryUsage.Auto* cannot be used to allocate a block with no resource bound to it — VMA infers " +
                "the memory type from a resource's usage flags, and there is no resource here. Use " +
                "MemoryUsage.Unknown with AllocationDescription.RequiredFlags (MemoryProperties.DeviceLocal for a " +
                "GPU-resident block), or call CreateBuffer/CreateImage if one resource per allocation is what you want.",
                nameof(allocation));
        }

        VkMemoryRequirements mr = default;
        mr.size           = requirements.Size;
        mr.alignment      = requirements.Alignment;
        mr.memoryTypeBits = requirements.MemoryTypeBits;

        VmaAllocationCreateInfo aci = ToNative(allocation);

        VmaAllocation_T* rawAlloc = null;
        VmaAllocationInfo info = default;
        VmaApi.vmaAllocateMemory(Handle, &mr, &aci, &rawAlloc, &info).ThrowIfFailed();
        return new MemoryBlock(rawAlloc, this, info.size, info.memoryType);
    }

    /// <summary>
    /// Creates a <c>VkImage</c> bound at <paramref name="offset"/> bytes into
    /// <paramref name="block"/>, instead of giving it an allocation of its own.
    /// </summary>
    /// <remarks>
    /// <para>The returned <see cref="Image"/> owns its <c>VkImage</c> and NOT the memory:
    /// its allocation handle is null, so disposing it destroys the image and frees nothing.
    /// That is what lets several images share one block. Dispose every resource before the
    /// block.</para>
    /// <para><paramref name="offset"/> must be a multiple of the alignment
    /// <see cref="Device.GetImageMemoryRequirements"/> reported for this description, and
    /// the image must fit inside the block. Aliased contents are undefined — see
    /// <see cref="MemoryBlock"/>.</para>
    /// </remarks>
    public Image CreateAliasingImage(in MemoryBlock block, ulong offset, in ImageDescription image)
    {
        VkImageCreateInfo ici = image.ToNative();

        VkImage_T* rawImage = null;
        VmaApi.vmaCreateAliasingImage2(Handle, block.Handle, offset, &ici, &rawImage).ThrowIfFailed();

        // The allocation handle stays null: the block owns the memory. Image.Dispose
        // forwards a null allocation to vmaDestroyImage, which VMA documents as "destroy
        // the image, free nothing" — exactly the aliasing contract, and it needs no second
        // disposal path on the handle.
        return new Image(
            rawImage, null, this,
            image.Format, image.Width, image.Height, image.Depth,
            image.MipLevels, image.ArrayLayers, image.Usage,
            persistentMapped: null);
    }

    /// <summary>
    /// Creates a <c>VkBuffer</c> bound at <paramref name="offset"/> bytes into
    /// <paramref name="block"/> — the buffer counterpart of
    /// <see cref="CreateAliasingImage"/>, with the same ownership and undefined-contents
    /// rules.
    /// </summary>
    /// <remarks>
    /// The returned buffer reports host-visible and host-coherent false and carries no
    /// mapped pointer, so mapping throws and flush/invalidate are no-ops. Host access to a
    /// block is the block owner's business, not an aliasing view's: two views of the same
    /// bytes disagreeing about coherency is a bug with no good answer.
    /// </remarks>
    public Buffer CreateAliasingBuffer(in MemoryBlock block, ulong offset, in BufferDescription buffer)
    {
        VkBufferCreateInfo bci = buffer.ToNative();

        VkBuffer_T* rawBuffer = null;
        VmaApi.vmaCreateAliasingBuffer2(Handle, block.Handle, offset, &bci, &rawBuffer).ThrowIfFailed();

        return new Buffer(
            rawBuffer, null, this, buffer.Size, buffer.Usage,
            isHostVisible: false, isHostCoherent: false, persistentMapped: null);
    }

    /// <summary>
    /// Fills <paramref name="destination"/> with one
    /// <see cref="MemoryHeapBudget"/> per memory heap the physical device
    /// reports and returns that count.
    /// </summary>
    /// <param name="destination">
    /// Caller-provided span, at least <see cref="HeapCount"/> long. Sixteen
    /// entries (<c>VK_MAX_MEMORY_HEAPS</c>) is always enough.
    /// </param>
    /// <returns>The number of entries written — the device's heap count.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> is shorter than the heap count; the
    /// message names both numbers.
    /// </exception>
    /// <remarks>
    /// <para>Allocates nothing: one <c>stackalloc</c> of VMA's fixed-size
    /// output array and one <c>vmaGetHeapBudgets</c> call. Still a
    /// diagnostic/setup-path API rather than a per-frame one — VMA takes an
    /// internal lock and walks its block lists.</para>
    /// <para><see cref="MemoryHeapBudget.Usage"/> and
    /// <see cref="MemoryHeapBudget.Budget"/> are only the driver's real numbers
    /// when the allocator was created with
    /// <see cref="AllocatorDescription.EnableMemoryBudget"/>; see that member.</para>
    /// </remarks>
    public int GetHeapBudgets(Span<MemoryHeapBudget> destination)
    {
        // default(Allocator) is a legal null handle and is publicly reachable —
        // Image.FromRaw(h).Owner is one. Its HeapCount is 0, so the length
        // guard below would pass for any span (an empty one included) and let
        // vmaGetHeapBudgets(null, ...) through. Answer "no heaps" instead.
        if (Handle == null) return 0;

        uint heapCount = HeapCount;
        if (destination.Length < heapCount)
        {
            throw new ArgumentException(
                $"Destination span holds {destination.Length} entries but this device reports {heapCount} memory heap(s). " +
                $"Size the span to Allocator.GetHeapBudgets' return value, or to {MaxMemoryHeaps} (VK_MAX_MEMORY_HEAPS) unconditionally.",
                nameof(destination));
        }

        // vmaGetHeapBudgets writes at least memoryHeapCount entries and takes
        // no capacity argument, so the scratch buffer is sized to the maximum a
        // device can ever report: VK_MAX_MEMORY_HEAPS. A fixed 16 is therefore
        // always sufficient and never has to be re-checked against the device.
        Span<VmaBudget> budgets = stackalloc VmaBudget[MaxMemoryHeaps];
        fixed (VmaBudget* pBudgets = budgets)
            VmaApi.vmaGetHeapBudgets(Handle, pBudgets);

        for (uint i = 0; i < heapCount; i++)
        {
            ref readonly VmaBudget b = ref budgets[(int)i];
            destination[(int)i] = new MemoryHeapBudget
            {
                HeapIndex       = i,
                BlockCount      = b.statistics.blockCount,
                AllocationCount = b.statistics.allocationCount,
                BlockBytes      = b.statistics.blockBytes,
                AllocationBytes = b.statistics.allocationBytes,
                Usage           = b.usage,
                Budget          = b.budget,
            };
        }

        return (int)heapCount;
    }

    /// <summary>
    /// The <c>VmaAllocationCreateInfo</c> an <see cref="AllocationDescription"/> denotes.
    /// Every field assigned explicitly — the struct exposes several optional pointers and a
    /// priority the wrapper does not drive, and pinning each to its zero here keeps a future
    /// field reorder honest.
    /// </summary>
    private static VmaAllocationCreateInfo ToNative(in AllocationDescription allocation)
    {
        VmaAllocationCreateInfo aci = default;
        aci.flags          = (uint)allocation.Flags;
        aci.usage          = (VmaMemoryUsage)allocation.Usage;
        aci.requiredFlags  = (uint)allocation.RequiredFlags;
        aci.preferredFlags = (uint)allocation.PreferredFlags;
        aci.memoryTypeBits = 0;
        aci.pool           = null;
        aci.pUserData      = null;
        aci.priority       = 0f;
        return aci;
    }

    public void Dispose()
    {
        if (Handle == null) return;

        VmaTotalStatistics stats = default;
        VmaApi.vmaCalculateStatistics(Handle, &stats);
        if (stats.total.statistics.allocationCount > 0)
        {
            AhjoDiagnostics.Write(DiagnosticSeverity.Warning, "Allocator",
                $"[VMA] Allocator disposed with {stats.total.statistics.allocationCount} live allocation(s) " +
                $"({stats.total.statistics.allocationBytes} bytes). Call DestroyBuffer/DestroyImage on every " +
                "resource before disposing the allocator.");
        }

        VmaApi.vmaDestroyAllocator(Handle);
        if (Loader != 0) NativeLibrary.Free(Loader);
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
