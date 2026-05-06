namespace Ahjo.Vulkan;

/// <summary>
/// One entry in <see cref="Queue.Submit2"/>'s wait or signal semaphore
/// list. Pairs a binary semaphore with the pipeline stage at which the
/// dependency operates — Vulkan needs both to compile a precise
/// dependency, not just "wait for the semaphore at the top of pipe".
/// </summary>
/// <remarks>
/// Binary semaphore only. Timeline semaphores carry a value scalar and
/// will get a sibling type when the wrapper grows a multi-queue
/// timeline-driven submit path.
/// </remarks>
public readonly record struct SemaphoreSubmit(BinarySemaphore Semaphore, Stage Stage);
