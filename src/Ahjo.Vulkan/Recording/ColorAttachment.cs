using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One color attachment for <see cref="CommandRecorder.BeginRendering"/>.
/// Maps onto <c>VkRenderingAttachmentInfo</c> minus the boilerplate
/// (<c>sType</c>, <c>pNext</c>) and the resolve-attachment fields, which
/// land in a follow-up alongside MSAA support.
/// </summary>
public readonly record struct ColorAttachment
{
    public ImageView           View       { get; init; }
    public VkImageLayout       Layout     { get; init; }
    public VkAttachmentLoadOp  LoadOp     { get; init; }
    public VkAttachmentStoreOp StoreOp    { get; init; }
    public VkClearColorValue   ClearColor { get; init; }

    internal unsafe VkRenderingAttachmentInfo ToNative() => new()
    {
        sType       = VkStructureType.VK_STRUCTURE_TYPE_RENDERING_ATTACHMENT_INFO,
        imageView   = View.Handle,
        imageLayout = Layout,
        loadOp      = LoadOp,
        storeOp     = StoreOp,
        clearValue  = new VkClearValue { color = ClearColor },
    };
}
