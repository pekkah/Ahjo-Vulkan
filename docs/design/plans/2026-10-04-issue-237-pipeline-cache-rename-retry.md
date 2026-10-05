Paired with ../specs/2026-10-04-issue-237-pipeline-cache-rename-retry-design.md

# Plan — issue #237: time-bound and serialize `PipelineCache`'s rename publish

Branch: `issue-237-pipeline-cache-rename-retry` (from `main`; no stacked PRs). Seven steps: 1-4 the
wrapper code, 5 the docs/comments, 6 the tests, 7 verification and PR.

**Files this PR may touch — and no others:**

- `src/Ahjo.Vulkan/Pipelines/PipelineCache.cs`
- `tests/Ahjo.Vulkan.Tests/PipelineCacheTests.cs`

**Explicitly out of scope:** `src/Ahjo.Vulkan/Lifecycle/Device.cs` (`LoadOrCreatePipelineCache`'s
`FileShare.Read` at `:562` stays — spec E3 shows a share-mode change is a no-op, and the time budget
absorbs a concurrent read), `samples/HelloCube/Program.cs`, `Save`'s two-call query loop
(`PipelineCache.cs:79-111`), the temp-file naming (`:157`), anything under `Generated/`, `native/`,
`tools/`. No public API changes.

Line numbers are as of `main` at `7d4e4eb`; re-locate by quoted text once you start editing.

---

## 1. `PipelineCache.cs` — constants, the gate, and the `using`

Add `using System.Diagnostics;` to the using block (`:1-6`, alphabetical position after
`System.Buffers`).

Add these private members to the struct, directly above the `// Test seam (InternalsVisibleTo)`
comment at `:148`:

```csharp
private const int RenameBudgetMilliseconds = 2_000;
private const int MaxRenameBackoffMilliseconds = 50;
private const int ErrorSharingViolationHResult = unchecked((int)0x80070020);
private static readonly Lock RenameGate = new();
```

`Lock` is `System.Threading.Lock` (`System.Threading` is already imported at `:5`). Give
`RenameGate` a one-line comment saying it serializes in-process renames only and is never held
across a sleep (spec D2), and that it is a process-wide gate, not a handle-keyed table (spec E6).

## 2. `PipelineCache.cs` — `WriteAtomic` budget overload

Replace the single `WriteAtomic` (`:151`) with two overloads:

```csharp
internal static void WriteAtomic(string path, ReadOnlySpan<byte> bytes)
    => WriteAtomic(path, bytes, TimeSpan.FromMilliseconds(RenameBudgetMilliseconds));

internal static void WriteAtomic(string path, ReadOnlySpan<byte> bytes, TimeSpan renameBudget)
```

The three-argument body is today's body (`:153-175`) unchanged except that `PublishByRename(tmp, path)`
(`:164`) becomes `PublishByRename(tmp, path, renameBudget)`. Keep the `catch` cleanup block
(`:166-175`) exactly as is (spec D4). `Save`'s two call sites (`:89`, `:107`) keep calling the
two-argument overload — do not touch them.

Update the test-seam comment above (`:148-150`) to say the three-argument overload exists so tests
can drive the retry budget without waiting out the default.

## 3. `PipelineCache.cs` — the classifier and two helpers

Add, after `PublishByRename`:

```csharp
internal static bool IsTransientReplaceFailure(Exception e)
private static bool ShouldRetryRename(Exception e, long start, TimeSpan budget)
private static int RenameBackoffMilliseconds(int attempt, long start, TimeSpan budget)
```

- `IsTransientReplaceFailure`: `true` iff `OperatingSystem.IsWindows()` **and** (`e is
  UnauthorizedAccessException` **or** (`e is IOException` **and** `e.HResult ==
  ErrorSharingViolationHResult`)). Nothing else — not `ERROR_LOCK_VIOLATION`, not other `IOException`
  subclasses (spec D3). `internal` because step 6 tests it directly.
- `ShouldRetryRename`: `IsTransientReplaceFailure(e) && Stopwatch.GetElapsedTime(start) < budget`.
- `RenameBackoffMilliseconds`: the doubling delay `1 << (attempt - 1)` capped at
  `MaxRenameBackoffMilliseconds` (cap the shift count first so it cannot overflow, e.g.
  `Math.Min(attempt - 1, 6)`), then clamped to the remaining budget, rounded up, never negative.
  Sequence for attempts 1, 2, 3, …: 1, 2, 4, 8, 16, 32, 50, 50, … ms.

## 4. `PipelineCache.cs` — rewrite `PublishByRename`

New signature: `private static void PublishByRename(string tmp, string path, TimeSpan budget)`.

Record `long start = Stopwatch.GetTimestamp();` once at entry. The loop must have **exactly** this
lock and catch structure. It is load-bearing (spec D2): the lock covers only `File.Move`, and the
sleep runs after the `lock` statement's `finally` has released the gate.

```csharp
for (int attempt = 1; ; attempt++)
{
    try
    {
        lock (RenameGate)
            File.Move(tmp, path, overwrite: true);
        return;
    }
    catch (UnauthorizedAccessException e) when (ShouldRetryRename(e, start, budget)) { }
    catch (IOException e) when (ShouldRetryRename(e, start, budget)) { }
    Thread.Sleep(RenameBackoffMilliseconds(attempt, start, budget));
}
```

Do **not** move the sleep inside the `lock`, and do not wrap the whole loop in the `lock`. The
filters keep a non-retried exception's original stack. Exception filters run during the first pass
of exception handling, **before** the `lock` statement's `finally` releases the gate — so
`ShouldRetryRename` must stay a pure check (classifier + one `Stopwatch` read). No sleeping, logging
or I/O in a filter; anything slow there holds `RenameGate` and silently undoes spec D2. The empty filtered `catch` bodies follow the
existing pattern at `:172-173`. If an analyzer objects to the empty bodies or to the shape under
`TreatWarningsAsErrors`, **stop and report** — do not `#pragma` it and do not restructure the lock
scope to dodge it.

Delete `const int maxAttempts = 10;` (`:190`) and both old `catch` clauses (`:198-205`).

## 5. Comments and XML docs

- **`PublishByRename`'s comment block** (`:180-189`): rewrite it. It currently claims a
  "sub-millisecond" window and "1..9 ms" backoff, both refuted by spec E1/E2. The new text must say:
  - Windows `MoveFileEx(REPLACE_EXISTING)` fails while **any** handle is open on the destination,
    whatever its share mode (`UnauthorizedAccessException`), or while the temp is held
    (`IOException`, `ERROR_SHARING_VIOLATION`). POSIX `rename(2)` has neither failure mode, so
    nothing is retried there.
  - In-process renames are serialized by `RenameGate`; that removed every retry in an 8-writer
    probe (issue #237). The time budget covers what a lock cannot see: other processes, in-process
    readers (`Device.LoadOrCreatePipelineCache` opens the file `FileShare.Read`), and scanners.
  - The budget is time-based because `Thread.Sleep`'s real duration depends on the OS timer tick,
    so an attempt count does not determine a duration.
  - A permanent `ERROR_ACCESS_DENIED` (read-only file, a directory at the path) cannot be told apart
    from a transient one and waits out the budget before throwing.
- **`WriteAtomic`'s comment** (`:153-156`): keep it, adding one sentence that on failure the
  destination is untouched, so the previously published cache survives.
- **`Save`'s XML docs** (`:51-62`): add a `<para>` to `<remarks>` along these lines (wording may be
  tightened, content may not change):

  > Concurrent saves of one path resolve to last-writer-wins. On Windows, replacing the file is
  > retried for up to 2 seconds while another handle holds it (another process saving, a reader, an
  > antivirus scan); after that the original exception propagates and the previously saved file is
  > left intact.

No `docs/` or README changes: nothing outside `PipelineCache.cs` describes the publish step (`grep`
of `README.md` and `docs/*.md` for pipeline cache finds nothing).

## 6. Tests — `tests/Ahjo.Vulkan.Tests/PipelineCacheTests.cs`

Add `using System.Diagnostics;`. All new tests are driverless (no `TestGate.RequireDriver`).

**6a. Existing `WriteAtomic_ConcurrentSaversOfOnePath_AllCompleteAndFileReadable` (`:155-216`):
leave the body untouched** (8 writers × 50 reps, default budget). Add two lines to its comment: it
also guards #237, where the count-bound rename retry ran out under this contention, and it is
deterministic in-process because `RenameGate` serializes the renames.

**6b. `WriteAtomic_DestinationHeldThenReleased_PublishesAfterRelease`**

- First statement: `TestGate.RequirePlatform(OperatingSystem.IsWindows(), "MoveFileEx refuses to
  replace an open destination; POSIX rename(2) does not, so there is nothing to wait out.");`
- Path built as in the existing test (`Path.GetTempPath()` + `ahjo-cache-{Guid:N}.bin`).
- `File.WriteAllBytes(path, old)` with `old` = 16 bytes of `1`.
- Open `holder = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)`.
- Start a worker `Thread` that calls `PipelineCache.WriteAtomic(path, @new, TimeSpan.FromSeconds(10))`
  (`@new` = 16 bytes of `2`) and captures any exception into a local.
- Main thread: `Thread.Sleep(200)`, then `Assert.True(worker.IsAlive)` — deterministic, because the
  rename cannot succeed while `holder` is open and a 10 s budget cannot have run out. This is what
  proves the retry path ran rather than a lucky first attempt.
- `holder.Dispose()`; `Assert.True(worker.Join(TimeSpan.FromSeconds(15)))`; assert no exception was
  captured; `Assert.Equal(@new, File.ReadAllBytes(path))`; assert no `name + ".*.tmp"` sibling.
- `finally`, in this order: dispose the holder (idempotent), then `worker.Join(TimeSpan.FromSeconds(15))`,
  **then** delete the file and any temp siblings (same cleanup as the existing test, `:210-215`). If
  an assertion fails while the worker is still retrying, deleting first would let its rename land
  after the cleanup and leave a stray file for the next test.

**6c. `WriteAtomic_DestinationHeldPastBudget_ThrowsAndKeepsPreviousCache`**

- Same platform gate and path setup; `File.WriteAllBytes(path, old)`.
- With the holder open for the whole call: `long t0 = Stopwatch.GetTimestamp();`, then
  `Assert.Throws<UnauthorizedAccessException>(() => PipelineCache.WriteAtomic(path, @new,
  TimeSpan.FromMilliseconds(100)))`, then
  `Assert.True(Stopwatch.GetElapsedTime(t0) >= TimeSpan.FromMilliseconds(100))`.
- Dispose the holder; `Assert.Equal(old, File.ReadAllBytes(path))` (spec D4: a failed save leaves
  the previous cache intact); assert no temp sibling.
- **No upper bound on elapsed time** — it would be the flaky half of this test.
- `finally` cleanup as in 6b.

**6d. `IsTransientReplaceFailure_RetriesOnlySharingFailuresOnWindows`** — `[Fact]`, no gate, runs on
every OS:

| Input | Expected |
|---|---|
| `new UnauthorizedAccessException()` | `OperatingSystem.IsWindows()` |
| `new IOException("x", unchecked((int)0x80070020))` | `OperatingSystem.IsWindows()` |
| `new FileNotFoundException()` | `false` |
| `new DirectoryNotFoundException()` | `false` |
| `new IOException("x", unchecked((int)0x80070070))` (disk full) | `false` |
| `new IOException("x", unchecked((int)0x80070021))` (lock violation) | `false` |
| `new IOException()` | `false` |
| `new InvalidOperationException()` | `false` |

**OPEN (implementer: verify, don't improvise):** if 6b's `worker.IsAlive` assertion or 6c's exception
type ever disagrees with spec E3 on the dev machine (e.g. a different Windows build returns
`IOException` for a held destination), stop and report the observed type/HRESULT — that would
change D3's transient set, which is a design decision.

## 7. Verification and PR

1. `dotnet build Ahjo.Vulkan.slnx` — zero warnings from the touched files.
2. Run the contention test **10 times**:
   `dotnet test tests/Ahjo.Vulkan.Tests -- --filter-method "*WriteAtomic_ConcurrentSaversOfOnePath*"`.
   Baseline at `7d4e4eb` was 4/10 failed (spec E1); the PR body must quote the after-count
   (expected 10/10 passed).
3. Run 6b-6d 5 times each the same way, then the full `dotnet test`.
4. Benchmarks: none. `Pipelines/PipelineCache.Save` is setup/shutdown-time (spec E5); state that in
   the PR body so `bench-coverage-checker` has the reasoning.
5. Reviewers, per the root `CLAUDE.md`: run `vulkan-validation-reviewer` (the diff is under
   `Pipelines/`; no Vulkan calls change, so findings are not expected) and `bench-coverage-checker`.
6. Commit: `Pipelines: time-bound and serialize PipelineCache's rename publish (#237)`. PR body:
   the spec's E1/E2 numbers (before/after), the behaviour changes callers can observe — POSIX no
   longer retries; a permanently denied save on Windows takes up to 2 s to throw;
   non-transient `IOException`s throw on the first attempt — and `Closes #237`.
