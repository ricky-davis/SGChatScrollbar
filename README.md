# SGChatScrollbar

Adds a working scrollbar to the **Sledding Game** chat box.

The game's chat ships with a scrollbar widget that is left disabled and never fed any input, so by default you can only see the most recent few messages. SGChatScrollbar enables it and wires up scrolling, letting you scroll back through your full chat history.

## Client-Side

This is a client-side mod — install it for yourself to get a scrollbar in your own chat. It does not change anything for other players and does not need to be installed by the lobby host.

## Features

- An always-visible scrollbar handle that sizes itself to how much history there is.
- Mouse-wheel scrolling while the pointer is over the chat (a quarter page per notch).
- Scrolls through the full retained history (up to 100 messages).
- Auto-follow: stays pinned to the newest message when you are at the bottom, and holds your position when you have scrolled up so new messages don't yank you back down.

## Installation

### Gale / r2modman

Install with Gale or r2modman. MelonLoader is declared as a dependency.

### Manual

1. Install MelonLoader for Sledding Game.
2. Launch the game once so MelonLoader generates its folders and IL2CPP assemblies.
3. Copy `SGChatScrollbar.dll` into the game's `Mods/` folder.
4. Start the game through MelonLoader.

## CI/CD (Thunderstore)

This repository includes GitHub Actions workflows for Thunderstore packaging and publishing:

- `.github/workflows/github-release.yml`
  - Manual workflow dispatch.
  - Creates a GitHub Release from a version in `CHANGELOG.md` (extracts that version's notes).
- `.github/workflows/thunderstore-build.yml`
  - Runs on push/PR/manual dispatch.
  - Builds `SGChatScrollbar` and uploads a Thunderstore zip artifact.
- `.github/workflows/thunderstore-publish.yml`
  - Runs when a GitHub Release is published (or manually via workflow dispatch).
  - Builds and publishes with `tcli publish`.
  - All workflows accept `dryrun` on manual dispatch; `dryrun=true` echoes commands and skips execution-sensitive steps.

Required repository secrets:

- `THUNDERSTORE_TOKEN`: Thunderstore service account token (used as `TCLI_AUTH_TOKEN`).
- `SGREFROOT_TOKEN`: GitHub token with read access to `ricky-davis/SGRefRoot` (used to fetch `Il2CppAssemblies` and `net6` refs).
- `RELEASE_WORKFLOW_TOKEN`: PAT used by `github-release.yml` to create GitHub Releases so `release`-triggered workflows run.

### Local Pre-Commit Hook

This repo includes a pre-commit hook that enforces version consistency across:

- `Directory.Build.props` (`<Version>`)
- `thunderstore.toml` (`versionNumber`)
- `CHANGELOG.md` (latest `## [x.y.z]` heading)

Enable repo hooks once per clone:

```bash
git config core.hooksPath .githooks
```
