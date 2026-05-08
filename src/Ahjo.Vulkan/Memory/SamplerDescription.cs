using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Inputs to <see cref="Device.CreateSampler"/>. Maps onto
/// <c>VkSamplerCreateInfo</c> minus the boilerplate (<c>sType</c>,
/// <c>pNext</c>, <c>flags</c>) that the wrapper fills in.
/// </summary>
/// <remarks>
/// <para>Vulkan-native enums (<c>VkFilter</c>, <c>VkSamplerMipmapMode</c>,
/// <c>VkSamplerAddressMode</c>, <c>VkCompareOp</c>, <c>VkBorderColor</c>)
/// pass through from the bindings. Defaults match a zero-initialised
/// struct: <c>VK_FILTER_NEAREST</c> filtering, <c>VK_SAMPLER_MIPMAP_MODE_NEAREST</c>,
/// <c>VK_SAMPLER_ADDRESS_MODE_REPEAT</c>, <c>VK_COMPARE_OP_NEVER</c>,
/// <c>VK_BORDER_COLOR_FLOAT_TRANSPARENT_BLACK</c>; callers configure
/// fields explicitly via record-init.</para>
/// <para><see cref="MaxAnisotropy"/> is honoured only when
/// <see cref="AnisotropyEnable"/> is set; <see cref="Device.CreateSampler"/>
/// clamps it to <c>VkPhysicalDeviceLimits.maxSamplerAnisotropy</c>.</para>
/// </remarks>
public readonly record struct SamplerDescription
{
    public VkFilter             MagFilter               { get; init; }
    public VkFilter             MinFilter               { get; init; }
    public VkSamplerMipmapMode  MipmapMode              { get; init; }
    public VkSamplerAddressMode AddressModeU            { get; init; }
    public VkSamplerAddressMode AddressModeV            { get; init; }
    public VkSamplerAddressMode AddressModeW            { get; init; }
    public float                MipLodBias              { get; init; }
    public bool                 AnisotropyEnable        { get; init; }
    public float                MaxAnisotropy           { get; init; }
    public bool                 CompareEnable           { get; init; }
    public VkCompareOp          CompareOp               { get; init; }
    public float                MinLod                  { get; init; }
    public float                MaxLod                  { get; init; }
    public VkBorderColor        BorderColor             { get; init; }
    public bool                 UnnormalizedCoordinates { get; init; }
}
