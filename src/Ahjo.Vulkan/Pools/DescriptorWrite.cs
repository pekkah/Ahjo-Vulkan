using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Discriminated descriptor-write record consumed by
/// <see cref="DescriptorSetExtensions.Update"/> and
/// <see cref="CommandRecorder.PushDescriptorSet(in PipelineLayout, uint, ReadOnlySpan{DescriptorWrite})"/>.
/// One <see cref="DescriptorWrite"/> equals one
/// <c>VkWriteDescriptorSet</c> with <c>descriptorCount = 1</c>; pass
/// many through a span when a binding takes multiple writes (bindless
/// arrays, per-pass push descriptors with heterogeneous bindings).
/// </summary>
/// <remarks>
/// <para><b>Why a non-templated path.</b> The templated
/// <see cref="DescriptorTemplate{T}"/> requires a fixed struct shape per
/// call site. Two engine patterns don't fit that shape:</para>
/// <list type="bullet">
///   <item><description><b>Bindless arrays</b> — write one element at a
///     time with <c>dstArrayElement = i</c>, the binding shape decided
///     by the call.</description></item>
///   <item><description><b>Per-pass push descriptors</b> — heterogeneous
///     bindings whose image views differ per pass. A single template
///     <c>T</c> would need pass-specific structs (one per pass × backend)
///     or a giant union.</description></item>
/// </list>
/// <para>The non-templated path matches Vulkan's <c>vkUpdateDescriptorSets</c>
/// shape directly. Allocation is <c>stackalloc</c> for <c>≤ 8</c> writes,
/// rented via <see cref="System.Buffers.ArrayPool{T}"/> beyond that.</para>
/// <para><b>Layout.</b> The internal payload reuses
/// <see cref="BufferDescriptorWrite"/> / <see cref="ImageDescriptorWrite"/>
/// — both are exact mirrors of <c>VkDescriptorBufferInfo</c> /
/// <c>VkDescriptorImageInfo</c>, so the conversion to native is a
/// pointer cast at use site, not a copy.</para>
/// <para><b>Acceleration-structure writes chain rather than point.</b> A
/// <c>VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR</c> descriptor has no
/// <c>pBufferInfo</c> / <c>pImageInfo</c> form: it is written by chaining a
/// <c>VkWriteDescriptorSetAccelerationStructureKHR</c> into
/// <c>VkWriteDescriptorSet.pNext</c> with both info pointers left null. The
/// handle for that chain node is stored inline on this struct and the node
/// itself is carved by the two call sites alongside the
/// <c>VkWriteDescriptorSet</c> array — see
/// <see cref="DescriptorWriteBuilder.BuildWrites"/>.</para>
/// <para><b>Lifetime the caller owns.</b> Whatever a write references — a
/// buffer, an image view, a sampler, an acceleration structure — must outlive
/// every use of the descriptor set it is written into, not merely the
/// <see cref="DescriptorSetExtensions.Update"/> call. For an acceleration
/// structure that is the strictest case in this file: a destroyed TLAS leaves
/// a bound descriptor pointing at freed memory, and beneath it the BLAS device
/// addresses inside the TLAS's instance data are bare numbers no layer can
/// validate (see
/// <see cref="AccelerationStructure.GetDeviceAddress"/>).</para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly unsafe struct DescriptorWrite
{
    internal enum Kind : byte
    {
        Buffer                = 0,
        Image                 = 1,
        AccelerationStructure = 2,
    }

    internal readonly uint                   _binding;
    internal readonly uint                   _arrayElement;
    internal readonly VkDescriptorType       _type;
    internal readonly Kind                   _kind;
    internal readonly BufferDescriptorWrite  _buffer;
    internal readonly ImageDescriptorWrite   _image;

    // Last on purpose: appending keeps every existing payload field at the
    // offset it already had, so the Buffer/Image pointer-cast trick above is
    // untouched. VkWriteDescriptorSetAccelerationStructureKHR wants a
    // *pointer to* a handle, so the chain node points at this field in place —
    // which is why the two call sites must pin the writes span, exactly as
    // they already do for the buffer and image payloads.
    internal readonly VkAccelerationStructureKHR_T* _accelerationStructure;

    private DescriptorWrite(
        uint                          binding,
        uint                          arrayElement,
        VkDescriptorType              type,
        Kind                          kind,
        in BufferDescriptorWrite      buffer,
        in ImageDescriptorWrite       image,
        VkAccelerationStructureKHR_T* accelerationStructure = null)
    {
        _binding               = binding;
        _arrayElement          = arrayElement;
        _type                  = type;
        _kind                  = kind;
        _buffer                = buffer;
        _image                 = image;
        _accelerationStructure = accelerationStructure;
    }

    /// <summary>
    /// Buffer descriptor write — for <c>UNIFORM_BUFFER</c>,
    /// <c>STORAGE_BUFFER</c>, and the dynamic variants of both.
    /// </summary>
    public static DescriptorWrite Buffer(
        uint                       binding,
        uint                       arrayElement,
        VkDescriptorType           type,
        in BufferDescriptorWrite   info)
        => new(binding, arrayElement, type, Kind.Buffer, in info, default);

    /// <summary>
    /// Image descriptor write — for <c>SAMPLED_IMAGE</c>,
    /// <c>STORAGE_IMAGE</c>, and <c>INPUT_ATTACHMENT</c>. The driver
    /// ignores the sampler slot for these types.
    /// </summary>
    public static DescriptorWrite Image(
        uint                     binding,
        uint                     arrayElement,
        VkDescriptorType         type,
        in ImageDescriptorWrite  info)
        => new(binding, arrayElement, type, Kind.Image, default, in info);

    /// <summary>
    /// Bare-sampler descriptor write — for
    /// <c>VK_DESCRIPTOR_TYPE_SAMPLER</c>. The driver reads only the
    /// sampler slot of the underlying <c>VkDescriptorImageInfo</c>; the
    /// image-info struct's view + layout fields stay zeroed.
    /// </summary>
    public static unsafe DescriptorWrite Sampler(
        uint                       binding,
        in SamplerDescriptorWrite  info)
    {
        // SamplerDescriptorWrite + ImageDescriptorWrite are both 24 B
        // and lay out a VkDescriptorImageInfo with the same offsets;
        // the Sampler factory just reuses the image-write storage with
        // a null view + zero layout.
        var img = new ImageDescriptorWrite(info.Sampler, view: null, layout: 0);
        return new(binding, arrayElement: 0,
                   VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLER, Kind.Image,
                   default, in img);
    }

    /// <summary>
    /// Combined image-sampler descriptor write — for
    /// <c>VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER</c>. The
    /// <paramref name="imageAndSampler"/> must carry both the sampler
    /// and the view; build via
    /// <see cref="ImageDescriptorWrite.Of(VkSampler_T*, in ImageView, VkImageLayout)"/>.
    /// </summary>
    public static DescriptorWrite CombinedImageSampler(
        uint                     binding,
        uint                     arrayElement,
        in ImageDescriptorWrite  imageAndSampler)
        => new(binding, arrayElement,
               VkDescriptorType.VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER, Kind.Image,
               default, in imageAndSampler);

    /// <summary>
    /// Acceleration-structure descriptor write — the binding a ray-query
    /// shader reads a TLAS through.
    /// </summary>
    /// <param name="binding">Binding index in the target set.</param>
    /// <param name="arrayElement">Element within the binding's array.</param>
    /// <param name="structure">
    /// The top-level acceleration structure to bind. It must outlive every use
    /// of the descriptor set, and — because a TLAS's instance entries carry
    /// BLAS device addresses that nothing can validate — every BLAS beneath it
    /// must outlive the TLAS.
    /// </param>
    /// <remarks>
    /// <b>No <c>type</c> parameter</b>, unlike the buffer and image factories
    /// which each cover several descriptor types:
    /// <c>VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR</c> is the only type
    /// this write can have, so accepting one would only create a value the
    /// wrapper would have to reject. Declare the matching binding with that
    /// type on <see cref="DescriptorBinding.Type"/> and budget the pool with a
    /// matching <c>VkDescriptorPoolSize</c>.
    /// </remarks>
    public static DescriptorWrite AccelerationStructure(
        uint                         binding,
        uint                         arrayElement,
        in AccelerationStructure     structure)
        => new(binding, arrayElement,
               VkDescriptorType.VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR,
               Kind.AccelerationStructure,
               default, default, structure.Handle);
}
