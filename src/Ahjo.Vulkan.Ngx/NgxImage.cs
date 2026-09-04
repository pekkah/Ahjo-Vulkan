using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Ngx.Native;

namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// One image bound to a DLSS slot: the <c>VkImage</c>, the <c>VkImageView</c>
/// over it, the subresource range that view covers, and the format and extent
/// NGX needs alongside them.
/// </summary>
/// <remarks>
/// <para><b>Why this type exists.</b> <c>NVSDK_NGX_ImageViewInfo_VK</c> wants six
/// correlated facts about one view, and <c>Ahjo.Vulkan</c>'s
/// <see cref="ImageView"/> is two pointers that carry none of them — the range
/// lives on the <see cref="ImageViewDescription"/> that
/// <see cref="Image.CreateView"/> throws away. Taking
/// <c>(image, view, range)</c> as three parameters would let every call site
/// disagree with itself, with nothing able to detect it: Vulkan has no query
/// that recovers a view's range. So this type is the <b>only</b> producer of
/// that struct, and both factories derive the range from the same description
/// that describes the view (spec D2).</para>
/// <para><b><c>ReadWrite</c> is deliberately not a member.</b> The wrapper sets
/// it from the slot — <see langword="false"/> for every input,
/// <see langword="true"/> for <see cref="DlssEvaluateInputs.Output"/> — so
/// "read-write on an image without <c>VK_IMAGE_USAGE_STORAGE_BIT</c>" is not a
/// sentence this API can say (spec D3).</para>
/// <para>A plain <c>readonly struct</c>, not a <c>record struct</c>: it carries
/// pointers, and a synthesized record <c>Equals</c> would need them as generic
/// type arguments (spec E16). <c>default(NgxImage)</c> is a legal null —
/// <see cref="IsNull"/> is <see langword="true"/> and <see cref="Dispose"/> a
/// no-op — which is how the optional slots say "none".</para>
/// </remarks>
public readonly unsafe struct NgxImage : IDisposable
{
    internal readonly VkImage_T*              ImageHandle;
    internal readonly VkImageSubresourceRange Range;
    internal readonly VkFormat                Format;
    internal readonly uint                    Width;
    internal readonly uint                    Height;
    internal readonly ImageUsage              Usage;

    // The Image/ImageView OwnsHandle split, reused rather than reinvented:
    // CreateView stores the owning ImageView it made, so Dispose runs the
    // wrapper's own teardown (including the double-dispose registry); Wrap
    // stores a FromRaw borrow, whose Dispose is already a no-op.
    private readonly ImageView _view;

    private NgxImage(
        in Image image,
        ImageView view,
        in ImageViewDescription description)
    {
        ImageHandle = image.Handle;
        _view       = view;

        Range = new VkImageSubresourceRange
        {
            aspectMask     = (uint)description.Aspect,
            baseMipLevel   = description.BaseMipLevel,
            // The VK_REMAINING_* sentinels are resolved to concrete counts
            // before the range reaches NGX. Nothing documents how the feature
            // DLL consumes a subresource range, a concrete count is exactly
            // equivalent for every Vulkan use of one, and Image.FromRaw reports
            // 1/1 — the right answer for a swapchain image. Spec OPEN-4: this
            // is an inference, and the first assumption to re-examine if a DLSS
            // binding is ever traced to a range fault.
            levelCount     = description.LevelCount == Vk.VK_REMAINING_MIP_LEVELS
                                 ? image.MipLevels - description.BaseMipLevel
                                 : description.LevelCount,
            baseArrayLayer = description.BaseArrayLayer,
            layerCount     = description.LayerCount == Vk.VK_REMAINING_ARRAY_LAYERS
                                 ? image.ArrayLayers - description.BaseArrayLayer
                                 : description.LayerCount,
        };

        // Same fallback Image.CreateView applies: an undefined format override
        // means "inherit the image's".
        Format = description.Format == default ? image.Format : description.Format;
        Width  = image.Width;
        Height = image.Height;
        Usage  = image.Usage;
    }

    /// <summary>
    /// Direct construction from already-resolved values, with no device and no
    /// live Vulkan object behind the handles.
    /// </summary>
    /// <remarks>
    /// The seam the validation tests drive, so the
    /// <see cref="AhjoValidation.Enabled"/>-gated checks in
    /// <see cref="DlssFeature.Evaluate"/> are provable on a host with no Vulkan
    /// driver at all (spec D13). <c>internal</c> and it stays that way:
    /// production code goes through <see cref="CreateView"/> or
    /// <see cref="Wrap"/>, which is what makes the view/image/range triple
    /// unable to disagree.
    /// </remarks>
    internal static NgxImage FromRaw(nint image, nint view, uint width, uint height, ImageUsage usage, VkFormat format)
        => new((VkImage_T*)image, ImageView.FromRaw(view), width, height, usage, format);

    private NgxImage(VkImage_T* image, ImageView view, uint width, uint height, ImageUsage usage, VkFormat format)
    {
        ImageHandle = image;
        _view       = view;
        Range       = default;
        Format      = format;
        Width       = width;
        Height      = height;
        Usage       = usage;
    }

    /// <summary>The view handle NGX is handed.</summary>
    internal VkImageView_T* ViewHandle => _view.Handle;

    /// <summary>
    /// Creates a view over <paramref name="image"/> from
    /// <paramref name="view"/> and binds both to a DLSS slot. The returned
    /// value <b>owns</b> the view and destroys it on <see cref="Dispose"/>.
    /// </summary>
    /// <remarks>
    /// The documented default. Nothing can get out of step here: the same
    /// description creates the view and describes the range.
    /// </remarks>
    public static NgxImage CreateView(Device device, in Image image, in ImageViewDescription view)
    {
        ArgumentNullException.ThrowIfNull(device);

        return new NgxImage(in image, image.CreateView(device, view), in view);
    }

    /// <summary>
    /// Binds an existing view to a DLSS slot without taking ownership.
    /// <see cref="Dispose"/> is a no-op.
    /// </summary>
    /// <param name="viewDescription">
    /// <b>Must be the description that created <paramref name="view"/>.</b>
    /// This is the one contract the compiler cannot check and the wrapper
    /// cannot verify — Vulkan exposes no query that recovers a
    /// <c>VkImageView</c>'s subresource range, format or view type. Get it
    /// wrong and DLSS reads the wrong subresource with no diagnostic.
    /// </param>
    /// <remarks>
    /// Exists because a renderer usually already has an attachment view for its
    /// colour, depth and motion-vector targets, and creating a second one per
    /// DLSS-bound target is pure waste. Prefer <see cref="CreateView"/> when
    /// you do not already have a view.
    /// </remarks>
    public static NgxImage Wrap(in Image image, in ImageView view, in ImageViewDescription viewDescription)
        // FromRaw drops the device handle, which is exactly what makes the
        // borrow real: ImageView.Dispose is already a no-op without one.
        => new(in image, ImageView.FromRaw((nint)view.Handle), in viewDescription);

    /// <summary><see langword="true"/> for <c>default(NgxImage)</c> — the
    /// "slot not bound" state for the optional inputs.</summary>
    public bool IsNull => _view.IsNull;

    /// <summary><see langword="true"/> when <see cref="Dispose"/> destroys the
    /// view: <see cref="CreateView"/> yes, <see cref="Wrap"/> no.</summary>
    public bool OwnsView => _view.OwnsHandle;

    /// <summary>
    /// The <c>NVSDK_NGX_Resource_VK</c> this image denotes.
    /// </summary>
    /// <param name="readWrite">
    /// Set by the caller from the <i>slot</i>, never by the application — see
    /// the type's remarks. Written as a C# <see langword="bool"/>: Phase 1
    /// measured the native field as <c>_Bool</c> generating to <c>bool</c>,
    /// against #214's prose (#216 E11).
    /// </param>
    internal NVSDK_NGX_Resource_VK ToNative(bool readWrite)
    {
        NVSDK_NGX_Resource_VK resource = default;
        resource.Type      = NVSDK_NGX_Resource_VK_Type.NVSDK_NGX_RESOURCE_VK_TYPE_VK_IMAGEVIEW;
        resource.ReadWrite = readWrite;

        ref NVSDK_NGX_ImageViewInfo_VK info = ref resource.Resource.ImageViewInfo;
        info.ImageView        = ViewHandle;
        info.Image            = ImageHandle;
        info.SubresourceRange = Range;
        info.Format           = Format;
        info.Width            = Width;
        info.Height           = Height;
        return resource;
    }

    /// <summary>
    /// Destroys the view when this value created it; otherwise a no-op.
    /// Never touches the <see cref="Image"/> — that is the caller's.
    /// </summary>
    public void Dispose() => _view.Dispose();
}
