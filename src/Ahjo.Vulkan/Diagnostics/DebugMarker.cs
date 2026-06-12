using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// RGBA color tag attached to a debug-utils label. RenderDoc / Nsight
/// surface the color as the marker's swatch in their event timeline.
/// Components are 0..1 floats; the wrapper does not normalize.
/// </summary>
/// <remarks>
/// <c>default(Color)</c> is fully-transparent black — RenderDoc treats
/// that as "no preferred color" and falls back to its default.
/// </remarks>
public readonly struct Color
{
    public readonly float R;
    public readonly float G;
    public readonly float B;
    public readonly float A;

    public Color(float r, float g, float b, float a = 1.0f)
    {
        R = r; G = g; B = b; A = a;
    }
}

/// <summary>
/// Names a Vulkan object via <c>VK_EXT_debug_utils</c> so RenderDoc /
/// Nsight / GPU profilers display the wrapper-supplied label instead of
/// a raw handle. No-op when the extension is not loaded on the device's
/// owning instance.
/// </summary>
/// <remarks>
/// <para>The <typeparamref name="T"/> generic parameter dispatches via
/// <see cref="IVulkanHandle{T}.ObjectType"/>'s static abstract member —
/// the JIT inlines the call so naming costs one Vulkan call plus one
/// struct copy regardless of handle type.</para>
/// <para><b>Lifetime.</b> Names are persistent for the object's
/// lifetime; re-calling <see cref="Set{T}"/> with a new name simply
/// overwrites. The byte span must be a UTF-8 literal (<c>"…"u8</c>) or
/// a buffer the caller pins for the duration of the call — the
/// <see cref="VkDebugUtilsObjectNameInfoEXT"/> struct keeps a raw
/// pointer that the driver dereferences synchronously.</para>
/// </remarks>
public static unsafe class ObjectName
{
    /// <summary>
    /// Tags <paramref name="handle"/> with <paramref name="name"/>. The
    /// span is treated as UTF-8 and a trailing NUL is required by the
    /// Vulkan spec — <c>"name"u8</c> literals carry one implicitly past
    /// <c>span.Length</c>; non-literal callers must include a NUL byte
    /// inside the span and pass the whole length.
    /// </summary>
    public static void Set<T>(Device device, T handle, ReadOnlySpan<byte> name)
        where T : struct, IVulkanHandle<T>
    {
        ArgumentNullException.ThrowIfNull(device);
        if (name.IsEmpty)
            throw new ArgumentException("Object name cannot be empty.", nameof(name));

        var fn = device.Functions.SetDebugUtilsObjectName;
        if (fn == null) return;        // VK_EXT_debug_utils not loaded → no-op.
        if (handle.IsNull) return;     // Naming a null handle is meaningless; silently skip.

        fixed (byte* pName = name)
        {
            var info = new VkDebugUtilsObjectNameInfoEXT
            {
                sType        = VkStructureType.VK_STRUCTURE_TYPE_DEBUG_UTILS_OBJECT_NAME_INFO_EXT,
                objectType   = T.ObjectType,
                objectHandle = handle.RawHandle,
                pObjectName  = (sbyte*)pName,
            };
            fn(device.Handle, &info).ThrowIfFailed();
        }
    }
}

/// <summary>
/// RAII label scope returned from
/// <see cref="CommandRecorder.LabelScope(System.ReadOnlySpan{byte})"/>.
/// <c>Dispose</c> calls <c>vkCmdEndDebugUtilsLabelEXT</c> on the
/// recorder's command buffer; <c>using</c> nesting produces a clean
/// hierarchy in RenderDoc / Nsight.
/// </summary>
/// <remarks>
/// <c>ref struct</c> so the scope cannot escape the recording frame.
/// Holds the raw <c>VkCommandBuffer</c> pointer plus the
/// <c>vkCmdEndDebugUtilsLabelEXT</c> pointer captured at scope-open
/// time — null fn pointer means VK_EXT_debug_utils wasn't loaded and
/// <c>Dispose</c> is a no-op (matching the no-op enter call).
/// </remarks>
public readonly unsafe ref struct DisposableLabel
{
    private readonly VkCommandBuffer_T* _cb;
    private readonly delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, void> _end;

    internal DisposableLabel(
        VkCommandBuffer_T* cb,
        delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, void> end)
    {
        _cb  = cb;
        _end = end;
    }

    public void Dispose()
    {
        if (_end != null && _cb != null) _end(_cb);
    }
}
