# Skills Inventory Scope

**Date:** 2026-09-03  
**Status:** Adopted

## Purpose

Define which skill artifacts are tracked by root `skills-lock.json` versus maintained manually in root `.claude/skills/`.

## Policy

### 1. Root `.claude/skills/` is the authoritative runtime inventory

All project-available skills live in root `.claude/skills/`.

In this repository, entries under `.claude/skills/` may resolve via symlinks into `.agents/skills/`. Treat `.claude/skills/` as the runtime entrypoint and `.agents/skills/` as the backing storage when present.

### 2. Root `skills-lock.json` tracks only externally sourced skills

`skills-lock.json` is reserved for skills installed from external sources, for example GitHub-backed `npx skills add ...` installs.

Do **not** invent synthetic `source`, `sourceType`, `skillPath`, or hash metadata for manually authored project skills just to make the lockfile exhaustive.

### 3. Faber-authored manual skills are maintained in-repo

As of 2026-09-07, these root skills are intentionally outside `skills-lock.json` scope:

- `errorOr-patterns`
- `faber-feature-template`
- `faber-module-bootstrapping`
- `fastendpoints`
- `github-workflow`
- `impl-gh-issue`
- `keycloak-aspnet-integration`

`mailpit-integration` was previously (incorrectly) listed here; it is externally sourced and tracked in `skills-lock.json` under `aaronontheweb/dotnet-skills` (sourceType `github`), so it has been removed from this list. `impl-gh-issue` was added after this doc's original 2026-05-03 date (PRs #441/#442). `impl-gh-issue` and `github-workflow` originally lived under `.claude/skills/` as real directories (not symlinks) — as of 2026-09-07 both were moved to `.agents/skills/` as backing storage with `.claude/skills/` symlinked back, so every Faber-authored skill now follows rule 1's symlink pattern uniformly; neither is tracked in `skills-lock.json`, so both belong here.

Note: GitHub issue #436 predicted the corrected count would be "6" — that prediction predates `impl-gh-issue` landing. The verified count was 8, with `impl-gh-issue` replacing `mailpit-integration` in the list and `github-workflow` added on 2026-09-07. `vault-secrets-aspnet` was removed with the Vault module (#83), leaving 7.

These skills are versioned directly through Git as normal repository content.

## Verification Rule

When comparing root `.claude/skills/` to root `skills-lock.json`:

- Every externally sourced installed skill should appear in `skills-lock.json`
- The only expected unmatched root skills are the eight Faber-authored manual skills listed above (see the note above on the `mailpit-integration` / `impl-gh-issue` / `github-workflow` correction)

## Skill Tier Decisions

The repo owner confirmed this tiering decision on 2026-09-03:

- **`simplify` stays `"on"`.** It is a built-in Claude Code quality-review skill. Auto-invocable is intentional: it only touches code already changed in the session and catches reuse/simplification drift before review. It is deliberately NOT in CLAUDE.md's skills table, because that table is now curated down to Faber-authored skills only.
CLAUDE.md's skills table is now curated to Faber-authored skills only, so absence from that table no longer implies anything about a skill's tier. `.claude/settings.json` is the single source of truth for tiering.

## Settings Scope

Tracked `.claude/settings.json` contains shared project policy: tiers for repository-available skills and intentional unprefixed built-in skills. Namespaced overrides such as `plugin-name:skill-name` depend on plugins installed by an individual developer, so they belong in ignored `.claude/settings.local.json` instead. This keeps personal plugin availability out of repository policy without changing each developer's local skill behavior.

## Maintenance Notes

- If a manual skill is later distributed through an external source and installed via `npx skills add`, it may then move under `skills-lock.json` tracking
- If a new project-authored skill is added under root `.claude/skills/`, update this document unless the skill is also added through an external installer
- `src/web/faber-app/skills-lock.json` is deprecated; root `skills-lock.json` is the only lockfile in scope
