using System;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Slang.Native;

namespace Ahjo.Vulkan.Slang;

/// <summary>
/// Provides default mappings from Slang reflection types to Vulkan types.
/// </summary>
public static class SlangVulkanMapping
{
    /// <summary>
    /// Maps a Slang descriptor-range binding type to the Vulkan descriptor type.
    /// </summary>
    public static VkDescriptorType MapBindingType(SlangBindingType type)
    {
        bool mutable = (type & SlangBindingType.SLANG_BINDING_TYPE_MUTABLE_FLAG) != 0;

        return (type & SlangBindingType.SLANG_BINDING_TYPE_BASE_MASK) switch
        {
            SlangBindingType.SLANG_BINDING_TYPE_SAMPLER
                => VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLER,
            SlangBindingType.SLANG_BINDING_TYPE_TEXTURE
                => mutable
                    ? VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_IMAGE
                    : VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE,
            SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER
                => VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
            SlangBindingType.SLANG_BINDING_TYPE_TYPED_BUFFER
                => mutable
                    ? VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_TEXEL_BUFFER
                    : VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_TEXEL_BUFFER,
            SlangBindingType.SLANG_BINDING_TYPE_RAW_BUFFER
                => VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
            SlangBindingType.SLANG_BINDING_TYPE_COMBINED_TEXTURE_SAMPLER
                => VkDescriptorType.VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
            SlangBindingType.SLANG_BINDING_TYPE_INPUT_RENDER_TARGET
                => VkDescriptorType.VK_DESCRIPTOR_TYPE_INPUT_ATTACHMENT,
            SlangBindingType.SLANG_BINDING_TYPE_INLINE_UNIFORM_DATA
                => VkDescriptorType.VK_DESCRIPTOR_TYPE_INLINE_UNIFORM_BLOCK,
            SlangBindingType.SLANG_BINDING_TYPE_RAY_TRACING_ACCELERATION_STRUCTURE
                => VkDescriptorType.VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR,

            _ => throw new NotSupportedException($"Slang binding type {type} has no Vulkan equivalent.")
        };
    }

    /// <summary>
    /// Maps a Slang vertex attribute description to a Vulkan vertex input attribute description.
    /// </summary>
    public static VertexAttributeDescription MapVertexAttribute(this SlangVertexAttributeDescription attr, uint binding = 0, uint offset = 0)
    {
        if (attr.Kind == SlangTypeKind.SLANG_TYPE_KIND_MATRIX)
        {
            throw new NotSupportedException(
                $"Vertex input '{attr.Name}' is a matrix ({attr.RowCount}x{attr.ColumnCount}). It occupies "
                + $"{attr.SizeInLocations} consecutive vertex-input locations, but the per-location component count "
                + "depends on the session's default matrix layout mode and only column-major has been verified against the "
                + "emitted SPIR-V, so the VkFormat for each location is not derivable here (issue #166, OPEN-6). "
                + "Declare the input as separate vector-typed fields, or fill VertexAttributeDescription by hand "
                + "for this entry point.");
        }

        if (attr.Kind != SlangTypeKind.SLANG_TYPE_KIND_VECTOR && attr.Kind != SlangTypeKind.SLANG_TYPE_KIND_SCALAR)
        {
            throw new NotSupportedException(
                $"Vertex input '{attr.Name}' has type kind {attr.Kind}, which has no VkFormat mapping. Vertex attributes "
                + "are scalars and vectors.");
        }

        VkFormat format = MapScalarFormat(attr.ScalarType, attr.ComponentCount);

        if (format == VkFormat.VK_FORMAT_UNDEFINED)
        {
            throw new NotSupportedException(
                $"Vertex input '{attr.Name}' is {attr.ComponentCount} x {attr.ScalarType}, which has no VkFormat mapping.");
        }

        return new VertexAttributeDescription
        {
            Location = attr.Location,
            Binding = binding,
            Format = format,
            Offset = offset
        };
    }

    /// <summary>
    /// <c>(scalar type, component count)</c> to <c>VkFormat</c>. Returns
    /// <c>VK_FORMAT_UNDEFINED</c> for a combination with no Vulkan format.
    /// </summary>
    public static VkFormat MapScalarFormat(SlangScalarType scalar, uint components) => (scalar, components) switch
    {
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT32, 1) => VkFormat.VK_FORMAT_R32_SFLOAT,
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT32, 2) => VkFormat.VK_FORMAT_R32G32_SFLOAT,
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT32, 3) => VkFormat.VK_FORMAT_R32G32B32_SFLOAT,
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT32, 4) => VkFormat.VK_FORMAT_R32G32B32A32_SFLOAT,

        (SlangScalarType.SLANG_SCALAR_TYPE_INT32, 1) => VkFormat.VK_FORMAT_R32_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT32, 2) => VkFormat.VK_FORMAT_R32G32_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT32, 3) => VkFormat.VK_FORMAT_R32G32B32_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT32, 4) => VkFormat.VK_FORMAT_R32G32B32A32_SINT,

        (SlangScalarType.SLANG_SCALAR_TYPE_UINT32, 1) => VkFormat.VK_FORMAT_R32_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT32, 2) => VkFormat.VK_FORMAT_R32G32_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT32, 3) => VkFormat.VK_FORMAT_R32G32B32_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT32, 4) => VkFormat.VK_FORMAT_R32G32B32A32_UINT,

        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT16, 1) => VkFormat.VK_FORMAT_R16_SFLOAT,
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT16, 2) => VkFormat.VK_FORMAT_R16G16_SFLOAT,
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT16, 3) => VkFormat.VK_FORMAT_R16G16B16_SFLOAT,
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT16, 4) => VkFormat.VK_FORMAT_R16G16B16A16_SFLOAT,

        (SlangScalarType.SLANG_SCALAR_TYPE_INT16, 1) => VkFormat.VK_FORMAT_R16_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT16, 2) => VkFormat.VK_FORMAT_R16G16_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT16, 3) => VkFormat.VK_FORMAT_R16G16B16_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT16, 4) => VkFormat.VK_FORMAT_R16G16B16A16_SINT,

        (SlangScalarType.SLANG_SCALAR_TYPE_UINT16, 1) => VkFormat.VK_FORMAT_R16_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT16, 2) => VkFormat.VK_FORMAT_R16G16_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT16, 3) => VkFormat.VK_FORMAT_R16G16B16_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT16, 4) => VkFormat.VK_FORMAT_R16G16B16A16_UINT,

        (SlangScalarType.SLANG_SCALAR_TYPE_INT8, 1) => VkFormat.VK_FORMAT_R8_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT8, 2) => VkFormat.VK_FORMAT_R8G8_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT8, 3) => VkFormat.VK_FORMAT_R8G8B8_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT8, 4) => VkFormat.VK_FORMAT_R8G8B8A8_SINT,

        (SlangScalarType.SLANG_SCALAR_TYPE_UINT8, 1) => VkFormat.VK_FORMAT_R8_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT8, 2) => VkFormat.VK_FORMAT_R8G8_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT8, 3) => VkFormat.VK_FORMAT_R8G8B8_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT8, 4) => VkFormat.VK_FORMAT_R8G8B8A8_UINT,

        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT64, 1) => VkFormat.VK_FORMAT_R64_SFLOAT,
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT64, 2) => VkFormat.VK_FORMAT_R64G64_SFLOAT,
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT64, 3) => VkFormat.VK_FORMAT_R64G64B64_SFLOAT,
        (SlangScalarType.SLANG_SCALAR_TYPE_FLOAT64, 4) => VkFormat.VK_FORMAT_R64G64B64A64_SFLOAT,

        (SlangScalarType.SLANG_SCALAR_TYPE_INT64, 1) => VkFormat.VK_FORMAT_R64_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT64, 2) => VkFormat.VK_FORMAT_R64G64_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT64, 3) => VkFormat.VK_FORMAT_R64G64B64_SINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_INT64, 4) => VkFormat.VK_FORMAT_R64G64B64A64_SINT,

        (SlangScalarType.SLANG_SCALAR_TYPE_UINT64, 1) => VkFormat.VK_FORMAT_R64_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT64, 2) => VkFormat.VK_FORMAT_R64G64_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT64, 3) => VkFormat.VK_FORMAT_R64G64B64_UINT,
        (SlangScalarType.SLANG_SCALAR_TYPE_UINT64, 4) => VkFormat.VK_FORMAT_R64G64B64A64_UINT,

        _ => VkFormat.VK_FORMAT_UNDEFINED,
    };

    /// <summary>
    /// Maps one reflected Slang descriptor binding to the <c>Ahjo.Vulkan</c>
    /// description type <c>Device.CreateDescriptorSetLayout</c> takes.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The binding's Slang type has no Vulkan descriptor equivalent.
    /// </exception>
    public static DescriptorBinding MapBinding(this SlangDescriptorBinding binding)
    {
        return new DescriptorBinding
        {
            Slot = binding.Slot,
            Type = MapBindingType(binding.Type),
            Count = binding.Count,
            Stages = binding.Stages
        };
    }

    /// <summary>
    /// Maps one reflected Slang push-constant range to the <c>Ahjo.Vulkan</c>
    /// description type <c>Device.CreatePipelineLayout</c> takes.
    /// </summary>
    public static PushConstantRange MapPushConstantRange(this SlangPushConstantRange range)
    {
        return new PushConstantRange
        {
            Stages = range.Stages,
            Offset = range.Offset,
            Size = range.Size
        };
    }

    /// <summary>
    /// Maps a reflected descriptor set's bindings — as returned by
    /// <c>SlangReflection.Bindings</c> — into a <c>DescriptorBinding[]</c>
    /// suitable for <c>DescriptorSetLayoutDescription.Bindings</c>.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// A binding's Slang type has no Vulkan descriptor equivalent.
    /// </exception>
    public static DescriptorBinding[] MapBindings(this ReadOnlySpan<SlangDescriptorBinding> bindings)
    {
        var result = new DescriptorBinding[bindings.Length];
        for (int i = 0; i < bindings.Length; i++)
            result[i] = bindings[i].MapBinding();
        return result;
    }

    /// <summary>
    /// Maps a program's reflected push-constant ranges — as returned by
    /// <c>SlangReflection.PushConstantRanges</c> — into a
    /// <c>PushConstantRange[]</c> suitable for
    /// <c>PipelineLayoutDescription.PushConstantRanges</c>.
    /// </summary>
    public static PushConstantRange[] MapPushConstantRanges(this ReadOnlySpan<SlangPushConstantRange> ranges)
    {
        var result = new PushConstantRange[ranges.Length];
        for (int i = 0; i < ranges.Length; i++)
            result[i] = ranges[i].MapPushConstantRange();
        return result;
    }
}
