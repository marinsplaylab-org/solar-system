#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.Loading;

namespace Assets.Scripts.Runtime
{
    /// <summary>
    /// Loads the dataset, spawns solar objects, and advances the simulation.
    /// </summary>
    public sealed partial class SolarSystemSimulator : MonoBehaviour
    {
        #region Serialized Fields
        [Header("JSON (Resources)")]
        [Tooltip("Resources path (no extension). Example: SolarSystemData_J2000_Keplerian_all_moons")]
        [SerializeField] private string resourcesJsonPathWithoutExtension =
            "SolarSystemData_J2000_Keplerian_all_moons";

        [Header("Prefabs (Resources)")]
        [Tooltip("Resources folder that contains solar object prefabs. Example: SolarObjects")]
        [SerializeField] private string prefabsResourcesFolder = "SolarObjects";

        [Header("Time")]
        // Simulation speed multiplier (sim seconds per real second).
        private float timeScale = 1.0f;

        [Header("Realism")]
        [Tooltip("Blend between simulation (0) and realistic (1). Example: 0.25")]
        [Range(0f, 1f)]
        [SerializeField] private float realismLevel = 0.0f;
        [Tooltip("Step size applied by +/- buttons. Higher = bigger jumps, lower = finer tuning. Example: 0.05")]
        [Range(0f, 0.5f)]
        [SerializeField] private float realismStep = 0.05f;

        [Header("Realism Targets")]
        [Tooltip("Global distance scale at realism = 0. Higher = more spread out, lower = more compact. Example: 0.02")]
        [Range(0.001f, 5f)]
        [SerializeField] private float simulationGlobalDistanceScale = 0.02f;
        [Tooltip("Global radius scale at realism = 0. Higher = larger bodies, lower = smaller. Example: 0.25")]
        [Range(0.01f, 2f)]
        [SerializeField] private float simulationGlobalRadiusScale = 0.25f;
        [Tooltip("Orbit line segments at realism = 0. Higher = smoother, lower = cheaper. Example: 64")]
        [Range(16, 1024)]
        [SerializeField] private int simulationOrbitLineSegments = 64;
        [Tooltip("Runtime line width scale at realism = 0. Higher = thicker lines, lower = thinner. Example: 0.25")]
        [Range(0.05f, 5f)]
        [SerializeField] private float simulationLineWidthScale = 0.25f;
        [Tooltip("Override orbit segments at realism = 1 (0 uses dataset default). Higher = smoother. Example: 0")]
        [Range(0, 2048)]
        [SerializeField] private int realismOrbitLineSegmentsOverride = 0;
        [Tooltip("Runtime line width scale at realism = 1. Higher = thicker lines, lower = thinner. Example: 1")]
        [Range(0.1f, 5f)]
        [SerializeField] private float realismLineWidthScale = 1.0f;

        [Header("Sun Light Presets")]
        [Tooltip("Sun point light intensity at realism = 1. Higher = brighter sun. Example: 225000")]
        [Range(0f, 500000f)]
        [SerializeField] private float sunLightRealisticIntensity = 225000.0f;
        [Tooltip("Sun point light range at realism = 1. Higher = light reaches farther. Example: 7500")]
        [Range(0f, 20000f)]
        [SerializeField] private float sunLightRealisticRange = 7500.0f;
        [Tooltip("Sun point light intensity at realism = 0. Higher = brighter sun. Example: 25")]
        [Range(0f, 500000f)]
        [SerializeField] private float sunLightSimulationIntensity = 25.0f;
        [Tooltip("Sun point light range at realism = 0. Higher = light reaches farther. Example: 1000")]
        [Range(0f, 20000f)]
        [SerializeField] private float sunLightSimulationRange = 1000.0f;
        [Tooltip("Realism blend multiplier for sun light. Higher = faster increase per realism step. Example: 1.5")]
        [Range(0.1f, 5f)]
        [SerializeField] private float sunLightRealismStepMultiplier = 1.5f;
        [Tooltip("Exponential ease-out rate for sun light intensity realism. Higher = faster early ramp. Example: 2")]
        [Range(0.1f, 10f)]
        [SerializeField] private float sunLightRealismIntensityExponent = 6.0f;

        [Header("Focus Orbit Lines")]
        [Tooltip("Orbit line near alpha when focused and close (0-255). Example: 15")]
        [Range(0f, 255f)]
        [SerializeField] private float focusedOrbitLineNearAlpha = 15.0f;
        [Tooltip("Orbit line distance scale threshold to apply focus alpha. Example: 1")]
        [Range(0.01f, 10f)]
        [SerializeField] private float focusedOrbitLineNearScaleThreshold = 1.0f;

        [Header("Axis Line Tuning")]
        [Tooltip("Global thickness multiplier for spin axis + world-up lines. Example: 2.5")]
        [Range(0.1f, 5f)]
        [SerializeField] private float axisLineThicknessMultiplier = 2.5f;
        [Tooltip("Length multiplier for star axis/world-up lines. Example: 2")]
        [Range(0.1f, 5f)]
        [SerializeField] private float axisLineStarLengthMultiplier = 2.0f;
        [Tooltip("Length multiplier for non-star axis/world-up lines. Example: 0.5")]
        [Range(0.1f, 5f)]
        [SerializeField] private float axisLineNonStarLengthMultiplier = 0.5f;

        [Header("Debug")]
        [Tooltip("Log per-object dataset values on spawn. Example: true")]
        [SerializeField] private bool logSpawnedSolarObjectData = true;

        [Header("Simulation Scale Profile")]
        [Tooltip("Global radius scale for the simulation profile. Higher = larger bodies overall. Example: 0.8")]
        [Range(0f, 3f)]
        [SerializeField] private float simulationRadiusScaleGlobal = 0.8f;
        [Tooltip("Extra radius scale for small planets. Higher = larger small planets. Example: 0.625")]
        [Range(0f, 3f)]
        [SerializeField] private float simulationSmallPlanetRadiusScale = 0.625f;
        [Tooltip("Extra radius scale for large planets. Higher = larger large planets. Example: 1")]
        [Range(0f, 3f)]
        [SerializeField] private float simulationLargePlanetRadiusScale = 1.0f;
        [Tooltip("Extra radius scale for moons. Higher = larger moons. Example: 0.4")]
        [Range(0f, 3f)]
        [SerializeField] private float simulationMoonRadiusScale = 0.4f;
        [Tooltip("Extra radius scale for dwarf planets. Higher = larger dwarfs. Example: 0.625")]
        [Range(0f, 3f)]
        [SerializeField] private float simulationDwarfRadiusScale = 0.625f;
        [Tooltip("Extra radius scale for other types. Higher = larger bodies. Example: 0.8")]
        [Range(0f, 3f)]
        [SerializeField] private float simulationOtherRadiusScale = 0.8f;
        [Tooltip("Small vs large planet cutoff in km. Higher = more planets treated as small. Example: 9000")]
        [Range(1000f, 20000f)]
        [SerializeField] private float simulationSmallPlanetRadiusKmCutoff = 9000.0f;
        [Tooltip("Inner planet spacing bias per order step. Higher = wider inner spacing. Example: 0.15")]
        [Range(0f, 1f)]
        [SerializeField] private float simulationInnerPlanetSpacingBiasPerOrder = 0.15f;
        [Tooltip("Max order index treated as inner. Higher = more inner planets. Example: 4")]
        [Range(0, 10)]
        [SerializeField] private int simulationInnerPlanetMaxOrderIndex = 4;
        [Tooltip("Base distance scale for planets in simulation. Higher = farther planets. Example: 1")]
        [Range(0f, 5f)]
        [SerializeField] private float simulationPlanetDistanceScaleGlobal = 1.0f;
        [Tooltip("Extra distance scale for outer planets. Higher = more separation. Example: 2")]
        [Range(0f, 10f)]
        [SerializeField] private float simulationOuterPlanetDistanceScale = 2.0f;
        [Tooltip("Min order index treated as outer. Higher = fewer outer planets. Example: 5")]
        [Range(0, 20)]
        [SerializeField] private int simulationOuterPlanetMinOrderIndex = 5;
        [Tooltip("AU threshold to treat dwarf planets as outer. Higher = more dwarfs treated as inner. Example: 5")]
        [Range(0f, 50f)]
        [SerializeField] private float simulationDwarfOuterDistanceAuCutoff = 5.0f;
        [Tooltip("Extra distance scale for inner dwarf planets. Higher = push inner dwarfs farther. Example: 1.45")]
        [Range(0f, 5f)]
        [SerializeField] private float simulationInnerDwarfDistanceScale = 1.45f;
        [Tooltip("Per-moon distance multiplier in simulation. Higher = moons farther out. Example: 0.9")]
        [Range(0f, 5f)]
        [SerializeField] private float simulationMoonOrbitDistanceScale = 0.9f;
        [Tooltip("Align moon orbits to the primary axial tilt unless overridden. Example: true")]
        [SerializeField] private bool alignMoonOrbitsToPrimaryAxialTilt = true;

        #endregion

        #region Runtime State
        // Accumulated simulation time in seconds.
        private double simulationTimeSeconds = 0.0;
        // Loaded datasets and lookup tables.
        private SolarSystemJsonLoader.Result? activeDatabase;

        // Spawned SolarObject instances keyed by id.
        private readonly Dictionary<string, SolarObject> solarObjectsById =
            new(StringComparer.OrdinalIgnoreCase);

        // Spawned solar objects in display order for UI.
        private readonly List<SolarObject> solarObjectsOrdered = new();

        /// <summary>
        /// Read-only list of spawned solar objects in display order.
        /// </summary>
        public IReadOnlyList<SolarObject> OrderedSolarObjects => solarObjectsOrdered;

        /// <summary>
        /// Raised after solar objects are spawned and initialized.
        /// </summary>
        public event Action<IReadOnlyList<SolarObject>>? SolarObjectsReady;

        /// <summary>
        /// Raised when the realism level changes.
        /// </summary>
        public event Action<float>? RealismLevelChanged;

        /// <summary>
        /// Current realism level (0 = Simulation-like, 1 = fully realistic).
        /// </summary>
        public float RealismLevel => realismLevel;

        /// <summary>
        /// Current simulation time scale.
        /// </summary>
        public float TimeScale => timeScale;

        // Prefabs found in Resources by name.
        private readonly Dictionary<string, GameObject> prefabsByName =
            new(StringComparer.OrdinalIgnoreCase);

        // Global visual scaling and defaults shared by all solar objects.
        private readonly SolarObject.VisualContext visualContext = new();
        private double defaultGlobalDistanceScale = 1.0;
        private double defaultGlobalRadiusScale = 1.0;
        private int defaultOrbitLineSegments = 256;

        // Guard for runtime controls initialization.
        private bool runtimeControlsInitialized = false;
        // Cached reference to the Sun point light.
        private Light? sunPointLight = null;
        private bool sunPointLightLookupAttempted = false;

        // Time label update throttle.
        private float timeLabelRefreshTimer = 0.0f;

        // One-time guard for spawn data logging.
        private bool spawnDataLogged = false;
        private float RealismLevel01 => Mathf.Clamp01(realismLevel);

        // Current focus target for line visibility.
        private SolarObject? focusedSolarObject = null;

        #endregion

        #region Runtime Control Levels
        // Level values mapped to control indices.
        private readonly float[] timeScaleLevels = new float[4];

        // Active indices for each control level.
        private int timeScaleLevelIndex = 0;
        #endregion

    }
}
