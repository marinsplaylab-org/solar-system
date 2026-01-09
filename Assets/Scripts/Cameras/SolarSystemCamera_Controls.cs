#nullable enable
using System;
using Assets.Scripts.Helpers.Debugging;
using Assets.Scripts.Runtime;
using UnityEngine;

namespace Assets.Scripts.Cameras
{
    public sealed partial class SolarSystemCamera
    {
        #region Controls
        /// <summary>
        /// Apply a discrete orbit step around the active target.
        /// </summary>
        private void HandleCameraOrbitStepRequested(Vector2 _direction)
        {
            if (_direction == Vector2.zero)
            {
                return;
            }

            EnsureInitializedForRuntime();
            if (mainCamera == null)
            {
                return;
            }

            CancelTransition();

            bool _useFocus = currentMode == CameraMode.Focus;
            Transform? _target = _useFocus ? focusTarget : overviewTarget;
            if (_target == null)
            {
                HelpLogs.Warn("Camera", "Orbit request ignored because target is missing.");
                return;
            }

            float _distance = _useFocus ? GetFocusDistanceForCurrentTarget() : GetOverviewDistance();
            float _radius = GetOrbitRadiusFromDistance(_distance, _useFocus ? focusOrbitMaxOffset : overviewOrbitMaxOffset);
            float _step = GetOrbitStepDegrees(_radius);
            if (!_useFocus)
            {
                _step *= GetOverviewOrbitStepMultiplier(_distance);
            }

            if (_useFocus)
            {
                focusYaw = WrapAngle(focusYaw + _direction.x * _step);
                focusPitch = Mathf.Clamp(focusPitch + _direction.y * _step, -orbitMaxPitchDegrees, orbitMaxPitchDegrees);
                focusOrbitInitialized = true;
            }
            else
            {
                overviewYaw = WrapAngle(overviewYaw + _direction.x * _step);
                overviewPitch = Mathf.Clamp(overviewPitch + _direction.y * _step, -orbitMaxPitchDegrees, orbitMaxPitchDegrees);
                overviewOrbitInitialized = true;
            }

            lastOrbitInputTime = Time.unscaledTime;
        }

        /// <summary>
        /// Apply a discrete zoom step for focus or overview.
        /// </summary>
        private void HandleCameraZoomStepRequested(int _delta)
        {
            if (_delta == 0)
            {
                return;
            }

            EnsureInitializedForRuntime();
            if (mainCamera == null)
            {
                return;
            }

            CancelTransition();

            if (currentMode == CameraMode.Focus)
            {
                if (focusSolarObject == null)
                {
                    HelpLogs.Warn("Camera", "Zoom request ignored because focus target is missing.");
                    return;
                }

                if (_delta > 0 && focusZoomNormalized >= 0.999f)
                {
                    ShowOverview();
                    return;
                }

                AdjustFocusZoomNormalized(_delta);
            }
            else
            {
                AdjustOverviewZoomNormalized(_delta);
            }
        }

        /// <summary>
        /// Adjust focus zoom using normalized steps.
        /// </summary>
        private void AdjustFocusZoomNormalized(int _delta)
        {
            if (focusSolarObject == null)
            {
                return;
            }

            GetFocusZoomRange(focusSolarObject, out float _minDistance, out float _maxDistance);
            float _range = Mathf.Max(0.0001f, _maxDistance - _minDistance);
            float _step = focusZoomStepSize / _range;
            if (focusSolarObject.IsStar ||
                string.Equals(focusSolarObject.Id, "sun", StringComparison.OrdinalIgnoreCase))
            {
                _step *= Mathf.Max(0.1f, focusZoomStarStepMultiplier);
            }

            focusZoomNormalized = Mathf.Clamp01(focusZoomNormalized + _delta * _step);
            ApplyFocusDistance(focusSolarObject);
        }

        /// <summary>
        /// Adjust overview zoom using normalized steps.
        /// </summary>
        private void AdjustOverviewZoomNormalized(int _delta)
        {
            GetOverviewZoomRange(out float _minDistance, out float _maxDistance);
            float _range = Mathf.Max(0.0001f, _maxDistance - _minDistance);
            float _normalized = Mathf.Clamp01(overviewZoomNormalized);
            float _distance = Mathf.Lerp(_minDistance, _maxDistance, _normalized);
            float _step = (overviewZoomStepSize * GetOverviewZoomSpeedMultiplier(_distance)) / _range;

            overviewZoomNormalized = Mathf.Clamp01(_normalized + _delta * _step);
            ApplyOverviewDistance();
        }

        /// <summary>
        /// Convert orbit step size to degrees based on radius.
        /// </summary>
        private float GetOrbitStepDegrees(float _radius)
        {
            if (_radius <= 0f)
            {
                return 0f;
            }

            float _radians = orbitStepSize / _radius;
            float _degrees = Mathf.Rad2Deg * _radians;
            float _max = Mathf.Max(0.01f, orbitStepMaxDegrees);
            return Mathf.Min(_degrees, _max);
        }

        /// <summary>
        /// Increase orbit step size as overview distance grows.
        /// </summary>
        private float GetOverviewOrbitStepMultiplier(float _cameraDistance)
        {
            float _distance = Mathf.Max(0f, _cameraDistance);
            float _start = Mathf.Max(1f, overviewOrbitStepBoostStartDistance);
            if (_distance <= _start)
            {
                return 1.0f;
            }

            float _t = Mathf.Clamp01((_distance - _start) / _start);
            float _target = Mathf.Max(1.0f, overviewOrbitStepDistanceScale);
            return Mathf.Lerp(1.0f, _target, _t);
        }

        /// <summary>
        /// Sync orbit yaw/pitch to the current camera direction.
        /// </summary>
        private void SyncOrbitToCameraView(
            Transform _target,
            float _radius,
            ref float _yaw,
            ref float _pitch,
            ref bool _initialized
        )
        {
            if (!TryGetCameraDirection(_target, out Vector3 _direction))
            {
                return;
            }

            SetOrbitFromDirection(_direction, _radius, ref _yaw, ref _pitch);
            _initialized = true;
        }

        /// <summary>
        /// Convert a direction vector into yaw/pitch orbit values.
        /// </summary>
        private void SetOrbitFromDirection(Vector3 _direction, float _radius, ref float _yaw, ref float _pitch)
        {
            if (_direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 _forward = -_direction.normalized;
            float _yawRad = Mathf.Atan2(_forward.x, _forward.z);
            float _pitchRad = -Mathf.Asin(Mathf.Clamp(_forward.y, -1f, 1f));

            _yaw = WrapAngle(Mathf.Rad2Deg * _yawRad);
            _pitch = Mathf.Clamp(Mathf.Rad2Deg * _pitchRad, -orbitMaxPitchDegrees, orbitMaxPitchDegrees);
        }

        /// <summary>
        /// Wrap degrees into a stable range.
        /// </summary>
        private float WrapAngle(float _angle)
        {
            if (_angle >= 360f || _angle <= -360f)
            {
                _angle %= 360f;
            }

            return _angle;
        }

        /// <summary>
        /// Resolve the camera direction vector from the target.
        /// </summary>
        private bool TryGetCameraDirection(Transform _target, out Vector3 _direction)
        {
            _direction = Vector3.zero;
            if (mainCamera == null)
            {
                return false;
            }

            _direction = mainCamera.transform.position - _target.position;
            if (_direction.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            _direction.Normalize();
            return true;
        }
        #endregion
    }
}
