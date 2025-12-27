#nullable enable
using System;
using UnityEngine;
using Assets.Scripts.Data;

namespace Assets.Scripts.Runtime
{
    /// <summary>
    /// Runtime behavior for a single solar object (orbit, spin, and line visuals).
    /// </summary>
    public sealed class SolarObject : MonoBehaviour
    {
        #region Types
        [Serializable]
        public sealed class VisualContext
        {
            // Reference solar object and global scale settings.
            public double ReferenceSolarObjectRadiusKm = 695700.0; // Sun radius km
            public float ReferenceSolarObjectDiameterUnity = 1.0f; // Sun diameter in Unity units (from actual sun instance)

            public double DistanceKmPerUnityUnit = 1_000_000.0;
            public double GlobalDistanceMultiplier = 1.0;
            public double GlobalRadiusMultiplier = 1.0;

            public int OrbitPathSegments = 256;
            public float MoonClearanceUnity = 0.02f;

            // Global runtime line toggles and scale.
            public bool ShowOrbitLines = true;
            public bool ShowSpinAxisLines = true;
            public bool ShowWorldUpLines = true;
            public float RuntimeLineWidthScale = 1.0f;
        }
        #endregion

        #region Serialized Fields
        [Header("Runtime Lines")]
        [SerializeField] private bool drawOrbitRuntime = true;
        [SerializeField] private bool drawAxisRuntime = true;
        [SerializeField] private bool drawWorldUpRuntime = true;
        [SerializeField] private float orbitLineWidth = 0.04f;
        [SerializeField] private float axisLineWidth = 0.02f;
        [SerializeField] private Color orbitLineColor = new Color(0.2f, 0.7f, 1.0f, 0.9f);
        [SerializeField] private Color moonOrbitLineColor = new Color(0.3f, 0.9f, 0.5f, 0.9f);
        [SerializeField] private Color dwarfOrbitLineColor = new Color(0.9f, 0.5f, 0.9f, 0.9f);
        [SerializeField] private Color axisLineColor = new Color(1.0f, 0.8f, 0.2f, 0.9f);
        [SerializeField] private Color worldUpLineColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        #endregion

        #region Runtime State
        // Identity and role.
        private string id = string.Empty;
        private bool isReference = false;
        private string type = string.Empty;

        // Cached references.
        private Transform? primaryTransform;
        private VisualContext? visualContext;

        // Physical and visual scale.
        private double radiusKm = 1.0;
        private double userRadiusMultiplier = 1.0;
        private double userDistanceMultiplier = 1.0;
        private float solarObjectDiameterUnity = 0.1f;

        // Spin state.
        private bool hasSpin = false;
        private double rotationPeriodSeconds = 0.0;
        private float axialTiltDeg = 0.0f;

        // Orbit state.
        private bool hasOrbit = false;
        private string orbitModel = "circular";
        private double orbitalPeriodSeconds = 0.0;
        private double semiMajorAxisKm = 0.0;
        private double initialPhaseRad = 0.0;

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
        private Vector3 lastPrimaryPosition;
        private bool hasPrimaryPosition = false;

        private static Material? lineMaterial;
        private bool lineStylesDirty = true;
        #endregion

        #region Public API
        /// <summary>
        /// Initialize from dataset and shared visual context.
        /// </summary>
        public void Initialize(SolarObjectData _data, Transform? _primaryTransform, VisualContext _visualContext)
        {
            primaryTransform = _primaryTransform;
            visualContext = _visualContext;
            lineStylesDirty = true;

            id = _data.Id;
            isReference = _data.IsReference;
            type = _data.Type ?? string.Empty;

            name = string.IsNullOrWhiteSpace(_data.DisplayName) ? _data.Id : _data.DisplayName;

            userRadiusMultiplier = _data.VisualDefaults?.RadiusMultiplier ?? 1.0;
            userDistanceMultiplier = _data.VisualDefaults?.DistanceMultiplier ?? 1.0;

            if (isReference)
            {
                ApplyReferenceSpawn(_data);
                CacheSpin(_data);
                hasOrbit = false;
                orbitPointsDirty = true;
                return;
            }

            CacheRadius(_data);
            CacheSpin(_data);
            CacheOrbit(_data);

            ApplyScaleFromContext();
            CacheMoonOverlapGuard();

            Vector3 _primaryPosition = _primaryTransform != null ? _primaryTransform.position : Vector3.zero;
            transform.position = _primaryPosition + ComputeOrbitOffsetUnity(0.0);

            orbitPointsDirty = true;
        }

        /// <summary>
        /// Advance orbit/spin based on simulation time.
        /// </summary>
        public void Simulate(double _simulationTimeSeconds)
        {
            if (visualContext == null)
            {
                return;
            }

            if (hasOrbit)
            {
                Vector3 _primaryPosition = primaryTransform != null ? primaryTransform.position : Vector3.zero;
                transform.position = _primaryPosition + ComputeOrbitOffsetUnity(_simulationTimeSeconds);
            }

            if (hasSpin)
            {
                ApplySpin(_simulationTimeSeconds);
            }

            UpdateRuntimeRenderers();
        }

        /// <summary>
        /// Re-apply visual scaling after global changes.
        /// </summary>
        public void RefreshVisuals(VisualContext _visualContext)
        {
            visualContext = _visualContext;
            lineStylesDirty = true;

            if (!isReference)
            {
                ApplyScaleFromContext();
                CacheMoonOverlapGuard();
            }

            orbitPointsDirty = true;
        }

        /// <summary>
        /// Mark runtime line widths as needing a refresh.
        /// </summary>
        public void MarkLineStylesDirty()
        {
            lineStylesDirty = true;
        }
        #endregion

        #region Initialization and Caching
        /// <summary>
        /// Apply explicit spawn position/scale for the reference object.
        /// </summary>
        private void ApplyReferenceSpawn(SolarObjectData _data)
        {
            if (_data.Spawn?.PositionUnity is double[] _pos && _pos.Length == 3)
            {
                transform.position = new Vector3((float)_pos[0], (float)_pos[1], (float)_pos[2]);
            }
            else
            {
                transform.position = Vector3.zero;
            }

            if (_data.Spawn?.ScaleUnity is double[] _scale && _scale.Length == 3)
            {
                transform.localScale = new Vector3((float)_scale[0], (float)_scale[1], (float)_scale[2]);
            }
            else
            {
                transform.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// Cache the physical radius for scaling.
        /// </summary>
        private void CacheRadius(SolarObjectData _data)
        {
            radiusKm = _data.TruthPhysical?.MeanRadiusKm ?? 1.0;
        }

        /// <summary>
        /// Apply scaling based on the shared visual context.
        /// </summary>
        private void ApplyScaleFromContext()
        {
            if (visualContext == null)
            {
                return;
            }

            double _radiusRatio = radiusKm / Math.Max(1e-9, visualContext.ReferenceSolarObjectRadiusKm);

            // Unity scale represents diameter.
            double _diameterUnity =
                visualContext.ReferenceSolarObjectDiameterUnity *
                _radiusRatio *
                visualContext.GlobalRadiusMultiplier *
                userRadiusMultiplier;

            solarObjectDiameterUnity = (float)Math.Max(1e-6, _diameterUnity);
            transform.localScale = Vector3.one * solarObjectDiameterUnity;
        }

        /// <summary>
        /// Cache spin period and axial tilt.
        /// </summary>
        private void CacheSpin(SolarObjectData _data)
        {
            double? _periodDays = _data.TruthSpin?.SiderealRotationPeriodDays;
            double? _periodHours = _data.TruthSpin?.SiderealRotationPeriodHours;

            if (_periodHours.HasValue)
            {
                rotationPeriodSeconds = Math.Abs(_periodHours.Value) * 3600.0;
            }
            else if (_periodDays.HasValue)
            {
                rotationPeriodSeconds = Math.Abs(_periodDays.Value) * 86400.0;
            }
            else
            {
                rotationPeriodSeconds = 0.0;
            }

            hasSpin = rotationPeriodSeconds > 0.0;
            axialTiltDeg = (float)(_data.TruthSpin?.AxialTiltDeg ?? 0.0);
        }

        /// <summary>
        /// Apply spin rotation for the current simulation time.
        /// </summary>
        private void ApplySpin(double _simulationTimeSeconds)
        {
            double _spinCycles = _simulationTimeSeconds / Math.Max(1e-9, rotationPeriodSeconds);
            float _spinDeg = (float)((_spinCycles - Math.Floor(_spinCycles)) * 360.0);

            Quaternion _tiltRotation = Quaternion.AngleAxis(axialTiltDeg, Vector3.right);
            Quaternion _spinRotation = Quaternion.AngleAxis(_spinDeg, Vector3.up);

            transform.rotation = _tiltRotation * _spinRotation;
        }

        /// <summary>
        /// Cache orbit model and parameters.
        /// </summary>
        private void CacheOrbit(SolarObjectData _data)
        {
            TruthOrbitData _orbit = _data.TruthOrbit!;

            orbitModel = string.IsNullOrWhiteSpace(_orbit.Model) ? "circular" : _orbit.Model;

            if (_orbit.OrbitalPeriodDays.HasValue)
            {
                orbitalPeriodSeconds = Math.Abs(_orbit.OrbitalPeriodDays.Value) * 86400.0;
            }
            else
            {
                orbitalPeriodSeconds = Math.Abs(_orbit.OrbitalPeriodYears!.Value) * 365.25 * 86400.0;
            }

            if (_orbit.SemiMajorAxisKm.HasValue)
            {
                semiMajorAxisKm = Math.Abs(_orbit.SemiMajorAxisKm.Value);
            }
            else
            {
                semiMajorAxisKm = Math.Abs(_orbit.SemiMajorAxisAU!.Value) * 149_597_870.7;
            }

            double _initialAngleDeg = _data.Spawn?.InitialAngleDeg ?? 0.0;
            initialPhaseRad = DegToRad(_initialAngleDeg);

            hasOrbit = semiMajorAxisKm > 0.0 && orbitalPeriodSeconds > 0.0;

            if (string.Equals(orbitModel, "keplerian", StringComparison.OrdinalIgnoreCase))
            {
                eccentricity = _orbit.Eccentricity!.Value;
                inclinationRad = DegToRad(_orbit.InclinationDeg!.Value);
                longitudeAscendingNodeRad = DegToRad(_orbit.LongitudeAscendingNodeDeg!.Value);
                argumentPeriapsisRad = DegToRad(_orbit.ArgumentPeriapsisDeg!.Value);
                meanAnomalyRad = DegToRad(_orbit.MeanAnomalyDeg!.Value);
            }
            else
            {
                eccentricity = 0.0;
                inclinationRad = 0.0;
                longitudeAscendingNodeRad = 0.0;
                argumentPeriapsisRad = 0.0;
                meanAnomalyRad = 0.0;
            }
        }

        /// <summary>
        /// Enforce a minimum orbit radius so moons do not overlap their primary.
        /// </summary>
        private void CacheMoonOverlapGuard()
        {
            minOrbitRadiusUnity = 0.0f;
            if (visualContext == null)
            {
                return;
            }

            if (primaryTransform == null)
            {
                return;
            }

            float _primaryDiameterUnity = primaryTransform.localScale.x;

            float _requiredMinCenterDistance =
                (_primaryDiameterUnity * 0.5f) +
                (solarObjectDiameterUnity * 0.5f) +
                visualContext.MoonClearanceUnity;

            minOrbitRadiusUnity = _requiredMinCenterDistance;
        }
        #endregion

        #region Orbit Calculation
        /// <summary>
        /// Compute the local orbit offset for the current time.
        /// </summary>
        private Vector3 ComputeOrbitOffsetUnity(double _simulationTimeSeconds)
        {
            if (visualContext == null)
            {
                return Vector3.zero;
            }

            if (!hasOrbit)
            {
                return Vector3.zero;
            }

            double _distanceMultiplier = visualContext.GlobalDistanceMultiplier * userDistanceMultiplier;

            double _aUnity =
                (semiMajorAxisKm / Math.Max(1e-9, visualContext.DistanceKmPerUnityUnit)) * _distanceMultiplier;

            if (string.Equals(orbitModel, "keplerian", StringComparison.OrdinalIgnoreCase))
            {
                return ComputeKeplerianOffsetUnity(_simulationTimeSeconds, _aUnity);
            }

            double _theta = initialPhaseRad +
                (TwoPi() * (_simulationTimeSeconds / Math.Max(1e-9, orbitalPeriodSeconds)));
            _theta = WrapAngleRad(_theta);

            float _rUnity = (float)_aUnity;
            if (_rUnity < minOrbitRadiusUnity)
            {
                _rUnity = minOrbitRadiusUnity;
            }

            float _x = Mathf.Sin((float)_theta) * _rUnity;
            float _z = Mathf.Cos((float)_theta) * _rUnity;

            return new Vector3(_x, 0.0f, _z);
        }

        /// <summary>
        /// Compute the local orbit offset using Keplerian elements.
        /// </summary>
        private Vector3 ComputeKeplerianOffsetUnity(double _simulationTimeSeconds, double _aUnity)
        {
            double _n = TwoPi() / Math.Max(1e-9, orbitalPeriodSeconds);

            double _M = WrapAngleRad(meanAnomalyRad + _n * _simulationTimeSeconds);
            double _E = SolveEccentricAnomaly(_M, eccentricity);

            double _r = 1.0 - eccentricity * Math.Cos(_E);
            double _rUnity = _aUnity * _r;

            double _cosNu = (Math.Cos(_E) - eccentricity) /
                Math.Max(1e-9, (1.0 - eccentricity * Math.Cos(_E)));
            double _sinNu = (Math.Sqrt(Math.Max(0.0, 1.0 - eccentricity * eccentricity)) * Math.Sin(_E)) /
                Math.Max(1e-9, (1.0 - eccentricity * Math.Cos(_E)));
            double _nu = Math.Atan2(_sinNu, _cosNu);

            double _arg = argumentPeriapsisRad + _nu;
            double _cosO = Math.Cos(longitudeAscendingNodeRad);
            double _sinO = Math.Sin(longitudeAscendingNodeRad);
            double _cosI = Math.Cos(inclinationRad);
            double _sinI = Math.Sin(inclinationRad);
            double _cosArg = Math.Cos(_arg);
            double _sinArg = Math.Sin(_arg);

            // Astro XYZ -> Unity XZY (Z becomes Unity Y).
            double _xAstro = _rUnity * (_cosO * _cosArg - _sinO * _sinArg * _cosI);
            double _yAstro = _rUnity * (_sinO * _cosArg + _cosO * _sinArg * _cosI);
            double _zAstro = _rUnity * (_sinArg * _sinI);

            return new Vector3((float)_xAstro, (float)_zAstro, (float)_yAstro);
        }
        #endregion

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

            bool _drawOrbit = drawOrbitRuntime && visualContext.ShowOrbitLines;
            bool _drawSpinAxis = drawAxisRuntime && visualContext.ShowSpinAxisLines;
            bool _drawWorldUp = drawWorldUpRuntime && visualContext.ShowWorldUpLines;

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

            if (lineStylesDirty && ApplyRuntimeLineStyles())
            {
                lineStylesDirty = false;
            }
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

            int _segments = Math.Max(64, visualContext.OrbitPathSegments);

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
            if (string.Equals(type, "moon", StringComparison.OrdinalIgnoreCase))
            {
                _color = moonOrbitLineColor;
            }
            else if (string.Equals(type, "dwarf_planet", StringComparison.OrdinalIgnoreCase))
            {
                _color = dwarfOrbitLineColor;
            }

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
            float _len = Mathf.Max(0.25f, transform.localScale.x * 1.25f);

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
            bool _drawOrbit = drawOrbitRuntime && (visualContext?.ShowOrbitLines ?? true);
            bool _drawAxis = drawAxisRuntime && (visualContext?.ShowSpinAxisLines ?? true);
            bool _drawWorldUp = drawWorldUpRuntime && (visualContext?.ShowWorldUpLines ?? true);

            if (orbitLine == null && _drawOrbit)
            {
                orbitLine = CreateLineRenderer("OrbitLine", orbitLineColor, orbitLineWidth, true);
            }

            if ((axisLine == null && _drawAxis) || (worldUpLine == null && _drawWorldUp))
            {
                axisLine = CreateLineRenderer("AxisLine", axisLineColor, axisLineWidth, false);
                worldUpLine = CreateLineRenderer("WorldUpLine", worldUpLineColor, axisLineWidth, false);
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
                float _width = Mathf.Max(0.0001f, orbitLineWidth * _scale);
                orbitLine.startWidth = _width;
                orbitLine.endWidth = _width;
                _applied = true;
            }

            if (axisLine != null)
            {
                float _width = Mathf.Max(0.0001f, axisLineWidth * _scale);
                axisLine.startWidth = _width;
                axisLine.endWidth = _width;
                _applied = true;
            }

            if (worldUpLine != null)
            {
                float _width = Mathf.Max(0.0001f, axisLineWidth * _scale);
                worldUpLine.startWidth = _width;
                worldUpLine.endWidth = _width;
                _applied = true;
            }

            return _applied;
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

            float _distance = (float)visualContext.GlobalDistanceMultiplier;
            float _radius = (float)visualContext.GlobalRadiusMultiplier;
            float _avg = (_distance + _radius) * 0.5f;

            float _base = Mathf.Clamp(_avg, 0.2f, 2.0f);
            return _base * Mathf.Clamp(visualContext.RuntimeLineWidthScale, 0.1f, 2.0f);
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

        #region Helpers
        private static double DegToRad(double _deg) => _deg * (Math.PI / 180.0);
        private static double TwoPi() => Math.PI * 2.0;

        private static double WrapAngleRad(double _rad)
        {
            double _twoPi = TwoPi();
            _rad %= _twoPi;
            if (_rad < 0.0)
            {
                _rad += _twoPi;
            }
            return _rad;
        }

        private static double SolveEccentricAnomaly(double _M, double _e)
        {
            double _E = (_e < 0.8) ? _M : Math.PI;

            for (int _i = 0; _i < 10; _i++)
            {
                double _f = _E - _e * Math.Sin(_E) - _M;
                double _fp = 1.0 - _e * Math.Cos(_E);
                _E -= _f / Math.Max(1e-12, _fp);
            }

            return _E;
        }
        #endregion
    }
}
