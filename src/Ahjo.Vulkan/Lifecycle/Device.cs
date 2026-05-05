using System.Diagnostics;
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
            if (!_allocatorCreated)
            {
                _allocator        = Ahjo.Vulkan.Allocator.Create(this);
                _allocatorCreated = true;
            }
            return _allocator;
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
            var ci = new VkDescriptorSetLayoutCreateInfo
            {
                sType        = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO,
                flags        = desc.UpdateAfterBindPool
                    ? (uint)VkDescriptorSetLayoutCreateFlagBits.VK_DESCRIPTOR_SET_LAYOUT_CREATE_UPDATE_AFTER_BIND_POOL_BIT
                    : 0u,
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
    /// Builds a <see cref="PipelineLayout"/> from descriptor-set layouts +
    /// push-constant ranges.
    /// </summary>
    public PipelineLayout CreatePipelineLayout(in PipelineLayoutDescription desc)
    {
        Span<nint> setLayouts = stackalloc nint[desc.SetLayouts.Length];
        for (int i = 0; i < desc.SetLayouts.Length; i++)
            setLayouts[i] = (nint)desc.SetLayouts[i].Handle;

        Span<VkPushConstantRange> ranges = stackalloc VkPushConstantRange[desc.PushConstantRanges.Length];
        for (int i = 0; i < desc.PushConstantRanges.Length; i++)
        {
            ref readonly var r = ref desc.PushConstantRanges[i];
            ranges[i] = new VkPushConstantRange
            {
                stageFlags = (uint)r.Stages,
                offset     = r.Offset,
                size       = r.Size,
            };
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
        return new PipelineLayout(raw, Handle);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Handle != null)
        {
            // Best-effort wait-idle before destroy; Dispose mustn't throw on
            // the success path. A failing wait-idle (lost device, OOM)
            // already implies the device is going away — destroy still runs.
            Vk.vkDeviceWaitIdle(Handle);
            // Allocator must die before the VkDevice — vmaDestroyAllocator
            // calls into the device's function table.
            if (_allocatorCreated) _allocator.Dispose();
            Vk.vkDestroyDevice(Handle, null);
        }
        GC.SuppressFinalize(this);
    }

    ~Device()
    {
        Debug.Fail("Device was not disposed.");
        Dispose();
    }
}
