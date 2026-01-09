#nullable enable
using System;
using System.Collections.Generic;
using Assets.Scripts.Data;
using Assets.Scripts.Helpers.Debugging;
using Assets.Scripts.Loading;
using UnityEngine;

namespace Assets.Scripts.Runtime
{
    public sealed partial class SolarSystemSimulator
    {
        #region Initialization
        /// <summary>
        /// Build global visual settings from the dataset.
        /// </summary>
        private void BuildVisualContext(SolarSystemJsonLoader.Result _db)
        {
            GlobalVisualDefaultsData? _defaults = _db.Data.GlobalVisualDefaults;
            if (_defaults != null)
            {
                visualContext.KilometersPerUnityUnit = _defaults.KilometersPerUnityUnit;
                defaultGlobalDistanceScale = _defaults.GlobalDistanceScale;
                defaultGlobalRadiusScale = _defaults.GlobalRadiusScale;
                defaultOrbitLineSegments = Math.Max(64, _defaults.OrbitLineSegmentsDefault);

                visualContext.GlobalDistanceScale = defaultGlobalDistanceScale;
                visualContext.GlobalRadiusScale = defaultGlobalRadiusScale;
                visualContext.OrbitLineSegments = defaultOrbitLineSegments;
                visualContext.MoonClearanceUnity = _defaults.MoonClearanceUnity;
            }

            visualContext.SimulationRadiusScaleGlobal = simulationRadiusScaleGlobal;
            visualContext.SimulationSmallPlanetRadiusScale = simulationSmallPlanetRadiusScale;
            visualContext.SimulationLargePlanetRadiusScale = simulationLargePlanetRadiusScale;
            visualContext.SimulationMoonRadiusScale = simulationMoonRadiusScale;
            visualContext.SimulationDwarfRadiusScale = simulationDwarfRadiusScale;
            visualContext.SimulationOtherRadiusScale = simulationOtherRadiusScale;
            visualContext.SimulationSmallPlanetRadiusKmCutoff = simulationSmallPlanetRadiusKmCutoff;
            visualContext.SimulationInnerPlanetSpacingBiasPerOrder = simulationInnerPlanetSpacingBiasPerOrder;
            visualContext.SimulationInnerPlanetMaxOrderIndex = simulationInnerPlanetMaxOrderIndex;
            visualContext.SimulationPlanetDistanceScaleGlobal = simulationPlanetDistanceScaleGlobal;
            visualContext.SimulationOuterPlanetDistanceScale = simulationOuterPlanetDistanceScale;
            visualContext.SimulationOuterPlanetMinOrderIndex = simulationOuterPlanetMinOrderIndex;
            visualContext.SimulationDwarfOuterDistanceAuCutoff = simulationDwarfOuterDistanceAuCutoff;
            visualContext.SimulationInnerDwarfDistanceScale = simulationInnerDwarfDistanceScale;
            visualContext.SimulationMoonOrbitDistanceScale = simulationMoonOrbitDistanceScale;
            visualContext.AlignMoonOrbitsToPrimaryAxialTilt = alignMoonOrbitsToPrimaryAxialTilt;
            visualContext.ShowOrbitLines = true;
            visualContext.ShowSpinAxisLines = true;
            visualContext.ShowWorldUpLines = true;
            visualContext.ShowSpinDirectionLines = true;
            visualContext.FocusedOrbitLineNearAlpha = focusedOrbitLineNearAlpha;
            visualContext.FocusedOrbitLineNearScaleThreshold = focusedOrbitLineNearScaleThreshold;
            visualContext.AxisLineThicknessMultiplier = axisLineThicknessMultiplier;
            visualContext.AxisLineStarLengthMultiplier = axisLineStarLengthMultiplier;
            visualContext.AxisLineNonStarLengthMultiplier = axisLineNonStarLengthMultiplier;

            float _minAlpha = Mathf.Clamp(orbitSpeedAlphaMin, 0f, 255f);
            float _maxAlpha = Mathf.Clamp(orbitSpeedAlphaMax, 0f, 255f);
            if (_maxAlpha < _minAlpha)
            {
                float _swap = _minAlpha;
                _minAlpha = _maxAlpha;
                _maxAlpha = _swap;
            }

            visualContext.OrbitSpeedAlphaMin = _minAlpha;
            visualContext.OrbitSpeedAlphaMax = _maxAlpha;
            visualContext.OrbitSpeedAlphaExponent = Mathf.Clamp(orbitSpeedAlphaExponent, 0.1f, 3f);
            CacheOrbitSpeedRange(_db);

            if (_db.ById.TryGetValue("sun", out SolarObjectData _sunData))
            {
                visualContext.ReferenceSolarObjectRadiusKm = _sunData.TruthPhysical?.MeanRadiusKm ?? 695700.0;
            }
        }

        private void CacheOrbitSpeedRange(SolarSystemJsonLoader.Result _db)
        {
            double _minSpeed = double.MaxValue;
            double _maxSpeed = 0.0;

            foreach (SolarObjectData _data in _db.ById.Values)
            {
                if (!TryGetOrbitSpeedKmPerSecond(_data, out double _speed))
                {
                    continue;
                }

                if (_speed <= 0.0)
                {
                    continue;
                }

                _minSpeed = Math.Min(_minSpeed, _speed);
                _maxSpeed = Math.Max(_maxSpeed, _speed);
            }

            if (_minSpeed == double.MaxValue)
            {
                _minSpeed = 0.0;
                _maxSpeed = 0.0;
            }

            visualContext.OrbitSpeedMinKmPerSec = _minSpeed;
            visualContext.OrbitSpeedMaxKmPerSec = Math.Max(_minSpeed, _maxSpeed);
        }

        private static bool TryGetOrbitSpeedKmPerSecond(SolarObjectData _data, out double _speed)
        {
            _speed = 0.0;
            TruthOrbitData? _orbit = _data.TruthOrbit;
            if (_orbit == null)
            {
                return false;
            }

            double _periodSeconds = 0.0;
            if (_orbit.OrbitalPeriodDays.HasValue)
            {
                _periodSeconds = Math.Abs(_orbit.OrbitalPeriodDays.Value) * 86400.0;
            }
            else if (_orbit.OrbitalPeriodYears.HasValue)
            {
                _periodSeconds = Math.Abs(_orbit.OrbitalPeriodYears.Value) * 365.25 * 86400.0;
            }

            if (_periodSeconds <= 0.0)
            {
                return false;
            }

            double _semiMajorAxisKm = 0.0;
            if (_orbit.SemiMajorAxisKm.HasValue)
            {
                _semiMajorAxisKm = Math.Abs(_orbit.SemiMajorAxisKm.Value);
            }
            else if (_orbit.SemiMajorAxisAU.HasValue)
            {
                _semiMajorAxisKm = Math.Abs(_orbit.SemiMajorAxisAU.Value) * 149_597_870.7;
            }

            if (_semiMajorAxisKm <= 0.0)
            {
                return false;
            }

            _speed = (2.0 * Math.PI * _semiMajorAxisKm) / _periodSeconds;
            return _speed > 0.0;
        }
        #endregion

        #region Dataset Management
        /// <summary>
        /// Apply a dataset and refresh or respawn solar objects as required.
        /// </summary>
        private void ApplyDatabase(SolarSystemJsonLoader.Result _db, bool _forceRespawn)
        {
            activeDatabase = _db;
            BuildVisualContext(_db);
            ApplyRealismValues(realismLevel);

            bool _canReinit = CanReinitializeInPlace(_db);
            if (_forceRespawn || !_canReinit)
            {
                DestroyAllInstances();
                SpawnAll(_db);
            }

            InitializeAllTwoPass(_db);
            RebuildOrderedSolarObjects(_db);
            EnsureHypotheticalObjectsActive();
            LogSpawnedSolarObjects(_db);
            ApplySunLightRealism();

            for (int _i = 0; _i < solarObjectsOrdered.Count; _i++)
            {
                SolarObject _object = solarObjectsOrdered[_i];
                _object.SetOrbitLinesEnabled(true);
                _object.Simulate(simulationTimeSeconds);
            }

            SetFocusedSolarObject(null);
        }

        /// <summary>
        /// Ensure hypothetical objects (Planet X) stay active.
        /// </summary>
        private void EnsureHypotheticalObjectsActive()
        {
            foreach (KeyValuePair<string, SolarObject> _pair in solarObjectsById)
            {
                SolarObject _object = _pair.Value;
                if (!_object.IsHypothetical)
                {
                    continue;
                }

                if (!_object.gameObject.activeSelf)
                {
                    _object.gameObject.SetActive(true);
                }
            }
        }

        /// <summary>
        /// Decide whether existing instances can be reinitialized in place.
        /// </summary>
        private bool CanReinitializeInPlace(SolarSystemJsonLoader.Result _db)
        {
            if (solarObjectsById.Count == 0)
            {
                return false;
            }

            if (solarObjectsById.Count != _db.ById.Count)
            {
                return false;
            }

            foreach (string _id in _db.ById.Keys)
            {
                if (!solarObjectsById.ContainsKey(_id))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Destroy all spawned solar objects and clear cached lists.
        /// </summary>
        private void DestroyAllInstances()
        {
            foreach (KeyValuePair<string, SolarObject> _pair in solarObjectsById)
            {
                if (_pair.Value != null)
                {
                    Destroy(_pair.Value.gameObject);
                }
            }

            solarObjectsById.Clear();
            solarObjectsOrdered.Clear();
        }

        /// <summary>
        /// Rebuild the ordered list used by UI and focus systems.
        /// </summary>
        private void RebuildOrderedSolarObjects(SolarSystemJsonLoader.Result _db)
        {
            solarObjectsOrdered.Clear();

            List<SolarObjectData> _sorted = new List<SolarObjectData>(_db.ById.Values);
            _sorted.Sort((_a, _b) =>
            {
                int _ar = _a.IsReference ? 0 : 1;
                int _br = _b.IsReference ? 0 : 1;
                if (_ar != _br)
                {
                    return _ar.CompareTo(_br);
                }

                int _ao = _a.OrderFromSun ?? int.MaxValue;
                int _bo = _b.OrderFromSun ?? int.MaxValue;
                int _cmp = _ao.CompareTo(_bo);
                if (_cmp != 0)
                {
                    return _cmp;
                }

                return string.Compare(_a.Id, _b.Id, StringComparison.OrdinalIgnoreCase);
            });

            for (int _i = 0; _i < _sorted.Count; _i++)
            {
                SolarObjectData _data = _sorted[_i];
                if (solarObjectsById.TryGetValue(_data.Id, out SolarObject _instance))
                {
                    solarObjectsOrdered.Add(_instance);
                }
            }
        }

        /// <summary>
        /// Load all prefabs from Resources into a name lookup table.
        /// </summary>
        private void LoadPrefabsFromResources()
        {
            prefabsByName.Clear();

            GameObject[] _prefabs = Resources.LoadAll<GameObject>(prefabsResourcesFolder);
            if (_prefabs.Length == 0)
            {
                HelpLogs.Warn(
                    "Simulator",
                    $"No prefabs found in Resources/{prefabsResourcesFolder}."
                );
            }
            for (int _i = 0; _i < _prefabs.Length; _i++)
            {
                GameObject _p = _prefabs[_i];
                if (_p == null)
                {
                    continue;
                }

                if (prefabsByName.ContainsKey(_p.name))
                {
                    continue;
                }

                prefabsByName.Add(_p.name, _p);
            }
        }

        /// <summary>
        /// Spawn all solar objects from the dataset.
        /// </summary>
        private void SpawnAll(SolarSystemJsonLoader.Result _db)
        {
            solarObjectsById.Clear();
            solarObjectsOrdered.Clear();

            List<SolarObjectData> _sorted = new List<SolarObjectData>(_db.ById.Values);
            _sorted.Sort((_a, _b) =>
            {
                int _ar = _a.IsReference ? 0 : 1;
                int _br = _b.IsReference ? 0 : 1;
                if (_ar != _br)
                {
                    return _ar.CompareTo(_br);
                }

                int _ao = _a.OrderFromSun ?? int.MaxValue;
                int _bo = _b.OrderFromSun ?? int.MaxValue;
                int _cmp = _ao.CompareTo(_bo);
                if (_cmp != 0)
                {
                    return _cmp;
                }

                return string.Compare(_a.Id, _b.Id, StringComparison.OrdinalIgnoreCase);
            });

            for (int _i = 0; _i < _sorted.Count; _i++)
            {
                SolarObjectData _data = _sorted[_i];

                GameObject _prefab = GetPrefabOrTemplate(_data);
                GameObject _go = Instantiate(_prefab);
                _go.name = string.IsNullOrWhiteSpace(_data.DisplayName) ? _data.Id : _data.DisplayName;

                SolarObject _solarObject = _go.GetComponent<SolarObject>();
                if (_solarObject == null)
                {
                    _solarObject = _go.AddComponent<SolarObject>();
                }

                solarObjectsById[_data.Id] = _solarObject;
                solarObjectsOrdered.Add(_solarObject);
            }
        }

        /// <summary>
        /// Initialize SolarObject instances (reference first, then dependents).
        /// </summary>
        private void InitializeAllTwoPass(SolarSystemJsonLoader.Result _db)
        {
            // Pass 1: reference solar object (Sun).
            foreach (KeyValuePair<string, SolarObject> _pair in solarObjectsById)
            {
                SolarObjectData _data = _db.ById[_pair.Key];
                if (!_data.IsReference)
                {
                    continue;
                }

                _pair.Value.Initialize(_data, null, null, visualContext);
            }

            if (solarObjectsById.TryGetValue("sun", out SolarObject _sun))
            {
                visualContext.ReferenceSolarObjectDiameterUnity = _sun.transform.localScale.x;
            }

            // Pass 2: all other solar objects, resolved to their primary transforms.
            foreach (KeyValuePair<string, SolarObject> _pair in solarObjectsById)
            {
                SolarObjectData _data = _db.ById[_pair.Key];
                if (_data.IsReference)
                {
                    continue;
                }

                Transform? _primaryTransform = null;
                SolarObject? _primarySolarObject = null;
                if (!string.IsNullOrWhiteSpace(_data.PrimaryId) &&
                    solarObjectsById.TryGetValue(_data.PrimaryId, out SolarObject _primary))
                {
                    _primaryTransform = _primary.transform;
                    _primarySolarObject = _primary;
                }

                _pair.Value.Initialize(_data, _primaryTransform, _primarySolarObject, visualContext);
            }
        }

        /// <summary>
        /// Resolve a prefab by id/name, falling back to Template or a primitive.
        /// </summary>
        private GameObject GetPrefabOrTemplate(SolarObjectData _data)
        {
            if (prefabsByName.TryGetValue(_data.Id, out GameObject _p))
            {
                return _p;
            }

            if (!string.IsNullOrWhiteSpace(_data.DisplayName) &&
                prefabsByName.TryGetValue(_data.DisplayName, out GameObject _pd))
            {
                return _pd;
            }

            if (prefabsByName.TryGetValue("Template", out GameObject _t))
            {
                HelpLogs.Warn("Simulator", $"Prefab missing for '{_data.Id}'. Using 'Template'.");
                return _t;
            }

            HelpLogs.Warn(
                "Simulator",
                $"Prefab missing for '{_data.Id}' and no Template found. Using Unity sphere."
            );
            return GameObject.CreatePrimitive(PrimitiveType.Sphere);
        }
        #endregion
    }
}
