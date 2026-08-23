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
/// <para><see cref="BufferDescriptorWrite"/> and
/// <see cref="ImageDescriptorWrite"/> already lay out as
/// <see cref="VkDescriptorBufferInfo"/> / <see cref="VkDescriptorImageInfo"/>,
/// so the per-write fill is just a pointer cast — no per-call copy of the
/// payload structs. The caller owns the writes span; pointers stitched into
/// the <c>VkWriteDescriptorSet</c> entries are valid only while
/// <c>writes</c> is pinned.</para>
/// <para>An acceleration-structure write has no info-pointer form at all: it
/// is expressed as a <see cref="VkWriteDescriptorSetAccelerationStructureKHR"/>
/// chained into <c>pNext</c> with both info pointers null. Those chain nodes
/// are a <em>second</em> caller-owned buffer, which is why
/// <see cref="BuildWrites"/> takes a <c>chains</c> span — see its remarks for
/// the pinning obligation that comes with it.</para>
/// </remarks>
internal static unsafe class DescriptorWriteBuilder
{
    /// <summary>
    /// Fills <paramref name="dst"/> with one <see cref="VkWriteDescriptorSet"/>
    /// per entry in <paramref name="writes"/>, and — for acceleration-structure
    /// writes — the matching <c>pNext</c> chain node in
    /// <paramref name="chains"/> at the same index.
    /// </summary>
    /// <param name="writes">The wrapper-level writes.</param>
    /// <param name="setHandle">Target set, or null for a push descriptor.</param>
    /// <param name="dst">Native output, at least as long as
    /// <paramref name="writes"/>.</param>
    /// <param name="chains">
    /// Chain-node scratch, the <b>same length</b> as <paramref name="dst"/>.
    /// Only the slots belonging to acceleration-structure writes are written;
    /// the rest are left alone, since nothing points at them.
    /// </param>
    /// <remarks>
    /// The caller MUST keep <b>both</b> the <paramref name="writes"/> span and
    /// the <paramref name="chains"/> span alive and pinned for the duration of
    /// the native call: the per-write <c>pBufferInfo</c> / <c>pImageInfo</c>
    /// pointers reference into <paramref name="writes"/>, each chain node's
    /// <c>pAccelerationStructures</c> references into <paramref name="writes"/>
    /// as well, and <c>dst[i].pNext</c> references into
    /// <paramref name="chains"/>.
    /// </remarks>
    internal static void BuildWrites(
        ReadOnlySpan<DescriptorWrite>                     writes,
        VkDescriptorSet_T*                                setHandle,
        Span<VkWriteDescriptorSet>                        dst,
        Span<VkWriteDescriptorSetAccelerationStructureKHR> chains)
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

            void* pNext = null;
            if (w._kind == DescriptorWrite.Kind.AccelerationStructure)
            {
                // pAccelerationStructures is a pointer to an ARRAY of handles;
                // with accelerationStructureCount = 1 the handle field stored
                // inline on the write is that array. Same idiom, same pinning
                // requirement, as the buffer/image payloads above — but taken
                // through the write's own struct pointer, because a pointer
                // type cannot be a type argument to Unsafe.AsRef.
                var pWrite = (DescriptorWrite*)Unsafe.AsPointer(ref Unsafe.AsRef(in w));
                chains[i] = new VkWriteDescriptorSetAccelerationStructureKHR
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET_ACCELERATION_STRUCTURE_KHR,
                    accelerationStructureCount = 1,
                    pAccelerationStructures    = &pWrite->_accelerationStructure,
                };
                pNext = Unsafe.AsPointer(ref chains[i]);
            }

            dst[i] = new VkWriteDescriptorSet
            {
                sType            = VkStructureType.VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET,
                pNext            = pNext,
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
