using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan.Testing;

/// <summary>Ordered ladder of Vulkan capability a test host can offer.</summary>
/// <remarks>
/// Ordered on purpose: a comparison against the declared tier is the whole
/// mechanism, so every higher rung implies every lower one. The ordering
/// describes the <em>declaration</em> only — individual capabilities are probed
/// independently, see <see cref="VulkanEnvironment.HasValidationLayer"/>.
/// </remarks>
internal enum VulkanCapability
{
    /// <summary>No usable ICD. Every driver-gated test may skip.</summary>
    None = 0,

    /// <summary><c>vkCreateInstance</c> succeeds and at least one physical device is enumerable.</summary>
    Software = 1,

    /// <summary>As <see cref="Software"/>, and the first enumerated device is not a CPU device.</summary>
    Hardware = 2,

    /// <summary>As <see cref="Hardware"/>, and <c>VK_LAYER_KHRONOS_validation</c> is enumerable.</summary>
    Validation = 3,
}

/// <summary>
/// What Vulkan capability this lane <em>declared</em> via <c>AHJO_VULKAN_TIER</c>
/// versus what the host actually offers. The gap between the two is the
/// coverage hole a green CI run would otherwise hide — see
/// <c>docs/ci-coverage.md</c> and issue #158.
/// </summary>
/// <remarks>
/// Linked into the three suites that touch Vulkan
/// (<c>Ahjo.Vulkan.Tests</c>, <c>Ahjo.Vulkan.Native.Tests</c>,
/// <c>Ahjo.Vulkan.Vma.Native.Tests</c>) via
/// <c>&lt;Compile Include="..\Shared\*.cs" /&gt;</c>. Deliberately not linked
/// into <c>Ahjo.Vulkan.Ktx.Native.Tests</c>, whose contract is to pass with no
/// loader and no ICD at all.
/// </remarks>
internal static unsafe class VulkanEnvironment
{
    /// <summary>Name of the environment variable each lane declares its tier in.</summary>
    public const string TierVariable = "AHJO_VULKAN_TIER";

    /// <summary>
    /// The pre-#158 variable this replaces. Still read — but only to fail
    /// loudly, so a stale lane or shell script cannot silently lose its guard.
    /// </summary>
    public const string RetiredVariable = "AHJO_REQUIRE_VULKAN_DEVICE";

    private static readonly Lazy<VulkanCapability> _declared = new(ParseDeclaredTier);
    private static readonly Lazy<(VulkanCapability Capability, string Detail)> _observed = new(Probe);
    private static readonly Lazy<bool> _hasLayer = new(ProbeValidationLayer);

    /// <summary>
    /// Parsed <c>AHJO_VULKAN_TIER</c>. Unset or empty =&gt;
    /// <see cref="VulkanCapability.None"/>. Throws on an unrecognized value or
    /// when the retired <c>AHJO_REQUIRE_VULKAN_DEVICE</c> is still set.
    /// </summary>
    public static VulkanCapability Declared => _declared.Value;

    /// <summary>What this host actually offers. Probed once, cached, never throws.</summary>
    public static VulkanCapability Observed => _observed.Value.Capability;

    /// <summary>One sentence naming why <see cref="Observed"/> stopped where it did.</summary>
    public static string ObservedDetail => _observed.Value.Detail;

    /// <summary>A usable ICD answered and enumerated at least one device.</summary>
    public static bool HasDriver => Observed >= VulkanCapability.Software;

    /// <summary>The ICD that answered reports <c>VK_PHYSICAL_DEVICE_TYPE_CPU</c>.</summary>
    public static bool IsSoftwareDriver => Observed == VulkanCapability.Software;

    /// <summary>
    /// <c>VK_LAYER_KHRONOS_validation</c> is available on this host.
    /// </summary>
    /// <remarks>
    /// Deliberately <em>not</em> <c>Observed &gt;= Validation</c>. The layer is an
    /// instance-level fact independent of the device type, and reading it off the
    /// ladder would cap a software-ICD host at <see cref="VulkanCapability.Software"/>
    /// and make ten driver+validation-gated tests report
    /// <c>[gate:validation] … not installed</c> when the layer is in fact
    /// installed and the real cause is the CPU device. That is a misdiagnosis by
    /// the mechanism whose entire purpose is honest classification. It would also
    /// mean the Windows lane could never declare above <c>software</c> even with a
    /// working SwiftShader ICD, foreclosing the one route to CI-provable
    /// validation-layer coverage.
    ///
    /// Still gated on <see cref="HasDriver"/>: every layer-gated test needs a
    /// device to do anything with the layer, and this keeps a driverless host
    /// reporting the driver gap rather than a layer gap.
    /// </remarks>
    public static bool HasValidationLayer => HasDriver && _hasLayer.Value;

    /// <summary>The lowercase spelling a lane writes into <c>AHJO_VULKAN_TIER</c>.</summary>
    public static string Name(VulkanCapability capability) => capability switch
    {
        VulkanCapability.None => "none",
        VulkanCapability.Software => "software",
        VulkanCapability.Hardware => "hardware",
        VulkanCapability.Validation => "validation",
        _ => capability.ToString(),
    };

    private static VulkanCapability ParseDeclaredTier()
    {
        // Fail closed. #144 shipped a SIGSEGVing libvma.so because nothing
        // executed it; AHJO_REQUIRE_VULKAN_DEVICE was the guard added in
        // response. Silently ignoring it now would re-open that hole for any
        // lane or script that still sets it.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RetiredVariable)))
        {
            throw new InvalidOperationException(
                $"{RetiredVariable} is no longer read (issue #158). Set {TierVariable}=software " +
                $"instead, then unset {RetiredVariable}.");
        }

        string? raw = Environment.GetEnvironmentVariable(TierVariable);
        if (string.IsNullOrWhiteSpace(raw))
            return VulkanCapability.None;

        return raw.Trim().ToLowerInvariant() switch
        {
            "none" => VulkanCapability.None,
            "software" => VulkanCapability.Software,
            "hardware" => VulkanCapability.Hardware,
            "validation" => VulkanCapability.Validation,
            _ => throw new InvalidOperationException(
                $"{TierVariable}='{raw}' is not a recognized tier. " +
                "Expected one of: none, software, hardware, validation."),
        };
    }

    private static (VulkanCapability, string) Probe() => GuardProbe(ProbeCore);

    /// <summary>
    /// Turns any throw out of the probe into <see cref="VulkanCapability.None"/>
    /// plus a detail naming the exception. The seam exists so
    /// <c>VulkanEnvironmentProbeTests</c> can prove the conversion without a
    /// deliberately broken loader on the host.
    /// </summary>
    /// <remarks>
    /// Load-bearing, not defensive tidiness. <see cref="Lazy{T}"/> in its default
    /// mode caches a factory exception and rethrows it on every subsequent
    /// <c>.Value</c>, so one throw here turns all ~225
    /// <c>TestGate.RequireDriver()</c> gates into <em>errors</em> rather than
    /// skips. Those errors report <c>outcome="Failed"</c>, which the coverage
    /// summary counts as neither a coverage gap nor an unclassified skip — the
    /// table would claim zero gaps in exactly the situation it was built for —
    /// and <c>VulkanTierContractTests</c> would error too, so the one actionable
    /// message never prints. Fail closed instead: the gates skip, and the tier
    /// contract goes red once with the exception text as its detail.
    /// </remarks>
    internal static (VulkanCapability Capability, string Detail) GuardProbe(
        Func<(VulkanCapability, string)> probe)
    {
        try
        {
            return probe();
        }
        catch (DllNotFoundException)
        {
            return (VulkanCapability.None, "no vulkan-1 loader on this host");
        }
        catch (Exception ex)
        {
            // Realistic and not DllNotFoundException: a wrong-architecture
            // vulkan-1.dll on the search path (BadImageFormatException), a
            // loader that resolves but does not export vkCreateInstance
            // (EntryPointNotFoundException).
            return (VulkanCapability.None, $"vulkan probe threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Walks the ladder bottom-up and stops at the first rung the host fails,
    // recording why. One instance at apiVersion 1.3, which is what the pre-#158
    // device probe used: a modern loader clamps a higher request down, and
    // vkEnumeratePhysicalDevices / vkGetPhysicalDeviceProperties are both core
    // 1.0, so a 1.0-only ICD behind a modern loader still answers.
    //
    // Every ObservedDetail below has to be distinct and literally true: it is the
    // one sentence VulkanTierContractTests prints when a lane goes red, so it is
    // the deliverable, not decoration.
    private static (VulkanCapability, string) ProbeCore()
    {
        VkInstance_T* instance = null;
        var appInfo = new VkApplicationInfo
        {
            sType      = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
            apiVersion = (1u << 22) | (3u << 12), // 1.3
        };
        var createInfo = new VkInstanceCreateInfo
        {
            sType            = VkStructureType.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
            pApplicationInfo = &appInfo,
        };

        VkResult created = Vk.vkCreateInstance(&createInfo, null, &instance);
        if (created != VkResult.VK_SUCCESS)
            return (VulkanCapability.None, $"vkCreateInstance returned {created}");

        // VK_SUCCESS with a null handle is a configuration this repo already
        // treats as reachable (VmaSmokeTests asserts it). `instance` is not
        // optional on vkEnumeratePhysicalDevices — passing VK_NULL_HANDLE faults
        // in the loader's dispatch-table lookup rather than returning an error.
        if (instance == null)
            return (VulkanCapability.None, "vkCreateInstance returned VK_SUCCESS but wrote a null instance handle");

        try
        {
            uint gpuCount = 0;
            VkResult counted = Vk.vkEnumeratePhysicalDevices(instance, &gpuCount, null);
            if (counted != VkResult.VK_SUCCESS)
                return (VulkanCapability.None, $"vkEnumeratePhysicalDevices (count query) returned {counted}");
            if (gpuCount == 0)
                return (VulkanCapability.None, "vkEnumeratePhysicalDevices reported zero devices");

            VkPhysicalDevice_T* gpu = null;
            uint one = 1;
            VkResult fetched = Vk.vkEnumeratePhysicalDevices(instance, &one, &gpu);
            if (fetched is not (VkResult.VK_SUCCESS or VkResult.VK_INCOMPLETE))
                return (VulkanCapability.None, $"vkEnumeratePhysicalDevices (handle fetch) returned {fetched}");
            if (gpu == null)
            {
                return (VulkanCapability.None,
                    $"vkEnumeratePhysicalDevices returned {fetched} but wrote a null physical-device handle");
            }

            VkPhysicalDeviceProperties props;
            Vk.vkGetPhysicalDeviceProperties(gpu, &props);
            if (props.deviceType == VkPhysicalDeviceType.VK_PHYSICAL_DEVICE_TYPE_CPU)
            {
                return (VulkanCapability.Software,
                    "first enumerated device reports VK_PHYSICAL_DEVICE_TYPE_CPU (software ICD)");
            }
        }
        finally
        {
            Vk.vkDestroyInstance(instance, null);
        }

        if (!_hasLayer.Value)
            return (VulkanCapability.Hardware, "VK_LAYER_KHRONOS_validation is not installed");

        return (VulkanCapability.Validation, "hardware device + VK_LAYER_KHRONOS_validation");
    }

    // Independent of any instance and of the device type — see HasValidationLayer.
    // Guarded for the same reason Probe is: a throw cached in this Lazy would turn
    // the 13 layer gates into errors instead of skips.
    private static bool ProbeValidationLayer()
    {
        try
        {
            uint count = 0;
            if (Vk.vkEnumerateInstanceLayerProperties(&count, null) != VkResult.VK_SUCCESS || count == 0)
                return false;

            var props = new VkLayerProperties[count];
            fixed (VkLayerProperties* p = props)
            {
                if (Vk.vkEnumerateInstanceLayerProperties(&count, p) != VkResult.VK_SUCCESS)
                    return false;
            }

            ReadOnlySpan<byte> target = "VK_LAYER_KHRONOS_validation"u8;
            for (int i = 0; i < count; i++)
            {
                fixed (VkLayerProperties* entry = &props[i])
                {
                    if (Match((sbyte*)entry, target)) return true;
                }
            }
            return false;
        }
        catch (Exception)
        {
            // No layer we can prove is present. Gates skip; a lane that declared
            // `validation` still goes red through the tier contract.
            return false;
        }
    }

    /// <summary>
    /// Compares a NUL-terminated Vulkan name field against a UTF-8 literal.
    /// Shared with the wrapper suite's extension probe.
    /// </summary>
    public static bool Match(sbyte* name, ReadOnlySpan<byte> target)
    {
        for (int i = 0; i < target.Length; i++)
        {
            if (name[i] == 0 || (byte)name[i] != target[i]) return false;
        }
        return name[target.Length] == 0;
    }
}
