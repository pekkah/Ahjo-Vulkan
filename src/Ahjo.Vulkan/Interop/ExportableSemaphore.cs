using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// A <c>VkSemaphore</c> created exportable (<c>VkExportSemaphoreCreateInfo</c>)
/// so its payload can be shared with another GPU API for the cross-process
/// sync handshake — the semaphore the compositor waits on before sampling the
/// <see cref="ExportableImage"/> the engine just rendered. Binary or timeline;
/// <see cref="ExportOpaqueWin32Handle"/> / <see cref="ExportOpaqueFd"/> pull
/// the OS handle.
/// </summary>
/// <remarks>
/// <para><b>Owned, unlike pooled semaphores.</b>
/// <see cref="BinarySemaphore"/> / <see cref="TimelineSemaphore"/> are owned
/// by <see cref="SemaphorePool"/> and never destroy themselves. An exportable
/// semaphore has a distinct, caller-managed lifetime, so it is its own
/// owning type; <see cref="Dispose"/> calls <c>vkDestroySemaphore</c>.
/// Use <see cref="AsBinary"/> / <see cref="AsTimeline"/> to get a borrowed
/// handle for submits and CPU signal/wait.</para>
/// <para><b>Extensions.</b> The owning <see cref="Device"/> must have enabled
/// the handle type's export extension:
/// <see cref="VulkanExtensions.KhrExternalSemaphoreWin32"/> for
/// <see cref="ExternalHandleType.OpaqueWin32"/>,
/// <see cref="VulkanExtensions.KhrExternalSemaphoreFd"/> for
/// <see cref="ExternalHandleType.OpaqueFd"/>.</para>
/// <para><b>Lifetime.</b> <c>default(ExportableSemaphore)</c> is a legal null
/// handle. Double-dispose is undefined behavior. Each <c>Export*</c> call
/// returns a fresh OS handle/fd the caller owns and must close.</para>
/// </remarks>
public readonly unsafe struct ExportableSemaphore : IDisposable
{
    public readonly VkSemaphore_T* Handle;
    internal readonly VkDevice_T*  DeviceHandle;
    private  readonly Device?      _owner;

    /// <summary><see langword="true"/> for a timeline semaphore, <see langword="false"/> for binary.</summary>
    public readonly bool IsTimeline;

    /// <summary>The resolved handle type the semaphore was made exportable for (never <see cref="ExternalHandleType.Auto"/>).</summary>
    public readonly ExternalHandleType HandleType;

    internal ExportableSemaphore(VkSemaphore_T* handle, Device owner, bool isTimeline, ExternalHandleType handleType)
    {
        Handle       = handle;
        DeviceHandle = owner.Handle;
        _owner       = owner;
        IsTimeline   = isTimeline;
        HandleType   = handleType;
    }

    public bool IsNull => Handle == null;

    /// <summary>Creates an exportable binary semaphore.</summary>
    public static ExportableSemaphore CreateBinary(Device device, ExternalHandleType handleType = ExternalHandleType.Auto)
        => Create(device, isTimeline: false, initialValue: 0, handleType);

    /// <summary>
    /// Creates an exportable timeline semaphore starting at
    /// <paramref name="initialValue"/> (0 is the usual base).
    /// </summary>
    public static ExportableSemaphore CreateTimeline(Device device, ulong initialValue = 0, ExternalHandleType handleType = ExternalHandleType.Auto)
        => Create(device, isTimeline: true, initialValue, handleType);

    private static ExportableSemaphore Create(Device device, bool isTimeline, ulong initialValue, ExternalHandleType handleType)
    {
        ArgumentNullException.ThrowIfNull(device);
        ExternalHandleType resolved = handleType.Resolve();

        var exportInfo = new VkExportSemaphoreCreateInfo
        {
            sType       = VkStructureType.VK_STRUCTURE_TYPE_EXPORT_SEMAPHORE_CREATE_INFO,
            handleTypes = (uint)resolved.ToSemaphoreFlag(),
        };

        // Binary: SemaphoreCreateInfo.pNext -> exportInfo.
        // Timeline: SemaphoreCreateInfo.pNext -> typeInfo -> exportInfo.
        var typeInfo = new VkSemaphoreTypeCreateInfo
        {
            sType         = VkStructureType.VK_STRUCTURE_TYPE_SEMAPHORE_TYPE_CREATE_INFO,
            semaphoreType = VkSemaphoreType.VK_SEMAPHORE_TYPE_TIMELINE,
            initialValue  = initialValue,
            pNext         = &exportInfo,
        };
        var ci = new VkSemaphoreCreateInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO,
            pNext = isTimeline ? (void*)&typeInfo : &exportInfo,
        };

        VkSemaphore_T* raw = null;
        Vk.vkCreateSemaphore(device.Handle, &ci, null, &raw).ThrowIfFailed();
        return new ExportableSemaphore(raw, device, isTimeline, resolved);
    }

    /// <summary>
    /// Borrowed <see cref="BinarySemaphore"/> view for feeding submits. Throws
    /// when this is a timeline semaphore. The returned handle owns no lifetime.
    /// </summary>
    public BinarySemaphore AsBinary()
    {
        ThrowIfNull();
        if (IsTimeline)
            throw new InvalidOperationException("AsBinary called on a timeline ExportableSemaphore; use AsTimeline.");
        return new BinarySemaphore(Handle);
    }

    /// <summary>
    /// Borrowed <see cref="TimelineSemaphore"/> view for submits and CPU
    /// signal/wait (it carries the owning device). Throws when this is a
    /// binary semaphore. The returned handle owns no lifetime.
    /// </summary>
    public TimelineSemaphore AsTimeline()
    {
        ThrowIfNull();
        if (!IsTimeline)
            throw new InvalidOperationException("AsTimeline called on a binary ExportableSemaphore; use AsBinary.");
        return new TimelineSemaphore(Handle, _owner!);
    }

    /// <summary>
    /// Exports the semaphore payload as a Win32 NT <c>HANDLE</c> via
    /// <c>vkGetSemaphoreWin32HandleKHR</c>. Caller owns the handle and must
    /// <c>CloseHandle</c> it. Valid only when <see cref="HandleType"/> is
    /// <see cref="ExternalHandleType.OpaqueWin32"/>.
    /// </summary>
    public nint ExportOpaqueWin32Handle()
    {
        ThrowIfNull();
        if (HandleType != ExternalHandleType.OpaqueWin32)
            throw new InvalidOperationException(
                $"ExportOpaqueWin32Handle requires HandleType OpaqueWin32; this semaphore is {HandleType}.");

        var info = new VkSemaphoreGetWin32HandleInfoKHR
        {
            sType      = VkStructureType.VK_STRUCTURE_TYPE_SEMAPHORE_GET_WIN32_HANDLE_INFO_KHR,
            semaphore  = Handle,
            handleType = VkExternalSemaphoreHandleTypeFlagBits.VK_EXTERNAL_SEMAPHORE_HANDLE_TYPE_OPAQUE_WIN32_BIT,
        };
        var vkGetSemaphoreWin32HandleKHR =
            (delegate* unmanaged[Stdcall]<VkDevice_T*, VkSemaphoreGetWin32HandleInfoKHR*, nint*, VkResult>)
            DeviceExtensionProcs.Load(DeviceHandle, "vkGetSemaphoreWin32HandleKHR"u8);
        nint handle = 0;
        vkGetSemaphoreWin32HandleKHR(DeviceHandle, &info, &handle).ThrowIfFailed();
        return handle;
    }

    /// <summary>
    /// Exports the semaphore payload as a POSIX file descriptor via
    /// <c>vkGetSemaphoreFdKHR</c>. Caller owns the fd and must <c>close</c>
    /// it. Valid only when <see cref="HandleType"/> is
    /// <see cref="ExternalHandleType.OpaqueFd"/>.
    /// </summary>
    public int ExportOpaqueFd()
    {
        ThrowIfNull();
        if (HandleType != ExternalHandleType.OpaqueFd)
            throw new InvalidOperationException(
                $"ExportOpaqueFd requires HandleType OpaqueFd; this semaphore is {HandleType}.");

        var info = new VkSemaphoreGetFdInfoKHR
        {
            sType      = VkStructureType.VK_STRUCTURE_TYPE_SEMAPHORE_GET_FD_INFO_KHR,
            semaphore  = Handle,
            handleType = VkExternalSemaphoreHandleTypeFlagBits.VK_EXTERNAL_SEMAPHORE_HANDLE_TYPE_OPAQUE_FD_BIT,
        };
        var vkGetSemaphoreFdKHR =
            (delegate* unmanaged[Stdcall]<VkDevice_T*, VkSemaphoreGetFdInfoKHR*, int*, VkResult>)
            DeviceExtensionProcs.Load(DeviceHandle, "vkGetSemaphoreFdKHR"u8);
        int fd = -1;
        vkGetSemaphoreFdKHR(DeviceHandle, &info, &fd).ThrowIfFailed();
        return fd;
    }

    public void Dispose()
    {
        if (Handle == null) return;
        Vk.vkDestroySemaphore(DeviceHandle, Handle, null);
    }

    private void ThrowIfNull()
    {
        if (IsNull)
            throw new InvalidOperationException("ExportableSemaphore is a null handle.");
    }
}
