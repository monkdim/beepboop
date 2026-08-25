#!/usr/bin/env python3
"""
Generates TwoButton.Core job action tables from BossMod's ActionQueue definitions.

BossMod (BSD 3-Clause, https://github.com/awgil/ffxiv_bossmod) keeps per-job action and
status id tables that are regenerated from the game's own data every patch. Those ids are
facts about the game, and transcribing them by hand is exactly how a plugin ends up casting
the wrong ability in a raid - so we read them instead.

Only the id/name/level/kind facts are taken. The rotation logic in this repository is our
own; see docs/DESIGN.md.

Usage:  python3 tools/generate_action_tables.py <path-to-bossmod-checkout>
"""

import pathlib
import re
import sys

# ClassJob row ids for every DPS job, with where BossMod keeps its table.
JOBS = [
    ("Monk",        20, "Melee/MNK.cs",    "MNK"),
    ("Dragoon",     22, "Melee/DRG.cs",    "DRG"),
    ("Bard",        23, "Ranged/BRD.cs",   "BRD"),
    ("BlackMage",   25, "Casters/BLM.cs",  "BLM"),
    ("Summoner",    27, "Casters/SMN.cs",  "SMN"),
    ("Ninja",       30, "Melee/NIN.cs",    "NIN"),
    ("Machinist",   31, "Ranged/MCH.cs",   "MCH"),
    ("Samurai",     34, "Melee/SAM.cs",    "SAM"),
    ("RedMage",     35, "Casters/RDM.cs",  "RDM"),
    ("Dancer",      38, "Ranged/DNC.cs",   "DNC"),
    ("Reaper",      39, "Melee/RPR.cs",    "RPR"),
    ("Viper",       41, "Melee/VPR.cs",    "VPR"),
    ("Pictomancer", 42, "Casters/PCT.cs",  "PCT"),
]

MELEE = {"Monk", "Dragoon", "Ninja", "Samurai", "Reaper", "Viper"}

ENTRY = re.compile(r"^\s*(\w+)\s*=\s*(\d+)\s*,\s*//\s*(.*)$")
SHARED = re.compile(r"^\s*(\w+)\s*=\s*ClassShared\.(?:AID|SID)\.(\w+)\s*,")
LEVEL = re.compile(r"\bL(\d+)\b")
ROMAN = {"1": "", "2": " II", "3": " III", "4": " IV", "5": " V"}

# Cooldown group 57 is the global cooldown. A weaponskill with its own cooldown - Drill,
# Air Anchor, Chain Saw - is listed as "(group 4/57)" and still rolls the GCD, so matching
# only on the literal ", GCD" would classify those as off-globals and have the engine try
# to weave them.
GCD_MARKER = re.compile(r",\s*GCD\b|\bgroup\s+\d+/57\b|\bgroup\s+57\b")


def extract_enum(text, name):
    """Returns the body of `enum <name>` as a list of lines."""
    match = re.search(r"enum\s+" + name + r"\s*:\s*uint\s*\{(.*?)\n\}", text, re.S)
    return match.group(1).splitlines() if match else []


def display_name(identifier):
    """
    CamelCase identifier -> the game's display name, near enough for the verifier's
    normalised match. Trailing digits become roman numerals, which is how the game spells
    Fire III and Thunder II.
    """
    suffix = ""
    if identifier[-1] in ROMAN and not identifier[-2:].isdigit():
        suffix = ROMAN[identifier[-1]]
        identifier = identifier[:-1]

    words = re.findall(r"[A-Z][a-z0-9]*|[A-Z]+(?![a-z])", identifier)
    return " ".join(words) + suffix


def parse(path, shared_actions, shared_statuses):
    text = path.read_text(encoding="utf-8-sig")
    actions, statuses = [], []

    for line in extract_enum(text, "AID"):
        shared = SHARED.match(line)
        if shared and shared.group(2) in shared_actions:
            member, (aid, level, kind) = shared.group(1), shared_actions[shared.group(2)]
            actions.append((member, aid, level, kind))
            continue

        entry = ENTRY.match(line)
        if not entry:
            continue

        member, aid, comment = entry.group(1), int(entry.group(2)), entry.group(3)

        # Limit breaks have no level and are never part of a rotation.
        level = LEVEL.search(comment)
        if not level:
            continue

        kind = "Gcd" if GCD_MARKER.search(comment) else "OGcd"
        actions.append((member, aid, int(level.group(1)), kind))

    for line in extract_enum(text, "SID"):
        shared = SHARED.match(line)
        if shared and shared.group(2) in shared_statuses:
            statuses.append((shared.group(1), shared_statuses[shared.group(2)]))
            continue

        entry = ENTRY.match(line)
        if entry and int(entry.group(2)) != 0:
            statuses.append((entry.group(1), int(entry.group(2))))

    return actions, statuses


def parse_shared(path):
    text = path.read_text(encoding="utf-8-sig")
    actions, statuses = {}, {}

    for line in extract_enum(text, "AID"):
        entry = ENTRY.match(line)
        if not entry:
            continue
        level = LEVEL.search(entry.group(3))
        if not level:
            continue
        kind = "Gcd" if GCD_MARKER.search(entry.group(3)) else "OGcd"
        actions[entry.group(1)] = (int(entry.group(2)), int(level.group(1)), kind)

    for line in extract_enum(text, "SID"):
        entry = ENTRY.match(line)
        if entry and int(entry.group(2)) != 0:
            statuses[entry.group(1)] = int(entry.group(2))

    return actions, statuses


def dedupe(rows, key_index):
    """First entry wins, so the job's own table beats a shared alias."""
    seen, out = set(), []
    for row in rows:
        if row[0] in seen:
            continue
        seen.add(row[0])
        out.append(row)
    return out


def emit(job, job_id, actions, statuses, is_melee):
    lines = [
        "using TwoButton.Core.Model;",
        "",
        f"namespace TwoButton.Core.Jobs.{job};",
        "",
        "/// <summary>",
        f"/// {job} action and status ids.",
        "/// <para>",
        "/// Generated by <c>tools/generate_action_tables.py</c> from BossMod's ActionQueue",
        "/// definitions (BSD 3-Clause), which are regenerated from the game's own data each",
        "/// patch. Do not edit by hand - rerun the generator instead.",
        "/// </para>",
        "/// <para>",
        "/// Ids are still verified against the game's Action and Status sheets at startup and",
        "/// repaired by name on a mismatch, so a patch that shuffles them is corrected rather",
        "/// than mis-cast.",
        "/// </para>",
        "/// </summary>",
        f"public static class {job}Actions",
        "{",
    ]

    for member, aid, level, kind in actions:
        lines.append(
            f'    public static readonly ActionRef {member} = '
            f'new({aid}, "{display_name(member)}", ActionKind.{kind}, {level});'
        )

    action_names = {m for m, _, _, _ in actions}
    status_member = {m: (m + "Buff" if m in action_names else m) for m, _ in statuses}

    lines.append("")
    for member, sid in statuses:
        lines.append(
            f'    public static readonly StatusRef {status_member[member]} = '
            f'new({sid}, "{display_name(member)}");'
        )

    lines.append("")
    lines.append("    public static readonly IReadOnlyList<ActionRef> All =")
    lines.append("    [")
    for chunk_start in range(0, len(actions), 4):
        chunk = actions[chunk_start:chunk_start + 4]
        lines.append("        " + ", ".join(m for m, _, _, _ in chunk) + ",")
    lines.append("    ];")

    lines.append("")
    lines.append("    public static readonly IReadOnlyList<StatusRef> AllStatuses =")
    lines.append("    [")
    for chunk_start in range(0, len(statuses), 4):
        chunk = statuses[chunk_start:chunk_start + 4]
        lines.append("        " + ", ".join(status_member[m] for m, _ in chunk) + ",")
    lines.append("    ];")
    lines.append("}")
    return "\n".join(lines) + "\n"


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)

    root = pathlib.Path(sys.argv[1]) / "BossMod" / "ActionQueue"
    out_root = pathlib.Path(__file__).resolve().parent.parent / "src" / "TwoButton.Core" / "Jobs"

    shared_actions, shared_statuses = parse_shared(root / "ClassShared.cs")

    for job, job_id, rel, _abbrev in JOBS:
        actions, statuses = parse(root / rel, shared_actions, shared_statuses)
        actions = dedupe(actions, 0)
        statuses = dedupe(statuses, 0)

        target = out_root / job
        target.mkdir(parents=True, exist_ok=True)
        (target / f"{job}Actions.cs").write_text(
            emit(job, job_id, actions, statuses, job in MELEE), encoding="utf-8")

        print(f"{job:12} {len(actions):3} actions  {len(statuses):3} statuses")


if __name__ == "__main__":
    main()
