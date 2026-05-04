using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Friendly form delivered to <see cref="InstanceDescription.DebugCallback"/>.
/// Marshalled once per validation message inside the unmanaged trampoline
/// (allocates two <c>string</c> instances). Validation messages are not a
/// hot path; the cost is irrelevant.
/// </summary>
public readonly record struct DebugMessage(
    VkDebugUtilsMessageSeverityFlagBitsEXT Severity,
    VkDebugUtilsMessageTypeFlagBitsEXT     Type,
    string                                 Message,
    string?                                MessageIdName,
    int                                    MessageIdNumber);
