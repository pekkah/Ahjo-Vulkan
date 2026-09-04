namespace Ahjo.Vulkan;

/// <summary>
/// Allocator-level options, distinct from the per-allocation
/// <see cref="AllocationDescription"/>: these configure the VMA allocator
/// itself, once, when <see cref="Device.Allocator"/> is first created. Carried
/// on <see cref="DeviceDescription.Allocator"/>.
/// </summary>
/// <remarks>
/// <para><c>default(AllocatorDescription)</c> is byte-identical to the
/// pre-issue-#218 behaviour — every member's default is the value the wrapper
/// hardcoded before this type existed. This is not a default change.</para>
/// </remarks>
public readonly record struct AllocatorDescription
{
    /// <summary>
    /// Sets <c>VMA_ALLOCATOR_CREATE_EXT_MEMORY_BUDGET_BIT</c>, which makes VMA
    /// query the driver's real per-heap usage and budget through
    /// <c>VK_EXT_memory_budget</c> instead of estimating them from its own
    /// bookkeeping. Read the result with
    /// <see cref="Allocator.GetHeapBudgets"/>. Defaults to
    /// <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>You must also list <see cref="VulkanExtensions.ExtMemoryBudget"/>
    /// in <see cref="DeviceDescription.Extensions"/>.</b> VMA needs the device
    /// extension enabled at <c>vkCreateDevice</c> time, and the wrapper cannot
    /// add it for you without silently changing what device creation asks for.
    /// VMA's other prerequisite, <c>VK_KHR_get_physical_device_properties2</c>,
    /// is core from Vulkan 1.1 and the wrapper requires 1.3+, so nothing more
    /// is needed.</para>
    /// <para>Setting this without the extension is caught in two places, and
    /// the second one is not optional:</para>
    /// <list type="bullet">
    ///   <item><description><see cref="PhysicalDevice.CreateDevice"/> fails
    ///   early — but only under <see cref="AhjoValidation.Enabled"/>. It is a
    ///   helpful warning about a description, not a
    ///   guarantee.</description></item>
    ///   <item><description><see cref="Allocator.Create(Device, in AllocatorDescription)"/>
    ///   throws <see cref="ArgumentException"/> <b>unconditionally</b> — in
    ///   Release, with validation off, and from the
    ///   <see cref="Device.Allocator"/> property getter too. It is the last
    ///   point before the flag reaches VMA, which would otherwise chain
    ///   <c>VkPhysicalDeviceMemoryBudgetPropertiesEXT</c> into a device that
    ///   never enabled the extension and read back numbers that look plausible
    ///   and are wrong.</description></item>
    /// </list>
    /// <para><b>Why you would want it.</b> Anything allocated outside VMA is
    /// invisible to VMA's own accounting — notably DLSS, which allocates its
    /// history and scratch surfaces inside the driver
    /// (<c>Ahjo.Vulkan.Ngx</c>, issue #214). Without this flag
    /// <see cref="Allocator.GetHeapBudgets"/> under-reports by exactly that
    /// amount.</para>
    /// </remarks>
    public bool EnableMemoryBudget { get; init; }
}
