using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Ngx;

namespace Ahjo.Vulkan.Samples.HelloFrameGen;

/// <summary>
/// One frame slot's Frame Generation images, their views, their
/// <see cref="NgxImage"/>s, and the barrier recipe that moves them between
/// layouts.
/// </summary>
/// <remarks>
/// <para><b>Per slot, not shared.</b> With two frames in flight, frame N+1's
/// rendering can overlap frame N's evaluate and present blits on the GPU, and
/// nothing orders two submissions against each other except semaphores and
/// barriers. Per-slot sets remove the question: <c>FrameRing.BeginFrame</c> has
/// already waited on that slot's fence, and that fence covers both of the
/// frame's submits (spec E6).</para>
/// <para>Every image is <see cref="AllocationFlags.DedicatedMemory"/>: full-
/// screen, session-lifetime targets (#214). The usages are the plan's (#253
/// step 17), checked by the validation layer in step 18:</para>
/// <list type="bullet">
///   <item><description><see cref="Backbuffer"/> needs
///   <see cref="ImageUsage.TransferSrc"/> because, with <c>OutputReal</c>
///   bound, Frame Generation fills <c>OutputReal</c> with a
///   <c>vkCmdCopyImage</c> from it (PR B, <c>VUID-vkCmdCopyImage-aspect-06662</c>
///   without it); the <c>--mode off</c> present blit reads it too.</description></item>
///   <item><description><see cref="OutputReal"/> needs
///   <see cref="ImageUsage.TransferDst"/> as the destination of that copy
///   (<c>VUID-vkCmdCopyImage-aspect-06663</c>).</description></item>
///   <item><description><see cref="Ui"/> is R16G16B16A16_SFLOAT: the guide
///   says RGB10A2 is not precise enough for UI (l. 753-754).</description></item>
/// </list>
/// </remarks>
internal sealed class FrameGenTargets : IDisposable
{
    public const VkFormat ColorFormat  = VkFormat.VK_FORMAT_R8G8B8A8_UNORM;
    public const VkFormat UiFormat     = VkFormat.VK_FORMAT_R16G16B16A16_SFLOAT;
    public const VkFormat MotionFormat = VkFormat.VK_FORMAT_R16G16_SFLOAT;
    public const VkFormat DepthFormat  = VkFormat.VK_FORMAT_D32_SFLOAT;

    private Image _hudless;
    private Image _ui;
    private Image _backbuffer;
    private Image _depth;
    private Image _motion;
    private Image _outputInterpolated;
    private Image _outputReal;

    private ImageView _hudlessView;
    private ImageView _uiView;
    private ImageView _backbufferView;
    private ImageView _depthView;
    private ImageView _motionView;

    private FrameGenTargets() { }

    public uint Width  { get; private init; }
    public uint Height { get; private init; }

    public ref readonly Image Hudless            => ref _hudless;
    public ref readonly Image Ui                 => ref _ui;
    public ref readonly Image Backbuffer         => ref _backbuffer;
    public ref readonly Image OutputInterpolated => ref _outputInterpolated;
    public ref readonly Image OutputReal         => ref _outputReal;

    public ref readonly ImageView HudlessView    => ref _hudlessView;
    public ref readonly ImageView UiView         => ref _uiView;
    public ref readonly ImageView BackbufferView => ref _backbufferView;
    public ref readonly ImageView DepthView      => ref _depthView;
    public ref readonly ImageView MotionView     => ref _motionView;

    public NgxImage NgxBackbuffer         { get; private init; }
    public NgxImage NgxDepth              { get; private init; }
    public NgxImage NgxMotionVectors      { get; private init; }
    public NgxImage NgxHudless            { get; private init; }
    public NgxImage NgxUi                 { get; private init; }
    public NgxImage NgxOutputInterpolated { get; private init; }
    public NgxImage NgxOutputReal         { get; private init; }

    public static FrameGenTargets Create(Device device, uint width, uint height)
    {
        Allocator allocator = device.Allocator;
        var dedicated = new AllocationDescription
        {
            Usage = MemoryUsage.AutoPreferDevice,
            Flags = AllocationFlags.DedicatedMemory,
        };

        Image hudless = allocator.CreateImage(new ImageDescription
        {
            Format = ColorFormat, Width = width, Height = height,
            Usage  = ImageUsage.ColorAttachment | ImageUsage.Sampled | ImageUsage.TransferSrc,
        }, in dedicated);

        Image ui = allocator.CreateImage(new ImageDescription
        {
            Format = UiFormat, Width = width, Height = height,
            Usage  = ImageUsage.ColorAttachment | ImageUsage.Sampled,
        }, in dedicated);

        Image backbuffer = allocator.CreateImage(new ImageDescription
        {
            Format = ColorFormat, Width = width, Height = height,
            Usage  = ImageUsage.ColorAttachment | ImageUsage.Sampled | ImageUsage.TransferSrc,
        }, in dedicated);

        Image depth = allocator.CreateImage(new ImageDescription
        {
            Format = DepthFormat, Width = width, Height = height,
            Usage  = ImageUsage.DepthStencilAttachment | ImageUsage.Sampled,
        }, in dedicated);

        Image motion = allocator.CreateImage(new ImageDescription
        {
            Format = MotionFormat, Width = width, Height = height,
            Usage  = ImageUsage.ColorAttachment | ImageUsage.Sampled,
        }, in dedicated);

        // TransferSrc: the present blit (and the --capture readback) reads
        // both. TransferDst: OutputReal is the destination of Frame
        // Generation's own vkCmdCopyImage (PR B); OutputInterpolated is given
        // the same set so the two outputs stay interchangeable.
        var outputDescription = new ImageDescription
        {
            Format = ColorFormat, Width = width, Height = height,
            Usage  = ImageUsage.Storage | ImageUsage.TransferSrc | ImageUsage.TransferDst,
        };
        Image outputInterpolated = allocator.CreateImage(in outputDescription, in dedicated);
        Image outputReal         = allocator.CreateImage(in outputDescription, in dedicated);

        var colorView = new ImageViewDescription
        {
            Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT, LevelCount = 1, LayerCount = 1,
        };
        var depthView = new ImageViewDescription
        {
            Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT, LevelCount = 1, LayerCount = 1,
        };

        // The sample creates the NGX-facing views through NgxImage.CreateView
        // rather than NgxImage.Wrap, so the view and its description agree by
        // construction (the HelloDlaa reasoning).
        return new FrameGenTargets
        {
            Width  = width,
            Height = height,

            _hudless            = hudless,
            _ui                 = ui,
            _backbuffer         = backbuffer,
            _depth              = depth,
            _motion             = motion,
            _outputInterpolated = outputInterpolated,
            _outputReal         = outputReal,

            _hudlessView    = hudless.CreateView(device, in colorView),
            _uiView         = ui.CreateView(device, in colorView),
            _backbufferView = backbuffer.CreateView(device, in colorView),
            _depthView      = depth.CreateView(device, in depthView),
            _motionView     = motion.CreateView(device, in colorView),

            NgxBackbuffer         = NgxImage.CreateView(device, in backbuffer, in colorView),
            NgxDepth              = NgxImage.CreateView(device, in depth, in depthView),
            NgxMotionVectors      = NgxImage.CreateView(device, in motion, in colorView),
            NgxHudless            = NgxImage.CreateView(device, in hudless, in colorView),
            NgxUi                 = NgxImage.CreateView(device, in ui, in colorView),
            NgxOutputInterpolated = NgxImage.CreateView(device, in outputInterpolated, in colorView),
            NgxOutputReal         = NgxImage.CreateView(device, in outputReal, in colorView),
        };
    }

    /// <summary>
    /// Every render target into its attachment layout. <c>UNDEFINED</c> as the
    /// source because each one is cleared or fully overwritten this frame.
    /// That also absorbs the measured fact that, with <c>OutputReal</c> bound,
    /// Frame Generation leaves <see cref="Backbuffer"/> in <c>GENERAL</c> after
    /// its evaluate (PR B): nothing here names the previous layout.
    /// </summary>
    public void RecordPreRenderBarriers(ref CommandRecorder recorder)
    {
        Span<ImageBarrier> barriers =
        [
            ImageBarrier.Transition(
                in _hudless,
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
                Stage.EarlyFragmentTests | Stage.LateFragmentTests,
                Access.DepthStencilAttachmentRead | Access.DepthStencilAttachmentWrite,
                VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT),
            ImageBarrier.Transition(
                in _ui,
                VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                Stage.AllCommands, Access.None,
                Stage.ColorAttachmentOutput, Access.ColorAttachmentWrite),
            ImageBarrier.Transition(
                in _backbuffer,
                VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                Stage.AllCommands, Access.None,
                Stage.ColorAttachmentOutput, Access.ColorAttachmentWrite),
        ];
        recorder.PipelineBarrier(barriers);
    }

    /// <summary>
    /// <see cref="Hudless"/> and <see cref="Ui"/> from attachment to
    /// <c>SHADER_READ_ONLY_OPTIMAL</c>, before the composite samples them.
    /// They stay there through the Frame Generation evaluate, which reads them
    /// in that layout, so the destination scope covers both the composite's
    /// fragment reads and Frame Generation's compute reads.
    /// </summary>
    public void RecordPreCompositeBarriers(ref CommandRecorder recorder)
    {
        Span<ImageBarrier> barriers =
        [
            ImageBarrier.Transition(
                in _hudless,
                VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                Stage.ColorAttachmentOutput, Access.ColorAttachmentWrite,
                Stage.FragmentShader | Stage.ComputeShader, Access.ShaderRead),
            ImageBarrier.Transition(
                in _ui,
                VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                Stage.ColorAttachmentOutput, Access.ColorAttachmentWrite,
                Stage.FragmentShader | Stage.ComputeShader, Access.ShaderRead),
        ];
        recorder.PipelineBarrier(barriers);
    }

    /// <summary>
    /// The layout contract Frame Generation requires and the wrapper cannot
    /// enforce: the remaining inputs to <c>SHADER_READ_ONLY_OPTIMAL</c>, both
    /// outputs to <c>GENERAL</c> (<c>DlssFrameGenerationInputs</c> remarks).
    /// </summary>
    /// <remarks>
    /// The destination scopes include <see cref="Stage.AllTransfer"/> because
    /// Frame Generation does transfer work on these images too: it copies
    /// <see cref="Backbuffer"/> into <see cref="OutputReal"/> with
    /// <c>vkCmdCopyImage</c> (PR B). Compute-only scopes would leave that copy
    /// outside the barrier.
    /// </remarks>
    public void RecordPreEvaluateBarriers(ref CommandRecorder recorder)
    {
        Span<ImageBarrier> barriers =
        [
            ImageBarrier.Transition(
                in _backbuffer,
                VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                Stage.ColorAttachmentOutput, Access.ColorAttachmentWrite,
                Stage.ComputeShader | Stage.AllTransfer, Access.ShaderRead | Access.TransferRead),
            ImageBarrier.Transition(
                in _motion,
                VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                Stage.ColorAttachmentOutput, Access.ColorAttachmentWrite,
                Stage.ComputeShader, Access.ShaderRead),
            ImageBarrier.Transition(
                in _depth,
                VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_ATTACHMENT_OPTIMAL, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                Stage.EarlyFragmentTests | Stage.LateFragmentTests, Access.DepthStencilAttachmentWrite,
                Stage.ComputeShader, Access.ShaderRead,
                VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT),
            ImageBarrier.Transition(
                in _outputInterpolated,
                VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_GENERAL,
                Stage.AllCommands, Access.None,
                Stage.ComputeShader | Stage.AllTransfer, Access.ShaderWrite | Access.TransferWrite),
            ImageBarrier.Transition(
                in _outputReal,
                VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_GENERAL,
                Stage.AllCommands, Access.None,
                Stage.ComputeShader | Stage.AllTransfer, Access.ShaderWrite | Access.TransferWrite),
        ];
        recorder.PipelineBarrier(barriers);
    }

    /// <summary>
    /// Both outputs from <c>GENERAL</c> (where Frame Generation leaves them) to
    /// <c>TRANSFER_SRC_OPTIMAL</c> for the present blits. Same shape as
    /// HelloDlaa's DLSS-output blit barrier.
    /// </summary>
    public void RecordPostEvaluateBarriers(ref CommandRecorder recorder)
    {
        Span<ImageBarrier> barriers =
        [
            ImageBarrier.Transition(
                in _outputInterpolated,
                VkImageLayout.VK_IMAGE_LAYOUT_GENERAL, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                Stage.ComputeShader | Stage.AllTransfer, Access.ShaderWrite | Access.TransferWrite,
                Stage.AllTransfer, Access.TransferRead),
            ImageBarrier.Transition(
                in _outputReal,
                VkImageLayout.VK_IMAGE_LAYOUT_GENERAL, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                Stage.ComputeShader | Stage.AllTransfer, Access.ShaderWrite | Access.TransferWrite,
                Stage.AllTransfer, Access.TransferRead),
        ];
        recorder.PipelineBarrier(barriers);
    }

    /// <summary>
    /// <c>--mode off</c>: no Frame Generation in the path, so
    /// <see cref="Backbuffer"/> goes straight from the composite to
    /// <c>TRANSFER_SRC_OPTIMAL</c> for the single present blit.
    /// </summary>
    public void RecordBackbufferToTransferSource(ref CommandRecorder recorder)
    {
        recorder.PipelineBarrier(ImageBarrier.Transition(
            in _backbuffer,
            VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
            Stage.ColorAttachmentOutput, Access.ColorAttachmentWrite,
            Stage.AllTransfer, Access.TransferRead));
    }

    public void Dispose()
    {
        // The NgxImages own their views, so they go first.
        NgxBackbuffer.Dispose();
        NgxDepth.Dispose();
        NgxMotionVectors.Dispose();
        NgxHudless.Dispose();
        NgxUi.Dispose();
        NgxOutputInterpolated.Dispose();
        NgxOutputReal.Dispose();

        _hudlessView.Dispose();
        _uiView.Dispose();
        _backbufferView.Dispose();
        _depthView.Dispose();
        _motionView.Dispose();

        _hudless.Dispose();
        _ui.Dispose();
        _backbuffer.Dispose();
        _depth.Dispose();
        _motion.Dispose();
        _outputInterpolated.Dispose();
        _outputReal.Dispose();
    }
}
