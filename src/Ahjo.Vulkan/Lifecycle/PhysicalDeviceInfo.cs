using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// View handed to a <see cref="PhysicalDevicePicker"/>. Holds
/// <c>ref readonly</c> references and <see cref="ReadOnlySpan{T}"/> views
/// into stack / pooled scratch owned by
/// <see cref="Instance.PickPhysicalDevice"/>. Cannot escape the picker call.
/// </summary>
public readonly ref struct PhysicalDeviceInfo
{
    /// <summary>
    /// The physical device this view describes. <see cref="PhysicalDevice"/>
    /// is a class; <see cref="Instance"/> caches one instance per native
    /// handle, so this reference is the same one other callers see for the
    /// same GPU.
    /// </summary>
    public readonly PhysicalDevice                                 Device;
    public readonly ref readonly VkPhysicalDeviceProperties        Properties;
    public readonly ref readonly VkPhysicalDeviceFeatures          Features;
    public readonly ref readonly VkPhysicalDeviceVulkan11Features  Features11;
    public readonly ref readonly VkPhysicalDeviceVulkan12Features  Features12;
    public readonly ref readonly VkPhysicalDeviceVulkan13Features  Features13;
    public readonly ref readonly VkPhysicalDeviceVulkan14Features  Features14;
    public readonly ref readonly VkPhysicalDeviceMemoryProperties  Memory;

    public readonly ReadOnlySpan<QueueFamilyInfo>       QueueFamilies;
    public readonly ReadOnlySpan<VkExtensionProperties> Extensions;
    public readonly ReadOnlySpan<byte>                  Name;

    public PhysicalDeviceInfo(
        PhysicalDevice                                device,
        in VkPhysicalDeviceProperties                 properties,
        in VkPhysicalDeviceFeatures                   features,
        in VkPhysicalDeviceVulkan11Features           features11,
        in VkPhysicalDeviceVulkan12Features           features12,
        in VkPhysicalDeviceVulkan13Features           features13,
        in VkPhysicalDeviceVulkan14Features           features14,
        in VkPhysicalDeviceMemoryProperties           memory,
        ReadOnlySpan<QueueFamilyInfo>                 queueFamilies,
        ReadOnlySpan<VkExtensionProperties>           extensions,
        ReadOnlySpan<byte>                            name)
    {
        Device        = device;
        Properties    = ref properties;
        Features      = ref features;
        Features11    = ref features11;
        Features12    = ref features12;
        Features13    = ref features13;
        Features14    = ref features14;
        Memory        = ref memory;
        QueueFamilies = queueFamilies;
        Extensions    = extensions;
        Name          = name;
    }

    /// <summary>
    /// Shorthand for <see cref="Properties"/>.<c>deviceType</c>. The dominant
    /// picker discriminator, exposed flat for readability.
    /// </summary>
    public VkPhysicalDeviceType Type => Properties.deviceType;

    /// <summary>
    /// Linear-scan check for a NUL-terminated UTF-8 name in
    /// <see cref="Extensions"/>. Allocation-free.
    /// </summary>
    public unsafe bool SupportsExtension(ReadOnlySpan<byte> utf8Name)
    {
        if (Extensions.IsEmpty) return false;

        fixed (VkExtensionProperties* exts = Extensions)
        {
            for (int i = 0; i < Extensions.Length; i++)
            {
                if (NameEquals((sbyte*)&exts[i].extensionName.e0, utf8Name)) return true;
            }
        }
        return false;
    }

    private static unsafe bool NameEquals(sbyte* name, ReadOnlySpan<byte> target)
    {
        for (int i = 0; i < target.Length; i++)
        {
            if (name[i] == 0 || (byte)name[i] != target[i]) return false;
        }
        return name[target.Length] == 0;
    }
}
