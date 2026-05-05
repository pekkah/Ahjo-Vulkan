using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Scope object handed back from <see cref="CommandBufferPool.Begin"/>.
/// <c>ref struct</c> so the recorder cannot escape its frame, cannot be
/// captured into a closure, cannot live across an async boundary —
/// matching Vulkan's external-synchronization contract on
/// <c>VkCommandBuffer</c>.
/// </summary>
/// <remarks>
/// <para>This file is the structural skeleton from issue 12 (#13): begin
/// the buffer on construction, end on <see cref="Dispose"/>, surface the
/// raw handle for downstream submit calls. Issue 16 (#17) will land the
/// full recording surface (<c>BeginRendering</c>, bind family, draw
/// family, push constants, compute) on this same type.</para>
/// <para>The recorder owns the buffer's "recording" lifetime, not its
/// allocation lifetime. The pool retains ownership of the underlying
/// <c>VkCommandBuffer_T*</c> and recycles it on
/// <see cref="CommandBufferPool.ResetForFrame"/>.</para>
/// </remarks>
public unsafe ref struct CommandRecorder : IDisposable
{
    private readonly CommandBufferPool _pool;
    internal readonly VkCommandBuffer_T* Handle;
    private bool _ended;

    internal CommandRecorder(CommandBufferPool pool, VkCommandBuffer_T* handle)
    {
        _pool   = pool;
        Handle  = handle;
        _ended  = false;
    }

    public bool IsNull => Handle == null;

    /// <summary>
    /// Calls <c>vkEndCommandBuffer</c> and returns the buffer to the
    /// pool's free-list. Idempotent; safe to call from a <c>using</c>.
    /// </summary>
    public void Dispose()
    {
        if (_ended || Handle == null) return;
        _ended = true;
        Vk.vkEndCommandBuffer(Handle).ThrowIfFailed();
        _pool.Retire(Handle);
    }
}
