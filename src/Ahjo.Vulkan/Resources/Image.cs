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

    /// <summary>
    /// Persistent mapped pointer when a linear-tiled host-visible image was
    /// allocated with <see cref="AllocationFlags.Mapped"/>;
    /// <see langword="null"/> otherwise (the typical case — most images are
    /// optimal-tiled and device-local). Mirrors <see cref="Buffer.PersistentMapped"/>;
    /// VMA writes <c>info.pMappedData</c> through <c>vmaCreateImage</c>'s
    /// allocation-info pointer.
    /// </summary>
    internal readonly void* PersistentMapped;

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
        ImageUsage       usage,
        void*            persistentMapped)
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
        PersistentMapped = persistentMapped;
        HandleRegistry.TrackCreate(this);
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_IMAGE;

    // Valid-by-default (issue #119): a wrapped raw handle reports 1 mip, 1
    // array layer, depth 1 — correct for any single raw VkImage you'd wrap
    // (swapchain images are exactly that), and it makes the whole-image
    // subresource helpers (ImageBarrier.Transition, *Region.WholeImage,
    // GenerateMips, Clear*) read a valid >=1 count without a == 0 ? 1 guard.
    // Width/Height stay 0 — unknown for a bare handle and unused by those
    // helpers' subresource ranges.
    public static Image FromRaw(nint handle) =>
        new((VkImage_T*)handle, null, default, default, 0, 0, 1, 1, 1, ImageUsage.None, null);

    public ulong RawHandle => (ulong)Handle;

    public bool IsNull => Handle == null;

    /// <summary>
    /// <see langword="true"/> when this struct owns the VMA allocation —
    /// i.e. <see cref="Dispose"/> destroys it. <see langword="false"/> for
    /// <see cref="FromRaw"/>-constructed (borrowed) handles (notably
    /// swapchain-owned images) and <c>default</c>.
    /// </summary>
    public bool OwnsHandle => !Owner.IsNull;

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
        // FromRaw produces a borrowed handle with no owning Allocator — the
        // caller owns the lifetime. Crucially, a swapchain-owned image is
        // not a VMA allocation and must never reach vmaDestroyImage; the
        // guard makes the borrow contract real. Skip the destroy.
        if (!OwnsHandle) return;
        HandleRegistry.TrackDispose(this);
        VmaApi.vmaDestroyImage(Owner.Handle, Handle, AllocationHandle);
    }
}
