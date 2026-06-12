using System.Runtime.CompilerServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Wraps a <c>VkDescriptorUpdateTemplate</c> typed by <typeparamref name="T"/>.
/// The template's update entries are derived from <typeparamref name="T"/>'s
/// field layout — one field per descriptor binding, in declaration order —
/// so a <c>fixed</c> pointer to a <typeparamref name="T"/> is the contiguous
/// byte buffer Vulkan reads on
/// <c>vkUpdateDescriptorSetWithTemplate</c> /
/// <c>vkCmdPushDescriptorSetWithTemplate</c>.
/// </summary>
/// <remarks>
/// <para><b>Field ⇄ binding mapping.</b> The factory iterates
/// <typeparamref name="T"/>'s fields sorted by byte offset (== declaration
/// order for <c>LayoutKind.Sequential</c> structs, which is the default
/// for unmanaged structs) and pairs the i-th field with <c>bindings[i]</c>.
/// Field types should be one of
/// <see cref="BufferDescriptorWrite"/> / <see cref="ImageDescriptorWrite"/>
/// / <see cref="SamplerDescriptorWrite"/> (or an inline array of those for
/// <c>Count &gt; 1</c> bindings); per-entry stride is always 24 bytes —
/// the shared size of the three writer structs and of Vulkan's underlying
/// <c>VkDescriptorBufferInfo</c> / <c>VkDescriptorImageInfo</c>.</para>
/// <para><b>Template type.</b> Created via
/// <see cref="DescriptorSetLayout.CreateUpdateTemplate{T}"/> for the
/// long-lived <c>vkUpdateDescriptorSetWithTemplate</c> path or via
/// <see cref="PipelineLayout.CreatePushDescriptorTemplate{T}"/> for the
/// per-frame push-descriptor path. The two paths produce templates whose
/// underlying <c>VkDescriptorUpdateTemplateType</c> differs — mixing them
/// at use site is undefined behavior.</para>
/// </remarks>
public readonly unsafe struct DescriptorTemplate<T> : IDisposable
    where T : unmanaged
{
    public readonly VkDescriptorUpdateTemplate_T* Handle;
    internal readonly VkDevice_T* DeviceHandle;

    /// <summary>
    /// Set index baked into the template at creation time. Meaningful only
    /// for push-descriptor templates — the value is forwarded back to
    /// <c>vkCmdPushDescriptorSetWithTemplate</c> by
    /// <see cref="CommandRecorder.PushDescriptors{T}"/>. Always 0 for
    /// descriptor-set templates (the API ignores the field).
    /// </summary>
    internal readonly uint Set;

    internal DescriptorTemplate(VkDescriptorUpdateTemplate_T* handle, VkDevice_T* device, uint set)
    {
        Handle       = handle;
        DeviceHandle = device;
        Set          = set;
    }

    public ulong RawHandle => (ulong)Handle;
    public bool  IsNull    => Handle == null;

    /// <summary>
    /// Updates <paramref name="set"/> from <paramref name="data"/> via
    /// <c>vkUpdateDescriptorSetWithTemplate</c>. Requires this template to
    /// have been created for the descriptor-set path
    /// (<see cref="DescriptorSetLayout.CreateUpdateTemplate{T}"/>).
    /// </summary>
    public void Update(in DescriptorSet set, in T data)
    {
        if (Handle == null) return;
        if (set.IsNull) throw new ArgumentException("DescriptorSet is null.", nameof(set));
        fixed (T* pData = &data)
            Vk.vkUpdateDescriptorSetWithTemplate(DeviceHandle, set.Handle, Handle, pData);
    }

    public void Dispose()
    {
        if (Handle == null) return;
        Vk.vkDestroyDescriptorUpdateTemplate(DeviceHandle, Handle, null);
    }
}

/// <summary>
/// Reflection-driven entry builder for <see cref="DescriptorTemplate{T}"/>.
/// One-shot helper called at template creation only — not on the per-frame
/// path.
/// </summary>
internal static unsafe class DescriptorTemplateBuilder
{
    /// <summary>Fixed stride matching the size of every <c>*DescriptorWrite</c> struct.</summary>
    internal const nuint DescriptorWriteStride = 24;

    internal static DescriptorTemplate<T> CreateForSet<T>(
        VkDevice_T*                     device,
        VkDescriptorSetLayout_T*        setLayout,
        ReadOnlySpan<DescriptorBinding> bindings)
        where T : unmanaged
    {
        Span<VkDescriptorUpdateTemplateEntry> entries =
            stackalloc VkDescriptorUpdateTemplateEntry[bindings.Length];
        BuildEntries<T>(bindings, entries);

        VkDescriptorUpdateTemplate_T* raw = null;
        fixed (VkDescriptorUpdateTemplateEntry* pEntries = entries)
        {
            var ci = new VkDescriptorUpdateTemplateCreateInfo
            {
                sType                      = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_UPDATE_TEMPLATE_CREATE_INFO,
                descriptorUpdateEntryCount = (uint)entries.Length,
                pDescriptorUpdateEntries   = pEntries,
                templateType               = VkDescriptorUpdateTemplateType.VK_DESCRIPTOR_UPDATE_TEMPLATE_TYPE_DESCRIPTOR_SET,
                descriptorSetLayout        = setLayout,
            };
            Vk.vkCreateDescriptorUpdateTemplate(device, &ci, null, &raw).ThrowIfFailed();
        }
        return new DescriptorTemplate<T>(raw, device, set: 0);
    }

    internal static DescriptorTemplate<T> CreateForPush<T>(
        VkDevice_T*                     device,
        VkPipelineLayout_T*             pipelineLayout,
        VkPipelineBindPoint             bindPoint,
        uint                            set,
        ReadOnlySpan<DescriptorBinding> bindings)
        where T : unmanaged
    {
        Span<VkDescriptorUpdateTemplateEntry> entries =
            stackalloc VkDescriptorUpdateTemplateEntry[bindings.Length];
        BuildEntries<T>(bindings, entries);

        VkDescriptorUpdateTemplate_T* raw = null;
        fixed (VkDescriptorUpdateTemplateEntry* pEntries = entries)
        {
            var ci = new VkDescriptorUpdateTemplateCreateInfo
            {
                sType                      = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_UPDATE_TEMPLATE_CREATE_INFO,
                descriptorUpdateEntryCount = (uint)entries.Length,
                pDescriptorUpdateEntries   = pEntries,
                templateType               = VkDescriptorUpdateTemplateType.VK_DESCRIPTOR_UPDATE_TEMPLATE_TYPE_PUSH_DESCRIPTORS,
                pipelineBindPoint          = bindPoint,
                pipelineLayout             = pipelineLayout,
                set                        = set,
            };
            Vk.vkCreateDescriptorUpdateTemplate(device, &ci, null, &raw).ThrowIfFailed();
        }
        return new DescriptorTemplate<T>(raw, device, set);
    }

    // Lays out one VkDescriptorUpdateTemplateEntry per binding by walking
    // cumulative 24-byte strides from offset 0. Equivalent to the previous
    // reflection-driven path for the documented shape of T (a sequential
    // struct of *DescriptorWrite fields or inline arrays thereof — no
    // padding, no foreign fields) but AOT-clean: no GetFields/OffsetOf/
    // Marshal.SizeOf, no [DynamicallyAccessedMembers] propagation.
    private static void BuildEntries<T>(
        ReadOnlySpan<DescriptorBinding>       bindings,
        Span<VkDescriptorUpdateTemplateEntry> dst)
        where T : unmanaged
    {
        if (bindings.IsEmpty)
            throw new ArgumentException("DescriptorTemplate<T> requires at least one binding.", nameof(bindings));

        nuint structSize    = (nuint)Unsafe.SizeOf<T>();
        nuint runningOffset = 0;
        for (int i = 0; i < bindings.Length; i++)
        {
            ref readonly DescriptorBinding b = ref bindings[i];
            // Count defaults to 1 via field initializer (issue #119); guard a
            // default(DescriptorBinding) span element that bypasses it.
            uint  count = b.Count == 0 ? 1u : b.Count;
            nuint end   = runningOffset + (nuint)count * DescriptorWriteStride;
            if (end > structSize)
                throw new ArgumentException(
                    $"DescriptorTemplate<{typeof(T).Name}>: binding {i} (Count = {count}) extends past T's {structSize} bytes. " +
                    "Each binding must map to a *DescriptorWrite (or inline array of *DescriptorWrite) sized 24 bytes per descriptor.",
                    nameof(bindings));
            dst[i] = new VkDescriptorUpdateTemplateEntry
            {
                dstBinding      = b.Slot,
                dstArrayElement = 0,
                descriptorCount = count,
                descriptorType  = b.Type,
                offset          = runningOffset,
                stride          = DescriptorWriteStride,
            };
            runningOffset = end;
        }

        // Strict size match catches "T has padding or extra fields" — the
        // previous reflection-driven check would have caught the same
        // shape via fields.Length != bindings.Length.
        if (runningOffset != structSize)
            throw new ArgumentException(
                $"DescriptorTemplate<{typeof(T).Name}>: bindings cover {runningOffset} bytes but {typeof(T).Name} is {structSize} bytes. " +
                "T must be a LayoutKind.Sequential struct of *DescriptorWrite fields (or inline arrays thereof) with no foreign fields or padding.",
                nameof(bindings));
    }
}
