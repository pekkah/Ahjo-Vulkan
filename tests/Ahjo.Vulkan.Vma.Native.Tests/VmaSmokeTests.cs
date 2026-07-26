using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
using Ahjo.Vulkan.Vma.Native;
using Xunit;

namespace Ahjo.Vulkan.Vma.Native.Tests;

/// <summary>
/// End-to-end VMA smoke test. Builds a real Vulkan instance + device,
/// hands them to <c>vmaCreateAllocator</c> via <see cref="VmaVulkanFunctions"/>,
/// allocates a small buffer, frees it, tears everything down.
///
/// The fact that this code compiles is itself part of the test —
/// <see cref="VmaAllocatorCreateInfo.physicalDevice"/> is typed as
/// <c>Ahjo.Vulkan.Native.VkPhysicalDevice_T*</c> in the generated
/// bindings, so passing a <c>VkPhysicalDevice_T*</c> straight from the
/// Vulkan core bindings is a no-op cast. If the <c>--remap</c> rules in
/// <c>tools/generate-vma.rsp</c> ever diverge from reality, this file
/// stops compiling.
/// </summary>
public unsafe class VmaSmokeTests
{
    [Fact]
    public void CreateAllocator_AllocateBuffer_DestroyAllocator_RoundTrips()
    {
        // A driverless host genuinely can't run this, so it skips — but the
        // lane that provisions an ICD on purpose declares AHJO_VULKAN_TIER
        // and VulkanTierContractTests fails there instead, which is what
        // keeps the #144 guard (a shipped libvma.so nothing ever executed)
        // from quietly evaporating. See docs/ci-coverage.md.
        TestGate.RequireDriver();

        VkInstance_T* instance = null;
        VkDevice_T* device = null;
        VmaAllocator_T* allocator = null;
        VkBuffer_T* buffer = null;
        VmaAllocation_T* allocation = null;

        try
        {
            // ---- VkInstance ----
            var appInfo = new VkApplicationInfo
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
                apiVersion = MakeApiVersion(0, 1, 3, 0),
            };
            var instanceCreateInfo = new VkInstanceCreateInfo
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
                pApplicationInfo = &appInfo,
            };

            VkResult instanceResult = Vk.vkCreateInstance(&instanceCreateInfo, null, &instance);
            Assert.Equal(VkResult.VK_SUCCESS, instanceResult);
            Assert.True(instance != null, "VK_SUCCESS but instance pointer is null");

            // ---- Pick a physical device ----
            uint deviceCount = 0;
            Assert.Equal(VkResult.VK_SUCCESS, Vk.vkEnumeratePhysicalDevices(instance, &deviceCount, null));
            Assert.True(deviceCount >= 1, "vkEnumeratePhysicalDevices reported zero devices");

            VkPhysicalDevice_T** physicalDevices = (VkPhysicalDevice_T**)NativeMemory.Alloc(
                (nuint)(deviceCount * (uint)sizeof(nint)));
            try
            {
                Assert.Equal(VkResult.VK_SUCCESS, Vk.vkEnumeratePhysicalDevices(instance, &deviceCount, physicalDevices));
                VkPhysicalDevice_T* physicalDevice = physicalDevices[0];

                // ---- Pick any queue family (just need a valid index for vkCreateDevice) ----
                uint queueFamilyCount = 0;
                Vk.vkGetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, null);
                Assert.True(queueFamilyCount >= 1, "Physical device reports zero queue families");

                // Family 0 is universally present; we don't need a specific one
                // for VMA to function — VMA only needs a working VkDevice.
                uint queueFamilyIndex = 0;
                float queuePriority = 1.0f;
                var queueCreateInfo = new VkDeviceQueueCreateInfo
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO,
                    queueFamilyIndex = queueFamilyIndex,
                    queueCount = 1,
                    pQueuePriorities = &queuePriority,
                };

                var deviceCreateInfo = new VkDeviceCreateInfo
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO,
                    queueCreateInfoCount = 1,
                    pQueueCreateInfos = &queueCreateInfo,
                };

                Assert.Equal(VkResult.VK_SUCCESS, Vk.vkCreateDevice(physicalDevice, &deviceCreateInfo, null, &device));
                Assert.True(device != null);

                // ---- VmaAllocator ----
                // VMA was compiled with VMA_DYNAMIC_VULKAN_FUNCTIONS=1, so it
                // resolves every other entry point through these two callbacks.
                // Can't take address of a [DllImport] static method directly
                // (CS8757) — fetch the raw exports from the same loader handle
                // the DllImportResolver picked.
                nint loader = LoadVulkanLoader();
                var vulkanFunctions = new VmaVulkanFunctions
                {
                    vkGetInstanceProcAddr = (delegate* unmanaged[Stdcall]<VkInstance_T*, sbyte*, delegate* unmanaged[Stdcall]<void>>)
                        NativeLibrary.GetExport(loader, "vkGetInstanceProcAddr"),
                    vkGetDeviceProcAddr = (delegate* unmanaged[Stdcall]<VkDevice_T*, sbyte*, delegate* unmanaged[Stdcall]<void>>)
                        NativeLibrary.GetExport(loader, "vkGetDeviceProcAddr"),
                };

                var allocatorCreateInfo = new VmaAllocatorCreateInfo
                {
                    physicalDevice = physicalDevice,
                    device = device,
                    instance = instance,
                    pVulkanFunctions = &vulkanFunctions,
                    vulkanApiVersion = MakeApiVersion(0, 1, 3, 0),
                };

                Assert.Equal(VkResult.VK_SUCCESS, Vma.vmaCreateAllocator(&allocatorCreateInfo, &allocator));
                Assert.True(allocator != null);

                // ---- Allocate a tiny buffer ----
                var bufferCreateInfo = new VkBufferCreateInfo
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO,
                    size = 4096,
                    usage = (uint)VkBufferUsageFlagBits.VK_BUFFER_USAGE_TRANSFER_SRC_BIT,
                    sharingMode = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE,
                };
                var allocationCreateInfo = new VmaAllocationCreateInfo
                {
                    usage = VmaMemoryUsage.VMA_MEMORY_USAGE_AUTO,
                };

                Assert.Equal(VkResult.VK_SUCCESS,
                    Vma.vmaCreateBuffer(allocator, &bufferCreateInfo, &allocationCreateInfo, &buffer, &allocation, null));
                Assert.True(buffer != null);
                Assert.True(allocation != null);
            }
            finally
            {
                NativeMemory.Free(physicalDevices);
            }
        }
        finally
        {
            if (allocator != null && buffer != null && allocation != null)
            {
                Vma.vmaDestroyBuffer(allocator, buffer, allocation);
            }
            if (allocator != null)
            {
                Vma.vmaDestroyAllocator(allocator);
            }
            if (device != null)
            {
                Vk.vkDestroyDevice(device, null);
            }
            if (instance != null)
            {
                Vk.vkDestroyInstance(instance, null);
            }
        }
    }

    private static uint MakeApiVersion(uint variant, uint major, uint minor, uint patch)
        => (variant << 29) | (major << 22) | (minor << 12) | patch;

    /// <summary>
    /// Walk the same per-OS candidate list <c>VulkanLoaderResolver</c> uses
    /// and return the first handle that loads. Stays consistent with what
    /// the resolver picked for ordinary <c>[DllImport("vulkan-1")]</c> calls.
    /// </summary>
    private static nint LoadVulkanLoader()
    {
        string[] candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ["vulkan-1.dll", "vulkan-1"]
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? ["libvulkan.dylib", "libvulkan.1.dylib", "libMoltenVK.dylib"]
                : ["libvulkan.so.1", "libvulkan.so"];

        foreach (string c in candidates)
        {
            if (NativeLibrary.TryLoad(c, out nint handle))
            {
                return handle;
            }
        }
        throw new InvalidOperationException("Vulkan loader not present on this host.");
    }
}
