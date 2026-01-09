#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Assets.Scripts.Helpers.Debugging;
using Assets.Scripts.Runtime;
using UnityEngine;

namespace Assets.Scripts.Cameras
{
    public sealed partial class SolarSystemCamera
    {
        #region Helpers
        private void EnsureInitializedForRuntime()
        {
            if (!isInitialized)
            {
                Initialize();
            }
        }

        private void SnapToCurrentMode()
        {
            if (mainCamera == null)
            {
                return;
            }

            if (!TryGetModeTarget(currentMode, out Transform? _target))
            {
                return;
            }

            Vector3 _position = GetDesiredCameraPosition(currentMode, _target);
            Quaternion _rotation = GetLookRotation(_target.position, _position);
            mainCamera.transform.SetPositionAndRotation(_position, _rotation);
            positionVelocity = Vector3.zero;
        }

        private void UpdateCameraMovement(float _dt)
        {
            if (mainCamera == null)
            {
                return;
            }

            if (!TryGetModeTarget(currentMode, out Transform? _target))
            {
                return;
            }

            Vector3 _desiredPosition = GetDesiredCameraPosition(currentMode, _target);
            Vector3 _currentPosition = mainCamera.transform.position;
            bool _useOrbitInputSmooth = IsOrbitInputSmoothActive();
            Vector3 _newPosition = SmoothPosition(_currentPosition, _desiredPosition, _dt, _useOrbitInputSmooth);

            Quaternion _desiredRotation = GetLookRotation(_target.position, _newPosition);
            Quaternion _currentRotation = mainCamera.transform.rotation;
            Quaternion _newRotation = SmoothRotation(_currentRotation, _desiredRotation, _dt, _useOrbitInputSmooth);

            mainCamera.transform.SetPositionAndRotation(_newPosition, _newRotation);
        }

        private Vector3 SmoothPosition(Vector3 _current, Vector3 _target, float _dt, bool _useOrbitInputSmooth)
        {
            float _baseSmooth = _useOrbitInputSmooth ? orbitInputPositionSmoothSeconds : positionSmoothSeconds;
            float _smoothTime = GetSmoothSeconds(_baseSmooth);
            if (_smoothTime <= 0f)
            {
                positionVelocity = Vector3.zero;
                return _target;
            }

            return Vector3.SmoothDamp(_current, _target, ref positionVelocity, _smoothTime, Mathf.Infinity, _dt);
        }

        private Quaternion SmoothRotation(Quaternion _current, Quaternion _target, float _dt, bool _useOrbitInputSmooth)
        {
            float _baseSmooth = _useOrbitInputSmooth ? orbitInputRotationSmoothSeconds : rotationSmoothSeconds;
            float _smoothTime = GetSmoothSeconds(_baseSmooth);
            if (_smoothTime <= 0f)
            {
                return _target;
            }

            float _t = 1f - Mathf.Exp(-_dt / Mathf.Max(0.0001f, _smoothTime));
            return Quaternion.Slerp(_current, _target, _t);
        }

        private Quaternion GetLookRotation(Vector3 _lookAtPosition, Vector3 _cameraPosition)
        {
            Vector3 _dir = _lookAtPosition - _cameraPosition;
            if (_dir.sqrMagnitude <= 1e-8f)
            {
                return Quaternion.identity;
            }

            return Quaternion.LookRotation(_dir.normalized, Vector3.up);
        }

        private bool TryGetModeTarget(CameraMode _mode, [NotNullWhen(true)] out Transform? _target)
        {
            _target = _mode == CameraMode.Focus ? focusTarget : overviewTarget;
            return _target != null;
        }

        private Vector3 GetDesiredCameraPosition(CameraMode _mode, Transform _target)
        {
            float _distance = _mode == CameraMode.Focus ? GetFocusDistanceForCurrentTarget() : GetOverviewDistance();
            float _yaw = _mode == CameraMode.Focus ? focusYaw : overviewYaw;
            float _pitch = _mode == CameraMode.Focus ? focusPitch : overviewPitch;
            Vector3 _offset = GetOrbitOffset(_distance, _yaw, _pitch);

            return _target.position + _offset;
        }

        private Vector3 GetOrbitOffset(float _radius, float _yawDegrees, float _pitchDegrees)
        {
            Quaternion _rotation = Quaternion.Euler(_pitchDegrees, _yawDegrees, 0f);
            return _rotation * Vector3.back * _radius;
        }

        private float GetOrbitRadiusFromDistance(float _cameraDistance, float _maxOffset)
        {
            float _factor = Mathf.Clamp01(orbitRadiusMaxDistanceFactor);
            float _radius = _cameraDistance * _factor;
            _radius = Mathf.Min(_radius, _maxOffset);
            return Mathf.Max(0f, _radius);
        }

        private float GetFocusOrbitRadius(float _distance)
        {
            return GetOrbitRadiusFromDistance(_distance, focusOrbitMaxOffset);
        }

        private float GetOverviewOrbitRadius(float _distance)
        {
            return GetOrbitRadiusFromDistance(_distance, overviewOrbitMaxOffset);
        }

        private void BeginTransition(CameraMode _mode)
        {
            if (mainCamera == null)
            {
                return;
            }

            if (!TryGetModeTarget(_mode, out Transform? _target))
            {
                return;
            }

            transitionMode = _mode;
            transitionPhase = TransitionPhase.Align;
            transitionTimer = 0f;
            isTransitioning = true;

            transitionStartPosition = mainCamera.transform.position;
            transitionStartRotation = mainCamera.transform.rotation;
            transitionTargetLookAt = _target.position;
            transitionStartLookAt = transitionTargetLookAt;
            transitionBlendLookAt = false;
            if (transitionLookAtOverrideActive)
            {
                transitionStartLookAt = transitionLookAtOverride;
                transitionBlendLookAt = true;
                transitionLookAtOverrideActive = false;
            }
            transitionTargetPosition = GetDesiredCameraPosition(_mode, _target);

            Quaternion _targetRotation = GetLookRotation(transitionTargetLookAt, transitionStartPosition);
            float _angle = Quaternion.Angle(transitionStartRotation, _targetRotation);
            transitionAlignDuration = Mathf.Clamp(
                _angle * Mathf.Max(0f, transitionAlignSecondsPerDegree),
                transitionAlignMinSeconds,
                transitionAlignMaxSeconds
            );
            transitionAlignDuration = ApplyHighSpeedDurationMultiplier(transitionAlignDuration);

            float _distance = Vector3.Distance(transitionStartPosition, transitionTargetPosition);
            transitionTravelDuration = Mathf.Clamp(
                _distance * Mathf.Max(0f, transitionTravelSecondsPerUnit),
                transitionTravelMinSeconds,
                transitionTravelMaxSeconds
            );
            transitionTravelDuration = ApplyHighSpeedDurationMultiplier(transitionTravelDuration);

            if (transitionAlignDuration <= 0f)
            {
                transitionPhase = TransitionPhase.Travel;
                transitionTimer = 0f;
            }
        }

        private void UpdateTransition(float _dt)
        {
            if (!isTransitioning || mainCamera == null)
            {
                return;
            }

            if (!TryGetModeTarget(transitionMode, out Transform? _target))
            {
                CancelTransition();
                return;
            }

            transitionTargetLookAt = _target.position;
            transitionTargetPosition = GetDesiredCameraPosition(transitionMode, _target);

            Vector3 _lookAt = transitionTargetLookAt;
            if (transitionBlendLookAt)
            {
                float _total = Mathf.Max(0.0001f, transitionAlignDuration + transitionTravelDuration);
                float _elapsed = transitionPhase == TransitionPhase.Align
                    ? transitionTimer
                    : transitionAlignDuration + transitionTimer;
                float _t = Mathf.Clamp01(_elapsed / _total);
                float _ease = SmoothStep01(_t);
                _lookAt = Vector3.Lerp(transitionStartLookAt, transitionTargetLookAt, _ease);
            }

            transitionTimer += _dt;

            if (transitionPhase == TransitionPhase.Align)
            {
                float _duration = Mathf.Max(0.0001f, transitionAlignDuration);
                float _t = Mathf.Clamp01(transitionTimer / _duration);
                float _eased = SmoothStep01(_t);
                float _moveT = _eased * Mathf.Clamp01(transitionAlignMoveFraction);

                Vector3 _position = Vector3.Lerp(transitionStartPosition, transitionTargetPosition, _moveT);
                Quaternion _targetRotation = GetLookRotation(_lookAt, transitionStartPosition);
                Quaternion _rotation = Quaternion.Slerp(transitionStartRotation, _targetRotation, _eased);

                mainCamera.transform.SetPositionAndRotation(_position, _rotation);

                if (_t >= 1f)
                {
                    transitionPhase = TransitionPhase.Travel;
                    transitionTimer = 0f;
                    transitionStartPosition = _position;
                    transitionStartRotation = _rotation;
                }

                return;
            }

            if (transitionPhase == TransitionPhase.Travel)
            {
                float _duration = Mathf.Max(0.0001f, transitionTravelDuration);
                float _t = Mathf.Clamp01(transitionTimer / _duration);
                float _eased = SmoothStep01(_t);

                Vector3 _position = Vector3.Lerp(transitionStartPosition, transitionTargetPosition, _eased);
                Quaternion _targetRotation = GetLookRotation(_lookAt, _position);
                Quaternion _rotation = Quaternion.Slerp(transitionStartRotation, _targetRotation, _eased);

                mainCamera.transform.SetPositionAndRotation(_position, _rotation);

                if (_t >= 1f)
                {
                    EndTransition();
                }
            }
        }

        private void EndTransition()
        {
            if (mainCamera == null)
            {
                CancelTransition();
                return;
            }

            if (!TryGetModeTarget(transitionMode, out Transform? _target))
            {
                CancelTransition();
                return;
            }

            Vector3 _position = GetDesiredCameraPosition(transitionMode, _target);
            Quaternion _rotation = GetLookRotation(_target.position, _position);
            mainCamera.transform.SetPositionAndRotation(_position, _rotation);

            CancelTransition();
        }

        private void CancelTransition()
        {
            isTransitioning = false;
            transitionPhase = TransitionPhase.None;
            transitionTimer = 0f;
        }

        private static float SmoothStep01(float _t)
        {
            return _t * _t * (3f - 2f * _t);
        }

        private float ApplyHighSpeedDurationMultiplier(float _duration)
        {
            if (!IsHighSpeedMode())
            {
                return _duration;
            }

            return Mathf.Max(0f, _duration * HighSpeedTransitionDurationMultiplier);
        }

        private float GetSmoothSeconds(float _seconds)
        {
            float _smooth = Mathf.Max(0f, _seconds);
            if (IsHighSpeedMode())
            {
                _smooth *= HighSpeedSmoothTimeMultiplier;
            }

            return _smooth;
        }

        private bool IsOrbitInputSmoothActive()
        {
            if (orbitInputActiveHoldSeconds <= 0f)
            {
                return false;
            }

            float _elapsed = Time.unscaledTime - lastOrbitInputTime;
            return _elapsed <= orbitInputActiveHoldSeconds;
        }

        private float GetFocusDistanceForCurrentTarget()
        {
            float _minDistance;
            float _maxDistance;
            if (focusSolarObject == null)
            {
                GetProfileZoomRange(SolarObject.CameraFocusProfile.Terrestrial, out _minDistance, out _maxDistance);
                focusZoomNormalized = Mathf.Clamp01(focusZoomNormalized);
                return Mathf.Lerp(_minDistance, _maxDistance, focusZoomNormalized);
            }

            GetFocusZoomRange(focusSolarObject, out _minDistance, out _maxDistance);
            focusZoomNormalized = Mathf.Clamp01(focusZoomNormalized);
            return Mathf.Lerp(_minDistance, _maxDistance, focusZoomNormalized);
        }

        private float GetOverviewDistance()
        {
            GetOverviewZoomRange(out float _minDistance, out float _maxDistance);
            overviewZoomNormalized = Mathf.Clamp01(overviewZoomNormalized);
            return Mathf.Lerp(_minDistance, _maxDistance, overviewZoomNormalized);
        }

        private void ApplyFocusDistance(SolarObject _solarObject)
        {
            GetFocusZoomRange(_solarObject, out float _minDistance, out float _maxDistance);
            float _range = Mathf.Max(0.0001f, _maxDistance - _minDistance);

            focusZoomNormalized = Mathf.Clamp01(focusZoomNormalized);
            float _targetDistance = Mathf.Lerp(_minDistance, _maxDistance, focusZoomNormalized);
            focusZoomNormalized = _range <= 0.0001f ? 0f : (_targetDistance - _minDistance) / _range;
        }

        private void ApplyOverviewDistance()
        {
            GetOverviewZoomRange(out float _minDistance, out float _maxDistance);
            float _range = Mathf.Max(0.0001f, _maxDistance - _minDistance);

            overviewZoomNormalized = Mathf.Clamp01(overviewZoomNormalized);
            float _targetDistance = Mathf.Lerp(_minDistance, _maxDistance, overviewZoomNormalized);
            overviewZoomNormalized = _range <= 0.0001f ? 0f : (_targetDistance - _minDistance) / _range;
        }

        private void GetFocusZoomRange(SolarObject _solarObject, out float _minDistance, out float _maxDistance)
        {
            FocusZoomRange _range = ResolveFocusZoomRange(_solarObject);
            GetBlendRange(_range, out _minDistance, out _maxDistance);
        }

        private void GetOverviewZoomRange(out float _minDistance, out float _maxDistance)
        {
            float _realism = RealismLevel01;
            _minDistance = Mathf.Lerp(overviewZoomMinDistance, realisticOverviewZoomMinDistance, _realism);
            _maxDistance = Mathf.Lerp(overviewZoomMaxDistance, realisticOverviewZoomMaxDistance, _realism);
            _minDistance = Mathf.Max(0f, _minDistance);
            _maxDistance = Mathf.Max(_minDistance, _maxDistance);
        }

        private void SetFocusZoomForSelection()
        {
            focusZoomNormalized = Mathf.Clamp01(focusSelectZoomFraction);
        }

        private void AlignOverviewToCurrentCamera()
        {
            if (mainCamera == null || overviewTarget == null)
            {
                return;
            }

            Vector3 _position = mainCamera.transform.position;
            float _distance = Vector3.Distance(_position, overviewTarget.position);
            SyncOverviewZoomNormalizedFromDistance(_distance);

            SyncOrbitToCameraView(
                overviewTarget,
                GetOverviewOrbitRadius(_distance),
                ref overviewYaw,
                ref overviewPitch,
                ref overviewOrbitInitialized
            );
        }

        private void SyncOverviewZoomNormalizedFromDistance(float _distance)
        {
            GetOverviewZoomRange(out float _minDistance, out float _maxDistance);
            float _range = Mathf.Max(0.0001f, _maxDistance - _minDistance);
            overviewZoomNormalized = _range <= 0.0001f ? 0f : Mathf.Clamp01((_distance - _minDistance) / _range);
        }

        private FocusZoomRange ResolveFocusZoomRange(SolarObject _solarObject)
        {
            SolarObject.CameraFocusProfile _profile = ResolveProfile(_solarObject);
            FocusZoomRange _profileRange = GetProfileRange(_profile);
            return ClampRange(_profileRange);
        }

        private void GetProfileZoomRange(
            SolarObject.CameraFocusProfile _profile,
            out float _minDistance,
            out float _maxDistance
        )
        {
            FocusZoomRange _range = ClampRange(GetProfileRange(_profile));
            GetBlendRange(_range, out _minDistance, out _maxDistance);
        }

        private FocusZoomRange GetProfileRange(SolarObject.CameraFocusProfile _profile)
        {
            switch (_profile)
            {
                case SolarObject.CameraFocusProfile.Moon:
                    return moonZoomRange;
                case SolarObject.CameraFocusProfile.DwarfPlanet:
                    return dwarfZoomRange;
                case SolarObject.CameraFocusProfile.GasGiant:
                    return gasGiantZoomRange;
                case SolarObject.CameraFocusProfile.IceGiant:
                    return iceGiantZoomRange;
                case SolarObject.CameraFocusProfile.Star:
                    return starZoomRange;
                default:
                    return terrestrialZoomRange;
            }
        }

        private SolarObject.CameraFocusProfile ResolveProfile(SolarObject _solarObject)
        {
            SolarObject.CameraFocusProfile _profile = _solarObject.FocusProfile;
            if (_profile == SolarObject.CameraFocusProfile.Auto)
            {
                HelpLogs.Error(
                    "Camera",
                    $"'{_solarObject.name}' uses Auto camera_focus_profile. Set a profile in the JSON."
                );
                return SolarObject.CameraFocusProfile.Terrestrial;
            }

            return _profile;
        }

        private FocusZoomRange ClampRange(FocusZoomRange _range)
        {
            _range.MinSimulation = Mathf.Max(focusZoomAbsoluteMinDistance, _range.MinSimulation);
            _range.MaxSimulation = Mathf.Max(_range.MinSimulation, _range.MaxSimulation);
            _range.MinRealistic = Mathf.Max(focusZoomAbsoluteMinDistance, _range.MinRealistic);
            _range.MaxRealistic = Mathf.Max(_range.MinRealistic, _range.MaxRealistic);
            return _range;
        }

        private void GetBlendRange(FocusZoomRange _range, out float _minDistance, out float _maxDistance)
        {
            float _realism = RealismLevel01;
            _minDistance = Mathf.Lerp(_range.MinSimulation, _range.MinRealistic, _realism);
            _maxDistance = Mathf.Lerp(_range.MaxSimulation, _range.MaxRealistic, _realism);
            _minDistance = Mathf.Max(focusZoomAbsoluteMinDistance, _minDistance);
            _maxDistance = Mathf.Max(_minDistance, _maxDistance);
        }

        private bool IsHighSpeedMode()
        {
            if (simulator == null)
            {
                return false;
            }

            return simulator.TimeScale >= HighSpeedTimeScaleThreshold;
        }

        private float GetOverviewZoomSpeedMultiplier(float _distance)
        {
            float _realism = RealismLevel01;
            float _speed = Mathf.Lerp(overviewZoomSpeedScale, realisticOverviewZoomSpeedScale, _realism);
            float _distanceMultiplier = GetOverviewZoomDistanceMultiplier(_distance, _realism);
            return Mathf.Max(1.0f, _speed * _distanceMultiplier);
        }

        private float GetOverviewZoomDistanceMultiplier(float _distance, float _realism)
        {
            GetOverviewZoomRange(out float _minDistance, out float _maxDistance);
            float _range = Mathf.Max(0.0001f, _maxDistance - _minDistance);
            float _normalized = Mathf.Clamp01((_distance - _minDistance) / _range);
            float _start = Mathf.Clamp01(overviewZoomDistanceSpeedStartFraction);
            if (_normalized <= _start)
            {
                return 1.0f;
            }

            float _t = (_normalized - _start) / Mathf.Max(0.0001f, 1f - _start);
            float _maxMultiplier = Mathf.Lerp(
                overviewZoomDistanceSpeedMaxMultiplier,
                realisticOverviewZoomDistanceSpeedMaxMultiplier,
                _realism
            );
            _maxMultiplier = Mathf.Max(1.0f, _maxMultiplier);
            return Mathf.Lerp(1.0f, _maxMultiplier, SmoothStep01(_t));
        }

        private void EnsureOverviewTarget()
        {
            if (overviewTarget != null)
            {
                return;
            }

            TryAssignOverviewTargetFromSimulator();
        }

        private void EnsureOverviewProxy()
        {
            if (overviewProxy != null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(overviewProxyName))
            {
                return;
            }

            GameObject _proxyObject = GameObject.Find(overviewProxyName);
            if (_proxyObject == null)
            {
                return;
            }

            overviewProxy = _proxyObject.transform;
        }

        private void SyncOverviewProxyFromCamera()
        {
            if (mainCamera == null)
            {
                return;
            }

            EnsureOverviewProxy();
            if (overviewProxy == null)
            {
                return;
            }

            overviewProxy.position = mainCamera.transform.position;
        }

        private void TryAssignOverviewTargetFromSimulator()
        {
            SolarSystemSimulator _simulator = FindFirstObjectByType<SolarSystemSimulator>();
            if (_simulator == null)
            {
                return;
            }

            IReadOnlyList<SolarObject> _objects = _simulator.OrderedSolarObjects;
            for (int _i = 0; _i < _objects.Count; _i++)
            {
                SolarObject _object = _objects[_i];
                if (string.Equals(_object.Id, "sun", StringComparison.OrdinalIgnoreCase))
                {
                    overviewTarget = _object.transform;
                    return;
                }
            }
        }

        private void HandleRealismLevelChanged(float _level)
        {
            ApplyRealismLevel(_level);
        }

        private void ApplyRealismLevel(float _level)
        {
            float _clamped = Mathf.Clamp01(_level);
            if (Mathf.Approximately(_clamped, realismLevel))
            {
                return;
            }

            realismLevel = _clamped;

            if (focusSolarObject != null)
            {
                ApplyFocusDistance(focusSolarObject);
            }

            ApplyOverviewDistance();
        }

        private void ApplyOverviewStartHeightOffset(float _distance)
        {
            if (Mathf.Approximately(overviewStartHeightOffset, 0f))
            {
                return;
            }

            if (_distance <= 0.0001f)
            {
                return;
            }

            float _height = Mathf.Clamp(overviewStartHeightOffset, -_distance, _distance);
            float _pitchRad = Mathf.Asin(_height / Mathf.Max(0.0001f, _distance));
            float _pitchDeg = Mathf.Rad2Deg * _pitchRad;
            overviewPitch = Mathf.Clamp(_pitchDeg, -orbitMaxPitchDegrees, orbitMaxPitchDegrees);
        }
        #endregion
    }
}
