using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Vma.Native;
using VmaApi = Ahjo.Vulkan.Vma.Native.Vma;

namespace Ahjo.Vulkan;

/// <summary>
/// A <c>VkImage</c> paired with the VMA allocation that backs it and a
/// reference to the <see cref="Allocator"/> that produced both. Always
/// VMA-allocated; the wrapper has no raw <c>VkImage</c> path. Pairing all
/// three on one struct keeps disposal and view creation local — the caller
/// never has to thread the allocator separately.
/// </summary>
/// <remarks>
/// <para><c>default(Image)</c> is a legal null handle: <see cref="IsNull"/>
/// returns <see langword="true"/> and <see cref="Dispose"/> is a no-op.
/// Double-dispose is undefined behavior — the struct can't null its own
/// fields (they're <c>readonly</c>).</para>
/// <para>Layout tracking is deliberately not on this struct. Layout is a
/// pipeline-stage concern owned by the recorder (issue 17 — pipeline
/// barriers); pushing it onto the handle would either lie (mutating a
/// readonly struct via copy-by-value) or force every consumer to thread
/// the layout through their data flow.</para>
/// </remarks>
public readonly unsafe struct Image : IVulkanHandle<Image>, IDisposable
{
    public readonly VkImage_T*       Handle;
    public readonly VmaAllocation_T* AllocationHandle;
    public readonly Allocator        Owner;
    public readonly VkFormat         Format;
    public readonly uint             Width;
    public readonly uint             Height;
    public readonly uint             Depth;
    public readonly uint             MipLevels;
    public readonly uint             ArrayLayers;
    public readonly ImageUsage       Usage;

    internal Image(
        VkImage_T*       handle,
        VmaAllocation_T* allocation,
        Allocator        owner,
        VkFormat         format,
        uint             width,
        uint             height,
        uint             depth,
        uint             mipLevels,
        uint             arrayLayers,
        ImageUsage       usage)
    {
        Handle           = handle;
        AllocationHandle = allocation;
        Owner            = owner;
        Format           = format;
        Width            = width;
        Height           = height;
        Depth            = depth;
        MipLevels        = mipLevels;
        ArrayLayers      = arrayLayers;
        Usage            = usage;
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_IMAGE;

    public static Image FromRaw(nint handle) =>
        new((VkImage_T*)handle, null, default, default, 0, 0, 0, 0, 0, ImageUsage.None);

    public ulong RawHandle => (ulong)Handle;

    public bool IsNull => Handle == null;

    /// <summary>
    /// Creates an <see cref="ImageView"/> over this image. Caller owns the
    /// returned view's lifetime — dispose it before this <see cref="Image"/>
    /// goes away.
    /// </summary>
    public ImageView CreateView(Device device, in ImageViewDescription view)
    {
        var range = new VkImageSubresourceRange
        {
            aspectMask     = (uint)view.Aspect,
            baseMipLevel   = view.BaseMipLevel,
            levelCount     = view.LevelCount,
            baseArrayLayer = view.BaseArrayLayer,
            layerCount     = view.LayerCount,
        };
        var ci = new VkImageViewCreateInfo
        {
            sType            = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO,
            image            = Handle,
            viewType         = view.ViewType,
            format           = view.Format == default ? Format : view.Format,
            components       = default, // VK_COMPONENT_SWIZZLE_IDENTITY = 0, identity for all four channels.
            subresourceRange = range,
        };

        VkImageView_T* raw = null;
        Vk.vkCreateImageView(device.Handle, &ci, null, &raw).ThrowIfFailed();
        return new ImageView(raw, device.Handle);
    }

    public void Dispose()
    {
        if (Handle == null) return;
        VmaApi.vmaDestroyImage(Owner.Handle, Handle, AllocationHandle);
    }
}
