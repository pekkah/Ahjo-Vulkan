using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Native;

internal static class VulkanLoaderResolver
{
    private const string LibraryName = "vulkan-1";

    [ModuleInitializer]
    [SuppressMessage("Usage", "CA2255",
        Justification = "Registering a DllImportResolver at module load maps the canonical 'vulkan-1' DllImport name to the per-OS loader soname. A static ctor would only fire on first member access, which is too late.")]
    internal static void Register()
    {
        NativeLibrary.SetDllImportResolver(typeof(VulkanLoaderResolver).Assembly, Resolve);
    }

    private static nint Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != LibraryName)
        {
            return nint.Zero;
        }

        foreach (var candidate in CandidatesForCurrentPlatform())
        {
            if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out var handle))
            {
                return handle;
            }
        }

        return nint.Zero;
    }

    private static string[] CandidatesForCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ["vulkan-1.dll", "vulkan-1"];
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return ["libvulkan.dylib", "libvulkan.1.dylib", "libMoltenVK.dylib"];
        }

        return ["libvulkan.so.1", "libvulkan.so"];
    }
}
