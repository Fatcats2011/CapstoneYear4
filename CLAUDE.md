# Dead on Arrival

## Keep the design documents current

The GDD and TDD must match the game. Update them as part of the work, not afterwards.

| Document | Covers | Never put here |
|---|---|---|
| `docs/design/DoA GDD.docx` | What the player sees and why: rules, values, modes, controls, UI, levels, scope | Class names, code structure, tools |
| `docs/design/DoA TDD.docx` | How it is built: systems, classes, packages, networking, tests, builds, repo process | Design rationale that belongs in the GDD |

Keep them as two separate files. They may refer to each other by name, but never merge them or copy a section across.

- **When:** any change to gameplay, rules, tuning numbers, UI, modes, content, systems, packages, the build or the repo process updates the matching document in the same piece of work. A change can need both.
- **Skip:** bug fixes and refactors that change nothing either document says.
- **How:** edit the `.docx` itself with the docx skill, keeping its styles, and keep the table of contents in step with any heading change. It has no page numbers, so it can't go stale.
- **Record it:** add a line to the document's Revision History, bumping the minor version (3.0, 3.1...) on the cover too.
- **Truth:** check values against the code and scenes, not against other docs. When a doc and the code disagree, the code wins and the doc gets fixed.
- **Figures:** a figure that no longer matches the game gets replaced or removed.
