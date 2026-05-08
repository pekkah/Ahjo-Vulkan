using System.Runtime.CompilerServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Pack a span of <see cref="DescriptorWrite"/> records into the
/// native <see cref="VkWriteDescriptorSet"/> array shape Vulkan reads.
/// Reused by both <see cref="DescriptorSetExtensions.Update"/> and
/// <see cref="CommandRecorder.PushDescriptorSet"/>.
/// </summary>
/// <remarks>
/// <see cref="BufferDescriptorWrite"/> and <see cref="ImageDescriptorWrite"/>
/// already lay out as <see cref="VkDescriptorBufferInfo"/> /
/// <see cref="VkDescriptorImageInfo"/>, so the per-write fill is just a
/// pointer cast — no per-call copy of the payload structs. The caller
/// owns the writes span; pointers stitched into the
/// <c>VkWriteDescriptorSet</c> entries are valid only while
/// <paramref name="writes"/> is pinned.
/// </remarks>
internal static unsafe class DescriptorWriteBuilder
{
    /// <summary>
    /// Fills <paramref name="dst"/> with one <see cref="VkWriteDescriptorSet"/>
    /// per entry in <paramref name="writes"/>. The caller MUST keep the
    /// <paramref name="writes"/> span alive (and addressable through
    /// <paramref name="pWrites"/>) for the duration of the native call —
    /// the per-write pBufferInfo / pImageInfo pointers reference into
    /// the caller's own storage.
    /// </summary>
    internal static void BuildWrites(
        ReadOnlySpan<DescriptorWrite>  writes,
        VkDescriptorSet_T*             setHandle,
        Span<VkWriteDescriptorSet>     dst)
    {
        for (int i = 0; i < writes.Length; i++)
        {
            ref readonly DescriptorWrite w = ref writes[i];
            // BufferDescriptorWrite is a 24-byte mirror of
            // VkDescriptorBufferInfo; ImageDescriptorWrite likewise of
            // VkDescriptorImageInfo. Take the field's address through
            // the ref-readonly entry — the caller pins the writes span
            // for the duration of the native call so the pointer stays
            // valid.
            void* pBuffer = Unsafe.AsPointer(ref Unsafe.AsRef(in w._buffer));
            void* pImage  = Unsafe.AsPointer(ref Unsafe.AsRef(in w._image));

            dst[i] = new VkWriteDescriptorSet
            {
                sType            = VkStructureType.VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET,
                dstSet           = setHandle,
                dstBinding       = w._binding,
                dstArrayElement  = w._arrayElement,
                descriptorCount  = 1,
                descriptorType   = w._type,
                pBufferInfo      = w._kind == DescriptorWrite.Kind.Buffer
                                       ? (VkDescriptorBufferInfo*)pBuffer
                                       : null,
                pImageInfo       = w._kind == DescriptorWrite.Kind.Image
                                       ? (VkDescriptorImageInfo*)pImage
                                       : null,
                pTexelBufferView = null,
            };
        }
    }
}
