using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan;

/// <summary>
/// One contiguous push-constant range. Maps onto
/// <c>VkPushConstantRange</c>. Use the
/// <see cref="For{T}(ShaderStages, uint)"/> factory to derive
/// <see cref="Size"/> from <c>sizeof(T)</c>; spelling the size out by hand
/// is a frequent source of validation errors.
/// </summary>
/// <remarks>
/// Vulkan guarantees a minimum push-constant capacity of 128 bytes
/// (<c>maxPushConstantsSize</c>); the wrapper does not enforce it at
/// construction because the actual ceiling depends on the device and is
/// known later. <see cref="Device.CreatePipelineLayout"/> will surface a
/// <c>VK_ERROR_*</c> if the device rejects the range.
/// </remarks>
public readonly record struct PushConstantRange
{
    public ShaderStages Stages { get; init; }
    public uint         Offset { get; init; }
    public uint         Size   { get; init; }

    /// <summary>
    /// Range sized to <c>sizeof(T)</c>. Default <paramref name="offset"/>
    /// is 0 — the dominant case (a single push-constant block per layout).
    /// Throws when <paramref name="offset"/> is not a multiple of 4 —
    /// Vulkan's <c>VUID-VkPushConstantRange-offset-00295</c> requires
    /// 4-byte alignment, and catching it here is friendlier than the
    /// driver's <c>VK_ERROR_*</c> at <c>vkCreatePipelineLayout</c>.
    /// </summary>
    public static PushConstantRange For<T>(ShaderStages stages, uint offset = 0)
        where T : unmanaged
    {
        if ((offset & 3) != 0)
            throw new ArgumentException(
                $"Push-constant offset must be a multiple of 4 (got {offset}).", nameof(offset));
        return new PushConstantRange { Stages = stages, Offset = offset, Size = (uint)Unsafe.SizeOf<T>() };
    }
}
