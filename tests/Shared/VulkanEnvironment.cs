using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan.Testing;

/// <summary>Ordered ladder of Vulkan capability a test host can offer.</summary>
/// <remarks>
/// Ordered on purpose: a comparison against the declared tier is the whole
/// mechanism, so every higher rung implies every lower one.
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

    /// <summary>
    /// Parsed <c>AHJO_VULKAN_TIER</c>. Unset or empty =&gt;
    /// <see cref="VulkanCapability.None"/>. Throws on an unrecognized value or
    /// when the retired <c>AHJO_REQUIRE_VULKAN_DEVICE</c> is still set.
    /// </summary>
    public static VulkanCapability Declared => _declared.Value;

    /// <summary>What this host actually offers. Probed once, cached.</summary>
    public static VulkanCapability Observed => _observed.Value.Capability;

    /// <summary>One sentence naming why <see cref="Observed"/> stopped where it did.</summary>
    public static string ObservedDetail => _observed.Value.Detail;

    /// <summary>A usable ICD answered and enumerated at least one device.</summary>
    public static bool HasDriver => Observed >= VulkanCapability.Software;

    /// <summary>The ICD that answered reports <c>VK_PHYSICAL_DEVICE_TYPE_CPU</c>.</summary>
    public static bool IsSoftwareDriver => Observed == VulkanCapability.Software;

    /// <summary><c>VK_LAYER_KHRONOS_validation</c> is available on a hardware device.</summary>
    public static bool HasValidationLayer => Observed >= VulkanCapability.Validation;

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

    // Walks the ladder bottom-up and stops at the first rung the host fails,
    // recording why. The apiVersion values are the ones the pre-#158
    // VulkanDriverProbe used — 1.0 to ask "is there any ICD at all", 1.3 for
    // the device query. Changing either changes what the probe accepts.
    private static (VulkanCapability, string) Probe()
    {
        VkInstance_T* instance = null;
        var appInfo = new VkApplicationInfo
        {
            sType      = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
            apiVersion = (1u << 22) | (0u << 12), // 1.0
        };
        var createInfo = new VkInstanceCreateInfo
        {
            sType            = VkStructureType.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
            pApplicationInfo = &appInfo,
        };

        VkResult result;
        try
        {
            result = Vk.vkCreateInstance(&createInfo, null, &instance);
        }
        catch (DllNotFoundException)
        {
            return (VulkanCapability.None, "no vulkan-1 loader on this host");
        }

        if (result != VkResult.VK_SUCCESS)
            return (VulkanCapability.None, $"vkCreateInstance returned {result}");

        Vk.vkDestroyInstance(instance, null);

        // Device query on its own 1.3 instance, matching the pre-#158 probe.
        VkInstance_T* deviceInstance = null;
        var deviceAppInfo = new VkApplicationInfo
        {
            sType      = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
            apiVersion = (1u << 22) | (3u << 12), // 1.3
        };
        var deviceCreateInfo = new VkInstanceCreateInfo
        {
            sType            = VkStructureType.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
            pApplicationInfo = &deviceAppInfo,
        };
        VkResult deviceInstanceResult = Vk.vkCreateInstance(&deviceCreateInfo, null, &deviceInstance);
        if (deviceInstanceResult != VkResult.VK_SUCCESS)
            return (VulkanCapability.None, $"vkCreateInstance returned {deviceInstanceResult}");

        try
        {
            uint gpuCount = 0;
            if (Vk.vkEnumeratePhysicalDevices(deviceInstance, &gpuCount, null) != VkResult.VK_SUCCESS || gpuCount == 0)
                return (VulkanCapability.None, "vkEnumeratePhysicalDevices reported zero devices");

            VkPhysicalDevice_T* gpu = null;
            uint one = 1;
            if (Vk.vkEnumeratePhysicalDevices(deviceInstance, &one, &gpu) is not (VkResult.VK_SUCCESS or VkResult.VK_INCOMPLETE) || gpu == null)
                return (VulkanCapability.None, "vkEnumeratePhysicalDevices reported zero devices");

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
            Vk.vkDestroyInstance(deviceInstance, null);
        }

        // The layer probe needs no ICD in principle, but the pre-#158 code
        // reached it only when a driver existed and the ladder keeps that
        // coupling — see the spec's uncertainty section. Do not decouple here.
        if (!HasKhronosValidationLayer())
            return (VulkanCapability.Hardware, "VK_LAYER_KHRONOS_validation is not installed");

        return (VulkanCapability.Validation, "hardware device + VK_LAYER_KHRONOS_validation");
    }

    private static bool HasKhronosValidationLayer()
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
