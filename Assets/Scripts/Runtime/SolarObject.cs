#nullable enable
using System;
using UnityEngine;

namespace Assets.Scripts.Runtime
{
    /// <summary>
    /// Runtime behavior for a single solar object (orbit, spin, and line visuals).
    /// </summary>
    public sealed partial class SolarObject : MonoBehaviour
    {
        #region Types
        [Serializable]
        public sealed class VisualContext
        {
            // Reference solar object and global scale settings.
            public double ReferenceSolarObjectRadiusKm = 695700.0; // Sun radius km
            public float ReferenceSolarObjectDiameterUnity = 1.0f; // Sun diameter in Unity units (from actual sun instance)

            public double KilometersPerUnityUnit = 1_000_000.0;
            public double GlobalDistanceScale = 1.0;
            public double GlobalRadiusScale = 1.0;

            public int OrbitLineSegments = 256;
            public float MoonClearanceUnity = 0.02f;

            // Global runtime line toggles and scale.
            public bool ShowOrbitLines = true;
            public bool ShowSpinAxisLines = true;
            public bool ShowWorldUpLines = true;
            public bool ShowSpinDirectionLines = true;
            public float RuntimeLineWidthScale = 1.0f;
            public float AxisLineThicknessMultiplier = 1.0f;
            public float AxisLineStarLengthMultiplier = 1.0f;
            public float AxisLineNonStarLengthMultiplier = 1.0f;

            public string FocusedSolarObjectId = string.Empty;
            public float FocusedOrbitLineNearAlpha = 15.0f;
            public float FocusedOrbitLineNearScaleThreshold = 1.0f;

            // Blend per-object visual_defaults multipliers (0 = off, 1 = full).
            public float VisualDefaultsBlend = 1.0f;

            // Blend Simulation scale profile (0 = off, 1 = full).
            public float SimulationScaleBlend = 1.0f;
            public double SimulationRadiusScaleGlobal = 1.0;
            public double SimulationSmallPlanetRadiusScale = 1.0;
            public double SimulationLargePlanetRadiusScale = 1.0;
            public double SimulationMoonRadiusScale = 1.0;
            public double SimulationDwarfRadiusScale = 1.0;
            public double SimulationOtherRadiusScale = 1.0;
            public double SimulationSmallPlanetRadiusKmCutoff = 9000.0;
            public double SimulationInnerPlanetSpacingBiasPerOrder = 0.0;
            public int SimulationInnerPlanetMaxOrderIndex = 0;
            public double SimulationPlanetDistanceScaleGlobal = 1.0;
            public double SimulationOuterPlanetDistanceScale = 1.0;
            public int SimulationOuterPlanetMinOrderIndex = 0;
            public double SimulationDwarfOuterDistanceAuCutoff = 0.0;
            public double SimulationInnerDwarfDistanceScale = 1.0;
            public double SimulationMoonOrbitDistanceScale = 1.0;
            public bool AlignMoonOrbitsToPrimaryAxialTilt = true;
        }

        #endregion

        #region Serialized Fields
        [Header("Runtime Lines")]
        [Tooltip("Enable orbit line rendering for this object. Example: true. When false, orbit lines are hidden")]
        [SerializeField] private bool showOrbitLinesLocal = true;
        [Tooltip("Enable spin axis line rendering for this object. Example: true. When false, axis lines are hidden")]
        [SerializeField] private bool showSpinAxisLinesLocal = true;
        [Tooltip("Enable world-up line rendering for this object. Example: true. When false, world-up lines are hidden")]
        [SerializeField] private bool showWorldUpLinesLocal = true;
        [Tooltip("Enable spin direction arc rendering for this object. Example: true. When false, spin direction arcs are hidden")]
        [SerializeField] private bool showSpinDirectionLinesLocal = true;
        [Tooltip("Line distance scaling update interval in seconds. 0 = every frame. Example: 0.1 (recommended)")]
        [Range(0f, 1f)]
        [SerializeField] private float lineScaleUpdateIntervalSeconds = 0.1f;
        [Tooltip("Camera distance delta required to refresh line scaling. 0 = always update. Example: 0.25 (recommended)")]
        [Range(0f, 10f)]
        [SerializeField] private float lineScaleDistanceThreshold = 0.25f;
        [Tooltip("Base orbit line width before scaling. Higher = thicker orbits, lower = thinner. Example: 0.06")]
        [Range(0.001f, 0.5f)]
        [SerializeField] private float orbitLineWidth = 0.06f;
        [Tooltip("Base axis line width before scaling. Higher = thicker axes, lower = thinner. Example: 0.03")]
        [Range(0.001f, 0.5f)]
        [SerializeField] private float axisLineWidth = 0.03f;
        [Tooltip("Base spin direction line width before scaling. Higher = thicker arcs, lower = thinner. Example: 0.02")]
        [Range(0.001f, 0.5f)]
        [SerializeField] private float spinDirectionLineWidth = 0.02f;
        [Tooltip("Orbit line color for planets. Example: (0.2, 0.7, 1, 0.9)")]
        [SerializeField] private Color orbitLineColor = new Color(0.2f, 0.7f, 1.0f, 0.9f);
        [Tooltip("Orbit line color for moons. Example: (0.3, 0.9, 0.5, 0.9)")]
        [SerializeField] private Color moonOrbitLineColor = new Color(0.3f, 0.9f, 0.5f, 0.9f);
        [Tooltip("Orbit line color for dwarf planets. Example: (0.9, 0.5, 0.9, 0.9)")]
        [SerializeField] private Color dwarfOrbitLineColor = new Color(0.9f, 0.5f, 0.9f, 0.9f);
        [Tooltip("Orbit line color for hypothetical objects. Example: (1, 0.2, 0.2, 0.95)")]
        [SerializeField] private Color hypotheticalOrbitLineColor = new Color(1.0f, 0.2f, 0.2f, 0.95f);
        [Tooltip("Spin axis line color. Example: (1, 0.8, 0.2, 0.9)")]
        [SerializeField] private Color axisLineColor = new Color(1.0f, 0.8f, 0.2f, 0.9f);
        [Tooltip("World-up line color. Example: (0.7, 0.7, 0.7, 0.8)")]
        [SerializeField] private Color worldUpLineColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        [Tooltip("Spin direction line color for prograde. Example: (0.2, 1, 0.4, 0.9)")]
        [SerializeField] private Color spinDirectionProgradeColor = new Color(0.2f, 1.0f, 0.4f, 0.9f);
        [Tooltip("Spin direction line color for retrograde. Example: (1, 0.3, 0.2, 0.9)")]
        [SerializeField] private Color spinDirectionRetrogradeColor = new Color(1.0f, 0.3f, 0.2f, 0.9f);

        [Header("Line Transparency")]
        [Tooltip("Minimum line alpha when close to the camera. Lower = more transparent. Example: 75")]
        [Range(0f, 255f)]
        [SerializeField] private float lineAlphaNear = 75.0f;
        [Tooltip("Maximum line alpha when far from the camera. Higher = more opaque. Example: 110")]
        [Range(0f, 255f)]
        [SerializeField] private float lineAlphaFar = 110.0f;

        [Header("Spin Direction Arc")]
        [Tooltip("Arc radius multiplier relative to body radius. Higher = arc farther from body. Example: 1.1")]
        [Range(0.1f, 5f)]
        [SerializeField] private float spinDirectionArcRadiusMultiplier = 1.1f;
        [Tooltip("Arc sweep angle in degrees. Higher = longer arc, lower = shorter arc. Example: 240")]
        [Range(30f, 330f)]
        [SerializeField] private float spinDirectionArcAngleDeg = 240.0f;
        [Tooltip("Arc segment count. Higher = smoother arc, lower = cheaper. Example: 24")]
        [Range(6, 128)]
        [SerializeField] private int spinDirectionArcSegments = 24;
        [Tooltip("Arrow head length multiplier. Higher = longer arrow head. Example: 0.2")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float spinDirectionArrowHeadLengthMultiplier = 0.2f;
        [Tooltip("Arrow head angle in degrees. Higher = wider arrow head. Example: 25")]
        [Range(5f, 60f)]
        [SerializeField] private float spinDirectionArrowHeadAngleDeg = 25.0f;

        [Header("Axis Line Distance Scaling")]
        [Tooltip("Scale axis lines based on camera distance. Example: true. When false, distance scaling is disabled")]
        [SerializeField] private bool scaleAxisLinesByCameraDistance = true;
        [Tooltip("Axis line distance reference. Higher = scaling kicks in later. Example: 1.5")]
        [Range(0.1f, 50f)]
        [SerializeField] private float axisLineDistanceScaleReference = 1.5f;
        [Tooltip("Min axis line distance scale. Lower = thinner lines when close. Example: 0.15")]
        [Range(0.01f, 5f)]
        [SerializeField] private float axisLineDistanceMinScale = 0.15f;
        [Tooltip("Max axis line distance scale. Higher = thicker lines when far. Example: 6")]
        [Range(0.1f, 50f)]
        [SerializeField] private float axisLineDistanceMaxScale = 6.0f;
        [Tooltip("Axis line distance exponent. Higher = faster growth with distance. Example: 1.35")]
        [Range(0.1f, 3f)]
        [SerializeField] private float axisLineDistanceExponent = 1.35f;

        [Header("Axis Line Size Scaling")]
        [Tooltip("Axis line size reference (unity diameter). Higher = shorter lines for same object size. Example: 0.15")]
        [Range(0.01f, 2f)]
        [SerializeField] private float axisLineSizeScaleReference = 0.15f;
        [Tooltip("Min size scale for axis lines. Higher = longer lines on small bodies. Example: 1.3")]
        [Range(0.1f, 5f)]
        [SerializeField] private float axisLineSizeMinScale = 1.3f;
        [Tooltip("Max size scale for axis lines. Higher = longer lines on large bodies. Example: 2.2")]
        [Range(0.1f, 10f)]
        [SerializeField] private float axisLineSizeMaxScale = 2.2f;
        [Tooltip("Max width scale from size. Higher = thicker lines on large bodies. Example: 1.6")]
        [Range(0.1f, 5f)]
        [SerializeField] private float axisLineSizeMaxWidthScale = 1.6f;
        [Tooltip("Max size scale for stars. Higher = longer star axes. Example: 1.4")]
        [Range(0.1f, 5f)]
        [SerializeField] private float axisLineSizeStarMaxScale = 1.4f;
        [Tooltip("Global axis-line length multiplier. Higher = longer axis lines. Example: 0.5")]
        [Range(0.1f, 2f)]
        [SerializeField] private float axisLineLengthScale = 0.5f;
        [Tooltip("Global axis-line thickness multiplier. Higher = thicker axis/world lines. Example: 1.6")]
        [Range(0.1f, 5f)]
        [SerializeField] private float axisLineThicknessScale = 1.6f;
        [Tooltip("Max axis-line length multiplier for stars. Higher = longer star lines. Example: 0.6")]
        [Range(0.1f, 3f)]
        [SerializeField] private float axisLineStarLengthMaxScale = 0.6f;
        [Tooltip("Small body diameter threshold (unity). Higher = more planets treated as small. Example: 0.25")]
        [Range(0.01f, 2f)]
        [SerializeField] private float axisLineSmallBodyDiameterThresholdUnity = 0.25f;
        [Tooltip("Extra length scale for small bodies. Higher = longer lines on small bodies. Example: 1.6")]
        [Range(0.1f, 5f)]
        [SerializeField] private float axisLineSmallBodyLengthScaleMultiplier = 1.6f;

        [Header("Orbit Line Distance Scaling")]
        [Tooltip("Scale orbit lines based on camera distance. Example: true. When false, distance scaling is disabled")]
        [SerializeField] private bool scaleOrbitLinesByCameraDistance = true;
        [Tooltip("Orbit line distance reference. Higher = thickening happens later. Example: 6")]
        [Range(0.1f, 100f)]
        [SerializeField] private float orbitLineDistanceScaleReference = 6.0f;
        [Tooltip("Min orbit line distance scale. Lower = thinner orbits when close. Example: 0.12")]
        [Range(0.01f, 5f)]
        [SerializeField] private float orbitLineDistanceMinScale = 0.12f;
        [Tooltip("Max orbit line distance scale. Higher = thicker orbits when far. Example: 20")]
        [Range(0.1f, 100f)]
        [SerializeField] private float orbitLineDistanceMaxScale = 20.0f;
        [Tooltip("Orbit line distance exponent. Higher = thicker faster as you zoom out. Example: 1.6")]
        [Range(0.1f, 3f)]
        [SerializeField] private float orbitLineDistanceExponent = 1.6f;
        [Tooltip("Near distance exponent for orbit lines. Higher = thicker sooner when near. Example: 0.85")]
        [Range(0.1f, 3f)]
        [SerializeField] private float orbitLineNearDistanceExponent = 0.85f;
        [Tooltip("Boost orbit thickness earlier with distance. Higher = thicker earlier. Example: 1.4")]
        [Range(0.1f, 5f)]
        [SerializeField] private float orbitLineDistanceScaleBoost = 1.4f;
        [Tooltip("Slow down thinning when near the camera. Higher = less thinning. Example: 0.7")]
        [Range(0.1f, 2f)]
        [SerializeField] private float orbitLineNearExponentMultiplier = 0.7f;
        [Tooltip("Extra headroom for distant orbit thickness. Higher = more max thickness. Example: 1.4")]
        [Range(0.1f, 5f)]
        [SerializeField] private float orbitLineDistanceMaxScaleBoost = 1.4f;

        [Header("Hypothetical Orbit Boost")]
        [Tooltip("Focus distance threshold for Planet X orbit scaling. Higher = boost applies farther out. Example: 10")]
        [Range(0.1f, 1000f)]
        [SerializeField] private float hypotheticalOrbitFocusDistance = 10.0f;
        [Tooltip("Scale multiplier for Planet X orbit when focused. Higher = thicker orbit. Example: 3")]
        [Range(1f, 50f)]
        [SerializeField] private float hypotheticalOrbitFocusScale = 3.0f;
        [Tooltip("Fixed scale for Planet X orbit when far. Higher = thicker orbit. Example: 30")]
        [Range(1f, 200f)]
        [SerializeField] private float hypotheticalOrbitFarScale = 30.0f;

        [Header("Tidal Lock")]
        [Tooltip("Enable automatic tidal locking when periods match within tolerance. Example: true")]
        [SerializeField] private bool enableTidalLock = true;
        [Tooltip("Restrict tidal lock to moons only (unless overridden). Example: true")]
        [SerializeField] private bool tidalLockOnlyForMoons = true;
        [Tooltip("Allowed period mismatch ratio for tidal lock. Higher = easier to lock, lower = stricter. Example: 0.02")]
        [Range(0f, 0.25f)]
        [SerializeField] private float tidalLockPeriodTolerance = 0.02f;
        [Tooltip("Facing offset in degrees for tidal-locked objects. Positive = rotate forward, negative = rotate back. Example: 0")]
        [Range(-180f, 180f)]
        [SerializeField] private float tidalLockFacingOffsetDeg = 0.0f;

        [Header("UI")]
        [Tooltip("Sprite used for runtime UI avatar buttons. Example: Earth_Icon")]
        [SerializeField] private Sprite? avatarSprite;
        #endregion

        #region Runtime State
        // Identity and role.
        private string id = string.Empty;
        private bool isReference = false;
        private bool isHypothetical = false;
        private string type = string.Empty;
        private string primaryId = string.Empty;
        private int orderFromSun = -1;
        private SolarObject? primarySolarObject;
        private bool? alignOrbitToPrimaryTilt;
        private bool? tidalLockOverride;

        // Cached references.
        private Transform? primaryTransform;
        private VisualContext? visualContext;
        private CameraFocusProfile cameraFocusProfile = CameraFocusProfile.Auto;

        // Physical and visual scale.
        private double radiusKm = 1.0;
        private double dataRadiusMultiplier = 1.0;
        private double dataDistanceMultiplier = 1.0;
        private float solarObjectDiameterUnity = 0.1f;

        // Spin state.
        private bool hasSpin = false;
        private double rotationPeriodSeconds = 0.0;
        private float axialTiltDeg = 0.0f;
        private float spinDirection = 1.0f;

        // Orbit state.
        private bool hasOrbit = false;
        private double orbitalPeriodSeconds = 0.0;
        private double semiMajorAxisKm = 0.0;
        private double initialMeanAnomalyOffsetRad = 0.0;

        // Keplerian elements (radians).
        private double eccentricity = 0.0;
        private double inclinationRad = 0.0;
        private double argumentPeriapsisRad = 0.0;
        private double longitudeAscendingNodeRad = 0.0;
        private double meanAnomalyRad = 0.0;

        // Orbit line cache.
        private Vector3[]? orbitPoints;
        private Vector3[]? orbitWorldPoints;
        private bool orbitPointsDirty = true;
        private float minOrbitRadiusUnity = 0.0f;

        // Runtime renderers.
        private LineRenderer? orbitLine;
        private LineRenderer? axisLine;
        private LineRenderer? worldUpLine;
        private LineRenderer? spinDirectionLine;
        private Vector3 lastPrimaryPosition;
        private bool hasPrimaryPosition = false;

        private static Material? lineMaterial;
        private static Camera? lineScaleCamera;
        private bool lineStylesDirty = true;
        private float lineScaleUpdateTimer = 0.0f;
        private float lastLineScaleCameraDistance = -1.0f;
        private float axisLineDistanceScale = 1.0f;
        private float orbitLineDistanceScale = 1.0f;
        #endregion

        #region Public API
        /// <summary>
        /// Dataset id for this solar object.
        /// </summary>
        public string Id => id;

        /// <summary>
        /// Dataset id for the primary object (or empty if none).
        /// </summary>
        public string PrimaryId => primaryId;

        /// <summary>
        /// True when the object is a moon.
        /// </summary>
        public bool IsMoon => string.Equals(type, "moon", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// True when the object is a dwarf planet.
        /// </summary>
        public bool IsDwarfPlanet => string.Equals(type, "dwarf_planet", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// True when the object is a planet.
        /// </summary>
        public bool IsPlanet => string.Equals(type, "planet", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// True when the object is a star.
        /// </summary>
        public bool IsStar => string.Equals(type, "star", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// True when the object is marked as hypothetical in the dataset.
        /// </summary>
        public bool IsHypothetical => isHypothetical;

        /// <summary>
        /// Primary solar object instance (when available).
        /// </summary>
        public SolarObject? PrimarySolarObject => primarySolarObject;

        /// <summary>
        /// Current diameter in Unity units (includes global radius multiplier).
        /// </summary>
        public float DiameterUnity => solarObjectDiameterUnity;

        /// <summary>
        /// Axial tilt in degrees.
        /// </summary>
        public float AxialTiltDeg => axialTiltDeg;

        /// <summary>
        /// Sprite used for UI avatar buttons.
        /// </summary>
        public Sprite? AvatarSprite => avatarSprite;

        /// <summary>
        /// Camera zoom profile for focus navigation.
        /// </summary>
        public CameraFocusProfile FocusProfile => cameraFocusProfile;

        /// <summary>
        /// Diameter normalized to ignore the global radius multiplier.
        /// </summary>
        public float BaseDiameterUnity
        {
            get
            {
                if (visualContext == null)
                {
                    return solarObjectDiameterUnity;
                }

                double _global = Math.Max(1e-6, visualContext.GlobalRadiusScale);
                return (float)(solarObjectDiameterUnity / _global);
            }
        }
        #endregion
    }
}
