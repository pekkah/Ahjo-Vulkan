namespace Ahjo.Vulkan;

/// <summary>
/// User-supplied predicate that decides whether a candidate physical
/// device satisfies the caller's requirements. Hand-declared (cannot be
/// <see cref="System.Func{T1,T2}"/>) because the parameter is <c>in</c> of
/// a <c>ref struct</c>.
/// </summary>
/// <param name="info">View over the candidate's properties, features,
/// memory, queue families, and device extensions. Backed by stack and
/// pooled scratch owned by <see cref="Instance.PickPhysicalDevice"/>; do
/// not stash any references that escape the picker call.</param>
/// <returns><see langword="true"/> to select this candidate;
/// <see langword="false"/> to keep searching.</returns>
public delegate bool PhysicalDevicePicker(in PhysicalDeviceInfo info);
