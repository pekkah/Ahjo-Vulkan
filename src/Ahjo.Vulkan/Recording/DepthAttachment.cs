using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One depth (or depth-and-stencil) attachment for
/// <see cref="CommandRecorder.BeginRendering"/>. Maps onto
/// <c>VkRenderingAttachmentInfo</c>.
/// </summary>
public readonly record struct DepthAttachment
{
    public ImageView           View       { get; init; }
    public VkImageLayout       Layout     { get; init; }
    public VkAttachmentLoadOp  LoadOp     { get; init; }
    public VkAttachmentStoreOp StoreOp    { get; init; }
    public float               ClearDepth { get; init; }
    public uint                ClearStencil { get; init; }

    internal unsafe VkRenderingAttachmentInfo ToNative() => new()
    {
        sType       = VkStructureType.VK_STRUCTURE_TYPE_RENDERING_ATTACHMENT_INFO,
        imageView   = View.Handle,
        imageLayout = Layout,
        loadOp      = LoadOp,
        storeOp     = StoreOp,
        clearValue  = new VkClearValue
        {
            depthStencil = new VkClearDepthStencilValue { depth = ClearDepth, stencil = ClearStencil },
        },
    };
}
