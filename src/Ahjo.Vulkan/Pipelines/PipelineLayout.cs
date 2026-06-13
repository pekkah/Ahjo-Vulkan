using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Constraints declared at <c>vkCreatePipelineLayout</c> time — the
/// push-constant ranges and descriptor-set-layout handles the layout was
/// built from. Rides on the <see cref="PipelineLayout"/> struct as its one
/// managed reference field (issue #118; the relaxed
/// <see cref="IVulkanHandle{TSelf}"/> contract permits it), so the data is
/// device-scoped, copies with the handle, and dies with the last copy —
/// no process-global side table, no lock, no raw-pointer key exposed to
/// driver handle reuse. Read by <c>CommandRecorder</c>'s debug-only
/// assertions (PushConstants's range-fits check and BindDescriptorSets's
/// set-layout-matches check). Allocated once per layout at create time —
/// never on a per-frame path.
/// </summary>
internal sealed class PipelineLayoutMetadata
{
    public required PushConstantRange[] PushRanges { get; init; }
    public required nint[] SetLayouts { get; init; }
}

/// <summary>
/// Wrapper handle for a <c>VkPipelineLayout</c>. <c>readonly struct</c> +
/// <see cref="IDisposable"/>; built once and held for the lifetime of the
/// pipelines that reference it.
/// </summary>
public readonly unsafe struct PipelineLayout : IVulkanHandle<PipelineLayout>, IDisposable
{
    public readonly VkPipelineLayout_T* Handle;
    internal readonly VkDevice_T* DeviceHandle;

    // Declared constraints for CommandRecorder's debug assertions. Null for
    // FromRaw-constructed (borrowed) layouts and default — the assertions
    // gracefully no-op in that case.
    internal readonly PipelineLayoutMetadata? Metadata;

    internal PipelineLayout(VkPipelineLayout_T* handle, VkDevice_T* device, PipelineLayoutMetadata? metadata = null)
    {
        Handle       = handle;
        DeviceHandle = device;
        Metadata     = metadata;
        HandleRegistry.TrackCreate(this);
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_PIPELINE_LAYOUT;
    public static PipelineLayout FromRaw(nint handle) => new((VkPipelineLayout_T*)handle, null);
    public ulong RawHandle => (ulong)Handle;
    public bool IsNull => Handle == null;

    /// <inheritdoc/>
    public bool OwnsHandle => DeviceHandle != null;

    /// <summary>
    /// Builds a <see cref="DescriptorTemplate{T}"/> for the per-frame
    /// <c>vkCmdPushDescriptorSetWithTemplate</c> path. The descriptor-set
    /// layout backing <paramref name="set"/> in this pipeline layout must
    /// have been created with
    /// <see cref="DescriptorSetLayoutDescription.PushDescriptor"/>
    /// enabled. Template entries are derived from
    /// <typeparamref name="T"/>'s field offsets matched against
    /// <paramref name="bindings"/> in declaration order.
    /// </summary>
    public DescriptorTemplate<T> CreatePushDescriptorTemplate<T>(
        uint                            set,
        VkPipelineBindPoint             bindPoint,
        ReadOnlySpan<DescriptorBinding> bindings)
        where T : unmanaged
    {
        // FromRaw'd layouts carry no DeviceHandle; the template create call
        // would dispatch through a null device. Fail loudly instead.
        if (DeviceHandle == null)
            throw new InvalidOperationException(
                "PipelineLayout.CreatePushDescriptorTemplate requires an owning device; " +
                "a FromRaw-constructed (borrowed) layout has none.");
        return DescriptorTemplateBuilder.CreateForPush<T>(DeviceHandle, Handle, bindPoint, set, bindings);
    }

    public void Dispose()
    {
        if (Handle == null) return;
        // FromRaw produces a borrowed handle with no DeviceHandle — the
        // caller owns the lifetime; calling vkDestroyPipelineLayout with a
        // null device handle would crash on every loader.
        if (!OwnsHandle) return;
        HandleRegistry.TrackDispose(this);
        Vk.vkDestroyPipelineLayout(DeviceHandle, Handle, null);
    }
}
