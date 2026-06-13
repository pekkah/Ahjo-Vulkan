using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Debug-oriented registry of owning handles, used to catch double-dispose
/// during development (and when validation is flipped on in a Release build to
/// chase a bug). Each owning handle registers itself at construction and
/// unregisters on <c>Dispose</c>; disposing a handle that was tracked-then-
/// already-disposed trips <see cref="AhjoValidation.Fail"/>.
/// </summary>
/// <remarks>
/// <para>Keyed by <c>(VkObjectType, RawHandle)</c> rather than the raw pointer
/// alone: a driver freely reuses a handle <em>value</em> across object types,
/// and even within a type a destroyed handle's value can be handed back by the
/// next create.</para>
/// <para>Two sets, not one, so the registry never raises a false positive on
/// correct code regardless of <em>when</em> validation was enabled:</para>
/// <list type="bullet">
/// <item><c>s_live</c> — currently-live tracked handles. Create adds; the first
/// dispose removes.</item>
/// <item><c>s_disposed</c> — handles that were tracked and have since been
/// disposed. Only a key recorded here proves a genuine double-dispose. Create
/// clears a key from it, so a driver reusing a destroyed handle's value starts
/// clean (no false positive on the new handle's dispose).</item>
/// </list>
/// <para>A dispose whose key is in neither set is a handle created while
/// validation was <em>disabled</em> (e.g. the Release default, before the user
/// flipped validation on): it was never tracked, so its legitimate single
/// dispose is silently accepted — never flagged. That is the case the earlier
/// live-set-only design got wrong.</para>
/// <para>Entirely gated on <see cref="AhjoValidation.IsEnabled"/>: when
/// validation is off (the Release default) every entry point returns on the
/// first branch — no locking, no allocation — preserving the wrapper's
/// zero-per-frame-allocation contract. When on, the per-handle set op runs
/// under a lock; handle create/dispose is a cold path (setup / teardown), not
/// per-frame command recording, so the cost is acceptable for a bug hunt —
/// the only time validation is enabled in Release.</para>
/// <para>Borrowed handles (<c>FromRaw</c> / <c>default</c>, where
/// <see cref="IVulkanHandle{TSelf}.OwnsHandle"/> is <see langword="false"/>)
/// are never tracked: the wrapper doesn't destroy them, so there is no
/// double-dispose to catch.</para>
/// </remarks>
internal static class HandleRegistry
{
    private static readonly HashSet<(VkObjectType Type, ulong Handle)> s_live = [];
    private static readonly HashSet<(VkObjectType Type, ulong Handle)> s_disposed = [];
    private static readonly object s_lock = new();

    // Bound s_disposed so a long-running session with validation on (a Release
    // bug hunt, or churn of transient handles whose values never repeat) can't
    // grow it without limit. Clearing only forfeits double-dispose detection
    // for handles disposed before the reset — acceptable for a debug aid.
    private const int DisposedHighWater = 1 << 16;

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

        var key = (T.ObjectType, raw);
        lock (s_lock)
        {
            // Add is idempotent (a stale live entry from owner-teardown is
            // harmless); clearing the disposed mark is what makes driver
            // handle-value reuse a non-issue — the value is live again, so a
            // later dispose of the new handle is correct, not a double-dispose.
            s_live.Add(key);
            s_disposed.Remove(key);
        }
    }

    /// <summary>
    /// Unregisters <paramref name="handle"/>. Call from an owning handle's
    /// <c>Dispose</c>, after the <c>OwnsHandle</c> / null guards and before the
    /// <c>vkDestroy*</c> / <c>vmaDestroy*</c> call:
    /// <c>HandleRegistry.TrackDispose(this);</c>. A handle that is recorded as
    /// already-disposed (and not re-created since) is a double-dispose.
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

        var key = (T.ObjectType, raw);
        bool doubleDispose;
        lock (s_lock)
        {
            if (s_live.Remove(key))
            {
                // Normal first dispose. Record it so a second dispose is
                // caught — bounding the record so it can't grow unbounded.
                if (s_disposed.Count >= DisposedHighWater)
                    s_disposed.Clear();
                s_disposed.Add(key);
                doubleDispose = false;
            }
            else
            {
                // Not live: either a genuine double-dispose (recorded in
                // s_disposed) or a handle created while validation was off
                // (in neither set — silently accepted, never a false positive).
                doubleDispose = s_disposed.Contains(key);
            }
        }

        if (doubleDispose)
            AhjoValidation.Fail("HandleRegistry",
                $"{T.ObjectType} handle 0x{raw:X} disposed more than once — the first dispose already " +
                "destroyed it. A copy-by-value handle was disposed again from a second site.");
    }
}
