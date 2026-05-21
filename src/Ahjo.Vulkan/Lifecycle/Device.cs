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
    private  bool                         _allocatorCreated;
    private  bool                         _disposed;
    // Allocator lazy-init runs at most once per Device, so the cost of
    // always taking the lock is negligible and removes the
    // double-create race on concurrent first access.
    private  readonly object              _allocatorLock = new();

    internal Device(VkDevice_T* handle, PhysicalDevice physicalDevice, Queue[] queues)
    {
        Handle         = handle;
        Functions      = new DeviceFunctionTable(handle);
        PhysicalDevice = physicalDevice;
        _queues        = queues;
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
                    _allocator        = Ahjo.Vulkan.Allocator.Create(this);
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
    public DescriptorSetLayout CreateDescriptorSetLayout(in DescriptorSetLayoutDescription desc)
    {
        if (desc.Bindings.IsEmpty)
            throw new ArgumentException("DescriptorSetLayoutDescription.Bindings must contain at least one entry.");

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
    /// Mismatches are logged to <see cref="Console.Error"/> so the
    /// "user copied a cache from another machine" / "driver UUID
    /// rotated" cases surface visibly.
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
                        Console.Error.WriteLine(
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

        // Register the declared push-constant ranges and set layouts on
        // the side table PipelineLayout exposes for CommandRecorder's
        // debug-only assertions (PushConstants range-fits,
        // BindDescriptorSets layout-matches). PipelineLayout is
        // constrained to `unmanaged` by IVulkanHandle so this metadata
        // can't ride on the struct itself — see
        // PipelineLayout.RegisterPushRanges / RegisterSetLayouts.
        if (!desc.PushConstantRanges.IsEmpty)
            PipelineLayout.RegisterPushRanges(raw, desc.PushConstantRanges.ToArray());
        if (!desc.SetLayouts.IsEmpty)
        {
            nint[] handles = new nint[desc.SetLayouts.Length];
            for (int i = 0; i < desc.SetLayouts.Length; i++)
                handles[i] = (nint)desc.SetLayouts[i].Handle;
            PipelineLayout.RegisterSetLayouts(raw, handles);
        }
        return new PipelineLayout(raw, Handle);
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
                // Surface the VkResult to stderr so a shutdown after a crash
                // doesn't look like a clean exit in the logs.
                VkResult idleResult = Vk.vkDeviceWaitIdle(Handle);
                if (idleResult != VkResult.VK_SUCCESS)
                    Console.Error.WriteLine(
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
            GC.SuppressFinalize(this);
        }
    }

    ~Device()
    {
        Debug.Fail("Device was not disposed.");
        Dispose();
    }
}
