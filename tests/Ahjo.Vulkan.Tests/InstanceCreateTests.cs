using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed unsafe class InstanceCreateTests
{
    [Fact]
    public void Create_MinimalDescription_Succeeds()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(new InstanceDescription
        {
            ApiVersion = VulkanVersion.V1_4,
        });

        Assert.True(instance.Handle != null);
    }

    [Fact]
    public void Create_DefaultsApiVersionWhenZero()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        Assert.True(instance.Handle != null);
    }

    [Fact]
    public void Create_WithValidation_DefaultCallback_FiresOnUnknownExtension()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver,           "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer,  "Validation layer not installed.");

        var stderr = new StringWriter();
        var oldErr = Console.Error;
        Console.SetError(stderr);
        try
        {
            VulkanException? ex = null;
            try
            {
                ReadOnlySpan<Utf8Name> bogus = stackalloc Utf8Name[]
                {
                    Utf8Name.FromLiteral("VK_FAKE_extension_does_not_exist"u8),
                };
                using var _ = Instance.Create(new InstanceDescription
                {
                    ApiVersion = VulkanVersion.V1_4,
                    EnableValidation = true,
                    Extensions = bogus,
                });
            }
            catch (VulkanException e)
            {
                ex = e;
            }
            Assert.NotNull(ex);
        }
        finally
        {
            Console.SetError(oldErr);
        }

        Assert.NotEmpty(stderr.ToString());
    }

    [Fact]
    public void IsExtensionSupported_KnownInstanceExtension_True()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        // VK_KHR_surface is advertised by every desktop loader with at least
        // one WSI-capable ICD — the probe already established a driver exists.
        Assert.True(Instance.IsExtensionSupported(VulkanExtensions.KhrSurface));
    }

    [Fact]
    public void IsExtensionSupported_UnknownExtension_False()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        Assert.False(Instance.IsExtensionSupported(
            Utf8Name.FromLiteral("VK_FAKE_extension_does_not_exist"u8)));
    }

    [Fact]
    public void IsExtensionSupported_NullOrEmptyName_False()
    {
        // Short-circuits before any loader call — valid on loaderless hosts too.
        Assert.False(Instance.IsExtensionSupported(default(Utf8Name)));
        Assert.False(Instance.IsExtensionSupported(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void Create_WithValidation_ManagedCallback_RoundTripsMessage()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver,           "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer,  "Validation layer not installed.");

        var captured = new List<DebugMessage>();

        // The plan's Assert.Throws lambda would capture the stackalloc-backed
        // ReadOnlySpan and trigger CS8175 — use the same try/catch pattern
        // already established by Create_WithValidation_DefaultCallback_FiresOnUnknownExtension.
        VulkanException? ex = null;
        try
        {
            ReadOnlySpan<Utf8Name> bogus = stackalloc Utf8Name[]
            {
                Utf8Name.FromLiteral("VK_FAKE_extension_does_not_exist"u8),
            };

            _ = Instance.Create(new InstanceDescription
            {
                ApiVersion = VulkanVersion.V1_4,
                EnableValidation = true,
                Extensions = bogus,
                DebugCallback = m => { lock (captured) captured.Add(m); },
            });
        }
        catch (VulkanException e)
        {
            ex = e;
        }

        Assert.NotNull(ex);
        Assert.NotEmpty(captured);
        Assert.Contains(captured, m =>
            (m.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0);
    }

    private static int s_rawCallbackHits;

    [System.Runtime.InteropServices.UnmanagedCallersOnly(
        CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    private static uint RawCountingCallback(
        VkDebugUtilsMessageSeverityFlagBitsEXT severity,
        uint                                   type,
        VkDebugUtilsMessengerCallbackDataEXT*  data,
        void*                                  userData)
    {
        System.Threading.Interlocked.Increment(ref s_rawCallbackHits);
        return 0;
    }

    [Fact]
    public void Create_WithValidation_RawCallback_IsInvoked()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver,           "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer,  "Validation layer not installed.");

        s_rawCallbackHits = 0;

        // Same try/catch pattern as the other validation tests (CS8175 workaround).
        VulkanException? ex = null;
        try
        {
            ReadOnlySpan<Utf8Name> bogus = stackalloc Utf8Name[]
            {
                Utf8Name.FromLiteral("VK_FAKE_extension_does_not_exist"u8),
            };

            _ = Instance.Create(new InstanceDescription
            {
                ApiVersion = VulkanVersion.V1_4,
                EnableValidation = true,
                Extensions = bogus,
                DebugCallbackRaw = &RawCountingCallback,
            });
        }
        catch (VulkanException e)
        {
            ex = e;
        }

        Assert.NotNull(ex);
        Assert.True(s_rawCallbackHits > 0, $"Expected raw callback to fire; hits = {s_rawCallbackHits}");
    }

    [Fact]
    public void PersistentMessenger_FiresOnPostCreateValidationViolation()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver,           "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer,  "Validation layer not installed.");

        var captured = new List<DebugMessage>();
        using var instance = Instance.Create(new InstanceDescription
        {
            ApiVersion = VulkanVersion.V1_4,
            EnableValidation = true,
            DebugCallback = m => { lock (captured) captured.Add(m); },
        });

        captured.Clear();

        // Validation-layer callbacks are invoked synchronously on the calling
        // thread before the Vulkan API returns. There is no race between the
        // call below and the captured.Clear() above.

        // Intentional VUID violation: vkEnumeratePhysicalDevices requires a
        // non-null pPhysicalDeviceCount per VUID-vkEnumeratePhysicalDevices-
        // pPhysicalDeviceCount-parameter. The persistent messenger must fire.
        Native.Vk.vkEnumeratePhysicalDevices(instance.Handle, null, null);

        Assert.NotEmpty(captured);
    }

    [Fact]
    public void Dispose_TwiceIsIdempotent()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        var instance = Instance.Create(new InstanceDescription { ApiVersion = VulkanVersion.V1_4 });
        instance.Dispose();
        instance.Dispose(); // must not throw
    }

    [Fact]
    public void Dispose_AfterValidationCreate_DestroysMessengerAndInstance()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver,           "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer,  "Validation layer not installed.");

        var first = Instance.Create(new InstanceDescription
        {
            ApiVersion = VulkanVersion.V1_4,
            EnableValidation = true,
        });
        first.Dispose();

        // If the prior dispose left the messenger or instance dangling we'd
        // see a driver-level error or layer error on a fresh create.
        using var second = Instance.Create(new InstanceDescription
        {
            ApiVersion = VulkanVersion.V1_4,
            EnableValidation = true,
        });

        Assert.True(second.Handle != null);
    }

    [Fact]
    public void Create_FailureWithManagedCallback_FreesGCHandleAndAllowsSubsequentCreate()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver,           "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer,  "Validation layer not installed.");

        // Same try/catch CS8175 workaround pattern used by the other validation tests.
        VulkanException? ex = null;
        try
        {
            ReadOnlySpan<Utf8Name> bogus = stackalloc Utf8Name[]
            {
                Utf8Name.FromLiteral("VK_FAKE_extension_does_not_exist"u8),
            };

            _ = Instance.Create(new InstanceDescription
            {
                ApiVersion = VulkanVersion.V1_4,
                EnableValidation = true,
                Extensions = bogus,
                DebugCallback = _ => { },
            });
        }
        catch (VulkanException e)
        {
            ex = e;
        }

        Assert.NotNull(ex);

        // Subsequent successful create must work — proves the failed-create
        // cleanup path freed its GCHandle and didn't leak driver state.
        using var ok = Instance.Create(new InstanceDescription
        {
            ApiVersion = VulkanVersion.V1_4,
            EnableValidation = true,
            DebugCallback = _ => { },
        });

        Assert.True(ok.Handle != null);
    }
}
