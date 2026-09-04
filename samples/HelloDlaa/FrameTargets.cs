using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Ngx;

namespace Ahjo.Vulkan.Samples.HelloDlaa;

/// <summary>
/// One frame slot's DLSS-facing images, their views, their
/// <see cref="NgxImage"/>s, and the barrier recipe that moves them between
/// layouts (spec D5, D7).
/// </summary>
/// <remarks>
/// <para><b>Per slot, not shared.</b> With two frames in flight, frame N+1's
/// rasterization into the colour target can overlap frame N's DLSS read of it
/// on the GPU, and nothing orders two submissions against each other except
/// semaphores and barriers. Per-slot sets remove the question:
/// <c>FrameRing.BeginFrame</c> has already waited on that slot's fence.</para>
/// <para>The formats and usages are copied from
/// <c>tests/Ahjo.Vulkan.Ngx.Tests/DlssHardwareTests.cs</c>, which measured them
/// on hardware, plus what the sample needs on top: <c>TransferSrc</c> on colour
/// for the <c>off</c>/<c>bilinear</c> blit, and <c>ColorAttachment</c> on
/// colour and motion vectors because here they are actually rendered into.</para>
/// </remarks>
internal sealed class FrameTargets : IDisposable
{
    private Image _color;
    private Image _motion;
    private Image _depth;
    private Image _presentation;

    private ImageView _colorView;
    private ImageView _motionView;
    private ImageView _depthView;

    private FrameTargets() { }

    public ref readonly Image     Color        => ref _color;
    public ref readonly Image     Presentation => ref _presentation;
    public ref readonly ImageView ColorView    => ref _colorView;
    public ref readonly ImageView MotionView   => ref _motionView;
    public ref readonly ImageView DepthView    => ref _depthView;

    public NgxImage NgxColor         { get; private init; }
    public NgxImage NgxDepth         { get; private init; }
    public NgxImage NgxMotionVectors { get; private init; }
    public NgxImage NgxOutput        { get; private init; }

    public const VkFormat ColorFormat        = VkFormat.VK_FORMAT_R8G8B8A8_UNORM;
    public const VkFormat MotionFormat       = VkFormat.VK_FORMAT_R16G16_SFLOAT;
    public const VkFormat DepthFormat        = VkFormat.VK_FORMAT_D32_SFLOAT;
    public const VkFormat PresentationFormat = VkFormat.VK_FORMAT_R8G8B8A8_UNORM;

    public static FrameTargets Create(
        Device device,
        uint   renderWidth, uint renderHeight,
        uint   outputWidth, uint outputHeight)
    {
        Allocator allocator = device.Allocator;

        // DedicatedMemory on every one of these: #214's recommendation for
        // full-screen targets that live for the session rather than churning
        // through a sub-allocated block.
        var dedicated = new AllocationDescription
        {
            Usage = MemoryUsage.AutoPreferDevice,
            Flags = AllocationFlags.DedicatedMemory,
        };

        Image color = allocator.CreateImage(
            new ImageDescription
            {
                Format = ColorFormat,
                Width  = renderWidth, Height = renderHeight,
                // TransferSrc is for the off/bilinear blit into the
                // presentation image, where DLSS is not in the path.
                Usage  = ImageUsage.Sampled | ImageUsage.ColorAttachment | ImageUsage.TransferSrc,
            },
            in dedicated);

        Image motion = allocator.CreateImage(
            new ImageDescription
            {
                Format = MotionFormat,
                Width  = renderWidth, Height = renderHeight,
                Usage  = ImageUsage.Sampled | ImageUsage.ColorAttachment,
            },
            in dedicated);

        Image depth = allocator.CreateImage(
            new ImageDescription
            {
                Format = DepthFormat,
                Width  = renderWidth, Height = renderHeight,
                Usage  = ImageUsage.Sampled | ImageUsage.DepthStencilAttachment,
            },
            in dedicated);

        Image presentation = allocator.CreateImage(
            new ImageDescription
            {
                Format = PresentationFormat,
                Width  = outputWidth, Height = outputHeight,
                // TransferDst is NOT optional. DLSS clears this image itself
                // with vkCmdClearColorImage, and the validation layer is what
                // says so — VUID-vkCmdClearColorImage-image-00002, measured on
                // an RTX 4070 Ti / driver 610.47 (#218 D3). The wrapper's own
                // advisory only fires when Storage is ALSO missing, so
                // Storage-without-TransferDst produces no warning at all: just
                // a layer error, on hardware, at evaluate time.
                Usage  = ImageUsage.Storage | ImageUsage.TransferSrc | ImageUsage.TransferDst,
            },
            in dedicated);

        // The sample creates its own views through NgxImage.CreateView rather
        // than NgxImage.Wrap. Wrap's contract is "the description must be the
        // one that created the view", and that is the one thing the wrapper
        // cannot verify — CreateView makes the view and the description agree
        // by construction (NgxImage's documented default).
        return new FrameTargets
        {
            _color        = color,
            _motion       = motion,
            _depth        = depth,
            _presentation = presentation,

            _colorView = color.CreateView(device, new ImageViewDescription
            {
                Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                LevelCount = 1, LayerCount = 1,
            }),
            _motionView = motion.CreateView(device, new ImageViewDescription
            {
                Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                LevelCount = 1, LayerCount = 1,
            }),
            _depthView = depth.CreateView(device, new ImageViewDescription
            {
                Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT,
                LevelCount = 1, LayerCount = 1,
            }),

            NgxColor         = NgxImage.CreateView(device, in color,
                new ImageViewDescription { Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT }),
            NgxDepth         = NgxImage.CreateView(device, in depth,
                new ImageViewDescription { Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT }),
            NgxMotionVectors = NgxImage.CreateView(device, in motion,
                new ImageViewDescription { Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT }),
            NgxOutput        = NgxImage.CreateView(device, in presentation,
                new ImageViewDescription { Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT }),
        };
    }

    /// <summary>
    /// Spec D7 step 1 — the attachments into their rendering layouts.
    /// <c>UNDEFINED</c> as the source because every one of them is cleared on
    /// load, so nothing in them is worth preserving.
    /// </summary>
    public void RecordPreRasterBarriers(ref CommandRecorder recorder)
    {
        Span<ImageBarrier> barriers =
        [
            ImageBarrier.Transition(
                in _color,
                VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                Stage.AllCommands, Access.None,
                Stage.ColorAttachmentOutput, Access.ColorAttachmentWrite),
            ImageBarrier.Transition(
                in _motion,
                VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                Stage.AllCommands, Access.None,
                Stage.ColorAttachmentOutput, Access.ColorAttachmentWrite),
            ImageBarrier.Transition(
                in _depth,
                VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_ATTACHMENT_OPTIMAL,
                Stage.AllCommands, Access.None,
                Stage.EarlyFragmentTests | Stage.LateFragmentTests, Access.DepthStencilAttachmentWrite,
                VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT),
        ];

        recorder.PipelineBarrier(barriers);
    }

    /// <summary>
    /// Spec D7 step 5, and the load-bearing one: the layout contract DLSS
    /// requires and the wrapper deliberately cannot enforce (#218 D4). Inputs
    /// to a shader-read layout, output to <c>GENERAL</c>. Reproduces
    /// <c>DlssHardwareTests.RecordLayoutTransitions</c>.
    /// </summary>
    public void RecordPreEvaluateBarriers(ref CommandRecorder recorder)
    {
        Span<ImageBarrier> barriers =
        [
            ImageBarrier.Transition(
                in _color,
                VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                Stage.ColorAttachmentOutput, Access.ColorAttachmentWrite,
                Stage.ComputeShader, Access.ShaderRead),
            ImageBarrier.Transition(
                in _motion,
                VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                Stage.ColorAttachmentOutput, Access.ColorAttachmentWrite,
                Stage.ComputeShader, Access.ShaderRead),
            ImageBarrier.Transition(
                in _depth,
                VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_ATTACHMENT_OPTIMAL, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                Stage.LateFragmentTests, Access.DepthStencilAttachmentWrite,
                Stage.ComputeShader, Access.ShaderRead,
                VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT),
            // The destination scope covers BOTH stages that touch this image
            // next: DLSS's compute passes AND its own vkCmdClearColorImage,
            // which is a transfer-stage TRANSFER_WRITE. ComputeShader |
            // ShaderWrite alone would leave that clear outside the barrier's
            // destination scope — a write-after-write the layer may or may not
            // report depending on whether DLSS emits its own barrier first.
            ImageBarrier.Transition(
                in _presentation,
                VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_GENERAL,
                Stage.AllCommands, Access.None,
                Stage.ComputeShader | Stage.AllTransfer, Access.ShaderWrite | Access.TransferWrite),
        ];

        recorder.PipelineBarrier(barriers);
    }

    /// <summary>
    /// The <c>off</c>/<c>bilinear</c> variant of
    /// <see cref="RecordPreEvaluateBarriers"/>: no DLSS in the path, so colour
    /// goes to <c>TRANSFER_SRC</c> and the presentation image to
    /// <c>TRANSFER_DST</c> for a plain blit. Depth and motion vectors are
    /// rendered but unread — that is the point of the control mode.
    /// </summary>
    public void RecordPreUpscaleBlitBarriers(ref CommandRecorder recorder)
    {
        Span<ImageBarrier> barriers =
        [
            ImageBarrier.Transition(
                in _color,
                VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                Stage.ColorAttachmentOutput, Access.ColorAttachmentWrite,
                Stage.AllTransfer, Access.TransferRead),
            ImageBarrier.Transition(
                in _presentation,
                VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                Stage.AllCommands, Access.None,
                Stage.AllTransfer, Access.TransferWrite),
        ];

        recorder.PipelineBarrier(barriers);
    }

    /// <summary>
    /// Spec D7 step 7's presentation-image half: whatever wrote it, it is now
    /// a blit source. <paramref name="fromGeneral"/> distinguishes the DLSS
    /// path (which left it in <c>GENERAL</c>) from the blit path (which left it
    /// in <c>TRANSFER_DST_OPTIMAL</c>).
    /// </summary>
    public void RecordPreBlitBarriers(ref CommandRecorder recorder, bool fromGeneral)
    {
        recorder.PipelineBarrier(ImageBarrier.Transition(
            in _presentation,
            fromGeneral
                ? VkImageLayout.VK_IMAGE_LAYOUT_GENERAL
                : VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
            VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
            fromGeneral ? Stage.ComputeShader | Stage.AllTransfer : Stage.AllTransfer,
            fromGeneral ? Access.ShaderWrite | Access.TransferWrite : Access.TransferWrite,
            Stage.AllTransfer, Access.TransferRead));
    }

    public void Dispose()
    {
        // The NgxImages own their views, so they go first.
        NgxColor.Dispose();
        NgxDepth.Dispose();
        NgxMotionVectors.Dispose();
        NgxOutput.Dispose();

        _colorView.Dispose();
        _motionView.Dispose();
        _depthView.Dispose();

        _color.Dispose();
        _motion.Dispose();
        _depth.Dispose();
        _presentation.Dispose();
    }
}
