# Contributing to Connected Rooms Generator

Thanks for your interest in CRG! Bug reports, ideas and pull requests are all welcome. By taking part you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Reporting bugs and suggesting features

Search the existing issues first, then open a new one. For a bug, include:

- your Unity, ProBuilder and (if used) AI Navigation versions;
- the **Grid Type**, the other parameters you changed and the **Random Seed**, so the level can be generated again exactly;
- what you expected, what happened, and a screenshot if the problem is visible in the level;
- any errors or warnings from the Console.

Security problems should not be reported in public issues; see the [security policy](SECURITY.md).

## Setting up

CRG is a Unity package with no standalone build. To work on it:

1. Create or open a Unity project (2021.3 or newer; development uses Unity 6000.3).
2. Clone the repository into the project's `Assets` or `Packages` folder.
3. Let Unity install ProBuilder. Install **AI Navigation** too if you work on NavMesh features; everything else works without it.

The code is split into three assemblies: `CRG.Runtime` (everything that works in player builds), `CRG.Editor` (windows, menus, inspectors, Scene-view overlays) and `CRG.Tests.Editor`. Data flows one way: **Core** (grid topologies and cell data) → **Generation** (fills the grid with rooms) → **Geometry** (builds the mesh and post-processing).

## Making changes

- **Keep the style of the code around you**: naming, comment density and structure.
- **Commit the `.meta` files** Unity creates for new files and folders, and never edit their GUIDs by hand.
- **Keep seeds stable.** The same seed and settings must keep producing the same level. A new setting must not draw extra random numbers at its default value, and all randomness goes through the generator's seeded `System.Random`.
- **Keep AI Navigation optional.** Put every AI Navigation type behind `#if CRG_AI_NAVIGATION`.
- **Keep scene and asset references out of `Editor/`.** Components and assets that scenes use belong in `CRG.Runtime`.
- **Keep old data loading.** When you rename a serialized field, add `[FormerlySerializedAs]` so existing scenes and assets keep their values.
- **Keep the minimum Unity version.** Don't use APIs newer than Unity 2021.3 without an `#if UNITY_x_OR_NEWER` guard.
- **Update the README** when parameters or the public API change.

### New grid types

A new grid implements `IGridTopology` and follows its conventions: corners are counter-clockwise seen from above, edge `i` runs from corner `i-1` to corner `i` and faces neighbor `i`, and every edge is `CellSize` long. Add it to `GridTopology.SingleGridTypes`; `GridTopologyTests` and the per-grid test fixtures then cover it automatically.

## Tests

Run the Edit Mode tests in **Window → General → Test Runner → EditMode** before opening a pull request, with and without AI Navigation installed if your change touches NavMesh code. From the command line (with the Editor closed):

```
Unity -batchmode -projectPath <your-project> -runTests -testPlatform EditMode -assemblyNames CRG.Tests.Editor -testResults results.xml
```

Add tests for new behavior. `GoldenLevelTests` pins the layout and geometry of 150 levels across every grid type. If it fails, your change altered levels that existing seeds produce. That is only acceptable when it is intended: explain why in the pull request and update the pinned values, which the failure message prints.

For changes you can see, also check the result in the Editor: generate a few levels with the Level Generator window and look at them with the Scene-view overlays turned on.

## Commit messages

Commits follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/):

```
type(scope): short description
```

- **Types**: `feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `chore`.
- **Scopes** (optional): `core`, `grid`, `generation`, `geometry`, `editor`, `runtime`, `setup`.
- **Breaking changes** are marked with `!` after the type or scope, or with a `BREAKING CHANGE:` footer.

Examples: `feat(grid): add octagon + square grid`, `fix(geometry): close gaps at triangle wall corners`.

## Pull requests

1. Fork the repository and create a branch from `main`.
2. Keep each pull request focused on one change.
3. Fill in the pull request template: what changed, why, and how you tested it (including seeds and settings for visual changes).
4. Make sure the tests pass and the README is up to date.

By contributing, you agree that your contributions are licensed under the project's [MIT License](../LICENSE).
