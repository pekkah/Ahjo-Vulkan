# Issue #237 — make `PipelineCache.WriteAtomic`'s publish step survive contention

Paired plan: `../plans/2026-10-04-issue-237-pipeline-cache-rename-retry.md`

Line citations are as of `main` at `7d4e4eb`.

## Problem

`PipelineCache.Save` (`src/Ahjo.Vulkan/Pipelines/PipelineCache.cs:63`) persists the cache through
`WriteAtomic` (`:151`): write a per-writer temp sibling (`:157`, `:160-163`), then publish it with
`PublishByRename` (`:164`, `:178-207`), a `File.Move(tmp, path, overwrite: true)` (`:195`) inside a
retry loop. #230/#231 (`dd96d49`) introduced that shape so concurrent savers of one path resolve to
last-writer-wins instead of throwing. The retry is **count-bound**: `maxAttempts = 10` (`:190`) with
`Thread.Sleep(attempt)` between attempts (`:200`, `:204`).

Issue #237 reports `WriteAtomic_ConcurrentSaversOfOnePath_AllCompleteAndFileReadable`
(`tests/Ahjo.Vulkan.Tests/PipelineCacheTests.cs:155-216`, 8 threads × 50 writes onto one path)
failing on `windows-latest` with `UnauthorizedAccessException` thrown from `:195` after the tenth
attempt. That is a production defect, not a test defect: `Save` throws where last-writer-wins was
the documented intent (`:180-189`).

Three things are wrong with `PublishByRename` as shipped:

1. **The budget is in the wrong unit.** The comment says "brief, growing backoff (1..9 ms)"
   (`:200`), i.e. ≈45 ms total. The real wall time is set by the OS timer, not the code: measured
   on this repo's dev machine (Windows 10.0.26200), `Thread.Sleep(1..9)` totals **141 ms**, because
   each sub-tick sleep rounds up to a ~15.6 ms timer tick. A count-bound budget's duration cannot be
   read off the code, and it is still too short (E2).
2. **The premise of the comment is false.** "That window is sub-millisecond" (`:187`) does not
   hold under sustained same-path contention: individual renames waited up to **259 ms** locally
   before succeeding (E2).
3. **The retry set is too broad.** `catch (IOException)` (`:198`) retries every `IOException`,
   which includes `FileNotFoundException` (HRESULT `0x80070002`), `DirectoryNotFoundException`,
   `PathTooLongException` and disk-full — deterministic failures that no amount of waiting fixes.
   It also retries on Linux/macOS, where `rename(2)` has no transient-contention failure mode at
   all, so the retries only delay a genuine error.

## Evidence

### E1 — the shipped test fails locally, not only on CI

`dotnet test tests/Ahjo.Vulkan.Tests -- --filter-method "*WriteAtomic_ConcurrentSaversOfOnePath*"`
at `7d4e4eb` on the dev machine: **4 of 10 runs failed**, each with
`System.UnauthorizedAccessException: Access to the path is denied.` — the same exception the issue
quotes from CI. The issue's framing ("a loaded hosted runner") is therefore incomplete: an
unloaded workstation reproduces it at ~40%.

### E2 — the denial is self-inflicted in-process contention

Standalone probes (scratch programs, not committed) replicate `WriteAtomic`'s publish loop with 8
threads × 50 writes onto one path, 10 repetitions each:

| Publish strategy | Failed runs | Retries | Longest single publish |
|---|---|---|---|
| Shipped: 10 attempts, `Sleep(attempt)` | **5 / 10** | 575 | 137 ms (longest *successful*; failed publishes exhausted the ~140 ms budget and were not timed) |
| Time-bound 2 s, backoff 1→50 ms, no lock | 0 / 10 | 516 | **259 ms** |
| Time-bound 2 s + process-wide lock around each `File.Move` | 0 / 10 | **0** | 35 ms (lock wait) |

Every retry in the unlocked runs was `UnauthorizedAccessException` (HRESULT `0x80070005`). With the
renames serialized in-process, 4 000 renames needed **zero** retries. So locally the entire failure
is concurrent `MoveFileEx` calls on one target denying each other, and individual threads starving
while seven others keep winning — not an antivirus scanner holding the destination after a rename.
Whether Defender holds files on the hosted runner is **unmeasured**; the design does not depend on
the answer (D1 covers both).

### E3 — which errors a held file produces (the retry set)

Probe on the same machine, holding a handle while calling `File.Move(tmp, dst, overwrite: true)`:

| Condition | Exception | HRESULT |
|---|---|---|
| destination open, `FileShare.Read` | `UnauthorizedAccessException` | `0x80070005` |
| destination open, `FileShare.ReadWrite` | `UnauthorizedAccessException` | `0x80070005` |
| destination open, `FileShare.Read \| Delete` | `UnauthorizedAccessException` | `0x80070005` |
| destination open, `FileShare.ReadWrite \| Delete` | `UnauthorizedAccessException` | `0x80070005` |
| destination open, `FileShare.None` | `UnauthorizedAccessException` | `0x80070005` |
| **source** (the temp) open, `FileShare.Read` | `IOException` (`ERROR_SHARING_VIOLATION`) | `0x80070020` |
| source missing | `FileNotFoundException` | `0x80070002` |
| destination read-only attribute | `UnauthorizedAccessException` | `0x80070005` |
| destination is a directory | `UnauthorizedAccessException` | `0x80070005` |

Consequences:

- **Any** open handle on the destination blocks the replace, regardless of `FILE_SHARE_DELETE`:
  `MoveFileEx(MOVEFILE_REPLACE_EXISTING)` here does not use POSIX rename semantics. Changing a
  reader's share mode cannot help.
- The transient set on Windows is exactly two shapes: `UnauthorizedAccessException`
  (destination held) and `IOException` with `HResult == 0x80070020` (temp held — e.g. a scanner
  inspecting the freshly written temp). Everything else `File.Move` throws is deterministic.
- `UnauthorizedAccessException` is **ambiguous**: a read-only destination or a directory at the
  path produces the same exception and HRESULT as a transient hold. No retry design can tell them
  apart without a second syscall; the cost of the ambiguity is that a permanently denied save
  waits out the whole budget before throwing (D1).

### E4 — in-repo holders of the destination

`Device.LoadOrCreatePipelineCache` (`src/Ahjo.Vulkan/Lifecycle/Device.cs:547`) reads the cache file
through a `FileStream` opened `FileShare.Read` (`:562`) for the duration of the read. Per E3, an
in-process load of path P concurrent with a save of P makes the save's rename fail with
`UnauthorizedAccessException` for as long as the read takes. The #230 scenario (Ahjo test suites in
parallel collections sharing one cache path) is exactly where load and save of one path can
overlap. A rename-scoped lock (D2) does not cover this holder; the time budget (D1) does.

### E5 — consumer audit

- `WriteAtomic` callers: `Save` only (`PipelineCache.cs:89`, `:107`), plus the test seam
  (`PipelineCacheTests.cs:185`).
- `Save` callers in this repo: `samples/HelloCube/Program.cs:543` (shutdown, wrapped in
  `try/catch` that logs to stderr) and `PipelineCacheTests.cs:52`, `:62`. The Ahjo engine calls it
  at shutdown (#230's body).
- Every caller is setup/shutdown time. `Pipelines/` is not one of the zero-allocation hot-path
  directories (`src/Ahjo.Vulkan/CLAUDE.md`), and `Save` already allocates (the temp-path string at
  `:157`). No benchmark covers it and none is needed.
- No other `Thread.Sleep`, `Stopwatch` or `Environment.TickCount64` use exists in `src/`
  (`grep`); this introduces the first, so there is no in-repo convention to match.

### E6 — static state on a handle struct

`HandleConventionsTests.PipelineLayout_HasNoStaticSideTables`
(`tests/Ahjo.Vulkan.Tests/HandleConventionsTests.cs:178-186`) forbids static fields on
`PipelineLayout` because #118 removed a static table keyed by raw handle values. No equivalent test
covers `PipelineCache`, and the field D2 adds is a process-wide gate unrelated to any handle — it
is not the shape #118 banned. Noted so a reviewer does not mistake one for the other.

## Decision

Two changes to `PublishByRename`, plus a narrowed retry set. No public API changes.

### D1 — time-bound the retry (backstop for what a lock cannot reach)

- **Budget: 2 seconds**, measured on a monotonic clock: `Stopwatch.GetTimestamp()` at entry,
  `Stopwatch.GetElapsedTime(start)` per check — allocation-free, unaffected by wall-clock changes,
  and high-resolution so the tests' elapsed assertions agree with the code's own measurement.
  `Environment.TickCount64` would also be monotonic but ticks at ~15.6 ms on Windows.
- **Backoff: 1, 2, 4, 8, 16, 32, 50, 50, … ms** (doubling, capped at 50 ms), each sleep clamped to
  the time remaining so the last attempt lands at the deadline. Small values round up to a timer
  tick in practice (E1's 141 ms); that is harmless because the loop exits on elapsed time, not
  attempt count.
- **Exit:** a transient failure is retried only while `elapsed < budget`; the first failure after
  that (or any non-transient failure) propagates unchanged — via exception filters, so the original
  exception and stack trace reach the caller.
- **No jitter.** The only source of lockstep retries was in-process threads, which D2 removes.

Why 2 s rather than the issue's "~1 s": a lock-free 8-writer run already needed 259 ms locally
(E2), CI is slower by an unmeasured factor, and the budget costs nothing on success. Its only cost
is E3's ambiguity — a save onto a read-only file or a directory now takes 2 s to throw instead of
~140 ms. That happens at shutdown on a misconfigured path; it is an acceptable price. The value is
a single `const` so it can be retuned with evidence.

### D2 — serialize in-process renames behind one process-wide lock

A `static readonly Lock` (the .NET 9+ `System.Threading.Lock`) on `PipelineCache`, held **only
around the single `File.Move` call** — never across the backoff sleep, never across the temp write.

E2 is the reason this is primary rather than optional: it removes the dominant failure mechanism
outright (0 retries in 4 000 renames), turns the #231 test deterministic in-process, and leaves D1
to cover what a lock cannot see — other processes, in-process readers (E4), and scanners. Neither
change alone is right: D1 alone still lets an in-process thread starve for hundreds of ms under
contention (E2, row 2) and depends on the budget outlasting the burst; D2 alone does nothing for
E4 or cross-process saves.

Shape decisions:

- **Process-wide, not per-path.** A rename is sub-millisecond when uncontended and `Save` is
  shutdown-rare, so serializing unrelated paths costs nothing measurable, while a per-path key
  would need a normalization that cannot be complete (case, 8.3 short names, junctions/symlinks,
  UNC vs mapped-drive aliases, per-directory case sensitivity) and a table whose entries never
  die.
- **Per attempt, not per publish.** Holding the lock across the retry loop would let one path
  stuck behind a cross-process hold block every other save in the process for up to the full
  budget.
- **Always taken, on every OS.** It is uncontended on POSIX and keeps one code path.

### D3 — retry only what is transient, only on Windows

`internal static bool IsTransientReplaceFailure(Exception e)` returns `true` only when
`OperatingSystem.IsWindows()` **and** `e` is either `UnauthorizedAccessException` or an
`IOException` whose `HResult == unchecked((int)0x80070020)` (`ERROR_SHARING_VIOLATION`). That is
exactly E3's transient set.

- `FileNotFoundException`, `DirectoryNotFoundException`, `PathTooLongException`, disk-full and
  every other `IOException` now propagate on the first attempt instead of being retried.
- On Linux/macOS nothing is retried: `rename(2)` atomically replaces the target even while it is
  open, so there is no contention failure to wait out. This is a behaviour change (today a POSIX
  failure is retried ten times) and a strict improvement.
- `ERROR_LOCK_VIOLATION` (`0x80070021`) is deliberately **not** included — `MoveFileEx` was not
  observed to produce it, and an unobserved code should not be retried for 2 s on speculation.

### D4 — failure leaves the previous cache intact and no temp behind

Unchanged from #231, now stated as a tested property: on final failure `WriteAtomic`'s existing
`catch` (`:166-175`) deletes the temp (best-effort; a delete failure is swallowed so the original
error surfaces) and rethrows. The destination is never opened for write — only replaced by the
rename — so a failed `Save` leaves the last successfully published cache byte-for-byte intact.
Nothing proves that today; the plan adds a test for it.

Residual, accepted: if a scanner holds the temp *without* `FILE_SHARE_DELETE` at cleanup time, the
best-effort delete fails and one `path.<pid>-<tid>.tmp` sibling is left behind. Retrying the cleanup
too would double the worst-case stall to fix a cosmetic leak; not done.

### D5 — test strategy

- **Keep the #231 contention test exactly as is** (8 × 50, default budget). With D2 it is
  deterministic in-process; it is the regression test for #237.
- **Add an internal budget overload** `WriteAtomic(string, ReadOnlySpan<byte>, TimeSpan)` as the
  test seam, so the hold tests below control the budget without making the suite slow.
- **Hold-then-release** (Windows only): hold the destination open, publish on a worker thread
  with a 10 s budget, check after ~200 ms that the worker is still running (deterministic — the
  rename cannot succeed while the handle is open), then release; the publish must complete with
  the new content. The still-running check proves the retry path ran, and the 10 s budget leaves a
  50× margin on a slow runner while the test finishes in ~200 ms.
- **Budget exhaustion** (Windows only): hold the destination for the whole call, publish with a
  100 ms budget; it must throw `UnauthorizedAccessException`, take at least 100 ms, leave the
  destination's prior content intact (D4) and leave no temp sibling. Fully deterministic — the
  handle is never released during the call.
- **Classifier** (all platforms): `IsTransientReplaceFailure` on constructed exceptions. The two
  transient shapes assert `== OperatingSystem.IsWindows()`, so the test is correct on every OS
  without a gate; the deterministic shapes assert `false`.
- The two hold tests gate on `TestGate.RequirePlatform(OperatingSystem.IsWindows(), …)`: on POSIX a
  held destination does not block `rename(2)`, so the exhaustion test would (correctly) not throw.
  This matches CI reality — wrapper tests run on `windows-latest` only (#32, `.github/CLAUDE.md`).

### Why not the alternatives

- **Option 2 — keep the budget, reduce the test's contention.** Rejected by the issue and by E1:
  the shipped code fails ~40% of local runs at 8 writers, so a smaller test would hide a defect
  that real concurrent savers (the #230 scenario) still hit.
- **Option 1 alone (time budget, no lock).** Works on E2's numbers but keeps 500+ retries per run
  and per-thread starvation up to 259 ms locally; whether it passes on CI depends on the
  contention burst ending before the budget, which is the same class of guess that produced
  #237.
- **Option 3 alone (lock, keep the count-bound retry).** Fixes the in-process case but leaves
  E4's in-process readers, cross-process savers and scanners on the ~140 ms count-bound budget
  that is unknowable from the code.
- **Per-path lock table.** Same benefit as the process-wide lock at much higher cost: incomplete
  key normalization and an ever-growing table (D2).
- **Lock held across the whole retry loop.** One path stuck behind another process would block
  every other save in the process for the full budget (D2).
- **Open `LoadOrCreatePipelineCache`'s reader with `FileShare.Delete`.** E3 shows `MoveFileEx`
  fails on any open destination handle regardless of share mode; the change would be a no-op.
- **Take the lock in `LoadOrCreatePipelineCache` too.** It would hold the gate for a
  multi-megabyte read and couple loading to saving; D1 already absorbs a concurrent read.
- **`File.Replace` / `ReplaceFile`.** It requires the destination to exist (a first save would
  need a second code path) and carries backup-file semantics this flow does not want; whether it
  behaves any better against a held destination was not probed, so it buys a second code path for
  an unproven gain.
- **Retry every `IOException` with a long budget.** Turns deterministic failures (missing temp,
  bad directory, disk full) into a 2 s stall before the same exception (D3).
- **Jittered backoff.** The only lockstep source is in-process threads, which D2 serializes.

## Cross-links

- **#230 / PR #231** (`dd96d49`) — introduced per-writer temp names and the count-bound retry this
  replaces. No spec/plan pair was written for #231; its rationale lives in the PR body and the
  `PublishByRename` comment (`:180-189`), whose "sub-millisecond window" claim E2 refutes and the
  plan rewrites.
- **#118** — static side tables on handle structs; E6 explains why D2's gate is not that.
- **#32** — wrapper tests are Windows-only; the Windows gate on D5's hold tests is consistent with
  it, and no Linux lane is added.

## Open questions

None. The budget (2 s) and the POSIX no-retry change are decided above; both are one-line edits
if evidence later argues otherwise.
