using System.Buffers;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Non-templated <c>vkUpdateDescriptorSets</c> entry on
/// <see cref="DescriptorSet"/>. Lives on an extension so the small
/// <see cref="DescriptorSet"/> struct stays unmanaged-shaped (two
/// pointers plus the allocated variable-descriptor count) without picking
/// up a method that captures device state.
/// </summary>
public static unsafe class DescriptorSetExtensions
{
    private const int StackThreshold = 8;

    /// <summary>
    /// Writes <paramref name="writes"/> to <paramref name="set"/> in a
    /// single <c>vkUpdateDescriptorSets</c> call. Zero allocations when
    /// <paramref name="writes"/> contains <c>≤ 8</c> entries; longer
    /// runs rent from <see cref="ArrayPool{T}"/>.
    /// </summary>
    public static void Update(
        this in DescriptorSet         set,
        Device                        device,
        ReadOnlySpan<DescriptorWrite> writes)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (set.IsNull) throw new ArgumentException("DescriptorSet is null.", nameof(set));
        if (writes.IsEmpty) return;

        int count = writes.Length;
        if (count <= StackThreshold)
        {
            Span<VkWriteDescriptorSet> raws = stackalloc VkWriteDescriptorSet[count];
            // Carved alongside raws by the same rule: an acceleration-structure
            // write needs a VkWriteDescriptorSetAccelerationStructureKHR chained
            // into pNext, and that node must outlive the native call.
            Span<VkWriteDescriptorSetAccelerationStructureKHR> chains =
                stackalloc VkWriteDescriptorSetAccelerationStructureKHR[count];
            FlushUpdate(device, set.Handle, writes, raws, chains);
            return;
        }

        VkWriteDescriptorSet[] rented = ArrayPool<VkWriteDescriptorSet>.Shared.Rent(count);
        try
        {
            VkWriteDescriptorSetAccelerationStructureKHR[] rentedChains =
                ArrayPool<VkWriteDescriptorSetAccelerationStructureKHR>.Shared.Rent(count);
            try
            {
                FlushUpdate(
                    device, set.Handle, writes,
                    rented.AsSpan(0, count), rentedChains.AsSpan(0, count));
            }
            finally
            {
                ArrayPool<VkWriteDescriptorSetAccelerationStructureKHR>.Shared.Return(rentedChains);
            }
        }
        finally
        {
            ArrayPool<VkWriteDescriptorSet>.Shared.Return(rented);
        }
    }

    private static void FlushUpdate(
        Device                                             device,
        VkDescriptorSet_T*                                 setHandle,
        ReadOnlySpan<DescriptorWrite>                      writes,
        Span<VkWriteDescriptorSet>                         raws,
        Span<VkWriteDescriptorSetAccelerationStructureKHR> chains)
    {
        // writes and chains are both pinned across BuildWrites AND the native
        // call: the produced entries point into both.
        fixed (DescriptorWrite* _ = writes)
        fixed (VkWriteDescriptorSetAccelerationStructureKHR* __ = chains)
        {
            DescriptorWriteBuilder.BuildWrites(writes, setHandle, raws, chains);
            fixed (VkWriteDescriptorSet* pRaws = raws)
                Vk.vkUpdateDescriptorSets(device.Handle, (uint)writes.Length, pRaws, 0, null);
        }
    }
}
