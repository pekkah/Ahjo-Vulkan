using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Selects the OS handle flavor an <see cref="ExportableImage"/> /
/// <see cref="ExportableSemaphore"/> is created for and exported as. Mirrors
/// the two handle types cross-API compositors (Avalonia's
/// <c>ImportGpuImage</c>, D3D shared resources) accept:
/// <see cref="OpaqueWin32"/> (an NT <c>HANDLE</c>) on Windows and
/// <see cref="OpaqueFd"/> (a POSIX file descriptor) on Linux.
/// </summary>
public enum ExternalHandleType
{
    /// <summary>
    /// Pick the platform default: <see cref="OpaqueWin32"/> on Windows,
    /// <see cref="OpaqueFd"/> on Linux. The common case — a consumer on the
    /// same machine wants the handle its OS understands.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// <c>VK_EXTERNAL_MEMORY_HANDLE_TYPE_OPAQUE_WIN32_BIT</c> /
    /// <c>VK_EXTERNAL_SEMAPHORE_HANDLE_TYPE_OPAQUE_WIN32_BIT</c>. Exports an
    /// NT <c>HANDLE</c>; requires <c>VK_KHR_external_memory_win32</c> /
    /// <c>VK_KHR_external_semaphore_win32</c> on the device.
    /// </summary>
    OpaqueWin32,

    /// <summary>
    /// <c>VK_EXTERNAL_MEMORY_HANDLE_TYPE_OPAQUE_FD_BIT</c> /
    /// <c>VK_EXTERNAL_SEMAPHORE_HANDLE_TYPE_OPAQUE_FD_BIT</c>. Exports a file
    /// descriptor; requires <c>VK_KHR_external_memory_fd</c> /
    /// <c>VK_KHR_external_semaphore_fd</c> on the device.
    /// </summary>
    OpaqueFd,
}

internal static class ExternalHandleTypeExtensions
{
    /// <summary>Resolves <see cref="ExternalHandleType.Auto"/> to the platform default.</summary>
    internal static ExternalHandleType Resolve(this ExternalHandleType type)
    {
        if (type != ExternalHandleType.Auto)
            return type;
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ExternalHandleType.OpaqueWin32
            : ExternalHandleType.OpaqueFd;
    }

    internal static VkExternalMemoryHandleTypeFlagBits ToMemoryFlag(this ExternalHandleType type) => type.Resolve() switch
    {
        ExternalHandleType.OpaqueWin32 => VkExternalMemoryHandleTypeFlagBits.VK_EXTERNAL_MEMORY_HANDLE_TYPE_OPAQUE_WIN32_BIT,
        ExternalHandleType.OpaqueFd    => VkExternalMemoryHandleTypeFlagBits.VK_EXTERNAL_MEMORY_HANDLE_TYPE_OPAQUE_FD_BIT,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported external handle type."),
    };

    internal static VkExternalSemaphoreHandleTypeFlagBits ToSemaphoreFlag(this ExternalHandleType type) => type.Resolve() switch
    {
        ExternalHandleType.OpaqueWin32 => VkExternalSemaphoreHandleTypeFlagBits.VK_EXTERNAL_SEMAPHORE_HANDLE_TYPE_OPAQUE_WIN32_BIT,
        ExternalHandleType.OpaqueFd    => VkExternalSemaphoreHandleTypeFlagBits.VK_EXTERNAL_SEMAPHORE_HANDLE_TYPE_OPAQUE_FD_BIT,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported external handle type."),
    };
}

/// <summary>
/// Resolves device-extension export entry points through
/// <c>vkGetDeviceProcAddr</c>. The Khronos loader is not required to
/// statically export device-extension functions (unlike the WSI surface
/// functions the <c>Manual/</c> bindings <c>[DllImport]</c>), so
/// <c>vkGetMemoryWin32HandleKHR</c> / <c>vkGetMemoryFdKHR</c> /
/// <c>vkGetSemaphore*HandleKHR</c> have to be looked up at runtime — a plain
/// <c>[DllImport]</c> would throw <see cref="EntryPointNotFoundException"/>
/// on the loaders that omit them.
/// </summary>
internal static unsafe class DeviceExtensionProcs
{
    /// <summary>
    /// Looks up <paramref name="name"/> (a null-terminated UTF-8 literal) on
    /// <paramref name="device"/>. Throws when the loader returns null — the
    /// export extension was not enabled on the device.
    /// </summary>
    internal static delegate* unmanaged[Stdcall]<void> Load(VkDevice_T* device, ReadOnlySpan<byte> name)
    {
        delegate* unmanaged[Stdcall]<void> fn = Vk.vkGetDeviceProcAddr(device, Utf8Name.FromLiteral(name).Ptr);
        if (fn == null)
            throw new VulkanException(VkResult.VK_ERROR_EXTENSION_NOT_PRESENT,
                System.Text.Encoding.UTF8.GetString(name));
        return fn;
    }
}
