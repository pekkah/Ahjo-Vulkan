# docs/design/ — spec-driven design docs

Non-trivial design work produces a paired spec + plan here, named after the GitHub issue that drives it:

- `specs/YYYY-MM-DD-issue-NN-<topic>-design.md` — the design spec: **what and why**
- `plans/YYYY-MM-DD-issue-NN-<topic>.md` — the implementation plan: **how**

The date is the day the spec is written; `NN` is the issue number; `<topic>` is a short kebab-case slug. The plan's first line links back to its spec (`Paired with ../specs/…`).

## Quality bar (calibrate against the issue-118 pair)

A spec is evidence-based, not aspirational:

- Opens with **Problem** — the defect or gap, citing `file.cs:line` for every claim.
- An **Evidence** section: what an actual audit of the codebase showed (call sites counted, consumers listed). Claims without locations don't belong.
- A **Decision** section that names the chosen option and has a "why not the alternatives" subsection — rejected options get a sentence each on why.
- Links related issues/specs it resolves, prevents, or must land consistently with.

A plan is executable without re-deciding anything:

- Numbered steps, each naming the exact files touched and the exact change (signatures, member names, message shapes — not "update the interface").
- A tests step describing the concrete cases to add, and a benchmarks/docs step when hot paths or public API change.
- Anything the architect deliberately left open is marked as such; the implementer stops and asks rather than improvising design.
