# Solar System Simulation
Open-source solar system visualization built with Unity (URP) for WebGL.

Disclaimer: Visualization only. Not for scientific analysis or ephemeris accuracy. For accurate ephemerides use NASA/JPL Horizons.

## Key Features
- Data-driven JSON pipeline
- Real-time orbits, spin, and axial tilt
- Custom camera with focus and overview
- WebGL build target

## Tech
- Unity 6000.3+
- URP
- WebGL 2.0
- Newtonsoft JSON (`com.unity.nuget.newtonsoft-json`)

## Project Layout
- `Assets/Resources/SolarSystemData_J2000_Keplerian_all_moons.json` (dataset)
- `Assets/Resources/SolarObjects/` (prefabs)
- `Assets/Scenes/Solar_System_Simulation.unity` (main scene)
- `SolarSystemSimulator` loads data, spawns objects, and advances time
- `SolarObject` handles orbit, spin, and runtime lines per object
- `SolarSystemJsonLoader` loads and validates JSON
- `SolarSystemCamera` manages focus and overview controls
- `SolarObjectBadgeManager` renders and handles icon badges
- `AGENTS.md` (project rules and automation guidance)
- `CREDITS.md` (third-party asset attributions)

## Quick Start
1. Install `com.unity.nuget.newtonsoft-json`.
2. Keep the dataset in `Assets/Resources/` with name `SolarSystemData_J2000_Keplerian_all_moons.json`.
3. Put prefabs in `Assets/Resources/SolarObjects/`.
   - Prefab name = JSON `id`. Example: `earth.prefab`.
   - `Template.prefab` is fallback.
4. Open `Assets/Scenes/Solar_System_Simulation.unity`.
5. Press Play.

## Controls
- Select any object from its circle badge label or click/tap it in the scene.
- Drag to orbit around the focused object.
- Mouse wheel or pinch to zoom.
- Double-click/tap to toggle axis, world-up, and spin-direction lines.
- Zoom out past focus max to enter the full system overview.
- At max overview zoom, only the Sun badge remains visible.
- Focused planet moon badges show only when you are zoomed in close.
- Optional: add a badge toggle button wired to `SolarObjectBadgeManager.ToggleBadgesEnabled`.

## Runtime UI
The runtime UI auto-binds by name from any Canvas with `Gui_RuntimeControlEvents`.

Text labels:
- `TimeScaleValueText`
- `RealismValueText`
- `AppVersionText`

Buttons:
- `TimeScaleMinusButton`
- `TimeScalePlusButton`
- `RealismMinusButton`
- `RealismPlusButton`

Time scale levels (focus): Default, 1,000x, 10,000x, 200,000x. Overview adds 20,000,000x. Default start is 1,000x.
Realism: 0.00 = simulation scale, 1.00 = dataset scale.

## Data Model Notes
- `primary_id` is required for non-reference objects and must exist in the dataset.
- `camera_focus_profile` controls focus zoom ranges.
- `global_visual_defaults` sets global distance and radius scaling.
- `visual_defaults` adds per-object scaling.

## Extending The System
1. Add a new entry under `solar_objects` in `SolarSystemData_J2000_Keplerian_all_moons.json`.
2. Set core fields: `id`, `type`, `camera_focus_profile`, `primary_id`.
3. Set `truth_physical`, `truth_spin`, and `truth_orbit` (model `keplerian`).
4. Add a prefab named the same as `id` in `Assets/Resources/SolarObjects/` (optional). `Template.prefab` is fallback.

Example JSON:
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

## Runtime Lines
Orbit lines always render. Focused object lines fade near the camera.
Axis, world-up, and spin-direction lines render only for the focused object.

## Performance Notes (WebGL)
- Orbit line segments increase cost at high counts.
- Moon orbit lines update every frame.
- `Resources.LoadAll` loads all prefabs at startup.

## Known Issues
- Some Saturn moons have incorrect orbit tilt.
- Some moons are missing correct shadowing.
- Asteroid belt is not implemented yet.

## Code Style
- Allman braces for all blocks and control statements.
- `_` prefix for locals and parameters. No `_` for fields or properties.
- Descriptive names. Long names are ok.
- Comments only when logic is not obvious. Include what, why, and example when useful.
- Avoid hardcoding. Prefer JSON, serialized fields, constants, or inspector values.
- Use `HelpLogs` for logging.
- Keep namespaces aligned with folder structure.
- Use `#region` blocks. Partial class files use underscores.
- Update README/AGENTS/JSON/prefabs/scenes on contract changes.

## License
Code is MIT. See `LICENSE`.
Third-party assets are listed in `CREDITS.md`.

## Contributing / Feedback
This is an open-source project. We are not accepting code pull requests at this time.

You can help with:
- scientific corrections to the dataset
- attribution fixes
- documentation improvements
- performance notes and WebGL fixes

Issues: bug reports, feature requests, scientific corrections, attribution fixes
PRs: documentation only (no code changes)

Code PRs may be closed without review. If you want to propose a code change, open an issue first.
