using System.Diagnostics.CodeAnalysis;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Ngx.Native;

namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// Capability queries you run <b>before</b> committing to DLSS: what extensions
/// NGX needs at instance and device creation, and whether this GPU can run
/// Super Resolution at all.
/// </summary>
/// <remarks>
/// <para>Every method here returns <see langword="false"/> rather than throwing
/// on a non-success result. These are the queries a settings screen runs, and
/// "this host cannot do DLSS" is an answer, not an error. A genuine misuse —
/// a malformed <see cref="NgxDescription"/> — still throws
/// <see cref="ArgumentException"/>.</para>
/// <para><b>Two facts #216 measured, so nobody re-derives them.</b>
/// <see cref="TryGetInstanceExtensions"/> is a pre-instance <i>static</i> query
/// answered out of NVIDIA's static client library: it never loads the
/// driver-side NGX core, and CI measured it byte-identical on a driverless
/// <c>windows-latest</c> runner and on an RTX 4070 Ti (#216 OPEN-1, resolved).
/// The other three need a live <c>VkInstance</c>
/// (<c>Generated/NgxApi.cs:84</c>, <c>:90</c>) and are <b>not</b> callable on
/// the driverless <c>ngx-native</c> lane (#216 finding 4).</para>
/// <para><b>The extension lists are mandatory, not advisory.</b> Measured on an
/// RTX 4070 Ti / driver 610.47 (issue #218):</para>
/// <list type="bullet">
///   <item><description>An instance created <i>without</i> the names
///   <see cref="TryGetInstanceExtensions"/> returns makes
///   <see cref="TryGetSuperSamplingRequirements"/> and
///   <see cref="TryGetDeviceExtensions"/> <b>access-violate</b> inside NVIDIA's
///   client library rather than return a failure result. No managed
///   <c>catch</c> recovers from that.</description></item>
///   <item><description>A device created <i>without</i> the names
///   <see cref="TryGetDeviceExtensions"/> returns lets
///   <see cref="NgxContext.Create"/> get past <c>Init</c> and then report
///   <c>SuperSampling.Available = 0</c> with
///   <c>FAIL_PlatformError</c>.</description></item>
/// </list>
/// <para>So the order is: query instance extensions → create the instance with
/// them → pick a physical device → query device extensions → create the device
/// with them → <see cref="NgxContext.Create"/>.</para>
/// </remarks>
public static unsafe class NgxSupport
{
    /// <summary>
    /// The instance extensions NGX requires for DLSS. Add them to
    /// <see cref="InstanceDescription.Extensions"/> before
    /// <c>vkCreateInstance</c>.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when NGX could not answer; then
    /// <paramref name="extensions"/> is <see langword="null"/>.
    /// </returns>
    /// <remarks>Takes no Vulkan object: this must be answerable before the
    /// instance it constrains exists.</remarks>
    public static bool TryGetInstanceExtensions(in NgxDescription description, [NotNullWhen(true)] out NgxExtensionSet? extensions)
    {
        description.Validate();

        description.MeasureUtf8(out int byteCapacity, out int stringCapacity);
        var block = new NgxUtf8Block(byteCapacity, stringCapacity);
        try
        {
            AhjoNgxInitInfo info = description.ToNative(ref block);

            uint                  count      = 0;
            VkExtensionProperties* properties = null;
            NVSDK_NGX_Result result = NgxApi.ahjo_ngx_vulkan_get_feature_instance_extension_requirements_utf8(
                NVSDK_NGX_Feature.NVSDK_NGX_Feature_SuperSampling, &info, &count, &properties);

            return Project(result, count, properties, out extensions);
        }
        finally
        {
            block.Dispose();
        }
    }

    /// <summary>
    /// The device extensions NGX requires for DLSS on
    /// <paramref name="physicalDevice"/>. Add them to
    /// <see cref="DeviceDescription.Extensions"/> before
    /// <c>vkCreateDevice</c>.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when NGX could not answer; then
    /// <paramref name="extensions"/> is <see langword="null"/>.
    /// </returns>
    public static bool TryGetDeviceExtensions(
        PhysicalDevice physicalDevice,
        in NgxDescription description,
        [NotNullWhen(true)] out NgxExtensionSet? extensions)
    {
        ArgumentNullException.ThrowIfNull(physicalDevice);
        description.Validate();

        description.MeasureUtf8(out int byteCapacity, out int stringCapacity);
        var block = new NgxUtf8Block(byteCapacity, stringCapacity);
        try
        {
            AhjoNgxInitInfo info = description.ToNative(ref block);

            uint                  count      = 0;
            VkExtensionProperties* properties = null;
            NVSDK_NGX_Result result = NgxApi.ahjo_ngx_vulkan_get_feature_device_extension_requirements_utf8(
                (VkInstance_T*)(nint)physicalDevice.Instance.RawHandle,
                (VkPhysicalDevice_T*)(nint)physicalDevice.RawHandle,
                NVSDK_NGX_Feature.NVSDK_NGX_Feature_SuperSampling,
                &info, &count, &properties);

            return Project(result, count, properties, out extensions);
        }
        finally
        {
            block.Dispose();
        }
    }

    /// <summary>
    /// Whether DLSS Super Resolution is supported on
    /// <paramref name="physicalDevice"/>. Shorthand for
    /// <see cref="TryGetSuperSamplingRequirements"/> when you do not need the
    /// reason.
    /// </summary>
    public static bool IsSuperSamplingSupported(PhysicalDevice physicalDevice, in NgxDescription description)
        => TryGetSuperSamplingRequirements(physicalDevice, in description, out DlssRequirements requirements)
           && requirements.IsSupported;

    /// <summary>
    /// What NGX says DLSS Super Resolution needs on
    /// <paramref name="physicalDevice"/>, and whether this host has it.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when NGX could not answer at all; then
    /// <paramref name="requirements"/> is <c>default</c>. A successful query
    /// that reports "unsupported" returns <see langword="true"/> with
    /// <see cref="DlssRequirements.IsSupported"/> <see langword="false"/> —
    /// that is an answer.
    /// </returns>
    public static bool TryGetSuperSamplingRequirements(
        PhysicalDevice physicalDevice,
        in NgxDescription description,
        out DlssRequirements requirements)
    {
        ArgumentNullException.ThrowIfNull(physicalDevice);
        description.Validate();

        description.MeasureUtf8(out int byteCapacity, out int stringCapacity);
        var block = new NgxUtf8Block(byteCapacity, stringCapacity);
        try
        {
            AhjoNgxInitInfo info = description.ToNative(ref block);

            NVSDK_NGX_FeatureRequirement requirement = default;
            NVSDK_NGX_Result result = NgxApi.ahjo_ngx_vulkan_get_feature_requirements_utf8(
                (VkInstance_T*)(nint)physicalDevice.Instance.RawHandle,
                (VkPhysicalDevice_T*)(nint)physicalDevice.RawHandle,
                NVSDK_NGX_Feature.NVSDK_NGX_Feature_SuperSampling,
                &info, &requirement);

            if (!NgxResult.Succeeded(result))
            {
                requirements = default;
                return false;
            }

            requirements = DlssRequirements.FromNative(in requirement);
            return true;
        }
        finally
        {
            block.Dispose();
        }
    }

    /// <summary>
    /// Shared tail of the two extension queries: copy NGX's array into an
    /// <see cref="NgxExtensionSet"/> on success, report failure otherwise.
    /// </summary>
    private static bool Project(
        NVSDK_NGX_Result result,
        uint count,
        VkExtensionProperties* properties,
        [NotNullWhen(true)] out NgxExtensionSet? extensions)
    {
        if (!NgxResult.Succeeded(result))
        {
            extensions = null;
            return false;
        }

        // A Success with a null array and a zero count is legal and means "no
        // extensions required" — not a failure.
        extensions = NgxExtensionSet.FromProperties(
            count == 0 || properties == null
                ? default
                : new ReadOnlySpan<VkExtensionProperties>(properties, (int)count));
        return true;
    }
}
