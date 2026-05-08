namespace Ahjo.Vulkan;

/// <summary>
/// Delegate handed to <see cref="Queue.ImmediateSubmit"/>. Receives the
/// already-begun <see cref="CommandRecorder"/> by reference; the caller
/// records work and returns. <see cref="Queue.ImmediateSubmit"/> ends,
/// submits, and waits on the queue afterwards.
/// </summary>
/// <remarks>
/// The <c>ref</c> parameter is required because <see cref="CommandRecorder"/>
/// is a <c>ref struct</c> — passing by value would copy a ref struct out
/// of its local context, which the C# ref-safety rules forbid in delegates.
/// Implementations should not call <see cref="CommandRecorder.End"/> or
/// <see cref="CommandRecorder.Dispose"/>; the surrounding helper does both.
/// </remarks>
public delegate void ImmediateRecord(ref CommandRecorder recorder);
