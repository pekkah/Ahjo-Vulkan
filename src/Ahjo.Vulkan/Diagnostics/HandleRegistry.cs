using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Debug-only registry of live owning handles, used to catch double-dispose
/// (and dispose of a handle whose owner already died) during development.
/// Each owning handle registers itself at construction and unregisters on
/// <c>Dispose</c>; a second dispose finds the handle missing and trips
/// <see cref="AhjoValidation.Fail"/>.
/// </summary>
/// <remarks>
/// <para>Keyed by <c>(VkObjectType, RawHandle)</c> rather than the raw
/// pointer alone: a driver freely reuses a handle <em>value</em> across
/// object types, and even within a type a destroyed handle's value can be
/// handed back by the next create. Registering on create (not "remembering
/// the disposed set") is what makes that reuse a non-issue — the value
/// re-enters the live set legitimately, so a later dispose of the new handle
/// is correct, not a false double-dispose.</para>
/// <para>Entirely gated on <see cref="AhjoValidation.IsEnabled"/>: when
/// validation is off (the Release default) every entry point is a single
/// branch with no locking and no allocation, preserving the wrapper's
/// zero-per-frame-allocation contract. When on, the per-handle dictionary
/// op runs under a lock; handle create/dispose is a cold path (setup /
/// teardown), not per-frame command recording, so the cost is acceptable for
/// a bug hunt — which is the only time validation is enabled in Release.</para>
/// <para>Borrowed handles (<c>FromRaw</c> / <c>default</c>, where
/// <see cref="IVulkanHandle{TSelf}.OwnsHandle"/> is <see langword="false"/>)
/// are never tracked: the wrapper doesn't destroy them, so there is no
/// double-dispose to catch.</para>
/// </remarks>
internal static class HandleRegistry
{
    private static readonly HashSet<(VkObjectType Type, ulong Handle)> s_live = [];
    private static readonly object s_lock = new();

    /// <summary>
    /// Registers <paramref name="handle"/> as live if it is an owning,
    /// non-null handle and validation is enabled. Call as the last statement
    /// of an owning handle's constructor: <c>HandleRegistry.TrackCreate(this);</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void TrackCreate<T>(in T handle)
        where T : struct, IVulkanHandle<T>
    {
        if (!AhjoValidation.IsEnabled || !handle.OwnsHandle)
            return;

        ulong raw = handle.RawHandle;
        if (raw == 0)
            return;

        // Add is idempotent on purpose: a driver reuses a destroyed handle's
        // value for the next create, and a handle freed by tearing down its
        // owning Device/Allocator (rather than its own Dispose) leaves a stale
        // live entry. Re-adding that value is the correct, false-positive-free
        // outcome — the double-dispose signal we care about comes from the
        // Remove side, not from rejecting a re-created value here.
        lock (s_lock)
        {
            s_live.Add((T.ObjectType, raw));
        }
    }

    /// <summary>
    /// Unregisters <paramref name="handle"/>. Call from an owning handle's
    /// <c>Dispose</c>, after the <c>OwnsHandle</c> / null guards and before the
    /// <c>vkDestroy*</c> / <c>vmaDestroy*</c> call:
    /// <c>HandleRegistry.TrackDispose(this);</c>. A handle that is not live
    /// here is a double-dispose (or a dispose after its owner was destroyed).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void TrackDispose<T>(in T handle)
        where T : struct, IVulkanHandle<T>
    {
        if (!AhjoValidation.IsEnabled || !handle.OwnsHandle)
            return;

        ulong raw = handle.RawHandle;
        if (raw == 0)
            return;

        bool removed;
        lock (s_lock)
        {
            removed = s_live.Remove((T.ObjectType, raw));
        }

        if (!removed)
            AhjoValidation.Fail("HandleRegistry",
                $"{T.ObjectType} handle 0x{raw:X} disposed but not live — double-dispose, or dispose " +
                "of a handle whose owning device/instance/allocator was already destroyed.");
    }
}
