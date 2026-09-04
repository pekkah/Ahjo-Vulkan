using System.Runtime.InteropServices;
using Ahjo.Vulkan.Ngx.Native;

namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// Loads the platform Vulkan loader and pulls raw export addresses out of it.
/// </summary>
/// <remarks>
/// <para>Mirrors <c>Allocator.LoadVulkanLoader</c>
/// (<c>src/Ahjo.Vulkan/Memory/Allocator.cs:342-364</c>), and for the same
/// reason: NGX needs raw <c>vkGetInstanceProcAddr</c> /
/// <c>vkGetDeviceProcAddr</c> function pointers, and <c>[DllImport]</c> static
/// methods do not expose theirs (CS8757). So the loader DLL is re-loaded and
/// the exports read directly. The loader is reference-counted by the OS —
/// this handle and the resolver's handle point at the same image.</para>
/// <para>The candidate list is narrower than the allocator's: NGX ships for
/// <c>win-x64</c> and <c>linux-x64</c> only, so there is no macOS/MoltenVK row
/// to carry.</para>
/// </remarks>
internal static unsafe class NgxLoader
{
    /// <summary>
    /// Loads the Vulkan loader. The caller owns the returned OS handle and
    /// must <see cref="NativeLibrary.Free"/> it.
    /// </summary>
    internal static nint Load()
    {
        string[] candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ["vulkan-1.dll", "vulkan-1"]
            : ["libvulkan.so.1", "libvulkan.so"];

        foreach (string candidate in candidates)
        {
            if (NativeLibrary.TryLoad(candidate, out nint handle))
                return handle;
        }

        throw new NgxException(
            NVSDK_NGX_Result.NVSDK_NGX_Result_FAIL_NotInitialized,
            $"Vulkan loader not present on this host (tried: {string.Join(", ", candidates)}). " +
            "NGX dispatches through vkGetInstanceProcAddr / vkGetDeviceProcAddr taken from it.");
    }

    /// <summary>
    /// Resolves one export by name, throwing <see cref="NgxException"/> rather
    /// than the framework's <c>EntryPointNotFoundException</c> so the message
    /// says which loader and which symbol.
    /// </summary>
    /// <param name="loader">Handle from <see cref="Load"/>.</param>
    /// <param name="name">The symbol name, ASCII.</param>
    internal static void* GetExport(nint loader, string name)
    {
        if (!NativeLibrary.TryGetExport(loader, name, out nint address))
        {
            throw new NgxException(
                NVSDK_NGX_Result.NVSDK_NGX_Result_FAIL_NotInitialized,
                $"The Vulkan loader on this host does not export '{name}'. NGX cannot be initialized without it.");
        }

        return (void*)address;
    }
}
