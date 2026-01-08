#nullable enable
using System;
using System.Collections.Generic;
using Assets.Scripts.Helpers.Debugging;
using Assets.Scripts.Runtime;
using UnityEngine;

namespace Assets.Scripts.Cameras
{
    public sealed partial class SolarSystemCamera
    {
        #region Public API
        /// <summary>
        /// True when the overview target is assigned.
        /// </summary>
        public bool HasOverviewTarget => overviewTarget != null;

        /// <summary>
        /// True when the camera is in focus mode.
        /// </summary>
        public bool IsFocusMode => currentMode == CameraMode.Focus;

        /// <summary>
        /// True when the camera is in overview mode.
        /// </summary>
        public bool IsOverviewMode => currentMode == CameraMode.Overview;

        /// <summary>
        /// Normalized focus zoom fraction. 0 = min, 1 = max.
        /// </summary>
        public float FocusZoomNormalized => Mathf.Clamp01(focusZoomNormalized);

        /// <summary>
        /// Normalized overview zoom fraction. 0 = min, 1 = max.
        /// </summary>
        public float OverviewZoomNormalized => Mathf.Clamp01(overviewZoomNormalized);

        /// <summary>
        /// Initialize references and apply the default overview.
        /// </summary>
        public void Initialize()
        {
            if (isInitialized)
            {
                HelpLogs.Warn("Camera", "SolarSystemCamera already initialized.");
                return;
            }

            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                HelpLogs.Warn("Camera", "No camera found for SolarSystemCamera.");
                return;
            }

            if (simulator == null)
            {
                simulator = FindFirstObjectByType<SolarSystemSimulator>();
                if (simulator == null)
                {
                    HelpLogs.Warn("Camera", "SolarSystemSimulator not found.");
                }
            }

            EnsureOverviewTarget();
            currentMode = CameraMode.Overview;

            if (overviewTarget != null)
            {
                SyncOverviewZoomNormalizedFromDistance(overviewDefaultDistance);
                SyncOrbitToCameraView(
                    overviewTarget,
                    GetOverviewOrbitRadius(GetOverviewDistance()),
                    ref overviewYaw,
                    ref overviewPitch,
                    ref overviewOrbitInitialized
                );
                ApplyOverviewStartHeightOffset(GetOverviewDistance());
            }

            SnapToCurrentMode();
            isInitialized = true;
        }

        /// <summary>
        /// Focus the camera on the requested solar object.
        /// </summary>
        public void FocusOn(SolarObject _solarObject)
        {
            if (_solarObject == null)
            {
                HelpLogs.Warn("Camera", "Focus request ignored because solar object is null.");
                return;
            }

            EnsureInitializedForRuntime();
            if (mainCamera == null)
            {
                return;
            }

            if (IsHighSpeedMode() && _solarObject.IsMoon)
            {
                SolarObject? _primary = _solarObject.PrimarySolarObject;
                if (_primary == null && simulator != null && !string.IsNullOrWhiteSpace(_solarObject.PrimaryId))
                {
                    IReadOnlyList<SolarObject> _objects = simulator.OrderedSolarObjects;
                    for (int _i = 0; _i < _objects.Count; _i++)
                    {
                        SolarObject _candidate = _objects[_i];
                        if (string.Equals(_candidate.Id, _solarObject.PrimaryId, StringComparison.OrdinalIgnoreCase))
                        {
                            _primary = _candidate;
                            break;
                        }
                    }
                }

                string _moonName = _solarObject.name;
                if (_primary != null)
                {
                    HelpLogs.Warn(
                        "Camera",
                        $"High time scale active. Moon '{_moonName}' focus redirected to primary '{_primary.name}'."
                    );
                    _solarObject = _primary;
                }
                else
                {
                    HelpLogs.Warn(
                        "Camera",
                        $"High time scale active. Moon '{_moonName}' focus requested but primary not found."
                    );
                }
            }

            bool _isNewTarget = focusSolarObject == null || focusSolarObject != _solarObject;
            focusSolarObject = _solarObject;
            focusTarget = _solarObject.transform;
            currentMode = CameraMode.Focus;

            if (simulator == null)
            {
                simulator = FindFirstObjectByType<SolarSystemSimulator>();
            }

            if (simulator != null)
            {
                simulator.SetFocusedSolarObject(_solarObject);
            }

            if (_isNewTarget)
            {
                SetFocusZoomForSelection();
            }

            ApplyFocusDistance(_solarObject);

            if (_isNewTarget || !focusOrbitInitialized)
            {
                SyncOrbitToCameraView(
                    focusTarget,
                    GetFocusOrbitRadius(GetFocusDistanceForCurrentTarget()),
                    ref focusYaw,
                    ref focusPitch,
                    ref focusOrbitInitialized
                );
            }

            BeginTransition(CameraMode.Focus);
        }

        /// <summary>
        /// Switch to the overview camera.
        /// </summary>
        public void ShowOverview()
        {
            EnsureInitializedForRuntime();
            if (mainCamera == null)
            {
                return;
            }

            EnsureOverviewTarget();
            if (overviewTarget == null)
            {
                HelpLogs.Warn("Camera", "Overview target not found.");
                return;
            }

            currentMode = CameraMode.Overview;

            if (simulator == null)
            {
                simulator = FindFirstObjectByType<SolarSystemSimulator>();
            }

            if (simulator != null)
            {
                simulator.SetFocusedSolarObject(null);
            }

            ApplyOverviewDistance();

            if (!overviewOrbitInitialized)
            {
                SyncOrbitToCameraView(
                    overviewTarget,
                    GetOverviewOrbitRadius(GetOverviewDistance()),
                    ref overviewYaw,
                    ref overviewPitch,
                    ref overviewOrbitInitialized
                );
            }

            BeginTransition(CameraMode.Overview);
        }

        /// <summary>
        /// Assign the overview target explicitly.
        /// </summary>
        public void SetOverviewTarget(Transform? _target)
        {
            if (_target == null)
            {
                HelpLogs.Warn("Camera", "Overview target is null; keeping previous target.");
                return;
            }

            overviewTarget = _target;
        }

        /// <summary>
        /// Toggle axis, world-up, and spin-direction lines for the focused object.
        /// </summary>
        public void ToggleFocusAxisLines(SolarObject _solarObject)
        {
            if (_solarObject == null)
            {
                HelpLogs.Warn("Camera", "Axis line toggle ignored because solar object is null.");
                return;
            }

            if (simulator == null)
            {
                simulator = FindFirstObjectByType<SolarSystemSimulator>();
                if (simulator == null)
                {
                    HelpLogs.Warn("Camera", "SolarSystemSimulator not found for axis line toggle.");
                    return;
                }
            }

            simulator.ToggleFocusAxisLines(_solarObject);
        }
        #endregion
    }
}
