namespace Ahjo.Vulkan;

/// <summary>
/// The path a <see cref="Device.TryGetDeviceFault(out DeviceFaultReport)"/>
/// read takes, decided by which device-fault extension was enabled at
/// <c>vkCreateDevice</c>. When both are enabled, <see cref="Khr"/> wins.
/// </summary>
public enum DeviceFaultApi
{
    /// <summary>Neither <c>VK_EXT_device_fault</c> nor <c>VK_KHR_device_fault</c>
    /// was enabled; no device-fault read is possible.</summary>
    None = 0,

    /// <summary>The read goes through <c>vkGetDeviceFaultInfoEXT</c>
    /// (<c>VK_EXT_device_fault</c> enabled, <c>VK_KHR_device_fault</c>
    /// not).</summary>
    Ext = 1,

    /// <summary>The read goes through <c>vkGetDeviceFaultReportsKHR</c> and
    /// <c>vkGetDeviceFaultDebugInfoKHR</c> (<c>VK_KHR_device_fault</c>
    /// enabled, with or without <c>VK_EXT_device_fault</c>).</summary>
    Khr = 2,
}
