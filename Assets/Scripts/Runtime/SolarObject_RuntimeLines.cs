#nullable enable
using System;
using UnityEngine;

namespace Assets.Scripts.Runtime
{
    public sealed partial class SolarObject
    {
        #region Runtime Lines
        /// <summary>
        /// Update runtime line renderers for orbits and axes.
        /// </summary>
        private void UpdateRuntimeRenderers()
        {
            if (visualContext == null)
            {
                return;
            }

            bool _drawOrbit = showOrbitLinesLocal && visualContext.ShowOrbitLines;
            bool _drawSpinAxis = showSpinAxisLinesLocal && visualContext.ShowSpinAxisLines;
            bool _drawWorldUp = showWorldUpLinesLocal && visualContext.ShowWorldUpLines;
            bool _drawSpinDirection = showSpinDirectionLinesLocal && visualContext.ShowSpinDirectionLines;

            if (!_drawOrbit && !_drawSpinAxis && !_drawWorldUp && !_drawSpinDirection)
            {
                if (orbitLine != null)
                {
                    orbitLine.enabled = false;
                }

                DeactivateAxisLines();

                if (spinDirectionLine != null)
                {
                    spinDirectionLine.enabled = false;
                }

                return;
            }

            if (ShouldUpdateLineDistanceScale())
            {
                bool _axisScaleChanged = UpdateAxisLineDistanceScale();
                bool _orbitScaleChanged = UpdateOrbitLineDistanceScale();
                if (_axisScaleChanged || _orbitScaleChanged)
                {
                    lineStylesDirty = true;
                }
            }

            if (_drawOrbit)
            {
                UpdateOrbitLine();
            }
            else if (orbitLine != null)
            {
                orbitLine.enabled = false;
            }

            if (_drawSpinAxis || _drawWorldUp)
            {
                UpdateAxisLines(_drawSpinAxis, _drawWorldUp);
            }
            else
            {
                DeactivateAxisLines();
            }

            UpdateSpinDirectionLine(_drawSpinDirection);

            if (lineStylesDirty && ApplyRuntimeLineStyles())
            {
                lineStylesDirty = false;
            }
        }

        /// <summary>
        /// Decide whether line distance scaling should update this frame.
        /// </summary>
        private bool ShouldUpdateLineDistanceScale()
        {
            bool _intervalEnabled = lineScaleUpdateIntervalSeconds > 0f;
            bool _thresholdEnabled = lineScaleDistanceThreshold > 0f;

            if (!_intervalEnabled && !_thresholdEnabled)
            {
                return true;
            }

            if (_intervalEnabled)
            {
                lineScaleUpdateTimer += Time.deltaTime;
                if (lineScaleUpdateTimer < lineScaleUpdateIntervalSeconds && !lineStylesDirty)
                {
                    return false;
                }
            }

            if (_thresholdEnabled)
            {
                Camera? _camera = GetLineScaleCamera();
                if (_camera != null)
                {
                    float _distance = Vector3.Distance(_camera.transform.position, transform.position);
                    float _threshold = Mathf.Max(0.0f, lineScaleDistanceThreshold);
                    if (lastLineScaleCameraDistance >= 0.0f &&
                        Mathf.Abs(_distance - lastLineScaleCameraDistance) < _threshold &&
                        !lineStylesDirty)
                    {
                        return false;
                    }

                    lastLineScaleCameraDistance = _distance;
                }
            }

            lineScaleUpdateTimer = 0f;
            return true;
        }

        /// <summary>
        /// Build or update the orbit line renderer.
        /// </summary>
        private void UpdateOrbitLine()
        {
            if (!hasOrbit)
            {
                if (orbitLine != null)
                {
                    orbitLine.enabled = false;
                }

                return;
            }

            EnsureRuntimeRenderers();
            if (orbitLine == null || visualContext == null)
            {
                return;
            }

            ApplyOrbitColor();

            int _segments = Math.Max(64, visualContext.OrbitLineSegments);

            bool _rebuild = orbitPoints == null || orbitPoints.Length != _segments || orbitPointsDirty;
            if (_rebuild)
            {
                orbitPoints = new Vector3[_segments];
                orbitWorldPoints = new Vector3[_segments];
                orbitPointsDirty = false;

                for (int _i = 0; _i < _segments; _i++)
                {
                    double _t = (double)_i / _segments;
                    orbitPoints[_i] = ComputeOrbitOffsetUnity(_t * orbitalPeriodSeconds);
                }
            }

            if (orbitPoints == null)
            {
                return;
            }

            if (orbitWorldPoints == null || orbitWorldPoints.Length != orbitPoints.Length)
            {
                orbitWorldPoints = new Vector3[orbitPoints.Length];
            }

            Vector3 _primaryPosition = primaryTransform != null ? primaryTransform.position : Vector3.zero;
            bool _primaryMoved = !hasPrimaryPosition || _primaryPosition != lastPrimaryPosition;

            if (_rebuild || _primaryMoved)
            {
                for (int _i = 0; _i < orbitPoints.Length; _i++)
                {
                    orbitWorldPoints[_i] = _primaryPosition + orbitPoints[_i];
                }

                orbitLine.positionCount = orbitWorldPoints.Length;
                orbitLine.loop = true;
                orbitLine.SetPositions(orbitWorldPoints);

                lastPrimaryPosition = _primaryPosition;
                hasPrimaryPosition = true;
            }

            orbitLine.enabled = true;
        }

        /// <summary>
        /// Apply orbit line color based on object type.
        /// </summary>
        private void ApplyOrbitColor()
        {
            if (orbitLine == null)
            {
                return;
            }

            Color _color = orbitLineColor;
            if (isHypothetical)
            {
                _color = hypotheticalOrbitLineColor;
            }
            else if (string.Equals(type, "moon", StringComparison.OrdinalIgnoreCase))
            {
                _color = moonOrbitLineColor;
            }
            else if (string.Equals(type, "dwarf_planet", StringComparison.OrdinalIgnoreCase))
            {
                _color = dwarfOrbitLineColor;
            }

            _color.a = GetOrbitLineAlpha();

            if (orbitLine.startColor != _color || orbitLine.endColor != _color)
            {
                orbitLine.startColor = _color;
                orbitLine.endColor = _color;
            }
        }

        /// <summary>
        /// Update axis and world-up lines.
        /// </summary>
        private void UpdateAxisLines(bool _drawSpinAxis, bool _drawWorldUp)
        {
            EnsureRuntimeRenderers();
            if (axisLine == null || worldUpLine == null)
            {
                return;
            }

            Vector3 _p = transform.position;
            Vector3 _axis = transform.rotation * Vector3.up;
            float _len = GetAxisLineLength();

            if (_drawSpinAxis)
            {
                axisLine.positionCount = 2;
                axisLine.SetPosition(0, _p - _axis * _len);
                axisLine.SetPosition(1, _p + _axis * _len);
                axisLine.enabled = true;
            }
            else
            {
                axisLine.enabled = false;
            }

            if (_drawWorldUp)
            {
                worldUpLine.positionCount = 2;
                worldUpLine.SetPosition(0, _p - Vector3.up * _len);
                worldUpLine.SetPosition(1, _p + Vector3.up * _len);
                worldUpLine.enabled = true;
            }
            else
            {
                worldUpLine.enabled = false;
            }
        }

        /// <summary>
        /// Update the curved spin-direction arc line.
        /// </summary>
        private void UpdateSpinDirectionLine(bool _drawSpinDirection)
        {
            if (!_drawSpinDirection || !hasSpin)
            {
                if (spinDirectionLine != null)
                {
                    spinDirectionLine.enabled = false;
                }

                return;
            }

            EnsureRuntimeRenderers();
            if (spinDirectionLine == null)
            {
                return;
            }

            Vector3 _axis = transform.rotation * Vector3.up;
            if (_axis.sqrMagnitude <= 1e-8f)
            {
                spinDirectionLine.enabled = false;
                return;
            }

            _axis.Normalize();
            bool _axisFlipped = false;
            if (Vector3.Dot(_axis, Vector3.up) < 0.0f)
            {
                _axis = -_axis;
                _axisFlipped = true;
            }

            Vector3 _reference = Mathf.Abs(Vector3.Dot(_axis, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
            Vector3 _right = Vector3.Cross(_axis, _reference).normalized;
            Vector3 _forward = Vector3.Cross(_axis, _right).normalized;

            float _radius = GetSpinDirectionArcRadius();
            if (_radius <= 1e-6f)
            {
                spinDirectionLine.enabled = false;
                return;
            }

            float _angleDeg = Mathf.Clamp(spinDirectionArcAngleDeg, 30.0f, 330.0f);
            float _angleRad = _angleDeg * Mathf.Deg2Rad;
            int _segments = Mathf.Max(6, spinDirectionArcSegments);

            float _start = -_angleRad * 0.5f;
            float _end = _angleRad * 0.5f;
            float _directionSign = GetSpinDirectionSign();
            if (_axisFlipped)
            {
                _directionSign *= -1.0f;
            }
            if (_directionSign < 0.0f)
            {
                float _temp = _start;
                _start = _end;
                _end = _temp;
            }

            int _arcCount = _segments + 1;
            int _count = _arcCount + 3;
            spinDirectionLine.positionCount = _count;

            Vector3 _center = transform.position;
            for (int _i = 0; _i < _arcCount; _i++)
            {
                float _t = _segments == 0 ? 0.0f : (float)_i / _segments;
                float _angle = Mathf.Lerp(_start, _end, _t);
                Vector3 _offset = (_right * Mathf.Cos(_angle) + _forward * Mathf.Sin(_angle)) * _radius;
                spinDirectionLine.SetPosition(_i, _center + _offset);
            }

            Vector3 _endOffset = (_right * Mathf.Cos(_end) + _forward * Mathf.Sin(_end)) * _radius;
            Vector3 _endPoint = _center + _endOffset;
            float _direction = _end >= _start ? 1.0f : -1.0f;
            Vector3 _tangent = (-Mathf.Sin(_end) * _right + Mathf.Cos(_end) * _forward) * _direction;
            Vector3 _arrowDir = -_tangent.normalized;

            float _arrowLen = Mathf.Max(0.001f, _radius * Mathf.Clamp(spinDirectionArrowHeadLengthMultiplier, 0.05f, 0.5f));
            float _arrowAngle = Mathf.Clamp(spinDirectionArrowHeadAngleDeg, 5.0f, 60.0f);
            Vector3 _arrowLeft = Quaternion.AngleAxis(_arrowAngle, _axis) * _arrowDir;
            Vector3 _arrowRight = Quaternion.AngleAxis(-_arrowAngle, _axis) * _arrowDir;

            spinDirectionLine.SetPosition(_arcCount, _endPoint + _arrowLeft * _arrowLen);
            spinDirectionLine.SetPosition(_arcCount + 1, _endPoint);
            spinDirectionLine.SetPosition(_arcCount + 2, _endPoint + _arrowRight * _arrowLen);

            Color _color = spinDirection >= 0.0f ? spinDirectionProgradeColor : spinDirectionRetrogradeColor;
            _color.a = GetAxisLineAlpha();
            if (spinDirectionLine.startColor != _color || spinDirectionLine.endColor != _color)
            {
                spinDirectionLine.startColor = _color;
                spinDirectionLine.endColor = _color;
            }

            spinDirectionLine.enabled = true;
        }

        /// <summary>
        /// Compute axis line length from scale and solar object type.
        /// </summary>
        private float GetAxisLineLength()
        {
            float _baseLen = transform.localScale.x * 0.5f;
            float _typeScale = GetAxisLineTypeScale();
            float _sizeScale = GetAxisLineSizeScale();
            float _smallBodyScale = GetAxisLineSmallBodyLengthScale();
            float _length = _baseLen * _typeScale * _sizeScale * _smallBodyScale * axisLineDistanceScale;
            float _lengthMultiplier = Mathf.Clamp(axisLineLengthScale, 0.1f, 2.0f);
            _length *= _lengthMultiplier;

            float _typeLengthMultiplier = 1.0f;
            if (visualContext != null)
            {
                bool _isStar = string.Equals(type, "star", StringComparison.OrdinalIgnoreCase);
                float _starMultiplier = Mathf.Clamp(visualContext.AxisLineStarLengthMultiplier, 0.1f, 5.0f);
                float _nonStarMultiplier = Mathf.Clamp(visualContext.AxisLineNonStarLengthMultiplier, 0.1f, 5.0f);
                _typeLengthMultiplier = _isStar ? _starMultiplier : _nonStarMultiplier;
            }

            _length *= _typeLengthMultiplier;

            if (string.Equals(type, "star", StringComparison.OrdinalIgnoreCase))
            {
                float _cap = Mathf.Clamp(axisLineStarLengthMaxScale, 0.1f, 3.0f);
                float _minLen = _baseLen * 1.1f * _typeLengthMultiplier;
                float _maxLen = _baseLen * _cap * _typeLengthMultiplier;
                if (_maxLen < _minLen)
                {
                    _maxLen = _minLen;
                }

                _length = Mathf.Clamp(_length, _minLen, _maxLen);
            }

            return Mathf.Max(0.1f, _length);
        }

        /// <summary>
        /// Compute the radius for the spin-direction arc.
        /// </summary>
        private float GetSpinDirectionArcRadius()
        {
            float _baseLen = transform.localScale.x * 0.5f;
            float _sizeScale = GetAxisLineSizeScale();
            float _distanceScale = Mathf.Clamp(axisLineDistanceScale, 1.0f, 3.0f);
            float _multiplier = Mathf.Clamp(spinDirectionArcRadiusMultiplier, 0.1f, 5.0f);
            float _radius = _baseLen * _sizeScale * _distanceScale * _multiplier;
            float _minRadius = _baseLen * Mathf.Max(1.05f, _multiplier);
            return Mathf.Max(_radius, _minRadius);
        }

        /// <summary>
        /// Resolve the spin-direction sign used for the arc.
        /// </summary>
        private float GetSpinDirectionSign()
        {
            float _direction = GetEffectiveSpinDirection();
            return _direction >= 0.0f ? -1.0f : 1.0f;
        }

        /// <summary>
        /// Resolve the effective spin direction sign for the current axial tilt.
        /// </summary>
        private float GetEffectiveSpinDirection()
        {
            float _direction = spinDirection;
            if (axialTiltDeg > 90.0f)
            {
                _direction *= -1.0f;
            }

            return _direction;
        }

        /// <summary>
        /// Per-type length scaling to keep lines readable across object classes.
        /// </summary>
        private float GetAxisLineTypeScale()
        {
            if (string.Equals(type, "moon", StringComparison.OrdinalIgnoreCase))
            {
                return 0.6f;
            }

            if (string.Equals(type, "dwarf_planet", StringComparison.OrdinalIgnoreCase))
            {
                return 0.85f;
            }

            if (string.Equals(type, "star", StringComparison.OrdinalIgnoreCase))
            {
                return 1.6f;
            }

            return 1.0f;
        }

        /// <summary>
        /// Deactivate axis lines when not in use.
        /// </summary>
        private void DeactivateAxisLines()
        {
            if (axisLine != null)
            {
                axisLine.enabled = false;
            }

            if (worldUpLine != null)
            {
                worldUpLine.enabled = false;
            }
        }

        /// <summary>
        /// Create line renderers on demand.
        /// </summary>
        private void EnsureRuntimeRenderers()
        {
            bool _drawOrbit = showOrbitLinesLocal && (visualContext?.ShowOrbitLines ?? true);
            bool _drawAxis = showSpinAxisLinesLocal && (visualContext?.ShowSpinAxisLines ?? true);
            bool _drawWorldUp = showWorldUpLinesLocal && (visualContext?.ShowWorldUpLines ?? true);
            bool _drawSpinDirection = showSpinDirectionLinesLocal && (visualContext?.ShowSpinDirectionLines ?? true);

            if (orbitLine == null && _drawOrbit)
            {
                orbitLine = CreateLineRenderer("OrbitLine", orbitLineColor, orbitLineWidth, true);
            }

            if ((axisLine == null && _drawAxis) || (worldUpLine == null && _drawWorldUp))
            {
                axisLine = CreateLineRenderer("AxisLine", axisLineColor, axisLineWidth, false);
                worldUpLine = CreateLineRenderer("WorldUpLine", worldUpLineColor, axisLineWidth, false);
            }

            if (spinDirectionLine == null && _drawSpinDirection)
            {
                spinDirectionLine = CreateLineRenderer(
                    "SpinDirectionLine",
                    spinDirectionProgradeColor,
                    spinDirectionLineWidth,
                    false
                );
            }
        }

        /// <summary>
        /// Scale line widths based on global distance/radius settings.
        /// </summary>
        private bool ApplyRuntimeLineStyles()
        {
            float _scale = GetLineWidthScale();
            bool _applied = false;

            if (orbitLine != null)
            {
                float _width = Mathf.Max(0.0001f, orbitLineWidth * _scale * orbitLineDistanceScale);
                orbitLine.startWidth = _width;
                orbitLine.endWidth = _width;
                _applied = true;
            }

            if (axisLine != null)
            {
                float _widthScale = GetAxisLineWidthScale();
                float _localThickness = Mathf.Clamp(axisLineThicknessScale, 0.1f, 5.0f);
                float _globalThickness = visualContext != null
                    ? Mathf.Clamp(visualContext.AxisLineThicknessMultiplier, 0.1f, 5.0f)
                    : 1.0f;
                float _thicknessMultiplier = Mathf.Clamp(_localThickness * _globalThickness, 0.1f, 5.0f);
                float _width = Mathf.Max(
                    0.0001f,
                    axisLineWidth * _scale * 0.5f * _widthScale * _thicknessMultiplier
                );
                axisLine.startWidth = _width;
                axisLine.endWidth = _width;
                _applied = true;
            }

            if (worldUpLine != null)
            {
                float _widthScale = GetAxisLineWidthScale();
                float _localThickness = Mathf.Clamp(axisLineThicknessScale, 0.1f, 5.0f);
                float _globalThickness = visualContext != null
                    ? Mathf.Clamp(visualContext.AxisLineThicknessMultiplier, 0.1f, 5.0f)
                    : 1.0f;
                float _thicknessMultiplier = Mathf.Clamp(_localThickness * _globalThickness, 0.1f, 5.0f);
                float _width = Mathf.Max(
                    0.0001f,
                    axisLineWidth * _scale * 0.5f * _widthScale * _thicknessMultiplier
                );
                worldUpLine.startWidth = _width;
                worldUpLine.endWidth = _width;
                _applied = true;
            }

            if (spinDirectionLine != null)
            {
                float _widthScale = Mathf.Max(0.8f, GetAxisLineWidthScale());
                float _width = Mathf.Max(0.0001f, spinDirectionLineWidth * _scale * _widthScale);
                spinDirectionLine.startWidth = _width;
                spinDirectionLine.endWidth = _width;
                _applied = true;
            }

            if (ApplyAxisLineColors())
            {
                _applied = true;
            }

            return _applied;
        }

        /// <summary>
        /// Apply axis and world-up line colors with distance-based alpha.
        /// </summary>
        private bool ApplyAxisLineColors()
        {
            bool _updated = false;
            float _alpha = GetAxisLineAlpha();

            if (axisLine != null)
            {
                Color _color = axisLineColor;
                _color.a = _alpha;
                if (axisLine.startColor != _color || axisLine.endColor != _color)
                {
                    axisLine.startColor = _color;
                    axisLine.endColor = _color;
                    _updated = true;
                }
            }

            if (worldUpLine != null)
            {
                Color _color = worldUpLineColor;
                _color.a = _alpha;
                if (worldUpLine.startColor != _color || worldUpLine.endColor != _color)
                {
                    worldUpLine.startColor = _color;
                    worldUpLine.endColor = _color;
                    _updated = true;
                }
            }

            return _updated;
        }

        /// <summary>
        /// Compute a width scale from global distance/radius multipliers.
        /// </summary>
        private float GetLineWidthScale()
        {
            if (visualContext == null)
            {
                return 1.0f;
            }

            float _distance = (float)visualContext.GlobalDistanceScale;
            float _radius = (float)visualContext.GlobalRadiusScale;
            float _avg = (_distance + _radius) * 0.5f;

            float _base = Mathf.Clamp(_avg, 0.2f, 2.0f);
            return _base * Mathf.Clamp(visualContext.RuntimeLineWidthScale, 0.1f, 2.0f);
        }

        /// <summary>
        /// Compute a distance-based alpha between the configured near/far values.
        /// </summary>
        private float GetLineAlphaForScale(float _scale, float _minScale, float _maxScale)
        {
            return GetLineAlphaForScale(_scale, _minScale, _maxScale, lineAlphaNear, lineAlphaFar);
        }

        private float GetLineAlphaForScale(
            float _scale,
            float _minScale,
            float _maxScale,
            float _minAlphaValue,
            float _maxAlphaValue
        )
        {
            float _minAlpha = Mathf.Clamp01(_minAlphaValue / 255.0f);
            float _maxAlpha = Mathf.Clamp01(_maxAlphaValue / 255.0f);
            if (_maxAlpha < _minAlpha)
            {
                float _temp = _minAlpha;
                _minAlpha = _maxAlpha;
                _maxAlpha = _temp;
            }

            float _min = Mathf.Min(_minScale, _maxScale);
            float _max = Mathf.Max(_minScale, _maxScale);
            if (Mathf.Approximately(_min, _max))
            {
                return _maxAlpha;
            }

            float _t = Mathf.InverseLerp(_min, _max, _scale);
            return Mathf.Lerp(_minAlpha, _maxAlpha, _t);
        }

        /// <summary>
        /// Resolve axis/world-up alpha based on camera distance.
        /// </summary>
        private float GetAxisLineAlpha()
        {
            return GetLineAlphaForScale(axisLineDistanceScale, axisLineDistanceMinScale, axisLineDistanceMaxScale);
        }

        /// <summary>
        /// Resolve orbit alpha based on camera distance.
        /// </summary>
        private float GetOrbitLineAlpha()
        {
            float _maxScale = orbitLineDistanceMaxScale * Mathf.Max(0.1f, orbitLineDistanceMaxScaleBoost);
            if (isHypothetical)
            {
                _maxScale = Mathf.Max(_maxScale, hypotheticalOrbitFarScale);
            }

            float _minAlpha = lineAlphaNear;
            float _maxAlpha = lineAlphaFar;
            if (visualContext != null && IsFocusedObject())
            {
                float _threshold = Mathf.Max(0.01f, visualContext.FocusedOrbitLineNearScaleThreshold);
                if (orbitLineDistanceScale <= _threshold)
                {
                    _minAlpha = Mathf.Min(_minAlpha, visualContext.FocusedOrbitLineNearAlpha);
                }
            }

            return GetLineAlphaForScale(
                orbitLineDistanceScale,
                orbitLineDistanceMinScale,
                _maxScale,
                _minAlpha,
                _maxAlpha
            );
        }

        private bool IsFocusedObject()
        {
            if (visualContext == null)
            {
                return false;
            }

            string _focusedId = visualContext.FocusedSolarObjectId;
            if (string.IsNullOrWhiteSpace(_focusedId))
            {
                return false;
            }

            return string.Equals(_focusedId, id, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Compute the axis/world-up line width scale.
        /// </summary>
        private float GetAxisLineWidthScale()
        {
            float _scale = axisLineDistanceScale;
            float _sizeScale = GetAxisLineSizeWidthScale();
            _scale *= _sizeScale;

            if (string.Equals(type, "star", StringComparison.OrdinalIgnoreCase) &&
                visualContext != null &&
                visualContext.RuntimeLineWidthScale < 1.0f)
            {
                _scale *= 2.0f;
            }

            return _scale;
        }

        /// <summary>
        /// Compute axis line length scale based on object size.
        /// </summary>
        private float GetAxisLineSizeScale()
        {
            if (visualContext != null && visualContext.RuntimeLineWidthScale < 1.0f)
            {
                return 1.0f;
            }

            float _reference = Mathf.Max(0.001f, axisLineSizeScaleReference);
            float _scale = transform.localScale.x / _reference;
            _scale = Mathf.Clamp(_scale, axisLineSizeMinScale, axisLineSizeMaxScale);

            if (string.Equals(type, "star", StringComparison.OrdinalIgnoreCase))
            {
                _scale = Mathf.Min(_scale, axisLineSizeStarMaxScale);
            }

            return _scale;
        }

        /// <summary>
        /// Clamp axis line width scaling to avoid overly thick lines.
        /// </summary>
        private float GetAxisLineSizeWidthScale()
        {
            float _scale = GetAxisLineSizeScale();
            return Mathf.Min(_scale, axisLineSizeMaxWidthScale);
        }

        /// <summary>
        /// Apply extra axis line length for small planets if enabled.
        /// </summary>
        private float GetAxisLineSmallBodyLengthScale()
        {
            if (visualContext != null && visualContext.RuntimeLineWidthScale < 1.0f)
            {
                return 1.0f;
            }

            if (!string.Equals(type, "planet", StringComparison.OrdinalIgnoreCase))
            {
                return 1.0f;
            }

            float _threshold = Mathf.Max(0.01f, axisLineSmallBodyDiameterThresholdUnity);
            if (BaseDiameterUnity >= _threshold)
            {
                return 1.0f;
            }

            return axisLineSmallBodyLengthScaleMultiplier;
        }

        /// <summary>
        /// Update axis line scaling based on camera distance.
        /// </summary>
        private bool UpdateAxisLineDistanceScale()
        {
            float _scale = 1.0f;

            if (scaleAxisLinesByCameraDistance)
            {
                Camera? _camera = GetLineScaleCamera();
                if (_camera != null)
                {
                    float _reference = Mathf.Max(0.01f, axisLineDistanceScaleReference);
                    float _distance = Vector3.Distance(_camera.transform.position, transform.position);
                    float _raw = Mathf.Max(1e-4f, _distance / _reference);
                    float _exponent = Mathf.Max(0.1f, axisLineDistanceExponent);
                    _scale = Mathf.Pow(_raw, _exponent);
                    _scale = Mathf.Clamp(_scale, axisLineDistanceMinScale, axisLineDistanceMaxScale);
                }
            }

            if (Mathf.Approximately(axisLineDistanceScale, _scale))
            {
                return false;
            }

            axisLineDistanceScale = _scale;
            return true;
        }

        /// <summary>
        /// Update orbit line scaling based on camera distance.
        /// </summary>
        private bool UpdateOrbitLineDistanceScale()
        {
            float _scale = 1.0f;

            if (scaleOrbitLinesByCameraDistance)
            {
                Camera? _camera = GetLineScaleCamera();
                if (_camera != null)
                {
                    float _reference = Mathf.Max(0.01f, orbitLineDistanceScaleReference);
                    float _distance = Vector3.Distance(_camera.transform.position, transform.position);
                    float _boost = Mathf.Max(0.1f, orbitLineDistanceScaleBoost);
                    float _raw = Mathf.Max(1e-4f, (_distance / _reference) * _boost);
                    float _nearExponentMultiplier = Mathf.Max(0.1f, orbitLineNearExponentMultiplier);
                    float _exponent = _raw <= 1.0f
                        ? Mathf.Max(0.1f, orbitLineNearDistanceExponent * _nearExponentMultiplier)
                        : Mathf.Max(0.1f, orbitLineDistanceExponent);
                    _scale = Mathf.Pow(_raw, _exponent);

                    float _maxScaleMultiplier = Mathf.Max(0.1f, orbitLineDistanceMaxScaleBoost);
                    float _maxScale = orbitLineDistanceMaxScale * _maxScaleMultiplier;
                    if (isHypothetical)
                    {
                        float _focusDistance = Mathf.Max(0.01f, hypotheticalOrbitFocusDistance);
                        if (_distance > _focusDistance)
                        {
                            float _farScale = Mathf.Max(_maxScale, hypotheticalOrbitFarScale);
                            _scale = _farScale;
                            _maxScale = _farScale;
                        }
                        else
                        {
                            float _mult = Mathf.Max(1.0f, hypotheticalOrbitFocusScale);
                            _scale *= _mult;
                        }
                    }

                    _scale = Mathf.Clamp(_scale, orbitLineDistanceMinScale, _maxScale);
                }
            }

            if (Mathf.Approximately(orbitLineDistanceScale, _scale))
            {
                return false;
            }

            orbitLineDistanceScale = _scale;
            return true;
        }

        /// <summary>
        /// Resolve or build a shared line material.
        /// </summary>
        private static Material GetLineMaterial()
        {
            if (lineMaterial != null)
            {
                return lineMaterial;
            }

            Shader _shader = Shader.Find("Sprites/Default");
            if (_shader == null)
            {
                _shader = Shader.Find("Unlit/Color");
            }

            lineMaterial = new Material(_shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            return lineMaterial;
        }

        /// <summary>
        /// Create a new LineRenderer child with standard settings.
        /// </summary>
        private LineRenderer CreateLineRenderer(string _name, Color _color, float _width, bool _loop)
        {
            GameObject _go = new GameObject(_name);
            _go.transform.SetParent(transform, false);

            LineRenderer _lr = _go.AddComponent<LineRenderer>();
            _lr.useWorldSpace = true;
            _lr.material = GetLineMaterial();
            _lr.startColor = _color;
            _lr.endColor = _color;
            _lr.startWidth = _width;
            _lr.endWidth = _width;
            _lr.loop = _loop;
            _lr.positionCount = 0;
            _lr.enabled = false;

            return _lr;
        }
        #endregion
    }
}
