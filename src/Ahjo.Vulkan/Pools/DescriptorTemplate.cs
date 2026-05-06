using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

    private static void BuildEntries<T>(
        ReadOnlySpan<DescriptorBinding>       bindings,
        Span<VkDescriptorUpdateTemplateEntry> dst)
        where T : unmanaged
    {
        if (bindings.IsEmpty)
            throw new ArgumentException("DescriptorTemplate<T> requires at least one binding.", nameof(bindings));

        FieldInfo[] fields = typeof(T).GetFields(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        // GetFields() doesn't guarantee declaration order; sort by byte
        // offset, which matches declaration order for sequential layout
        // and is the order Vulkan reads anyway.
        Array.Sort(fields, static (a, b) =>
            Marshal.OffsetOf(a.DeclaringType!, a.Name).ToInt64()
                .CompareTo(Marshal.OffsetOf(b.DeclaringType!, b.Name).ToInt64()));

        if (fields.Length != bindings.Length)
            throw new ArgumentException(
                $"DescriptorTemplate<{typeof(T).Name}> has {fields.Length} field(s) but {bindings.Length} binding(s) were provided. " +
                "Each binding must map to exactly one field in declaration order.", nameof(bindings));

        for (int i = 0; i < bindings.Length; i++)
        {
            ref readonly DescriptorBinding b = ref bindings[i];
            nuint offset = (nuint)(nint)Marshal.OffsetOf<T>(fields[i].Name);
            dst[i] = new VkDescriptorUpdateTemplateEntry
            {
                dstBinding      = b.Slot,
                dstArrayElement = 0,
                descriptorCount = b.Count == 0 ? 1u : b.Count,
                descriptorType  = b.Type,
                offset          = offset,
                stride          = DescriptorWriteStride,
            };
        }

        // Sanity check: total size must accommodate the last entry's range.
        nuint structSize = (nuint)Unsafe.SizeOf<T>();
        for (int i = 0; i < bindings.Length; i++)
        {
            nuint count = bindings[i].Count == 0 ? 1u : bindings[i].Count;
            nuint end   = dst[i].offset + count * DescriptorWriteStride;
            if (end > structSize)
                throw new ArgumentException(
                    $"Field {i} ({fields[i].Name}) extends to byte {end} but {typeof(T).Name} is only {structSize} bytes. " +
                    "Field type must be a *DescriptorWrite (or inline array thereof) sized 24 bytes per descriptor.",
                    nameof(bindings));
        }
    }
}
