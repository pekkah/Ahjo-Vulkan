using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Ngx.Tests;

/// <summary>
/// What a host with any Vulkan driver — SwiftShader included — can prove about
/// the pieces #218 added, without NGX or NVIDIA hardware anywhere in sight.
/// </summary>
/// <remarks>
/// Two things: that an <see cref="NgxExtensionSet"/>'s copied, NUL-terminated
/// names survive a real <c>vkCreateDevice</c> (the pointer/termination
/// contract, against a real loader rather than a fabricated array), and that the
/// <see cref="AllocatorDescription.EnableMemoryBudget"/> pairing behaves.
/// </remarks>
public sealed unsafe class NgxDevicePlumbingTests
{
    [Fact]
    public void ExtensionSetNamesReachCreateDevice()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);

        uint family = uint.MaxValue;
        NgxExtensionSet? names = null;
        PhysicalDevice gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (!info.QueueFamilies[i].SupportsGraphics) continue;
                family = info.QueueFamilies[i].Index;

                // Copy exactly the entry the driver advertised, through the
                // same seam NgxSupport uses for NGX's array. VK_EXT_memory_budget
                // is the pick because it is dependency-free: enabling it cannot
                // fail vkCreateDevice for a reason unrelated to this test.
                names = FindAdvertised(info.Extensions, "VK_EXT_memory_budget"u8);
                return true;
            }
            return false;
        });

        try
        {
            TestGate.RequireDeviceFeature(names is not null, "Device does not advertise VK_EXT_memory_budget.");

            var description = new DeviceDescription
            {
                Queues     = [new QueueRequest(family, count: 1, priority: 1.0f)],
                Extensions = names!.Names,
            };

            using Device device = gpu.CreateDevice(in description);
            Assert.False(device.IsNull);
        }
        finally
        {
            names?.Dispose();
        }
    }

    [Fact]
    public void EnableMemoryBudget_WithoutTheExtension_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);

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

        bool previous = AhjoValidation.Enabled;
        AhjoValidation.Enabled = true;
        try
        {
            // ref struct can't be captured in a lambda; build it inside the call.
            var ex = Assert.Throws<AhjoValidationException>(() =>
            {
                var description = new DeviceDescription
                {
                    Queues    = [new QueueRequest(family, count: 1, priority: 1.0f)],
                    Allocator = new AllocatorDescription { EnableMemoryBudget = true },
                    // Extensions deliberately left empty — that is the trap.
                };
                using Device device = gpu.CreateDevice(in description);
            });

            Assert.Contains("VK_EXT_memory_budget", ex.Message, StringComparison.Ordinal);
            Assert.Contains("ExtMemoryBudget", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            AhjoValidation.Enabled = previous;
        }
    }

    /// <summary>
    /// The <see cref="Allocator.Create(Device, in AllocatorDescription)"/> half
    /// of the pairing check, which is <b>not</b> gated on
    /// <see cref="AhjoValidation.Enabled"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="PhysicalDevice.CreateDevice"/> check is a validation-gated
    /// early warning; this one is the last point before the flag reaches VMA,
    /// which would then chain <c>VkPhysicalDeviceMemoryBudgetPropertiesEXT</c>
    /// into a device that never enabled the extension. A Release build with
    /// validation off has to fail here, so the test runs with validation
    /// explicitly <b>off</b> — that is the configuration under test.
    /// </remarks>
    [Fact]
    public void AllocatorCreate_EnableMemoryBudget_WithoutTheExtension_ThrowsEvenWithValidationOff()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);

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

        bool previous = AhjoValidation.Enabled;
        AhjoValidation.Enabled = false;
        try
        {
            // A device created WITHOUT the extension — which CreateDevice now
            // permits, because its own check is validation-gated and validation
            // is off.
            var description = new DeviceDescription
            {
                Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
            };
            using Device device = gpu.CreateDevice(in description);

            var ex = Assert.Throws<ArgumentException>(
                () => Allocator.Create(device, new AllocatorDescription { EnableMemoryBudget = true }));

            Assert.Contains("VK_EXT_memory_budget", ex.Message, StringComparison.Ordinal);
            Assert.Contains("was not enabled", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            AhjoValidation.Enabled = previous;
        }
    }

    [Fact]
    public void GetHeapBudgets_ReportsOneRowPerHeap()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);

        uint family = uint.MaxValue;
        bool hasBudget = false;
        PhysicalDevice gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (!info.QueueFamilies[i].SupportsGraphics) continue;
                family    = info.QueueFamilies[i].Index;
                hasBudget = info.SupportsExtension("VK_EXT_memory_budget"u8);
                return true;
            }
            return false;
        });

        TestGate.RequireDeviceFeature(hasBudget, "Device does not advertise VK_EXT_memory_budget.");

        var description = new DeviceDescription
        {
            Queues     = [new QueueRequest(family, count: 1, priority: 1.0f)],
            Extensions = [VulkanExtensions.ExtMemoryBudget],
            Allocator  = new AllocatorDescription { EnableMemoryBudget = true },
        };

        using Device device = gpu.CreateDevice(in description);

        // 16 is VK_MAX_MEMORY_HEAPS — always enough, so a caller never has to
        // ask twice.
        Span<MemoryHeapBudget> budgets = stackalloc MemoryHeapBudget[16];
        int count = device.Allocator.GetHeapBudgets(budgets);

        Assert.True(count > 0, "a device with memory reports at least one heap");
        for (int i = 0; i < count; i++)
        {
            Assert.Equal((uint)i, budgets[i].HeapIndex);
            Assert.True(budgets[i].HeapIndex < (uint)count);
        }
    }

    [Fact]
    public void GetHeapBudgets_TooSmallSpan_ThrowsNamingBothNumbers()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);

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

        var description = new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        };

        using Device device = gpu.CreateDevice(in description);
        Allocator allocator = device.Allocator;

        var ex = Assert.Throws<ArgumentException>(() => allocator.GetHeapBudgets(default));
        Assert.Contains("0 entries", ex.Message, StringComparison.Ordinal);
        Assert.Contains("memory heap", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Copies the one advertised entry whose name matches, through
    /// <see cref="NgxExtensionSet"/>'s own construction path.
    /// </summary>
    private static NgxExtensionSet? FindAdvertised(ReadOnlySpan<VkExtensionProperties> advertised, ReadOnlySpan<byte> name)
    {
        for (int i = 0; i < advertised.Length; i++)
        {
            ref readonly VkExtensionProperties candidate = ref advertised[i];
            fixed (VkExtensionProperties* p = &candidate)
            {
                var field = new ReadOnlySpan<byte>(&p->extensionName, 256);
                int nul = field.IndexOf((byte)0);
                if (nul >= 0 && field[..nul].SequenceEqual(name))
                    return NgxExtensionSet.FromProperties(advertised.Slice(i, 1));
            }
        }
        return null;
    }
}
