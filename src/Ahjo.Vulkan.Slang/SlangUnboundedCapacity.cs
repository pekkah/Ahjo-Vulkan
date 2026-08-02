namespace Ahjo.Vulkan.Slang;

/// <summary>
/// Supplies the descriptor capacity for a binding reflection could not size.
/// </summary>
/// <remarks>
/// Called by
/// <c>SlangVulkanMapping.MapBindings(ReadOnlySpan{SlangDescriptorBinding}, SlangUnboundedCapacity)</c>
/// only for a binding whose <see cref="SlangDescriptorBinding.Count"/> is not
/// <see cref="SlangDescriptorCountKind.Fixed"/>, so an implementation may
/// assume it is only ever asked about bindings it must size.
/// </remarks>
/// <param name="binding">The binding whose capacity the caller must choose.</param>
/// <returns>The number of descriptors to reserve. Must be non-zero.</returns>
public delegate uint SlangUnboundedCapacity(SlangDescriptorBinding binding);
