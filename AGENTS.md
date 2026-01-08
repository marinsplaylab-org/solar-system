# AGENTS.md

## Purpose
- Use for all work in this repo.
- Keep project goals, constraints, and style.
- Keep `README.md` and `CREDITS.md` aligned with current project contracts.

## Project Summary
- Unity 6000.3+ with URP.
- Target WebGL 2.0.
- Visualization, not scientific accuracy.
- Data-driven JSON pipeline.

## Core Systems
- SolarSystemSimulator: load JSON, spawn objects, time, realism.
- SolarObject: orbit, spin, runtime lines.
- SolarSystemJsonLoader: JSON load and validation.
- SolarSystemCamera: focus and overview rig.
- SolarObjectBadgeManager: icon badges for object selection.

## Data And Resources
- JSON dataset: `Assets/Resources/SolarSystemData_J2000_Keplerian_all_moons.json`
- JSON path in code: `SolarSystemData_J2000_Keplerian_all_moons` (no extension).
- Prefabs folder: `Assets/Resources/SolarObjects/`
- Prefab naming: match JSON `id` (example: `earth.prefab`).
- Fallback prefab: `Template.prefab`.

## Scene Contracts
- Overview button: `View_SolarSystem_Overview_Button`
- Required components: `SolarSystemCamera`, `SolarObjectBadgeManager`, `SolarSystemSimulator`

## Runtime Controls (Optional)
- Buttons: `TimeScaleMinusButton`, `TimeScalePlusButton`, `RealismMinusButton`, `RealismPlusButton`
- Text labels: `TimeScaleValueText`, `RealismValueText`, `AppVersionText`
- Control levels:
  - Time Scale (focus): Default, 1,000x, 10,000x, 200,000x
  - Time Scale (overview): Default, 1,000x, 10,000x, 200,000x, 2,000,000x
  - Realism: 0.00 to 1.00
- Camera orbit and zoom: drag, scroll, pinch. No UI buttons.
- Axis, world-up, and spin-direction lines: double-click/tap focused object. Others off.
- Hypothetical objects: always visible. No toggle.
- Badge UI toggle: call `SolarObjectBadgeManager.ToggleBadgesEnabled` or `SetBadgesEnabled(bool)`.

## Performance And Known Issues
- Orbit line segments can be expensive at high counts.
- Moons rebuild orbit lines every frame for moving primaries.
- Axis and spin lines update every frame.
- `Resources.LoadAll` loads all prefabs at startup.
- Spawn data logging is on by default.
- Missing Moon shadows on some objects.
- Saturn rings need rework.

## Code Style
- Allman braces for all blocks.
- `_` prefix for local variables and parameters.
- No `_` prefix for fields or properties.
- Descriptive names. Long names ok.
- Comments only when logic is not obvious.
- Comments include what and why. Add example when useful.
- Use `HelpLogs` for logs, warnings, errors.
- Use `#region` blocks for grouping.
- Keep namespaces aligned with folder structure.
- Partial class files use underscore in names.
- Avoid obsolete APIs.
- Prefer modular and customizable settings.
- Prefer serialized fields for tuning and editor control.
- Avoid hardcoding. Use JSON, serialized fields, constants, or inspector values.
- Keep README and AGENTS in sync when rules change.

Comment format example:
```csharp
/// Focus camera on target.
/// Example: FocusOn(earth).
```

## Audit Before Changes
- Read `README.md` and Known Issues.
- Locate all partial files for the class.
- Search for call sites with `rg`.
- Check serialized fields and scene object name contracts.
- Check JSON schema and Resources paths.
- Check runtime GUI name contracts.
- Avoid breaking public API or event signatures.
- Update docs when behavior changes.
- Update related files when contracts change: README, AGENTS, JSON, prefabs, scenes.

## Validation
- Open Unity editor.
- Enter Play mode and test main flows:
  - Load JSON and spawn objects.
  - Focus and overview camera.
  - Runtime controls (if enabled).

## Repo Policy
- Code PRs not accepted upstream.
- Docs and data fixes ok.
