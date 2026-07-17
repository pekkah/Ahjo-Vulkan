namespace Ahjo.Vulkan.Native;

/// <summary>
/// Hand-authored mirrors of the Win32 external-memory / external-semaphore
/// <b>get-handle</b> info structs (<c>VK_KHR_external_memory_win32</c> /
/// <c>VK_KHR_external_semaphore_win32</c>). The clang-sharp generator skips
/// these for the same reason it skips the platform surface extensions: the
/// enclosing extensions are guarded by <c>VK_USE_PLATFORM_WIN32_KHR</c> in
/// the headers, so libclang never sees them without the Win32 SDK. The
/// fields themselves are platform-neutral (a Vulkan handle plus a
/// handle-type flag), so mirroring them by hand is trivial.
/// </summary>
/// <remarks>
/// <para>Only the <b>structs</b> live here, not the functions. Unlike the WSI
/// surface entry points — which the Khronos loader statically exports, so a
/// plain <c>[DllImport("vulkan-1")]</c> resolves them —
/// <c>vkGetMemoryWin32HandleKHR</c> / <c>vkGetSemaphoreWin32HandleKHR</c>
/// (and their <c>*Fd*</c> siblings) are <b>device-extension</b> entry points
/// that the loader is not required to export. They must be resolved through
/// <c>vkGetDeviceProcAddr</c>; the wrapper's <c>ExportableImage</c> /
/// <c>ExportableSemaphore</c> do exactly that. Declaring them as
/// <c>[DllImport]</c> throws <see cref="System.EntryPointNotFoundException"/>
/// on the loaders that omit them.</para>
/// <para>The Linux <c>fd</c> info structs
/// (<c>VkMemoryGetFdInfoKHR</c> / <c>VkSemaphoreGetFdInfoKHR</c>) are
/// generated normally — a file descriptor is a plain <c>int</c>, so those
/// headers need no platform SDK.</para>
/// </remarks>
public unsafe partial struct VkMemoryGetWin32HandleInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    /// <summary>The device memory whose OS handle is requested.</summary>
    [NativeTypeName("VkDeviceMemory")]
    public VkDeviceMemory_T* memory;

    /// <summary>Must be a single <c>OPAQUE_WIN32</c>-family bit that the memory was allocated exportable for.</summary>
    public VkExternalMemoryHandleTypeFlagBits handleType;
}

/// <summary>
/// Hand-authored mirror of <c>VkSemaphoreGetWin32HandleInfoKHR</c>. Same
/// rationale as <see cref="VkMemoryGetWin32HandleInfoKHR"/> — neutral fields,
/// Win32-guarded parent extension.
/// </summary>
public unsafe partial struct VkSemaphoreGetWin32HandleInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    /// <summary>The semaphore whose OS handle is requested.</summary>
    [NativeTypeName("VkSemaphore")]
    public VkSemaphore_T* semaphore;

    /// <summary>Must be a single <c>OPAQUE_WIN32</c>-family bit that the semaphore was created exportable for.</summary>
    public VkExternalSemaphoreHandleTypeFlagBits handleType;
}
