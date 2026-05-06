using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One descriptor entry in a buffer-shaped binding: the user-side mirror of
/// <c>VkDescriptorBufferInfo</c>. <c>readonly struct</c> with the exact
/// binary layout Vulkan reads when the surrounding
/// <see cref="DescriptorTemplate{T}"/> is invoked, so a <c>fixed</c>
/// pointer to the caller's struct is what
/// <c>vkUpdateDescriptorSetWithTemplate</c> /
/// <c>vkCmdPushDescriptorSetWithTemplate</c> dereferences directly.
/// </summary>
/// <remarks>
/// Used for <c>UNIFORM_BUFFER</c>, <c>STORAGE_BUFFER</c>, and the dynamic
/// variants of both. <see cref="Range"/> may be <c>VK_WHOLE_SIZE</c>
/// (<c>~0ul</c>) — the buffer's <see cref="Buffer.Size"/> is the
/// dominant ergonomic, and the parameterless <see cref="Of(in Buffer)"/>
/// factory wires that up.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Size = 24)]
public readonly unsafe struct BufferDescriptorWrite
{
    internal readonly VkBuffer_T* Buffer;
    internal readonly ulong       Offset;
    internal readonly ulong       Range;

    public BufferDescriptorWrite(in Buffer buffer)
    {
        Buffer = buffer.Handle;
        Offset = 0;
        Range  = buffer.Size;
    }

    public BufferDescriptorWrite(in Buffer buffer, ulong offset, ulong range)
    {
        Buffer = buffer.Handle;
        Offset = offset;
        Range  = range;
    }

    public BufferDescriptorWrite(VkBuffer_T* buffer, ulong offset, ulong range)
    {
        Buffer = buffer;
        Offset = offset;
        Range  = range;
    }

    /// <summary>Whole-buffer write at offset 0.</summary>
    public static BufferDescriptorWrite Of(in Buffer buffer) => new(in buffer);

    /// <summary>Range-bounded write.</summary>
    public static BufferDescriptorWrite Of(in Buffer buffer, ulong offset, ulong range)
        => new(in buffer, offset, range);
}
