using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One descriptor entry in an image-shaped binding: the user-side mirror
/// of <c>VkDescriptorImageInfo</c>. Used by <c>SAMPLED_IMAGE</c>,
/// <c>STORAGE_IMAGE</c>, <c>INPUT_ATTACHMENT</c>, and
/// <c>COMBINED_IMAGE_SAMPLER</c>; the driver reads only the fields its
/// descriptor type needs (the spec guarantees the others are ignored), so
/// the same 24-byte layout serves every image variant.
/// </summary>
/// <remarks>
/// Layout matches <c>VkDescriptorImageInfo</c> on x64: sampler pointer
/// (8 bytes) + image-view pointer (8 bytes) + <c>VkImageLayout</c>
/// (4 bytes) + 4 bytes trailing padding to the natural 8-byte struct
/// alignment.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Size = 24)]
public readonly unsafe struct ImageDescriptorWrite
{
    internal readonly VkSampler_T*   Sampler;
    internal readonly VkImageView_T* View;
    internal readonly VkImageLayout  Layout;

    public ImageDescriptorWrite(in ImageView view, VkImageLayout layout)
    {
        Sampler = null;
        View    = view.Handle;
        Layout  = layout;
    }

    public ImageDescriptorWrite(VkSampler_T* sampler, in ImageView view, VkImageLayout layout)
    {
        Sampler = sampler;
        View    = view.Handle;
        Layout  = layout;
    }

    public ImageDescriptorWrite(VkSampler_T* sampler, VkImageView_T* view, VkImageLayout layout)
    {
        Sampler = sampler;
        View    = view;
        Layout  = layout;
    }

    public ImageDescriptorWrite(in Sampler sampler, in ImageView view, VkImageLayout layout)
        : this(sampler.Handle, in view, layout) { }

    /// <summary>
    /// Image-only entry — for <c>SAMPLED_IMAGE</c>, <c>STORAGE_IMAGE</c>,
    /// or <c>INPUT_ATTACHMENT</c>. The driver ignores the (null) sampler
    /// pointer for these types.
    /// </summary>
    public static ImageDescriptorWrite Of(in ImageView view, VkImageLayout layout)
        => new(in view, layout);

    /// <summary>
    /// Combined-image-sampler entry — for
    /// <c>COMBINED_IMAGE_SAMPLER</c>. Use the dedicated
    /// <see cref="SamplerDescriptorWrite"/> when the binding is a bare
    /// sampler.
    /// </summary>
    public static ImageDescriptorWrite Of(VkSampler_T* sampler, in ImageView view, VkImageLayout layout)
        => new(sampler, in view, layout);

    /// <summary>
    /// Combined-image-sampler entry over the wrapper's <see cref="Sampler"/>
    /// type — for <c>COMBINED_IMAGE_SAMPLER</c>.
    /// </summary>
    public static ImageDescriptorWrite Of(in Sampler sampler, in ImageView view, VkImageLayout layout)
        => new(sampler.Handle, in view, layout);
}
