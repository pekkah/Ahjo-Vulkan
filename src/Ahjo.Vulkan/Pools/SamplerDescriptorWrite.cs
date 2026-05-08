using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One descriptor entry in a bare-sampler binding (<c>VK_DESCRIPTOR_TYPE_SAMPLER</c>).
/// The driver reads only <see cref="Sampler"/> for this type; the trailing
/// fields exist so the struct keeps the same 24-byte stride as
/// <see cref="ImageDescriptorWrite"/> — a uniform stride lets a single
/// descriptor-update template iterate buffer / image / sampler entries
/// without per-binding stride bookkeeping.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 24)]
public readonly unsafe struct SamplerDescriptorWrite
{
    internal readonly VkSampler_T* Sampler;
    private  readonly nint         _viewPad;
    private  readonly int          _layoutPad;

    public SamplerDescriptorWrite(VkSampler_T* sampler)
    {
        Sampler    = sampler;
        _viewPad   = 0;
        _layoutPad = 0;
    }

    public SamplerDescriptorWrite(in Sampler sampler) : this(sampler.Handle) { }

    public static SamplerDescriptorWrite Of(VkSampler_T* sampler) => new(sampler);

    public static SamplerDescriptorWrite Of(in Sampler sampler) => new(sampler.Handle);
}
