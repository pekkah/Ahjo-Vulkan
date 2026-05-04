using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Inputs to <see cref="Instance.Create"/>. <c>ref struct</c> because
/// <see cref="ReadOnlySpan{T}"/> of <see cref="Utf8Name"/> cannot live
/// inside a non-<c>ref</c> type. Fields default to zero/null so callers
/// only set what they care about.
/// </summary>
public ref struct InstanceDescription
{
    public Utf8Name      ApplicationName;     // optional; default null
    public Utf8Name      EngineName;          // optional; default null
    public uint          ApplicationVersion;  // optional; default 0
    public uint          EngineVersion;       // optional; default 0
    public VulkanVersion ApiVersion;          // defaults to V1_4 inside Create when Packed == 0

    public bool          EnableValidation;

    public ReadOnlySpan<Utf8Name> Extensions;
    public ReadOnlySpan<Utf8Name> Layers;

    public Action<DebugMessage>? DebugCallback;

    public unsafe delegate* unmanaged[Stdcall]<
        VkDebugUtilsMessageSeverityFlagBitsEXT,
        uint,
        VkDebugUtilsMessengerCallbackDataEXT*,
        void*,
        uint> DebugCallbackRaw;
}
