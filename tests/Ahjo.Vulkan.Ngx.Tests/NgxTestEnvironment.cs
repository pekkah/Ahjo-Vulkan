using Ahjo.Vulkan.Ngx.Native;
using Ahjo.Vulkan.Testing;

namespace Ahjo.Vulkan.Ngx.Tests;

/// <summary>
/// Decides, once per process, what this host can actually prove about DLSS.
/// </summary>
/// <remarks>
/// <para>Two independent facts, because they fail for different reasons and
/// carry different gate classes:</para>
/// <list type="bullet">
///   <item><see cref="ShimPresent"/> — did <c>./tools/setup-ngx.ps1</c> run and
///   did the shim build? Absent on every fresh clone and in CI, because the NGX
///   SDK is proprietary and nothing here downloads it.
///   <c>[gate:platform]</c>.</item>
///   <item><see cref="IsDlssAvailable"/> — is there an NVIDIA GPU with a
///   DLSS-capable driver <i>and</i> a consumer-supplied feature DLL? Never true
///   on a hosted runner; there is no NVIDIA hardware in CI (#32).
///   <c>[gate:feature]</c>.</item>
/// </list>
/// <para>Mirrors <c>tests/Ahjo.Vulkan.Ngx.Native.Tests/NgxShimFixture.cs</c>,
/// with one difference: that suite has <c>AHJO_NGX_REQUIRE_SHIM</c> because its
/// lane exists to execute the shim. This one runs in <c>build-test</c>, whose
/// contract is the wrapper, so an absent shim is a correct skip and never a
/// failure.</para>
/// <para>No reflection anywhere — the subject is AOT-clean code.</para>
/// </remarks>
internal static class NgxTestEnvironment
{
    private static readonly Lazy<bool> _shimPresent = new(ProbeShim);
    private static readonly Lazy<bool> _dlssAvailable = new(ProbeDlss);

    /// <summary>The <c>ahjo_ngx</c> shim loaded.</summary>
    public static bool ShimPresent => _shimPresent.Value;

    /// <summary>The shim loaded, a device exists, and NGX reports DLSS Super
    /// Resolution supported on it.</summary>
    public static bool IsDlssAvailable => _dlssAvailable.Value;

    /// <summary>
    /// A description every test in this suite can use. The project ID is this
    /// repo's own, fixed so a run is reproducible.
    /// </summary>
    public static NgxDescription Description => new()
    {
        ProjectId       = "8d19b5b3-8f7d-4a2f-9f6e-6c2d9a1f4e73",
        EngineVersion   = "0.1.0-tests",
        // ApplicationDataPath deliberately left null: the wrapper materializes
        // the temp path, and a null reaching NGX access-violates (see
        // NgxDescription.ApplicationDataPath). Leaving it unset is what keeps
        // that fix covered.
        DlssSearchPaths = FeatureDllSearchPaths,
    };

    /// <summary>
    /// Where a locally staged feature DLL lives. <c>tools/setup-ngx.ps1</c>
    /// stages <c>rel/</c> under <c>native/ngx/staged/&lt;rid&gt;/</c>, which is
    /// git-ignored and never packed — this is a developer-machine path, not a
    /// deployment one.
    /// </summary>
    public static IReadOnlyList<string> FeatureDllSearchPaths { get; } = BuildSearchPaths();

    private static bool ProbeShim()
    {
        // Same name the generated DllImports use, resolved against the same
        // assembly, so this loads the very binary they will call.
        return System.Runtime.InteropServices.NativeLibrary.TryLoad(
            "ahjo_ngx", typeof(NgxApi).Assembly, null, out _);
    }

    /// <summary>
    /// Creates a <see cref="Instance"/> carrying the instance extensions NGX
    /// requires.
    /// </summary>
    /// <remarks>
    /// <b>Not optional, and not merely tidy.</b> Every NGX entry point that
    /// takes a <c>VkInstance</c> resolves
    /// <c>vkGetPhysicalDeviceProperties2KHR</c> through
    /// <c>vkGetInstanceProcAddr</c>, and the loader returns null for that name
    /// unless <c>VK_KHR_get_physical_device_properties2</c> was enabled at
    /// instance creation. NGX does not null-check it: the result is an access
    /// violation inside NVIDIA's client library, which no managed
    /// <c>catch</c> can turn into a skip. Measured on an RTX 4070 Ti / driver
    /// 610.47 while writing these tests.
    /// </remarks>
    public static Instance CreateInstance(out NgxExtensionSet? required)
    {
        NgxDescription description = Description;
        NgxSupport.TryGetInstanceExtensions(in description, out required);

        var instanceDescription = new InstanceDescription
        {
            Extensions = required is null ? default : required.Names,
            // Turn the layer on whenever the host has it. Spec D4 says the
            // validation layer is the ONLY oracle for the image-layout contract
            // the wrapper cannot enforce, so a hardware run without it proves
            // strictly less than one with it.
            EnableValidation = VulkanEnvironment.HasValidationLayer,
        };
        return Instance.Create(in instanceDescription);
    }

    /// <summary>
    /// Picks a graphics-capable physical device and creates a
    /// <see cref="Device"/> carrying the device extensions NGX requires.
    /// </summary>
    /// <remarks>
    /// <b>The probe and every test go through this one method, deliberately.</b>
    /// They used not to, and that is exactly how the WSL2 failure got through:
    /// the gate probed shim-load plus a capability query, the tests then did
    /// something strictly harder, and a host where only the harder half fails
    /// ran the tests instead of skipping them. A gate that does less than the
    /// thing it gates is not a gate.
    /// </remarks>
    public static Device CreateDevice(Instance instance, out uint graphicsFamily)
    {
        uint family = uint.MaxValue;
        PhysicalDevice gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    family = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });

        graphicsFamily = family;

        NgxDescription description = Description;
        NgxSupport.TryGetDeviceExtensions(gpu, in description, out NgxExtensionSet? deviceExtensions);
        try
        {
            var deviceDescription = new DeviceDescription
            {
                Queues     = [new QueueRequest(family, count: 1, priority: 1.0f)],
                Extensions = deviceExtensions is null ? default : deviceExtensions.Names,
            };
            return gpu.CreateDevice(in deviceDescription);
        }
        finally
        {
            deviceExtensions?.Dispose();   // vkCreateDevice copied the names
        }
    }

    private static bool ProbeDlss()
    {
        if (!ShimPresent || !VulkanEnvironment.HasDriver) return false;

        NgxExtensionSet? required = null;
        try
        {
            using Instance instance = CreateInstance(out required);

            PhysicalDevice gpu = instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);
            NgxDescription description = Description;
            if (!NgxSupport.IsSuperSamplingSupported(gpu, in description))
                return false;

            // NGX answering "supported" is necessary and NOT sufficient, and
            // the gap between the two is a real host: WSL2 exposes the same
            // RTX 4070 Ti through its own ICD, so NGX's adapter check says yes
            // — and then vkCreateDevice fails VK_ERROR_EXTENSION_NOT_PRESENT
            // because that ICD does not expose the device extensions NGX asked
            // for. Support is a property of the GPU; enablement is a property
            // of the driver stack in front of it. Only attempting the creation
            // distinguishes them, so the probe attempts it.
            using Device device = CreateDevice(instance, out _);

            // And "the device was creatable" is in turn necessary and not
            // sufficient: the adapter query above never loads nvngx_dlss.dll,
            // so a host with the shim built but no staged feature DLL still
            // gets this far and only fails inside NgxContext.Create. Three of
            // the four gated tests call it, so the gate has to.
            using NgxContext ngx = NgxContext.Create(device, in description);
            return ngx.IsSuperSamplingAvailable;
        }
        catch (VulkanException)
        {
            // The host cannot build the device DLSS needs. That is a
            // legitimate "no" and the exact answer this gate exists to give.
            return false;
        }
        catch (NgxFeatureLibraryNotFoundException)
        {
            // No nvngx_dlss.dll on this host. An environment fact, and the one
            // this package is least able to do anything about — the file is the
            // consumer's to supply (#214).
            return false;
        }
        catch (NgxDriverTooOldException)
        {
            // Likewise environmental, and likewise diagnosed: NGX told us
            // exactly which minimum this host is below.
            return false;
        }
        // Deliberately NOT catching the NgxException base type. The two
        // subtypes above are the cases where NGX named an environment fact, and
        // an environment fact is what a gate exists to convert into a skip. A
        // bare NgxException is the opposite: NGX refused and the wrapper could
        // not attribute it, which is exactly the shape a regression in
        // NgxContext.Create would take. Swallowing that would turn a broken
        // wrapper into a green run full of skips — the failure mode this gate
        // was rewritten to remove, reintroduced one layer in.
        finally
        {
            required?.Dispose();
        }
    }

    private static string[] BuildSearchPaths()
    {
        // Walk up from the test binary to the repository root, then into the
        // staging directory setup-ngx.ps1 writes.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "native", "ngx")))
            directory = directory.Parent;

        if (directory is null) return [];

        string rid = OperatingSystem.IsWindows() ? "win-x64" : "linux-x64";
        string staged = Path.Combine(directory.FullName, "native", "ngx", "staged", rid, "rel");
        return Directory.Exists(staged) ? [staged] : [];
    }
}
