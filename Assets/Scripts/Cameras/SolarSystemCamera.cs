#nullable enable
using System;
using Assets.Scripts.Runtime;
using UnityEngine;

namespace Assets.Scripts.Cameras
{
    /// <summary>
    /// Custom camera rig for focus and overview navigation.
    /// </summary>
    public sealed partial class SolarSystemCamera : MonoBehaviour
    {
        #region Types
        private enum CameraMode
        {
            Overview,
            Focus,
        }

        private enum TransitionPhase
        {
            None,
            Align,
            Travel,
        }

        [Serializable]
        private struct FocusZoomRange
        {
            [Tooltip("Minimum focus distance at realism = 0. Higher = less zoom-in allowed. Example: 0.05")]
            [Range(0.01f, 5000f)]
            public float MinSimulation;
            [Tooltip("Maximum focus distance at realism = 0. Higher = more zoom-out allowed. Example: 0.5")]
            [Range(0.01f, 5000f)]
            public float MaxSimulation;
            [Tooltip("Minimum focus distance at realism = 1. Higher = less zoom-in allowed. Example: 0.2")]
            [Range(0.01f, 5000f)]
            public float MinRealistic;
            [Tooltip("Maximum focus distance at realism = 1. Higher = more zoom-out allowed. Example: 0.5")]
            [Range(0.01f, 5000f)]
            public float MaxRealistic;
        }
        #endregion

        #region Serialized Fields
        [Header("Smoothing")]
        [Tooltip("Smooth time in seconds for camera position. Higher = slower. Example: 0.25")]
        [Range(0f, 5f)]
        [SerializeField] private float positionSmoothSeconds = 0.35f;
        [Tooltip("Smooth time in seconds for camera rotation. Higher = slower. Example: 0.2")]
        [Range(0f, 5f)]
        [SerializeField] private float rotationSmoothSeconds = 0.3f;
        [Tooltip("Smooth time in seconds for camera position while orbit input is active. Lower = tighter follow. Example: 0.08")]
        [Range(0f, 5f)]
        [SerializeField] private float orbitInputPositionSmoothSeconds = 0.1f;
        [Tooltip("Smooth time in seconds for camera rotation while orbit input is active. Lower = tighter aim. Example: 0.05")]
        [Range(0f, 5f)]
        [SerializeField] private float orbitInputRotationSmoothSeconds = 0.07f;
        [Tooltip("Seconds to keep orbit input smoothing active after the last orbit step. Example: 0.12")]
        [Range(0f, 1f)]
        [SerializeField] private float orbitInputActiveHoldSeconds = 0.15f;

        [Header("Transitions")]
        [Tooltip("Seconds per degree during alignment. Higher = slower rotation. Example: 0.01")]
        [Range(0f, 0.2f)]
        [SerializeField] private float transitionAlignSecondsPerDegree = 0.015f;
        [Tooltip("Minimum align duration in seconds. Example: 0.5")]
        [Range(0f, 5f)]
        [SerializeField] private float transitionAlignMinSeconds = 0.75f;
        [Tooltip("Maximum align duration in seconds. Example: 2")]
        [Range(0f, 10f)]
        [SerializeField] private float transitionAlignMaxSeconds = 3.0f;
        [Tooltip("Fraction of travel allowed during alignment. Keep tiny for near-static. Example: 0.02")]
        [Range(0f, 0.25f)]
        [SerializeField] private float transitionAlignMoveFraction = 0.02f;
        [Tooltip("Seconds per unit during travel. Higher = slower travel. Example: 0.05")]
        [Range(0f, 1f)]
        [SerializeField] private float transitionTravelSecondsPerUnit = 0.07f;
        [Tooltip("Minimum travel duration in seconds. Example: 0.6")]
        [Range(0f, 10f)]
        [SerializeField] private float transitionTravelMinSeconds = 0.8f;
        [Tooltip("Maximum travel duration in seconds. Example: 4")]
        [Range(0f, 20f)]
        [SerializeField] private float transitionTravelMaxSeconds = 5.0f;

        [Header("Proxy Targets")]
        [Tooltip("Optional overview proxy object name. Leave empty to skip. Example: Overview_Proxy")]
        [SerializeField] private string overviewProxyName = "Overview_Proxy";

        [Header("Focus Zoom Profiles")]
        [Tooltip("Focus zoom range for moons.")]
        [SerializeField] private FocusZoomRange moonZoomRange = new FocusZoomRange
        {
            MinSimulation = 0.05f,
            MaxSimulation = 0.4f,
            MinRealistic = 0.2f,
            MaxRealistic = 0.4f,
        };
        [Tooltip("Focus zoom range for dwarf planets.")]
        [SerializeField] private FocusZoomRange dwarfZoomRange = new FocusZoomRange
        {
            MinSimulation = 0.06f,
            MaxSimulation = 0.45f,
            MinRealistic = 0.24f,
            MaxRealistic = 0.45f,
        };
        [Tooltip("Focus zoom range for terrestrial planets.")]
        [SerializeField] private FocusZoomRange terrestrialZoomRange = new FocusZoomRange
        {
            MinSimulation = 0.05f,
            MaxSimulation = 0.55f,
            MinRealistic = 0.2f,
            MaxRealistic = 0.55f,
        };
        [Tooltip("Focus zoom range for gas giants.")]
        [SerializeField] private FocusZoomRange gasGiantZoomRange = new FocusZoomRange
        {
            MinSimulation = 0.234f,
            MaxSimulation = 1.8f,
            MinRealistic = 0.936f,
            MaxRealistic = 1.8f,
        };
        [Tooltip("Focus zoom range for ice giants.")]
        [SerializeField] private FocusZoomRange iceGiantZoomRange = new FocusZoomRange
        {
            MinSimulation = 0.13f,
            MaxSimulation = 1.35f,
            MinRealistic = 0.52f,
            MaxRealistic = 1.35f,
        };
        [Tooltip("Focus zoom range for stars (Sun).")]
        [SerializeField] private FocusZoomRange starZoomRange = new FocusZoomRange
        {
            MinSimulation = 1.5f,
            MaxSimulation = 12.0f,
            MinRealistic = 2.5f,
            MaxRealistic = 12.0f,
        };

        [Header("Focus Zoom Safety")]
        [Tooltip("Hard clamp to prevent camera from going too close. Higher = stricter clamp. Example: 0.05")]
        [Range(0.01f, 10f)]
        [SerializeField] private float focusZoomAbsoluteMinDistance = 0.05f;

        [Header("Zoom Controls")]
        [Tooltip("Overview minimum camera distance. Higher = keeps overview farther. Example: 2")]
        [Range(0.1f, 500f)]
        [SerializeField] private float overviewZoomMinDistance = 2f;
        [Tooltip("Overview maximum camera distance. Higher = allows farther zoom out. Example: 120")]
        [Range(1f, 5000f)]
        [SerializeField] private float overviewZoomMaxDistance = 120f;
        [Tooltip("Overview default distance. Higher = start farther out. Example: 5")]
        [Range(0.1f, 500f)]
        [SerializeField] private float overviewDefaultDistance = 10f;
        [Tooltip("Overview zoom speed multiplier. Higher = faster zoom steps. Example: 4")]
        [Range(1f, 50f)]
        [SerializeField] private float overviewZoomSpeedScale = 4f;
        [Tooltip("Focus zoom step per click in world units. Higher = bigger steps. Example: 0.01")]
        [Range(0.001f, 1f)]
        [SerializeField] private float focusZoomStepSize = 0.01f;
        [Tooltip("Focus zoom step multiplier for stars. Higher = faster zoom for the Sun. Example: 2")]
        [Range(0.1f, 10f)]
        [SerializeField] private float focusZoomStarStepMultiplier = 2.0f;
        [Tooltip("Overview zoom step per click in world units. Higher = bigger steps. Example: 0.06")]
        [Range(0.001f, 5f)]
        [SerializeField] private float overviewZoomStepSize = 0.06f;
        [Tooltip("Normalized zoom fraction used when selecting a new focus target. 0 = min, 1 = max. Example: 0.15")]
        [Range(0f, 1f)]
        [SerializeField] private float focusSelectZoomFraction = 0.15f;

        [Header("Overview Start")]
        [Tooltip("Overview start height offset from the target in world units. Example: 2")]
        [Range(-100f, 100f)]
        [SerializeField] private float overviewStartHeightOffset = 2f;

        [Header("Overview Zoom Acceleration")]
        [Tooltip("Normalized distance where overview zoom acceleration begins. Lower = speed ramps earlier. Example: 0.4")]
        [Range(0f, 1f)]
        [SerializeField] private float overviewZoomDistanceSpeedStartFraction = 0.4f;
        [Tooltip("Max overview zoom speed multiplier at realism = 0. Higher = faster far zooming. Example: 3")]
        [Range(1f, 50f)]
        [SerializeField] private float overviewZoomDistanceSpeedMaxMultiplier = 3f;
        [Tooltip("Max overview zoom speed multiplier at realism = 1. Higher = faster far zooming. Example: 8")]
        [Range(1f, 200f)]
        [SerializeField] private float realisticOverviewZoomDistanceSpeedMaxMultiplier = 8f;

        [Header("Realism Overrides")]
        [Tooltip("Overview min distance when realism is 1. Higher = keeps overview farther. Example: 50")]
        [Range(0.1f, 1000f)]
        [SerializeField] private float realisticOverviewZoomMinDistance = 50f;
        [Tooltip("Overview max distance when realism is 1. Higher = allows farther zoom out. Example: 3000")]
        [Range(1f, 10000f)]
        [SerializeField] private float realisticOverviewZoomMaxDistance = 3000f;
        [Tooltip("Overview zoom speed multiplier when realism is 1. Higher = faster zoom steps. Example: 20")]
        [Range(1f, 100f)]
        [SerializeField] private float realisticOverviewZoomSpeedScale = 20f;

        [Header("Orbit Controls")]
        [Tooltip("Orbit step size in world units. Higher = larger orbit moves. Example: 0.06")]
        [Range(0.01f, 5f)]
        [SerializeField] private float orbitStepSize = 0.06f;
        [Tooltip("Max focus orbit offset in world units. Higher = wider orbit range. Example: 1")]
        [Range(0f, 50f)]
        [SerializeField] private float focusOrbitMaxOffset = 1f;
        [Tooltip("Max overview orbit offset in world units. Higher = wider orbit range. Example: 5")]
        [Range(0f, 200f)]
        [SerializeField] private float overviewOrbitMaxOffset = 5f;
        [Tooltip("Max orbit pitch in degrees. Higher = allow closer to top/bottom. Example: 80")]
        [Range(0f, 89f)]
        [SerializeField] private float orbitMaxPitchDegrees = 80f;
        [Tooltip("Max orbit radius factor of camera distance. Higher = larger orbit radius. Example: 0.5")]
        [Range(0f, 1f)]
        [SerializeField] private float orbitRadiusMaxDistanceFactor = 0.5f;
        [Tooltip("Max orbit step in degrees per input. Higher = larger steps. Example: 6")]
        [Range(0.1f, 45f)]
        [SerializeField] private float orbitStepMaxDegrees = 6f;
        [Tooltip("Overview orbit step multiplier when far away. Higher = faster orbiting. Example: 3")]
        [Range(1f, 20f)]
        [SerializeField] private float overviewOrbitStepDistanceScale = 3f;
        [Tooltip("Distance threshold for overview orbit step boost. Higher = boost starts later. Example: 25")]
        [Range(0.1f, 500f)]
        [SerializeField] private float overviewOrbitStepBoostStartDistance = 25f;
        #endregion

        #region Constants
        private const float HighSpeedTimeScaleThreshold = 200000f;
        private const float HighSpeedTransitionDurationMultiplier = 0.5f;
        private const float HighSpeedSmoothTimeMultiplier = 0.6f;
        #endregion

        #region Runtime State
        private bool isInitialized = false;
        private Camera? mainCamera;
        private SolarSystemSimulator? simulator = null;
        private float realismLevel = 0f;
        private CameraMode currentMode = CameraMode.Overview;

        private Transform? focusTarget;
        private Transform? overviewTarget;
        private Transform? overviewProxy;
        private SolarObject? focusSolarObject;

        private float focusYaw = 0f;
        private float focusPitch = 0f;
        private float overviewYaw = 0f;
        private float overviewPitch = 0f;
        private bool focusOrbitInitialized = false;
        private bool overviewOrbitInitialized = false;

        private float focusZoomNormalized = 0f;
        private float overviewZoomNormalized = 0f;

        private Vector3 positionVelocity = Vector3.zero;
        private float lastOrbitInputTime = -100f;

        private bool isTransitioning = false;
        private CameraMode transitionMode = CameraMode.Overview;
        private TransitionPhase transitionPhase = TransitionPhase.None;
        private float transitionTimer = 0f;
        private float transitionAlignDuration = 0f;
        private float transitionTravelDuration = 0f;
        private Vector3 transitionStartPosition = Vector3.zero;
        private Quaternion transitionStartRotation = Quaternion.identity;
        private Vector3 transitionTargetPosition = Vector3.zero;
        private Vector3 transitionTargetLookAt = Vector3.zero;
        private Vector3 transitionStartLookAt = Vector3.zero;
        private Vector3 transitionLookAtOverride = Vector3.zero;
        private bool transitionBlendLookAt = false;
        private bool transitionLookAtOverrideActive = false;

        private float RealismLevel01 => Mathf.Clamp01(realismLevel);
        #endregion
    }
}
