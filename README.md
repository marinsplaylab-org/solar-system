# Solar System Unity Web Simulator
A lightweight, open-source Solar System visualization built in **Unity (URP)** targeting **WebGL**.

This project focuses on:
- visually understandable **orbits** (including eccentric/tilted dwarf-planet orbits),
- **axial tilts** + **spin**,
- a data-driven pipeline (JSON) for repeatable, extensible setups.

> Disclaimer: This is a visualization project, **not** intended for scientific analysis, navigation, or ephemeris-grade accuracy. For accurate ephemerides, use NASA/JPL Horizons.

---

## Status
**Prototype / in progress.**  
Features, assets, and structure may change.

---

## Tech
- **Engine:** Unity **6000.3+**
- **Render Pipeline:** **URP**
- **Target:** WebGL (desktop browsers)
- **WebGL:** WebGL 2.0 minimum
- **JSON:** Newtonsoft JSON (`com.unity.nuget.newtonsoft-json`)
- **Camera:** Cinemachine (`com.unity.cinemachine`)

---

## Key Concepts
### Truth vs Visual
The simulator uses a **real-data baseline** (radius, rotation period, orbit period, semi-major axis, etc.) and then applies **visual multipliers** per solar object:

- `visual_defaults.radius_multiplier` (per-object size boost/suppression)
- `visual_defaults.distance_multiplier` (per-object orbit-distance boost/suppression)
- global multipliers in JSON: `global_visual_defaults`

This keeps proportions “educationally realistic” while staying visually readable in a single scene.

### Time Scaling (Simulation Speed)
Simulation time advances by:
```
simulation_time_seconds += Time.deltaTime * timeScale
```
`timeScale` defaults to `1.0f` inside `SolarSystemSimulator` and represents the **Standard** level for runtime controls.
If runtime controls are activated, the Time Scale slider switches between:
- Standard = default (`1.0x` unless you change it in code)
- Accelerated = `10,000x`
- Hyper = `100,000x`
- Maximum = `10,000,000x`

By default, runtime controls start at **Accelerated**.

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
Example (Earth, using defaults in `SolarSystemData.json`):
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
Example (Earth, using defaults in `SolarSystemData.json`):
- Sun radius = `695,700 km`, Earth radius = `6,371 km`
- Radius ratio = `0.00916`
- `visual_defaults.radius_multiplier = 21.842`
- If Sun diameter is `1.0` Unity unit, Earth diameter ≈ `0.20` Unity units

### Orbit Models
Each object’s `truth_orbit.model` is one of:
- `circular` — simple circular orbit using semi-major axis as radius
- `keplerian` — elliptical orbit using basic Keplerian elements (good for Pluto-style eccentricity and tilt)

> The Keplerian model here is intentionally simplified. It is not intended for ephemeris accuracy.

#### Orbit Model Notes (Important)
- `spawn.initial_angle_deg` currently only affects the **circular** model.
- For `keplerian`, the starting position is controlled by `truth_orbit.mean_anomaly_deg`.
- This means the same `initial_angle_deg` value does **not** align circular and keplerian objects. This is intentional for now to avoid breaking existing setups.

---

## Project Structure
High-level:
- `Assets/Resources/SolarSystemData.json` — master dataset (truth + visual defaults)
- `Assets/Resources/SolarObjects/` — prefabs loaded at runtime
  - Prefab names should match JSON `id` (recommended)
  - `Template.prefab` is used as a fallback when an id-matching prefab is missing

Scripts (namespaces follow folder structure, e.g. `Assets.Scripts.Runtime`):
- `SolarSystemSimulator` — scene entry point that loads JSON, loads prefabs, spawns objects, advances simulation time
- `SolarObject` — per-object behavior: orbit position, spin, and runtime line renderers
- `SolarSystemJsonLoader` — loads/validates JSON from `Resources`

---

## Getting Started
### 1) Install dependencies
In Unity Package Manager:
- Install **Newtonsoft Json**: `com.unity.nuget.newtonsoft-json`

### 2) Put the dataset in Resources
Ensure this file exists:
- `Assets/Resources/SolarSystemData.json`

Important:
- In code/Inspector you reference it as `"SolarSystemData"` (no extension).

### 3) Put your prefabs in Resources
Put your planet/moon prefabs here:
- `Assets/Resources/SolarObjects/`

Naming:
- Recommended: name each prefab exactly equal to the JSON `id`
  - `sun.prefab`, `earth.prefab`, `moon.prefab`, `mars.prefab`, `pluto.prefab`, etc.
- Add a fallback prefab:
  - `Template.prefab`

You do **not** need to manually add scripts to prefabs:
- the simulator will add `SolarObject` if missing.

### 4) Add the simulator to the scene
1. Create an empty GameObject: `SolarSystemSimulator`
2. Add component: `SolarSystemSimulator`
3. In Inspector set:
   - `Resources Json Path Without Extension` = `SolarSystemData`
   - `Prefabs Resources Folder` = `SolarObjects`

Press **Play**.
---

## Runtime Controls (Optional)

`SolarSystemSimulator` can auto-bind sliders and labels for live tuning at runtime.
Turn this on via `enableRuntimeControls` in the Inspector.

The runtime GUI uses the **first Canvas** it finds and looks for named widgets.
Add `Gui_RuntimeControlEvents` to the Canvas so slider changes are applied via `onValueChanged`.

Text labels (`TextMeshProUGUI`):
- `TimeScaleValueText`
- `VisualPresetValueText`
- `AppVersionText`

Sliders (`UnityEngine.UI.Slider`):
- `TimeScaleSlider`
- `VisualPresetSlider`

Toggles (`UnityEngine.UI.Toggle`):
- `OrbitLinesToggle`
- `SpinAxisToggle`
- `WorldUpToggle`

Slider levels (names and values):

| Control | Levels |
| --- | --- |
| Time Scale | Standard = default, Accelerated = 10,000x, Hyper = 100,000x, Maximum = 10,000,000x |
| Visual Preset | Normal = distance/radius defaults + orbit segments 128, Minimal (default) = distance 0.02 + radius 0.25 + orbit segments 64 |

Defaults come from `global_visual_defaults` for distance and radius (Normal preset), while Time Scale is set in code.
Orbit segments are 128 (Normal) and 64 (Minimal), and distance km/unit + moon clearance remain at their default values across presets.
When Minimal is active, runtime line widths are scaled down by 0.25x.

---

## Cinemachine Camera Controls
This project uses Cinemachine cameras and a runtime GUI grid to focus on solar objects.

Required scene objects (names must match):
- Focus vcam: `Follow-SolarObject-VCinemachine`
- Overview vcam: `Overview-SolarSystem-VCinemachine`
- Grid layout: `SolarObjects_View_Interaction_GridLayoutGroup`
- Focus button template (inactive): `Focus_SolarObject_VCinemachine_Button`
- Overview button: `View_SolarSystem_Overview_VCinemachine_Button`
- Button TMP child name: `Text`

Proxy targets (world-aligned, not parented):
- `Focus_Proxy`
- `Overview_Proxy`

Add these components to the scene:
- `SolarSystemCameraController` (camera logic)
- `Gui_SolarObjectGrid` (builds focus buttons at runtime)

---

## Runtime Lines (Orbits + Axes)
Orbit paths and axis lines are rendered with **LineRenderer** components at runtime (no gizmos).
These lines are children of each `SolarObject` and show in both the editor and builds.

Controls live on the `SolarObject` component:
- Toggle orbit lines / axis lines
- Adjust line widths and colors
- Widths auto-scale based on global distance/radius multipliers

Global toggles for orbit paths, spin axis, and world-up lines are available in the runtime GUI.

---

## Performance Notes (WebGL)
- Orbit line resolution uses runtime presets (Normal = 128, Minimal = 64). With runtime controls off, it falls back to `global_visual_defaults.orbit_path_segments_default`.
- Many objects + high segment counts can be expensive. Reduce segments for WebGL builds.
- Prefer compressed textures and reasonable texture sizes for WebGL memory limits.

---

## Known Issues
- Changing orbit parameters at runtime is not supported, make sure to edit the JSON and restart.
- Planetary shadows on some moons are missing or inconsistent, they should be occluded at certain angles.
- Realistic-size presets need a larger camera distance, the current 0.2 to 1.5 range is tuned for the Minimal preset.
- Maximum time scale (10,000,000x) is too fast and should likely be reduced by 10x.
- Grid layout buttons can override their borders when sized by layout groups.
- Moon tidal-locking is correct in motion, but the initial facing direction is not aligned to Earth by default.

---

## Code Guidelines
- All control statements use `{}` even for single lines, including `if`, `else`, `for`, `foreach`, `while`, and `do`.
- Local variables and parameters use `_` prefix, while non-local fields/properties do not.
- Prefer short, clear comments only when the logic is not obvious.
- Use `HelpLogs` for logs, warnings, and errors.
- Keep namespaces aligned with folder structure (e.g. `Assets.Scripts.Runtime`).
- Avoid obsolete APIs and keep code current with the project dependencies.

---

## Extending the System
To add a new object:
1. Add a new entry under `solar_objects` in `SolarSystemData.json`
2. Set:
   - `id` (unique)
   - `primary_id` (e.g. `"sun"` or a planet id for moons)
   - `truth_physical.mean_radius_km`
   - `truth_spin` (rotation period + optional axial tilt)
   - `truth_orbit` (semi-major axis + orbital period, set `model`)
3. Add a prefab named the same as `id` in `Assets/Resources/SolarObjects/` (optional, otherwise `Template` is used)

### JSON Examples
Circular orbit (simple):
```json
{
  "id": "example_planet",
  "type": "planet",
  "display_name": "Example",
  "order_from_sun": 5,
  "primary_id": "sun",
  "truth_physical": { "mean_radius_km": 3000.0 },
  "truth_spin": { "sidereal_rotation_period_hours": 20.0, "axial_tilt_deg": 10.0 },
  "truth_orbit": { "model": "circular", "semi_major_axis_AU": 2.5, "orbital_period_days": 1000.0 },
  "visual_defaults": { "radius_multiplier": 25.0, "distance_multiplier": 0.4 },
  "spawn": { "initial_angle_deg": 45.0 }
}
```

Keplerian orbit (elliptical + tilted):
```json
{
  "id": "example_dwarf",
  "type": "dwarf_planet",
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
  "visual_defaults": { "radius_multiplier": 40.0, "distance_multiplier": 0.06 }
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
- Other improvements and refinements

---