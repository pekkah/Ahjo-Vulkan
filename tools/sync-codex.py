"""Mirror the Claude Code layer into the Codex layer.

  CLAUDE.md (root + scoped)        -> AGENTS.md next to it
  .claude/skills/<s>/SKILL.md      -> .agents/skills/<s>/SKILL.md
  .claude/agents/<a>.md            -> .codex/agents/<a>.toml

Usage: python tools/sync-codex.py [--check]

The Claude files are the source of truth; edit those, then run this.
--check writes nothing and exits 1 if any Codex file is out of date.
"""
import pathlib
import subprocess
import sys
import tomllib

repo = pathlib.Path(__file__).resolve().parent.parent
check = "--check" in sys.argv

REPLACEMENTS = [
    ("# Ahjo.Vulkan — Claude Project Memory", "# Ahjo.Vulkan — Codex Project Memory"),
    ("## What lives in `~/.claude/projects/.../memory/` (auto-memory)",
     "## What stays out of this file"),
    ("lives in auto-memory, not in this file", "lives in agent memory or the issue tracker, not in this file"),
    ("`.claude/agents/`", "`.codex/agents/`"),
    (".claude/agents/bench-coverage-checker.md", ".codex/agents/bench-coverage-checker.toml"),
    (".claude/skills/", ".agents/skills/"),
    ("CLAUDE.md", "AGENTS.md"),
]


# Whole passages whose Codex wording is not a mechanical rename. Swapped for a
# placeholder before REPLACEMENTS run, so the renames cannot touch them.
VERBATIM = [
    ("`AGENTS.md` (root + scoped), `.codex/agents/*.toml` and `.agents/skills/` are generated "
     "from the CLAUDE.md files, `.claude/agents/` and `.claude/skills/`. Edit the Claude side, "
     "then run `python tools/sync-codex.py` and commit both. `--check` exits 1 when the mirror "
     "is stale.",
     "This file, the scoped AGENTS.md files, `.codex/agents/*.toml` and `.agents/skills/` are "
     "**generated** from the Claude Code layer (the CLAUDE.md files, `.claude/agents/` and "
     "`.claude/skills/`) by `tools/sync-codex.py`. Don't edit them directly: edit the Claude "
     "side, re-run the script and commit both. `--check` exits 1 when the mirror is stale."),
]


def transform(text):
    for i, (old, _) in enumerate(VERBATIM):
        text = text.replace(old, f"\0VERBATIM{i}\0")
    for old, new in REPLACEMENTS:
        text = text.replace(old, new)
    for i, (_, new) in enumerate(VERBATIM):
        text = text.replace(f"\0VERBATIM{i}\0", new)
    return text


def read(p):
    return p.read_bytes().decode("utf-8").replace("\r\n", "\n")


changed = []


def emit(dest, content):
    old = read(dest) if dest.exists() else None
    if old == content:
        return
    changed.append(str(dest.relative_to(repo)))
    if not check:
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(content.encode("utf-8"))


tracked = subprocess.run(["git", "-C", str(repo), "ls-files"], capture_output=True, text=True, check=True).stdout.split()
for rel in tracked:
    if rel == "CLAUDE.md" or rel.endswith("/CLAUDE.md"):
        src = repo / rel
        emit(src.with_name("AGENTS.md"), transform(read(src)))

for skill in sorted((repo / ".claude/skills").glob("*/SKILL.md")):
    emit(repo / ".agents/skills" / skill.parent.name / "SKILL.md", transform(read(skill)))


def toml_str(s):
    if "\n" not in s and '"' not in s and "\\" not in s:
        return f'"{s}"'
    if "\n" not in s and "'" not in s:
        return f"'{s}'"
    return '"""' + s.replace("\\", "\\\\").replace('"""', '\\"""') + '"""'


for agent in sorted((repo / ".claude/agents").glob("*.md")):
    text = read(agent)
    _, front, body = text.split("---\n", 2)
    meta = {}
    for line in front.splitlines():
        if ":" in line:
            k, v = line.split(":", 1)
            meta[k.strip()] = v.strip()
    instructions = transform(body.lstrip("\n").rstrip("\n"))
    out = (f"name = {toml_str(meta['name'])}\n"
           f"description = {toml_str(meta['description'])}\n"
           f"developer_instructions = {toml_str(chr(10) + instructions)}\n")
    parsed = tomllib.loads(out)
    assert parsed["developer_instructions"].lstrip("\n") == instructions, agent
    assert parsed["description"] == meta["description"], agent
    emit(repo / ".codex/agents" / f"{agent.stem}.toml", out)

print(("would change" if check else "changed") + f" {len(changed)} file(s):")
for c in changed:
    print("  " + c)
if check and changed:
    sys.exit(1)
