using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Owner of a <c>VkDevice</c>. <c>sealed class</c> for the same reasons
/// as <see cref="Instance"/>: created once per app, never copied,
/// deterministic <see cref="Dispose"/> calls <c>vkDeviceWaitIdle</c> +
/// <c>vkDestroyDevice</c>, finalizer backstops a missed dispose (a leaked
/// device leaves the GPU busy for the rest of the process).
/// </summary>
/// <remarks>
/// <para><b>Thread safety.</b> Disposal is not thread-safe — do not call
/// <see cref="Dispose"/> concurrently from multiple threads. Vulkan calls
/// made through the wrapped handle follow the spec's external-sync rules
/// (the underlying <c>VkDevice</c> is externally synchronizable).</para>
/// </remarks>
public sealed unsafe class Device : IDisposable
{
    internal readonly VkDevice_T*         Handle;
    internal readonly DeviceFunctionTable Functions;
    public   readonly PhysicalDevice      PhysicalDevice;
    private  readonly Queue[]             _queues;
    private  Allocator                    _allocator;
    private  readonly AllocatorDescription _allocatorDescription;
    // Whether VK_EXT_memory_budget was ENABLED at vkCreateDevice time, captured
    // from the extension span while it is still in scope. Vulkan exposes no
    // query for "what did I enable", and support is not the same question — a
    // driver can support an extension the device did not enable, which is
    // exactly the case that makes VMA's budget path chain
    // VkPhysicalDeviceMemoryBudgetPropertiesEXT into a device that will reject
    // it. One bool, set once, so Allocator.Create can ask the real question.
    internal readonly bool                MemoryBudgetExtensionEnabled;
    private  bool                         _allocatorCreated;
    private  bool                         _disposed;
    // Allocator lazy-init runs at most once per Device, so the cost of
    // always taking the lock is negligible and removes the
    // double-create race on concurrent first access.
    private  readonly object              _allocatorLock = new();
    // Set once when any wrapper call observes VK_ERROR_DEVICE_LOST; never
    // cleared. volatile (not Interlocked) because the flag is monotonic —
    // there is no lost-update hazard, and both race directions are benign:
    // a reader that misses a just-set flag performs the real Vulkan call
    // and observes DEVICE_LOST from the driver itself.
    private  volatile bool                _lost;

    // Process-wide registry of live devices so the context-free
    // ResultExtensions choke point can mark loss without threading device
    // identity through every ThrowIfFailed call site. WeakReference so the
    // registry never roots an undisposed Device past user reachability —
    // the leak-backstop finalizer must stay able to run. Every touch is
    // cold-path (ctor, Dispose, an already-throwing loss path).
    private  static readonly List<WeakReference<Device>> s_live     = [];
    private  static readonly object                      s_liveLock = new();

    internal Device(
        VkDevice_T*            handle,
        PhysicalDevice         physicalDevice,
        Queue[]                queues,
        ReadOnlySpan<Utf8Name> enabledExtensions,
        AllocatorDescription   allocatorDescription)
    {
        Handle         = handle;
        _allocatorDescription = allocatorDescription;
        MemoryBudgetExtensionEnabled =
            PhysicalDevice.ContainsExtension(enabledExtensions, "VK_EXT_memory_budget"u8);
        // The span is consumed here and never stored — a ReadOnlySpan field
        // in a class would not compile, which is the enforcement.
        Functions      = new DeviceFunctionTable(handle, enabledExtensions);
        PhysicalDevice = physicalDevice;
        _queues        = queues;

        lock (s_liveLock)
        {
            PruneDeadLocked();
            s_live.Add(new WeakReference<Device>(this));
        }
    }

    /// <summary>
    /// <see langword="true"/> once any wrapper call has observed
    /// <c>VK_ERROR_DEVICE_LOST</c> for this device (or, in a multi-device
    /// process, for any live device — see remarks). Set-once; never
    /// cleared. After loss the wrapper applies one policy everywhere:
    /// <see cref="Fence.Wait"/> / <see cref="TimelineSemaphore.WaitFor"/>
    /// return <see cref="WaitState.DeviceLost"/> immediately,
    /// <see cref="Fence.IsSignaled"/> throws deterministically without
    /// calling the driver, <see cref="FencePool.Release(Fence)"/> skips
    /// its status query, and <see cref="Swapchain.Recreate"/> fails fast.
    /// Recovery is to dispose every dependent resource, dispose the
    /// device, and rebuild from a fresh <see cref="PhysicalDevice"/>.
    /// </summary>
    /// <remarks>
    /// A <c>DEVICE_LOST</c> observed at the context-free
    /// <c>ResultExtensions</c> choke point marks <i>every</i> live device,
    /// because the throw site has no device identity. In the wrapper's
    /// target shape (one device per process) that is exact; in a
    /// multi-device process a healthy sibling is marked conservatively —
    /// its teardown still drains properly because
    /// <see cref="Dispose"/> never skips its real <c>vkDeviceWaitIdle</c>.
    /// </remarks>
    public bool IsLost => _lost;

    internal void MarkLost() => _lost = true;

    /// <summary>
    /// Called by <c>ResultExtensions.Throw</c> when a wrapper call fails
    /// with <c>VK_ERROR_DEVICE_LOST</c> outside any device context. Marks
    /// every live device lost and prunes collected entries.
    /// </summary>
    internal static void NotifyDeviceLossObserved()
    {
        lock (s_liveLock)
        {
            for (int i = s_live.Count - 1; i >= 0; i--)
            {
                if (s_live[i].TryGetTarget(out Device? device))
                    device.MarkLost();
                else
                    s_live.RemoveAt(i);
            }
        }
    }

    private void Unregister()
    {
        lock (s_liveLock)
        {
            for (int i = s_live.Count - 1; i >= 0; i--)
            {
                if (!s_live[i].TryGetTarget(out Device? device) || ReferenceEquals(device, this))
                    s_live.RemoveAt(i);
            }
        }
    }

    private static void PruneDeadLocked()
    {
        for (int i = s_live.Count - 1; i >= 0; i--)
        {
            if (!s_live[i].TryGetTarget(out _))
                s_live.RemoveAt(i);
        }
    }

    /// <summary>
    /// The device's VMA allocator. Created on first access and disposed
    /// during <see cref="Dispose"/>; do not call <see cref="Allocator.Dispose"/>
    /// on the returned struct — the device owns the lifetime.
    /// </summary>
    public Allocator Allocator
    {
        get
        {
            lock (_allocatorLock)
            {
                if (!_allocatorCreated)
                {
                    _allocator        = Ahjo.Vulkan.Allocator.Create(this, in _allocatorDescription);
                    _allocatorCreated = true;
                }
                return _allocator;
            }
        }
    }

    public ulong RawHandle => (ulong)(nint)Handle;
    public bool  IsNull    => Handle == null;
    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_DEVICE;

    /// <summary>
    /// Returns the cached <see cref="Queue"/> for the requested
    /// <c>(familyIndex, queueIndex)</c>. The pair must match a
    /// <see cref="QueueRequest"/> that was passed via
    /// <see cref="DeviceDescription.Queues"/>; otherwise an
    /// <see cref="ArgumentException"/> guides the caller to declare it.
    /// </summary>
    public Queue GetQueue(uint familyIndex, uint queueIndex)
    {
        var queues = _queues;
        for (int i = 0; i < queues.Length; i++)
        {
            if (queues[i].FamilyIndex == familyIndex && queues[i].QueueIndex == queueIndex)
                return queues[i];
        }

        throw new ArgumentException(
            $"No queue requested at (family: {familyIndex}, index: {queueIndex}). " +
            "Add a corresponding QueueRequest to DeviceDescription.Queues.");
    }

    /// <summary>Wraps <c>vkDeviceWaitIdle</c>.</summary>
    public void WaitIdle()
    {
        Vk.vkDeviceWaitIdle(Handle).ThrowIfFailed();
    }

    /// <summary>
    /// Builds a <see cref="DescriptorSetLayout"/> from a slot list.
    /// Bindings may carry per-slot
    /// <see cref="DescriptorBindingFlags"/>; if any do, the wrapper
    /// chains a <c>VkDescriptorSetLayoutBindingFlagsCreateInfo</c>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A binding carrying <see cref="DescriptorBindingFlags.VariableDescriptorCount"/>
    /// is not the one with the highest binding number in the set
    /// (VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-pBindingFlags-03004).
    /// </exception>
    /// <remarks>
    /// <para><b>An empty <see cref="DescriptorSetLayoutDescription.Bindings"/>
    /// span is legal</b> and produces a layout with zero bindings. Vulkan
    /// contemplates it:
    /// <c>VUID-VkDescriptorSetLayoutCreateInfo-pBindings-parameter</c> excuses
    /// <c>pBindings</c> "if <c>bindingCount</c> is not 0", and there is no
    /// <c>bindingCount-arraylength</c> VUID. Measured: <c>VK_SUCCESS</c>, and the
    /// validation layer silent (issue #183 §E7, NVIDIA RTX 4070 Ti, layer
    /// 1.4.341).</para>
    /// <para>It is the layout Vulkan wants for an unpopulated set index in a
    /// sparse-set program — a <c>PipelineLayout</c> needs a handle at that index
    /// — and for a set whose every binding is a zero-length resource array
    /// (issues #191, #183).</para>
    /// <para><b>Not to be confused with
    /// <see cref="DescriptorBinding.Count"/> == 0</b>, which is issue #119's
    /// sentinel for a zeroed span element and is still normalized to <c>1</c>
    /// below. An empty span is <i>zero bindings</i>; a one-element span holding
    /// <c>default(DescriptorBinding)</c> is <i>one binding of one
    /// descriptor</i>. The two are adjacent and mean opposite things.</para>
    /// <para>A layout with zero bindings cannot carry a
    /// <c>DescriptorTemplate&lt;T&gt;</c>: Vulkan requires
    /// <c>descriptorUpdateEntryCount &gt; 0</c>
    /// (VUID-VkDescriptorUpdateTemplateCreateInfo-descriptorUpdateEntryCount-arraylength).</para>
    /// </remarks>
    public DescriptorSetLayout CreateDescriptorSetLayout(in DescriptorSetLayoutDescription desc)
    {
        ValidateVariableDescriptorCountOrdering(desc.Bindings);

        Span<VkDescriptorSetLayoutBinding> nativeBindings =
            stackalloc VkDescriptorSetLayoutBinding[desc.Bindings.Length];
        Span<uint> flags = stackalloc uint[desc.Bindings.Length];
        bool anyFlagsSet = false;
        for (int i = 0; i < desc.Bindings.Length; i++)
        {
            ref readonly var b = ref desc.Bindings[i];
            nativeBindings[i] = new VkDescriptorSetLayoutBinding
            {
                binding         = b.Slot,
                descriptorType  = b.Type,
                // DescriptorBinding.Count defaults to 1 via field initializer
                // (issue #119); this == 0 ? 1 guard is belt-and-braces for a
                // default(DescriptorBinding) element in the bindings span, which
                // bypasses the initializer.
                descriptorCount = b.Count == 0 ? 1u : b.Count,
                stageFlags      = (uint)b.Stages,
            };
            flags[i] = (uint)b.BindingFlags;
            if (flags[i] != 0) anyFlagsSet = true;
        }

        VkDescriptorSetLayout_T* raw = null;
        fixed (VkDescriptorSetLayoutBinding* pBindings = nativeBindings)
        fixed (uint*                          pFlags    = flags)
        {
            uint createFlags = 0;
            if (desc.UpdateAfterBindPool)
                createFlags |= (uint)VkDescriptorSetLayoutCreateFlagBits.VK_DESCRIPTOR_SET_LAYOUT_CREATE_UPDATE_AFTER_BIND_POOL_BIT;
            if (desc.PushDescriptor)
                createFlags |= (uint)VkDescriptorSetLayoutCreateFlagBits.VK_DESCRIPTOR_SET_LAYOUT_CREATE_PUSH_DESCRIPTOR_BIT;
            var ci = new VkDescriptorSetLayoutCreateInfo
            {
                sType        = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO,
                flags        = createFlags,
                bindingCount = (uint)nativeBindings.Length,
                pBindings    = pBindings,
            };

            VkDescriptorSetLayoutBindingFlagsCreateInfo flagsInfo = default;
            if (anyFlagsSet)
            {
                flagsInfo = new VkDescriptorSetLayoutBindingFlagsCreateInfo
                {
                    sType         = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_BINDING_FLAGS_CREATE_INFO,
                    bindingCount  = (uint)flags.Length,
                    pBindingFlags = pFlags,
                };
                ci.pNext = &flagsInfo;
            }

            Vk.vkCreateDescriptorSetLayout(Handle, &ci, null, &raw).ThrowIfFailed();
        }
        return new DescriptorSetLayout(raw, Handle);
    }

    /// <summary>
    /// Enforces VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-pBindingFlags-03004:
    /// a binding carrying
    /// <see cref="DescriptorBindingFlags.VariableDescriptorCount"/> must be the
    /// element with the highest binding number in the set. The validation layer
    /// reports this at <c>vkCreateDescriptorSetLayout</c>; checking it here makes
    /// the same mistake fail in the wrapper's own vocabulary, at the call site
    /// that made it, for callers running without the layer.
    /// </summary>
    internal static void ValidateVariableDescriptorCountOrdering(
        ReadOnlySpan<DescriptorBinding> bindings)
    {
        uint highestSlot = 0;
        for (int i = 0; i < bindings.Length; i++)
        {
            if (bindings[i].Slot > highestSlot)
                highestSlot = bindings[i].Slot;
        }

        for (int i = 0; i < bindings.Length; i++)
        {
            if ((bindings[i].BindingFlags & DescriptorBindingFlags.VariableDescriptorCount) != 0
                && bindings[i].Slot != highestSlot)
            {
                throw new ArgumentException(
                    $"Binding {bindings[i].Slot} carries "
                    + "DescriptorBindingFlags.VariableDescriptorCount but is not the "
                    + $"highest binding number in the set (highest is {highestSlot}). "
                    + "Vulkan requires the variable-descriptor-count binding to be last "
                    + "(VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-pBindingFlags-03004).");
            }
        }
    }

    /// <summary>
    /// Returns a <see cref="ComputePipelineBuilder"/> for fluent compute-
    /// pipeline construction.
    /// </summary>
    public ComputePipelineBuilder BuildComputePipeline() => new(this);

    /// <summary>
    /// Returns a <see cref="GraphicsPipelineBuilder"/> for fluent graphics-
    /// pipeline construction. Vulkan 1.4 dynamic-rendering only — no
    /// <c>VkRenderPass</c>.
    /// </summary>
    public GraphicsPipelineBuilder BuildGraphicsPipeline() => new(this);

    /// <summary>
    /// Creates a <see cref="ShaderModule"/> from a span of SPIR-V words.
    /// Primary overload: SPIR-V is 32-bit-aligned by definition, so taking
    /// <see cref="ReadOnlySpan{UInt32}"/> enforces alignment at the call site.
    /// </summary>
    public ShaderModule CreateShaderModule(ReadOnlySpan<uint> spirv)
    {
        if (spirv.IsEmpty)
            throw new ArgumentException("SPIR-V blob cannot be empty.", nameof(spirv));

        VkShaderModule_T* raw = null;
        fixed (uint* pCode = spirv)
        {
            var ci = new VkShaderModuleCreateInfo
            {
                sType    = VkStructureType.VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO,
                codeSize = (nuint)spirv.Length * sizeof(uint),
                pCode    = pCode,
            };
            Vk.vkCreateShaderModule(Handle, &ci, null, &raw).ThrowIfFailed();
        }
        return new ShaderModule(raw, Handle);
    }

    /// <summary>
    /// Convenience overload over <see cref="ReadOnlySpan{Byte}"/>.
    /// SPIR-V's word size is 4 bytes; the byte span's length must be a
    /// multiple of 4 and the underlying memory must be 4-byte-aligned.
    /// </summary>
    public ShaderModule CreateShaderModule(ReadOnlySpan<byte> spirvBytes)
    {
        if (spirvBytes.Length == 0)
            throw new ArgumentException("SPIR-V blob cannot be empty.", nameof(spirvBytes));
        if ((spirvBytes.Length & 3) != 0)
            throw new ArgumentException(
                $"SPIR-V byte length must be a multiple of 4 (got {spirvBytes.Length}).", nameof(spirvBytes));
        return CreateShaderModule(System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(spirvBytes));
    }

    /// <summary>
    /// Creates an empty <see cref="PipelineCache"/>. The cache will fill
    /// as pipelines are built against it; persist via
    /// <see cref="PipelineCache.Save"/> on shutdown.
    /// </summary>
    public PipelineCache CreatePipelineCache()
    {
        var ci = new VkPipelineCacheCreateInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_CACHE_CREATE_INFO,
        };
        VkPipelineCache_T* raw = null;
        Vk.vkCreatePipelineCache(Handle, &ci, null, &raw).ThrowIfFailed();
        return new PipelineCache(raw, Handle);
    }

    /// <summary>
    /// Loads a <see cref="PipelineCache"/> from <paramref name="path"/> if
    /// the file exists and its header matches this device (vendor ID,
    /// device ID, cache UUID); otherwise creates an empty cache.
    /// Mismatches are reported through <see cref="AhjoDiagnostics.Sink"/>
    /// (stderr by default) so the "user copied a cache from another
    /// machine" / "driver UUID rotated" cases surface visibly.
    /// </summary>
    public PipelineCache LoadOrCreatePipelineCache(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        byte[]? rented = null;
        int     dataLen = 0;
        try
        {
            if (File.Exists(path))
            {
                long len = new FileInfo(path).Length;
                if (len > 0 && len <= int.MaxValue)
                {
                    rented  = ArrayPool<byte>.Shared.Rent((int)len);
                    dataLen = (int)len;
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    int read = 0;
                    while (read < dataLen)
                    {
                        int n = fs.Read(rented, read, dataLen - read);
                        if (n == 0) { dataLen = read; break; }
                        read += n;
                    }

                    if (!HeaderMatchesDevice(rented.AsSpan(0, dataLen)))
                    {
                        AhjoDiagnostics.Write(DiagnosticSeverity.Warning, "PipelineCache",
                            $"PipelineCache: header in '{path}' does not match this device (vendor/device/UUID); discarding and starting empty.");
                        ArrayPool<byte>.Shared.Return(rented);
                        rented  = null;
                        dataLen = 0;
                    }
                }
            }

            var ci = new VkPipelineCacheCreateInfo
            {
                sType           = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_CACHE_CREATE_INFO,
                initialDataSize = (nuint)dataLen,
            };
            fixed (byte* pData = rented)
            {
                ci.pInitialData = dataLen > 0 ? pData : null;
                VkPipelineCache_T* raw = null;
                Vk.vkCreatePipelineCache(Handle, &ci, null, &raw).ThrowIfFailed();
                return new PipelineCache(raw, Handle);
            }
        }
        finally
        {
            if (rented is not null) ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private bool HeaderMatchesDevice(ReadOnlySpan<byte> data)
    {
        // VkPipelineCacheHeaderVersionOne layout:
        //   uint32 headerSize          (==32)
        //   uint32 headerVersion       (==VK_PIPELINE_CACHE_HEADER_VERSION_ONE)
        //   uint32 vendorID
        //   uint32 deviceID
        //   uint8  pipelineCacheUUID[16]
        if (data.Length < sizeof(uint) * 4 + 16) return false;

        ref readonly byte b0 = ref data[0];
        uint headerSize    = Unsafe.ReadUnaligned<uint>(in b0);
        uint headerVersion = Unsafe.ReadUnaligned<uint>(in Unsafe.Add(ref Unsafe.AsRef(in b0), sizeof(uint)));
        uint vendorID      = Unsafe.ReadUnaligned<uint>(in Unsafe.Add(ref Unsafe.AsRef(in b0), sizeof(uint) * 2));
        uint deviceID      = Unsafe.ReadUnaligned<uint>(in Unsafe.Add(ref Unsafe.AsRef(in b0), sizeof(uint) * 3));
        ReadOnlySpan<byte> uuid = data.Slice(sizeof(uint) * 4, 16);

        if (headerSize != (uint)(sizeof(uint) * 4 + 16)) return false;
        if (headerVersion != (uint)VkPipelineCacheHeaderVersion.VK_PIPELINE_CACHE_HEADER_VERSION_ONE) return false;

        VkPhysicalDeviceProperties props;
        Vk.vkGetPhysicalDeviceProperties(PhysicalDevice.Handle, &props);
        if (props.vendorID != vendorID) return false;
        if (props.deviceID != deviceID) return false;

        ref readonly byte uuid0 = ref props.pipelineCacheUUID.e0;
        ReadOnlySpan<byte> deviceUuid = MemoryMarshal.CreateReadOnlySpan(in uuid0, 16);
        return uuid.SequenceEqual(deviceUuid);
    }

    /// <summary>
    /// Creates a <see cref="Sampler"/> from <paramref name="desc"/>. Validates
    /// <see cref="SamplerDescription.AnisotropyEnable"/> against the physical
    /// device's <c>samplerAnisotropy</c> feature, and clamps
    /// <see cref="SamplerDescription.MaxAnisotropy"/> to
    /// <c>VkPhysicalDeviceLimits.maxSamplerAnisotropy</c>. Throws
    /// <see cref="ArgumentException"/> when anisotropy is requested on a
    /// device that does not advertise the feature, since the driver would
    /// reject the create with an opaque error.
    /// </summary>
    public Sampler CreateSampler(in SamplerDescription desc)
    {
        float maxAnisotropy = 1f;
        if (desc.AnisotropyEnable)
        {
            VkPhysicalDeviceFeatures features;
            Vk.vkGetPhysicalDeviceFeatures(PhysicalDevice.Handle, &features);
            if (features.samplerAnisotropy == 0)
                throw new ArgumentException(
                    "SamplerDescription.AnisotropyEnable is true but the physical device does not advertise samplerAnisotropy.",
                    nameof(desc));

            VkPhysicalDeviceProperties props;
            Vk.vkGetPhysicalDeviceProperties(PhysicalDevice.Handle, &props);
            float limit = props.limits.maxSamplerAnisotropy;
            float requested = desc.MaxAnisotropy <= 0f ? limit : desc.MaxAnisotropy;
            maxAnisotropy = requested > limit ? limit : requested;
        }

        var ci = new VkSamplerCreateInfo
        {
            sType                   = VkStructureType.VK_STRUCTURE_TYPE_SAMPLER_CREATE_INFO,
            magFilter               = desc.MagFilter,
            minFilter               = desc.MinFilter,
            mipmapMode              = desc.MipmapMode,
            addressModeU            = desc.AddressModeU,
            addressModeV            = desc.AddressModeV,
            addressModeW            = desc.AddressModeW,
            mipLodBias              = desc.MipLodBias,
            anisotropyEnable        = desc.AnisotropyEnable ? 1u : 0u,
            maxAnisotropy           = maxAnisotropy,
            compareEnable           = desc.CompareEnable ? 1u : 0u,
            compareOp               = desc.CompareOp,
            minLod                  = desc.MinLod,
            maxLod                  = desc.MaxLod,
            borderColor             = desc.BorderColor,
            unnormalizedCoordinates = desc.UnnormalizedCoordinates ? 1u : 0u,
        };

        VkSampler_T* raw = null;
        Vk.vkCreateSampler(Handle, &ci, null, &raw).ThrowIfFailed();
        return new Sampler(raw, Handle);
    }

    /// <summary>
    /// Creates an <see cref="Event"/> — a <c>VkEvent</c> for split barriers
    /// (<see cref="CommandRecorder.SetEvent"/> /
    /// <see cref="CommandRecorder.WaitEvent"/> /
    /// <see cref="CommandRecorder.ResetEvent"/>). The returned handle is
    /// caller-owned; dispose it once no submission still references it.
    /// </summary>
    /// <param name="flags">
    /// Defaults to <see cref="EventCreateFlags.DeviceOnly"/>, the
    /// split-barrier case. Pass <see cref="EventCreateFlags.None"/> only if
    /// the event must be reachable from the host event commands
    /// (<c>vkSetEvent</c> / <c>vkGetEventStatus</c> / <c>vkResetEvent</c>),
    /// which are illegal on a device-only event and which the wrapper does
    /// not expose today.
    /// </param>
    /// <remarks>
    /// <b>Portability subset.</b> On a device exposing
    /// <c>VK_KHR_portability_subset</c> that does not advertise the
    /// <c>events</c> feature, <c>vkCreateEvent</c> must not be used at all
    /// (<c>VUID-vkCreateEvent-events-04468</c>) — relevant to macOS via
    /// MoltenVK.
    /// </remarks>
    public Event CreateEvent(EventCreateFlags flags = EventCreateFlags.DeviceOnly)
    {
        var ci = new VkEventCreateInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_EVENT_CREATE_INFO,
            flags = (uint)flags,
        };
        VkEvent_T* raw = null;
        Vk.vkCreateEvent(Handle, &ci, null, &raw).ThrowIfFailed();
        return new Event(raw, Handle, flags);
    }

    /// <summary>
    /// Creates a timestamp-typed <see cref="QueryPool"/>
    /// (<see cref="QueryType.Timestamp"/>) with
    /// <paramref name="queryCount"/> queries — a one-line forward to
    /// <see cref="CreateQueryPool(QueryType, uint)"/>, kept because timestamps
    /// were the only type this wrapper minted before #202 and every existing
    /// call site means exactly what it always meant. The returned handle is
    /// caller-owned; dispose it once no submission still references it.
    /// </summary>
    /// <remarks>
    /// Queries start <b>uninitialized</b>: record and submit
    /// <see cref="CommandRecorder.ResetQueryPool"/> over a query's index
    /// before its first <see cref="CommandRecorder.WriteTimestamp"/> or any
    /// readback via <see cref="QueryPool.TryGetResults(uint, Span{ulong})"/>.
    /// Convert raw ticks to nanoseconds with
    /// <see cref="QueueFamilyInfo.TimestampValidBits"/> +
    /// <see cref="TimestampPeriod"/>.
    /// </remarks>
    public QueryPool CreateQueryPool(uint queryCount)
        => CreateQueryPool(QueryType.Timestamp, queryCount);

    /// <summary>
    /// Creates a <see cref="QueryPool"/> of <paramref name="type"/> with
    /// <paramref name="queryCount"/> queries. The one create path; the
    /// <see cref="CreateQueryPool(uint)"/> overload forwards here with
    /// <see cref="QueryType.Timestamp"/>. The returned handle is caller-owned;
    /// dispose it once no submission still references it.
    /// </summary>
    /// <remarks>
    /// <para>The pool remembers its <paramref name="type"/> as
    /// <see cref="QueryPool.Type"/>, which is what lets
    /// <see cref="CommandRecorder.WriteAccelerationStructuresProperties"/> take
    /// no <c>queryType</c> parameter and therefore be unable to mismatch the
    /// pool
    /// (<c>VUID-vkCmdWriteAccelerationStructuresPropertiesKHR-queryPool-02493</c>).</para>
    /// <para>Queries start <b>uninitialized</b> whatever the type: record and
    /// submit <see cref="CommandRecorder.ResetQueryPool"/> over a query's index
    /// before it is first written and before any readback.</para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="type"/> is <see cref="QueryType.Unknown"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="queryCount"/> is 0.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="type"/> is
    /// <see cref="QueryType.AccelerationStructureCompactedSize"/> but
    /// <c>VK_KHR_acceleration_structure</c> was not enabled on this device —
    /// the pool would have nothing able to write it, and creating it is itself
    /// a valid-usage violation without the <c>accelerationStructure</c>
    /// feature.
    /// </exception>
    public QueryPool CreateQueryPool(QueryType type, uint queryCount)
    {
        if (type == QueryType.Unknown)
            throw new ArgumentException(
                "QueryType.Unknown is the borrowed-handle sentinel, not a creatable type. "
                + "Pass QueryType.Timestamp or QueryType.AccelerationStructureCompactedSize.",
                nameof(type));

        if (queryCount == 0)
            throw new ArgumentOutOfRangeException(nameof(queryCount),
                "A query pool must contain at least one query (VUID-VkQueryPoolCreateInfo-queryCount-02763).");

        if (type == QueryType.AccelerationStructureCompactedSize
            && Functions.CmdWriteAccelerationStructuresProperties == null)
            throw new InvalidOperationException(
                "QueryType.AccelerationStructureCompactedSize pools are not available on this device. "
                + AccelerationStructureSupport.EnableInstructions);

        var ci = new VkQueryPoolCreateInfo
        {
            sType      = VkStructureType.VK_STRUCTURE_TYPE_QUERY_POOL_CREATE_INFO,
            queryType  = (VkQueryType)type,
            queryCount = queryCount,
        };
        VkQueryPool_T* raw = null;
        Vk.vkCreateQueryPool(Handle, &ci, null, &raw).ThrowIfFailed();
        return new QueryPool(raw, Handle, queryCount, type);
    }

    /// <summary>
    /// Creates a <see cref="AccelerationStructure"/> of
    /// <paramref name="type"/> over <paramref name="size"/> bytes of
    /// <paramref name="buffer"/> starting at <paramref name="offset"/>, via
    /// <c>vkCreateAccelerationStructureKHR</c>. The structure is empty until a
    /// <see cref="CommandRecorder.BuildAccelerationStructures"/> writes it.
    /// </summary>
    /// <param name="type">TLAS or BLAS. Note
    /// <see cref="AccelerationStructureType.TopLevel"/> is the enum's default
    /// value.</param>
    /// <param name="buffer">
    /// The backing buffer, which must have been created with
    /// <see cref="BufferUsage.AccelerationStructureStorage"/>
    /// (<c>VUID-VkAccelerationStructureCreateInfoKHR-buffer-03614</c>).
    /// </param>
    /// <param name="offset">
    /// Byte offset into <paramref name="buffer"/>. Must be a multiple of
    /// <b>256</b>
    /// (<c>VUID-VkAccelerationStructureCreateInfoKHR-offset-03734</c>).
    /// </param>
    /// <param name="size">
    /// Bytes to reserve — take it from
    /// <see cref="AccelerationStructureBuildSizes.AccelerationStructureSize"/>,
    /// or from the compacted-size query when creating a compaction
    /// destination. <c>offset + size</c> must not exceed the buffer's size
    /// (<c>VUID-VkAccelerationStructureCreateInfoKHR-offset-03616</c>).
    /// </param>
    /// <remarks>
    /// <para><b>The returned structure does not own
    /// <paramref name="buffer"/>.</b> Neither the buffer nor its allocator is
    /// stored. The caller must keep the buffer alive <b>strictly longer</b>
    /// than the acceleration structure, and must not let a second acceleration
    /// structure or any other resource alias the
    /// <c>[offset, offset + size)</c> range. Suballocating many BLASes into a
    /// few large buffers at 256-byte-aligned offsets is the intended pattern —
    /// one buffer per structure would waste memory against that alignment and
    /// multiply VMA allocations.</para>
    /// <para>The guards below are unconditional rather than
    /// <see cref="AhjoValidation"/>-gated: this is a setup-time call, the
    /// <see cref="CreateQueryPool(QueryType, uint)"/> precedent.</para>
    /// <para><b>Capture/replay is out of scope.</b> <c>createFlags</c> is
    /// always 0 and <c>deviceAddress</c> always 0 — the latter is only
    /// meaningful with
    /// <c>VK_ACCELERATION_STRUCTURE_CREATE_DEVICE_ADDRESS_CAPTURE_REPLAY_BIT_KHR</c>
    /// (<c>VUID-VkAccelerationStructureCreateInfoKHR-deviceAddress-03612</c>),
    /// which needs the <c>accelerationStructureCaptureReplay</c> feature and a
    /// capture-tool lifetime model of its own.</para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// <c>VK_KHR_acceleration_structure</c> was not enabled on this device.
    /// </exception>
    public AccelerationStructure CreateAccelerationStructure(
        AccelerationStructureType type, in Buffer buffer, ulong offset, ulong size)
    {
        if (buffer.IsNull)
            throw new ArgumentException(
                "CreateAccelerationStructure requires a non-null backing buffer.", nameof(buffer));

        if (size == 0)
            throw new ArgumentOutOfRangeException(nameof(size),
                "An acceleration structure must be at least one byte; size it from "
                + "AccelerationStructureBuildSizes.AccelerationStructureSize.");

        if (offset % 256 != 0)
            throw new ArgumentException(
                $"CreateAccelerationStructure: offset ({offset}) must be a multiple of 256 bytes "
                + "(VUID-VkAccelerationStructureCreateInfoKHR-offset-03734).", nameof(offset));

        // Buffer caches Size and Usage at create time; a borrowed
        // (Buffer.FromRaw) buffer reports 0 / None, which these two read as
        // *unknown* and skip — the QueryPool.AssertRangeInBounds convention.
        if (buffer.Size != 0 && offset + size > buffer.Size)
            throw new ArgumentOutOfRangeException(nameof(size),
                $"CreateAccelerationStructure: offset + size ({offset} + {size} = {offset + size}) "
                + $"exceeds the buffer's size ({buffer.Size}) "
                + "(VUID-VkAccelerationStructureCreateInfoKHR-offset-03616).");

        if (buffer.Usage != BufferUsage.None
            && (buffer.Usage & BufferUsage.AccelerationStructureStorage) == 0)
            throw new ArgumentException(
                "CreateAccelerationStructure: the backing buffer must have been created with "
                + "BufferUsage.AccelerationStructureStorage "
                + "(VUID-VkAccelerationStructureCreateInfoKHR-buffer-03614).", nameof(buffer));

        var fn = Functions.CreateAccelerationStructure;
        if (fn == null)
            throw new InvalidOperationException(
                "Device.CreateAccelerationStructure is not available on this device. "
                + AccelerationStructureSupport.EnableInstructions);

        var ci = new VkAccelerationStructureCreateInfoKHR
        {
            sType         = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_CREATE_INFO_KHR,
            createFlags   = 0,
            buffer        = buffer.Handle,
            offset        = offset,
            size          = size,
            type          = (VkAccelerationStructureTypeKHR)type,
            deviceAddress = 0,
        };
        VkAccelerationStructureKHR_T* raw = null;
        fn(Handle, &ci, null, &raw).ThrowIfFailed();
        return new AccelerationStructure(raw, Handle, Functions.DestroyAccelerationStructure, size);
    }

    /// <summary>
    /// How much memory a prospective build would need, via
    /// <c>vkGetAccelerationStructureBuildSizesKHR</c> with
    /// <c>VK_ACCELERATION_STRUCTURE_BUILD_TYPE_DEVICE_KHR</c>: the backing size
    /// to pass to <see cref="CreateAccelerationStructure"/> and the scratch
    /// size for each of the two build modes.
    /// </summary>
    /// <param name="type">The structure type the build will target.</param>
    /// <param name="flags">
    /// The <b>same</b> flags the build will use — they change the sizes the
    /// driver reports, so sizing with one set and building with another gives
    /// ranges that are too small.
    /// </param>
    /// <param name="geometries">The geometries the build will carry.</param>
    /// <param name="maxPrimitiveCounts">
    /// Upper bound on <see cref="AccelerationStructureBuildRange.PrimitiveCount"/>
    /// for each geometry, one entry per geometry and in the same order.
    /// </param>
    /// <remarks>
    /// <para>There is no destination parameter because the query ignores
    /// <c>srcAccelerationStructure</c>, <c>dstAccelerationStructure</c> and
    /// <c>scratchData</c> — and no mode parameter because both scratch sizes
    /// come back from one call.</para>
    /// <para>Setup-time and stack-only: the native geometry array is a
    /// <c>stackalloc</c> at 16 geometries or fewer and an
    /// <see cref="ArrayPool{T}"/> rental beyond, so nothing is allocated on the
    /// common path.</para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// <c>VK_KHR_acceleration_structure</c> was not enabled on this device.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="maxPrimitiveCounts"/> and <paramref name="geometries"/>
    /// have different lengths.
    /// </exception>
    public AccelerationStructureBuildSizes GetAccelerationStructureBuildSizes(
        AccelerationStructureType                   type,
        AccelerationStructureBuildFlags             flags,
        ReadOnlySpan<AccelerationStructureGeometry> geometries,
        ReadOnlySpan<uint>                          maxPrimitiveCounts)
    {
        var fn = Functions.GetAccelerationStructureBuildSizes;
        if (fn == null)
            throw new InvalidOperationException(
                "Device.GetAccelerationStructureBuildSizes is not available on this device. "
                + AccelerationStructureSupport.EnableInstructions);

        if (maxPrimitiveCounts.Length != geometries.Length)
            throw new ArgumentException(
                $"GetAccelerationStructureBuildSizes: maxPrimitiveCounts has {maxPrimitiveCounts.Length} "
                + $"entries but geometries has {geometries.Length}; there must be exactly one primitive "
                + "count per geometry.", nameof(maxPrimitiveCounts));

        const int GeometryStackThreshold = 16;
        int count = geometries.Length;

        VkAccelerationStructureGeometryKHR[]? rented = null;
        Span<VkAccelerationStructureGeometryKHR> natives =
            count <= GeometryStackThreshold
                ? stackalloc VkAccelerationStructureGeometryKHR[GeometryStackThreshold].Slice(0, count)
                : (rented = ArrayPool<VkAccelerationStructureGeometryKHR>.Shared.Rent(count))
                    .AsSpan(0, count);
        try
        {
            // sType MUST be set on the sizes struct before the call — a zeroed
            // sType is the classic silent failure here, and the driver reads it
            // to decide what it is being handed.
            var sizes = new VkAccelerationStructureBuildSizesInfoKHR
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_SIZES_INFO_KHR,
            };

            fixed (VkAccelerationStructureGeometryKHR* pNatives = natives)
            fixed (uint* pCounts = maxPrimitiveCounts)
            {
                AccelerationStructureBuildTranslator.BuildSizeQueryInfo(
                    type, flags, geometries, pNatives, out var info);

                fn(Handle,
                   VkAccelerationStructureBuildTypeKHR.VK_ACCELERATION_STRUCTURE_BUILD_TYPE_DEVICE_KHR,
                   &info, pCounts, &sizes);
            }

            return new AccelerationStructureBuildSizes
            {
                AccelerationStructureSize = sizes.accelerationStructureSize,
                BuildScratchSize          = sizes.buildScratchSize,
                UpdateScratchSize         = sizes.updateScratchSize,
            };
        }
        finally
        {
            if (rented is not null)
                ArrayPool<VkAccelerationStructureGeometryKHR>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Nanoseconds per timestamp tick
    /// (<c>VkPhysicalDeviceLimits::timestampPeriod</c>). Multiply a masked
    /// tick delta from <see cref="QueryPool.TryGetResults(uint, Span{ulong})"/>
    /// by this to get nanoseconds.
    /// </summary>
    /// <remarks>
    /// Read on demand from the physical device into a stack struct —
    /// zero-alloc, no caching, the same shape as the
    /// <c>maxPushConstantsSize</c> read in
    /// <see cref="CreatePipelineLayout"/>. Typically read once at setup.
    /// The value is often non-integral (e.g. 52.083 on some tile GPUs), so
    /// multiply the masked tick delta rather than accumulating ticks as
    /// nanoseconds.
    /// </remarks>
    public float TimestampPeriod
    {
        get
        {
            VkPhysicalDeviceProperties props;
            Vk.vkGetPhysicalDeviceProperties(PhysicalDevice.Handle, &props);
            return props.limits.timestampPeriod;
        }
    }

    /// <summary>
    /// Builds a <see cref="PipelineLayout"/> from descriptor-set layouts +
    /// push-constant ranges.
    /// </summary>
    public PipelineLayout CreatePipelineLayout(in PipelineLayoutDescription desc)
    {
        Span<nint> setLayouts = stackalloc nint[desc.SetLayouts.Length];
        for (int i = 0; i < desc.SetLayouts.Length; i++)
            setLayouts[i] = (nint)desc.SetLayouts[i].Handle;

        Span<VkPushConstantRange> ranges = stackalloc VkPushConstantRange[desc.PushConstantRanges.Length];
        uint maxPushBytes = 0;
        for (int i = 0; i < desc.PushConstantRanges.Length; i++)
        {
            ref readonly var r = ref desc.PushConstantRanges[i];
            ranges[i] = new VkPushConstantRange
            {
                stageFlags = (uint)r.Stages,
                offset     = r.Offset,
                size       = r.Size,
            };
            uint end = r.Offset + r.Size;
            if (end > maxPushBytes) maxPushBytes = end;
        }

        // Validate against the device's reported push-constant ceiling so
        // a 256+ B layout fails at create time with a clear message,
        // rather than getting accepted and then exploding at the call
        // site. Vulkan guarantees ≥128 B; desktop GPUs typically expose
        // 256 B and the engine's CullPushConstants is 224 B, which the
        // old per-call literal assert rejected outright.
        if (maxPushBytes > 0)
        {
            VkPhysicalDeviceProperties props;
            Vk.vkGetPhysicalDeviceProperties(PhysicalDevice.Handle, &props);
            uint deviceLimit = props.limits.maxPushConstantsSize;
            if (maxPushBytes > deviceLimit)
                throw new ArgumentException(
                    $"PipelineLayoutDescription declares push-constant range ending at {maxPushBytes} bytes, " +
                    $"which exceeds the device's maxPushConstantsSize ({deviceLimit}).",
                    nameof(desc));
        }

        VkPipelineLayout_T* raw = null;
        fixed (nint*               pSetLayouts = setLayouts)
        fixed (VkPushConstantRange* pRanges    = ranges)
        {
            var ci = new VkPipelineLayoutCreateInfo
            {
                sType                  = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO,
                setLayoutCount         = (uint)setLayouts.Length,
                pSetLayouts            = (VkDescriptorSetLayout_T**)pSetLayouts,
                pushConstantRangeCount = (uint)ranges.Length,
                pPushConstantRanges    = pRanges,
            };
            Vk.vkCreatePipelineLayout(Handle, &ci, null, &raw).ThrowIfFailed();
        }

        // Stamp the declared push-constant ranges and set layouts on the
        // handle for CommandRecorder's debug-only assertions (PushConstants
        // range-fits, BindDescriptorSets layout-matches). One setup-time
        // allocation per layout; the metadata copies with the struct and
        // needs no unregistration (issue #118).
        nint[] handles = new nint[desc.SetLayouts.Length];
        for (int i = 0; i < desc.SetLayouts.Length; i++)
            handles[i] = (nint)desc.SetLayouts[i].Handle;
        var metadata = new PipelineLayoutMetadata
        {
            PushRanges = desc.PushConstantRanges.ToArray(),
            SetLayouts = handles,
        };
        return new PipelineLayout(raw, Handle, metadata);
    }

    /// <summary>
    /// What memory an image built from <paramref name="image"/> would need, WITHOUT
    /// creating one the caller has to own.
    /// </summary>
    /// <remarks>
    /// <para>For the aliasing path: a caller packing several resources into one
    /// <see cref="MemoryBlock"/> must know every size and alignment before anything is
    /// created. Ordinary code never needs this — <see cref="Allocator.CreateImage"/> lets
    /// VMA size, allocate and bind in one call.</para>
    /// <para>Implemented by creating an UNBOUND <c>VkImage</c>, querying it and destroying
    /// it again. Vulkan 1.3's <c>vkGetDeviceImageMemoryRequirements</c> would answer with no
    /// resource at all, but it is deliberately not used: the wrapper already caps VMA's
    /// <c>vulkanApiVersion</c> at 1.2 because lavapipe's exposure of exactly that entry
    /// point is unstable (see <see cref="Allocator.Create"/>), and one path that works on
    /// every device beats two that differ by driver. An unbound image is cheap — no memory
    /// is committed — but it is not free, so a per-frame caller should cache by
    /// description rather than ask every frame.</para>
    /// </remarks>
    public unsafe MemoryRequirements GetImageMemoryRequirements(in ImageDescription image)
    {
        VkImageCreateInfo ci = image.ToNative();

        VkImage_T* probe = null;
        Vk.vkCreateImage(Handle, &ci, null, &probe).ThrowIfFailed();
        try
        {
            VkMemoryRequirements mr = default;
            Vk.vkGetImageMemoryRequirements(Handle, probe, &mr);
            return new MemoryRequirements
            {
                Size = mr.size,
                Alignment = mr.alignment,
                MemoryTypeBits = mr.memoryTypeBits,
            };
        }
        finally
        {
            // In a finally, not after the query: the probe is the only thing this method
            // creates, and leaking a VkImage per query would show up as a device-teardown
            // leak report long after the call that caused it.
            Vk.vkDestroyImage(Handle, probe, null);
        }
    }

    /// <summary>
    /// What memory a buffer built from <paramref name="buffer"/> would need — the buffer
    /// counterpart of <see cref="GetImageMemoryRequirements"/>, with the same
    /// probe-and-destroy implementation and the same advice about caching.
    /// </summary>
    public unsafe MemoryRequirements GetBufferMemoryRequirements(in BufferDescription buffer)
    {
        VkBufferCreateInfo ci = buffer.ToNative();

        VkBuffer_T* probe = null;
        Vk.vkCreateBuffer(Handle, &ci, null, &probe).ThrowIfFailed();
        try
        {
            VkMemoryRequirements mr = default;
            Vk.vkGetBufferMemoryRequirements(Handle, probe, &mr);
            return new MemoryRequirements
            {
                Size = mr.size,
                Alignment = mr.alignment,
                MemoryTypeBits = mr.memoryTypeBits,
            };
        }
        finally
        {
            Vk.vkDestroyBuffer(Handle, probe, null);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        try
        {
            if (Handle != null)
            {
                // Best-effort wait-idle before destroy; Dispose mustn't throw on
                // the success path. A failing wait-idle (lost device, OOM)
                // already implies the device is going away — destroy still runs.
                // Surface the VkResult through the sink so a shutdown after a
                // crash doesn't look like a clean exit in the logs.
                //
                // Deliberately unconditional even when IsLost: on a truly lost
                // device vkDeviceWaitIdle returns DEVICE_LOST in bounded time,
                // and on a device conservatively marked by the context-free
                // loss registry (multi-device process) it actually drains —
                // skipping it would turn the registry's conservatism into a
                // destroy-while-pending UB.
                VkResult idleResult = Vk.vkDeviceWaitIdle(Handle);
                if (idleResult != VkResult.VK_SUCCESS)
                    AhjoDiagnostics.Write(DiagnosticSeverity.Warning, "Device",
                        $"Device.Dispose: vkDeviceWaitIdle returned {idleResult}; destroy proceeds anyway.");
                // Allocator must die before the VkDevice — vmaDestroyAllocator
                // calls into the device's function table.
                if (_allocatorCreated) _allocator.Dispose();
                Vk.vkDestroyDevice(Handle, null);
            }
        }
        finally
        {
            // Set the flag and suppress the finalizer in finally so a throw
            // out of destroy can't leave the handle alive AND have the
            // finalizer re-enter Dispose to destroy it a second time
            // (vkDestroyDevice on an already-destroyed handle is UB). The
            // tradeoff is that a destroy failure leaks the handle for the
            // rest of the process — preferable to UB.
            _disposed = true;
            Unregister();
            GC.SuppressFinalize(this);
        }
    }

    ~Device()
    {
        Debug.Fail("Device was not disposed.");
        Dispose();
    }
}
