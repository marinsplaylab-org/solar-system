# Solar System Simulation
A lightweight, open-source Solar System visualization built in **Unity (URP)** targeting **WebGL**.

This project focuses on:
- visually understandable **orbits** (including eccentric/tilted dwarf-planet orbits),
- **axial tilts** + **spin**,
- a data-driven pipeline (JSON) for repeatable, extensible setups.

> Disclaimer: This is a visualization project, **not** intended for scientific analysis, navigation, or ephemeris-grade accuracy. For accurate ephemerides, use NASA/JPL Horizons.

---

## Technical Overview
- **Engine:** Unity **6000.3+**
- **Render Pipeline:** **URP**
- **Target:** WebGL 2.0 (desktop browsers)
- **JSON:** Newtonsoft JSON (`com.unity.nuget.newtonsoft-json`)
- **Camera:** Custom runtime rig

---

## Style Quick Reference
- Rules apply to humans and automation.
- Allman braces and `{}` for all control statements.
- Prefix locals/params with `_`; no `_` for fields/properties; use descriptive names.
- Comments explain what and why; add example when useful.
- Avoid hardcoding; prefer JSON, serialized fields, constants, or inspector values; keep modular/customizable.
- Use `HelpLogs`, keep namespaces aligned, use `#region`, partial files use underscores, update README/AGENTS/JSON/prefabs/scenes on contract changes.

---

## Simulation Model
### Data vs Visual
The simulator uses a **real-data baseline** (radius, rotation period, orbit period, semi-major axis, etc.) and then applies **visual multipliers** per solar object:

- `visual_defaults.radius_multiplier` (per-object size boost/suppression)
- `visual_defaults.distance_multiplier` (per-object orbit-distance boost/suppression)
- global multipliers in JSON: `global_visual_defaults`

This keeps proportions educationally realistic while staying readable in a single scene.

### Time Scaling (Simulation Speed)
Simulation time advances by:
```
simulation_time_seconds += Time.deltaTime * timeScale
```
`timeScale` defaults to `1.0f` inside `SolarSystemSimulator` and represents the **Default** level for runtime controls.
If runtime controls are activated, the Time Scale buttons switch between:
- Default = default (`1.0x` unless you change it in code)
- Speed_1000x = `1,000x`
- Speed_10000x = `10,000x`
- Speed_200000x = `200,000x`

By default, runtime controls start at **Speed_1000x**.

If runtime controls are deactivated, change the default by editing `timeScale` in `SolarSystemSimulator`.
Example:
- `timeScale = 1` means 1 real second = 1 simulated second.
- `timeScale = 86400` means 1 real second = 1 simulated day.

### Orbit Size Scaling (Distance)
Orbit distances are computed per object using a global conversion plus optional multipliers:
```
aUnity = (semiMajorAxisKm / distance_km_per_unity_unit)
         * global_distance_multiplier
         * visual_defaults.distance_multiplier
```
Notes:
- `distance_km_per_unity_unit` is the base conversion (km per Unity unit). Larger values compress the whole system.
- `global_distance_multiplier` is a visual dial for the whole system and is the main runtime scaling knob.
- Both values come from `global_visual_defaults` in the JSON.
- Each object can override `visual_defaults.distance_multiplier` for per-object adjustments.
- A minimum orbit radius is enforced for moons so their orbit does not intersect the primary object:
  `moon_clearance_unity` + both object radii.
Example (Earth, using defaults in `SolarSystemData_J2000_Keplerian_all_moons.json`):
- `semi_major_axis_AU = 1.0` -> `149,597,870 km`
- `distance_km_per_unity_unit = 1,000,000`
- `visual_defaults.distance_multiplier = 0.468`
- `aUnity ≈ 149.6 * 0.468 ≈ 70.0` Unity units

### Object Scale (Size)
Each object’s rendered size is derived from the reference solar object (Sun) and scaled by multipliers:
```
diameterUnity = reference_solar_object_diameter_unity
               * (solar_object_radius_km / reference_solar_object_radius_km)
               * global_radius_multiplier
               * visual_defaults.radius_multiplier
```
Notes:
- The reference solar object's Unity diameter is taken from the actual spawned reference object (the Sun).
- `global_radius_multiplier` comes from `global_visual_defaults` (and can be adjusted via runtime controls).
- Changing the Sun’s prefab scale or its `spawn.scale_unity` will scale the entire system.
Example (Earth, using defaults in `SolarSystemData_J2000_Keplerian_all_moons.json`):
- Sun radius = `695,700 km`, Earth radius = `6,371 km`
- Radius ratio = `0.00916`
- `visual_defaults.radius_multiplier = 21.842`
- If Sun diameter is `1.0` Unity unit, Earth diameter ≈ `0.20` Unity units

### Orbit Model
Only `keplerian` is supported. The model uses basic Keplerian elements (good for Pluto-style eccentricity and tilt).

> The Keplerian model here is intentionally simplified. It is not intended for ephemeris accuracy.

#### Orbit Notes
- `truth_orbit.mean_anomaly_deg` sets the baseline starting position.
- `spawn.initial_angle_deg` adds an extra offset (degrees) to the mean anomaly at spawn.
- Moon orbit frames can be overridden per object with `align_to_primary_tilt`:
  - `true` to align to the primary’s axial tilt (equatorial plane)
  - `false` to keep the dataset plane (ecliptic/J2000)

## Project Layout
- `Assets/Resources/SolarSystemData_J2000_Keplerian_all_moons.json` — keplerian dataset (truth + visual defaults)
- `Assets/Resources/SolarObjects/` — prefabs loaded at runtime
- `SolarSystemSimulator` — scene entry point, loading + spawning + time
- `SolarObject` — per-object orbit, spin, and runtime lines
- `SolarSystemJsonLoader` — JSON load and validation

---

## Getting Started
### 1) Install dependencies
In Unity Package Manager:
- Install **Newtonsoft Json**: `com.unity.nuget.newtonsoft-json`

### 2) Put the dataset in Resources
Ensure this file exists:
- `Assets/Resources/SolarSystemData_J2000_Keplerian_all_moons.json`

Important:
- In code/Inspector you reference it as `"SolarSystemData_J2000_Keplerian_all_moons"` (no extension).

### 3) Put your prefabs in Resources
Put your planet/moon prefabs here:
- `Assets/Resources/SolarObjects/`

Naming:
- Recommended: name each prefab exactly equal to the JSON `id`
  - `sun.prefab`, `earth.prefab`, `moon.prefab`, `mars.prefab`, `pluto.prefab`, etc.
- Add a fallback prefab:
  - `Template.prefab`
Note:
- The dataset currently includes `ceres` and `eris`. If you do not add prefabs for them, they will use `Template.prefab`.

You do **not** need to manually add scripts to prefabs:
- the simulator will add `SolarObject` if missing.

### 4) Add the simulator to the scene
1. Create an empty GameObject: `SolarSystemSimulator`
2. Add component: `SolarSystemSimulator`
3. In Inspector set:
   - `Resources Json Path Without Extension` = `SolarSystemData_J2000_Keplerian_all_moons`
   - `Prefabs Resources Folder` = `SolarObjects`

Press **Play**.
---

## Runtime Controls (Optional)

`SolarSystemSimulator` can auto-bind buttons and labels for live tuning at runtime.
Turn this on via `enableRuntimeControls` in the Inspector.

The runtime GUI scans **all scene canvases** and looks for named widgets.
Add `Gui_RuntimeControlEvents` to the Canvas so button presses are applied via events.

Optional:
- Add `HypotheticalToggleButton` to show/hide hypothetical entries (objects with `is_hypothetical: true` in the JSON).

Text labels (`TextMeshProUGUI`):
- `TimeScaleValueText`
- `RealismValueText`
- `AppVersionText`

Buttons (`UnityEngine.UI.Button`):
- `TimeScaleMinusButton`
- `TimeScalePlusButton`
- `RealismMinusButton`
- `RealismPlusButton`
- `CameraOrbitUpButton`
- `CameraOrbitDownButton`
- `CameraOrbitLeftButton`
- `CameraOrbitRightButton`
- `CameraZoomInButton`
- `CameraZoomOutButton`

Toggles (`UnityEngine.UI.Toggle`):
- `HypotheticalToggleButton`
- `OrbitLinesToggle`
- `SpinAxisToggle`
- `WorldUpToggle`
- `SpinDirectionToggle`

Control levels (names and values):

| Control | Levels |
| --- | --- |
| Time Scale | Default = default, Speed_1000x = 1,000x, Speed_10000x = 10,000x, Speed_200000x = 200,000x |
| Realism | 0.00 = Simulation-style scaling, 1.00 = full JSON realism (blends in between) |

Realism blends between the Simulation visuals and raw JSON values
- At 0.00, global distance and radius multipliers use the Simulation targets and the Simulation scale profile is fully applied
- At 1.00, global multipliers use `global_visual_defaults`, per-object `visual_defaults` are neutralized, and Simulation scaling fades out
- Orbit segments and runtime line width scaling interpolate between the Simulation target and the dataset default
- The buttons step realism by `realismStep` (default 0.05) in code

The JSON dataset stays the same, only the visualization scaling changes

---

## Camera Controls
The camera is a custom runtime rig. A GUI grid is used to focus on solar objects.

Required scene objects (names must match):
- Grid layout: `SolarObjects_View_Interaction_GridLayoutGroup`
- Focus button template (inactive): `Focus_SolarObject_Button`
- Overview button: `View_SolarSystem_Overview_Button`
- Button TMP child name: `Text`

Add these components to the scene:
- `SolarSystemCamera` (camera logic)
- `Gui_SolarObjectGrid` (builds focus buttons at runtime)

Realism camera behavior:
- As realism increases, overview zoom range and zoom speed increase
- Focus zoom ranges blend between Simulation and Realistic per `camera_focus_profile`
- Use `camera_focus_profile` in JSON to control per-object focus ranges

---

## Runtime Lines (Orbits + Axes)
Orbit paths and axis lines are rendered with **LineRenderer** components at runtime (no gizmos).
These lines are children of each `SolarObject` and show in both the editor and builds.

Controls live on the `SolarObject` component:
- Toggle orbit lines / axis lines
- Adjust line widths and colors
- Widths auto-scale based on global distance/radius multipliers and camera distance

Global toggles for orbit paths, spin axis, and world-up lines are available in the runtime GUI.

---

## Performance Notes (WebGL)
- Orbit line resolution interpolates between `simulationOrbitSegments` and the dataset default (or override). Many objects + high segment counts can be expensive.
- Prefer compressed textures and reasonable texture sizes for WebGL memory limits.

---

## Known Issues
- Changing orbit parameters at runtime is not supported, make sure to edit the JSON and restart.
- Some Saturn moons have incorrect orbits outside Saturn's ring tilt (math mismatch).
- Planetary shadows on some moons are missing or inconsistent, they should be occluded at certain angles.
- High realism (near 1.00) needs a larger camera distance, the current focus distance range is tuned for the Simulation target.
- Maximum time scale (200,000x) can still be too fast for some devices.
- Grid layout buttons can override their borders when sized by layout groups.
- Asteroid belt is not implemented yet.
- Some moons need proper 3D models (e.g., Mars moons are still spheres).
- Saturn rings need a complete rework.
- Performance: orbit line positions are rebuilt every frame for moving primaries (moons), and axis/spin direction lines update every frame, which can be heavy on WebGL.
- Performance: if MainCamera is missing, line scaling falls back to scanning cameras at runtime, which can be expensive.
- Performance: spawn data logging is on by default and can spam logs in WebGL.
- Performance: `Resources.LoadAll` loads all prefabs at startup, which can be heavy as content grows.

---

## Code Guidelines
- Rules apply to humans and automation.
- Allman braces for all blocks.
- All control statements use `{}` even for single lines, including `if`, `else`, `for`, `foreach`, `while`, and `do`.
- Local variables and parameters use `_` prefix, while non-local fields/properties do not.
- Descriptive names. Long names ok.
- Comments explain what and why. Add example when useful.
- Avoid hardcoding. Use JSON, serialized fields, constants, or inspector values.
- Prefer modular and customizable settings.
- Use `HelpLogs` for logs, warnings, and errors.
- Keep namespaces aligned with folder structure (e.g. `Assets.Scripts.Runtime`).
- Avoid obsolete APIs and keep code current with the project dependencies.
- Use `#region` blocks to group related fields and methods.
- Partial class files use underscores, e.g. `SolarSystemCamera_PublicAPI.cs`.
- Update related files when contracts change: README, AGENTS, JSON, prefabs, scenes.

---

## Extending the System
To add a new object:
1. Add a new entry under `solar_objects` in `SolarSystemData_J2000_Keplerian_all_moons.json`
2. Set:
   - `id` (unique)
   - `camera_focus_profile` (e.g., `terrestrial`, `gas_giant`, `ice_giant`, `moon`, `star`)
   - `primary_id` (e.g. `"sun"` or a planet id for moons)
   - `truth_physical.mean_radius_km`
   - `truth_spin` (rotation period + optional axial tilt)
   - `truth_orbit` (keplerian elements)
3. Add a prefab named the same as `id` in `Assets/Resources/SolarObjects/` (optional, otherwise `Template` is used)

### JSON Example (Keplerian)
```json
{
  "id": "example_dwarf",
  "type": "dwarf_planet",
  "camera_focus_profile": "dwarf_planet",
  "display_name": "Example Dwarf",
  "primary_id": "sun",
  "truth_physical": { "mean_radius_km": 600.0 },
  "truth_spin": { "sidereal_rotation_period_hours": 12.0, "axial_tilt_deg": 30.0 },
  "truth_orbit": {
    "model": "keplerian",
    "semi_major_axis_AU": 40.0,
    "orbital_period_years": 250.0,
    "eccentricity": 0.25,
    "inclination_deg": 17.0,
    "longitude_ascending_node_deg": 110.0,
    "argument_periapsis_deg": 113.0,
    "mean_anomaly_deg": 0.0
  },
  "visual_defaults": { "radius_multiplier": 40.0, "distance_multiplier": 0.06 },
  "spawn": { "initial_angle_deg": 25.0 }
}
```

---

## License
Code is licensed under the **MIT License** (see `LICENSE`).

Third-party assets (textures/models/fonts) are attributed in `CREDITS.md` and remain subject to their original terms.

Custom shaders in `Assets/Shaders` are original made by MarinsPlayLab and are free to use under the MIT License.

---

## Contributing / Feedback
This is an open-source project. At the moment, we are **not accepting code pull requests**.

You can help with:

 - scientific corrections to the dataset
 - attribution fixes
 - documentation improvements
 - performance notes / WebGL fixes

Issues: bug reports, feature requests, scientific corrections, attribution fixes  
PRs: documentation only (no code changes)

Code PRs may be closed without review. If you want to propose a code change, please open an issue first.

---

## Future Plans
- Add more interaction
- Improve performance
- Continuously update with new content and features
- Add magnetic fields for planets
- Visualize the Moon's dark side
- Add gravity and spacetime visuals
- Add an asteroid belt
- Enhance lighting
- Add Sun fusion VFX and flare effects (especially visible from far away)
- Other improvements and refinements

---
