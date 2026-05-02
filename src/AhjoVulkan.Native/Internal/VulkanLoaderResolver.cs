using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AhjoVulkan.Native;

internal static class VulkanLoaderResolver
{
    private const string LibraryName = "vulkan-1";

    [ModuleInitializer]
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
