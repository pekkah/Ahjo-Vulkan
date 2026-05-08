using System.Buffers;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Non-templated <c>vkUpdateDescriptorSets</c> entry on
/// <see cref="DescriptorSet"/>. Lives on an extension so the small
/// <see cref="DescriptorSet"/> struct stays unmanaged-shaped (one
/// pointer + one layout pointer) without picking up a method that
/// captures device state.
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
            FlushUpdate(device, set.Handle, writes, raws);
            return;
        }

        VkWriteDescriptorSet[] rented = ArrayPool<VkWriteDescriptorSet>.Shared.Rent(count);
        try
        {
            FlushUpdate(device, set.Handle, writes, rented.AsSpan(0, count));
        }
        finally
        {
            ArrayPool<VkWriteDescriptorSet>.Shared.Return(rented);
        }
    }

    private static void FlushUpdate(
        Device                        device,
        VkDescriptorSet_T*            setHandle,
        ReadOnlySpan<DescriptorWrite> writes,
        Span<VkWriteDescriptorSet>    raws)
    {
        fixed (DescriptorWrite* _ = writes)
        {
            DescriptorWriteBuilder.BuildWrites(writes, setHandle, raws);
            fixed (VkWriteDescriptorSet* pRaws = raws)
                Vk.vkUpdateDescriptorSets(device.Handle, (uint)writes.Length, pRaws, 0, null);
        }
    }
}
