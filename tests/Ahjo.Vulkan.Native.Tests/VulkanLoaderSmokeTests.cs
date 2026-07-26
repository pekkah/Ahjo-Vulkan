using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Native.Tests;

/// <summary>
/// First real signal that the binding pipeline works end-to-end:
/// the <see cref="VulkanLoaderResolver"/> ModuleInitializer registers,
/// <c>vulkan-1</c> resolves to the per-OS soname, and a real Vulkan
/// entry point returns a sensible result.
///
/// These tests exercise the loader before any real work — if they fail,
/// every test that follows will fail in confusing ways.
/// </summary>
public unsafe class VulkanLoaderSmokeTests
{
    [Fact]
    public void EnumerateInstanceVersion_ReturnsAtLeast_1_0()
    {
        uint apiVersion = 0;
        VkResult result = Vk.vkEnumerateInstanceVersion(&apiVersion);

        Assert.Equal(VkResult.VK_SUCCESS, result);
        // VK_API_VERSION_1_0 is encoded as (1<<22 | 0<<12 | 0) = 0x00400000.
        // Any working loader reports >= 1.0; modern drivers report 1.3+.
        Assert.True(apiVersion >= MakeApiVersion(0, 1, 0, 0),
            $"Reported API version {DecodeApiVersion(apiVersion)} is below 1.0.0");
    }

    [Fact]
    public void CreateAndDestroyInstance_Succeeds()
    {
        // A usable ICD is required for vkCreateInstance to actually create an
        // instance. Without one this skips [gate:driver] rather than accepting
        // VK_ERROR_INCOMPATIBLE_DRIVER as a pass — a driverless lane that
        // declares AHJO_VULKAN_TIER=software still goes red through
        // VulkanTierContractTests, so the skip is a visible coverage gap and not
        // a green that proves nothing. See docs/ci-coverage.md and issue #161.
        TestGate.RequireDriver();

        VkInstance_T* instance = null;

        try
        {
            byte* appName = AllocAscii("Ahjo.Vulkan.Native smoke test");
            byte* engineName = AllocAscii("Ahjo");

            try
            {
                var appInfo = new VkApplicationInfo
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
                    pNext = null,
                    pApplicationName = (sbyte*)appName,
                    applicationVersion = MakeApiVersion(0, 0, 1, 0),
                    pEngineName = (sbyte*)engineName,
                    engineVersion = MakeApiVersion(0, 0, 1, 0),
                    apiVersion = MakeApiVersion(0, 1, 3, 0),
                };

                var createInfo = new VkInstanceCreateInfo
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
                    pNext = null,
                    flags = 0,
                    pApplicationInfo = &appInfo,
                    enabledLayerCount = 0,
                    ppEnabledLayerNames = null,
                    enabledExtensionCount = 0,
                    ppEnabledExtensionNames = null,
                };

                VkResult result = Vk.vkCreateInstance(&createInfo, null, &instance);

                // RequireDriver guaranteed a usable ICD, so this must genuinely
                // succeed — VK_ERROR_INCOMPATIBLE_DRIVER here would be a real
                // regression, not an accepted environment.
                Assert.Equal(VkResult.VK_SUCCESS, result);
                Assert.True(instance != null, "VK_SUCCESS but instance pointer is null");
            }
            finally
            {
                NativeMemory.Free(appName);
                NativeMemory.Free(engineName);
            }
        }
        finally
        {
            if (instance != null)
            {
                Vk.vkDestroyInstance(instance, null);
            }
        }
    }

    [Fact]
    public void EnumeratePhysicalDevices_ReturnsConsistentCount()
    {
        // Enumeration needs a usable ICD with at least one device — exactly what
        // RequireDriver checks. Without one this skips [gate:driver] rather than
        // early-returning a green pass that enumerated nothing. See issue #161.
        TestGate.RequireDriver();

        VkInstance_T* instance = CreateMinimalInstance();
        Assert.True(instance != null, "RequireDriver passed but vkCreateInstance returned no instance");

        try
        {
            uint count = 0;
            VkResult firstCall = Vk.vkEnumeratePhysicalDevices(instance, &count, null);
            Assert.Equal(VkResult.VK_SUCCESS, firstCall);
            Assert.True(count >= 1, "RequireDriver passed but zero physical devices enumerated");

            VkPhysicalDevice_T** devices = (VkPhysicalDevice_T**)NativeMemory.Alloc(
                (nuint)(count * (uint)sizeof(nint)));

            try
            {
                VkResult secondCall = Vk.vkEnumeratePhysicalDevices(instance, &count, devices);
                Assert.Equal(VkResult.VK_SUCCESS, secondCall);
                Assert.True(count >= 1);
                Assert.True(devices[0] != null);

                // Read at least one property to confirm the function pointer
                // for vkGetPhysicalDeviceProperties also resolves.
                VkPhysicalDeviceProperties props = default;
                Vk.vkGetPhysicalDeviceProperties(devices[0], &props);
                Assert.True(props.apiVersion > 0);
            }
            finally
            {
                NativeMemory.Free(devices);
            }
        }
        finally
        {
            Vk.vkDestroyInstance(instance, null);
        }
    }

    private static VkInstance_T* CreateMinimalInstance()
    {
        VkInstance_T* instance = null;

        var appInfo = new VkApplicationInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
            apiVersion = MakeApiVersion(0, 1, 0, 0),
        };
        var createInfo = new VkInstanceCreateInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
            pApplicationInfo = &appInfo,
        };

        VkResult result = Vk.vkCreateInstance(&createInfo, null, &instance);
        return result == VkResult.VK_SUCCESS ? instance : null;
    }

    /// <summary>VK_MAKE_API_VERSION packing: variant&lt;&lt;29 | major&lt;&lt;22 | minor&lt;&lt;12 | patch.</summary>
    private static uint MakeApiVersion(uint variant, uint major, uint minor, uint patch)
        => (variant << 29) | (major << 22) | (minor << 12) | patch;

    private static string DecodeApiVersion(uint v)
        => $"{(v >> 22) & 0x7Fu}.{(v >> 12) & 0x3FFu}.{v & 0xFFFu}";

    private static byte* AllocAscii(string s)
    {
        byte* p = (byte*)NativeMemory.Alloc((nuint)s.Length + 1);
        for (int i = 0; i < s.Length; i++)
        {
            p[i] = (byte)s[i];
        }
        p[s.Length] = 0;
        return p;
    }
}
