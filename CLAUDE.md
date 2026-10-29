# CLAUDE.md

@AGENTS.md

`AGENTS.md` (above) is the shared source of truth for project overview, commands,
architecture, migrations, and conventions — it's also read natively by Codex CLI and
JetBrains Junie. Everything below is specific to Claude Code.

## Conventions

- `.claude/rules/backend-patterns.md` — module feature structure, naming conventions, C# use/avoid, test conventions. Loads automatically when Claude reads files under `src/api/` or `tests/`.
- `.claude/rules/frontend-patterns.md` — Angular folder layout, TypeScript rules, Angular use/avoid, state management, HTTP patterns, accessibility. Loads automatically when Claude reads files under `src/web/`.

## Skills

Use the `Skill` tool to invoke these when relevant:

| Skill | When |
|-------|------|
| `fastendpoints` | Before creating any new Endpoint, Group, or Validator |
| `errorOr-patterns` | When working with ErrorOr, mapping errors to HTTP responses |
| `faber-feature-template` | When adding a new feature to an existing module |
| `faber-module-bootstrapping` | When creating a new module from scratch |
| `keycloak-aspnet-integration` | When working with sign-in, sessions or the Keycloak realm config |
| `vault-secrets-aspnet` | When working with Vault secrets or the Vault module |
| `impl-gh-issue` | `/impl-gh-issue <number>` — implement a GitHub issue end-to-end, sized to the issue (user-invoked only) |

The rest of the skill catalog is auto-tiered via `.claude/settings.json`; scope policy is documented in `docs/ai/skills-inventory-scope.md`. `AGENTS.md` lists the same 7 skills as plain file-path pointers for tools without a `Skill` mechanism (Codex, Junie).

## Git

- Do NOT add `Co-Authored-By` or any other self-references to commit messages
- Split changes into separate commits grouped by logical concern
- Commit messages: follow [Conventional Commits](https://www.conventionalcommits.org/) — `type(scope): description` (e.g., `feat(auth): add refresh token endpoint`)
- Branch naming: `{issue-number}-{kebab-case-description}` (e.g., `207-tests-features-signin`)

## GitHub Workflow

Issue labeling, release-please flow, and templates live in the `github-workflow` skill.
