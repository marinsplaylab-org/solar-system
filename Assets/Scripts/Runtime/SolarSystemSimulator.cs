#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.Data;
using Assets.Scripts.Loading;
using Assets.Scripts.Guis;
using Assets.Scripts.Helpers.Debugging;

namespace Assets.Scripts.Runtime
{
    /// <summary>
    /// Loads the dataset, spawns solar objects, and advances the simulation.
    /// </summary>
    public sealed class SolarSystemSimulator : MonoBehaviour
    {
        #region Serialized Fields
        [Header("JSON (Resources)")]
        // Resources path (no extension) to the solar system dataset.
        [SerializeField] private string resourcesJsonPathWithoutExtension = "SolarSystemData";

        [Header("Prefabs (Resources)")]
        // Resources folder that contains planet/moon prefabs.
        [SerializeField] private string prefabsResourcesFolder = "SolarObjects";

        [Header("Runtime Controls")]
        [Tooltip("Enable runtime UI sliders and value labels for live tuning.")]
        [SerializeField] private bool enableRuntimeControls = true;

        [Header("Time")]
        // Simulation speed multiplier (sim seconds per real second).
        private float timeScale = 1.0f;

        #endregion

        #region Runtime State
        // Accumulated simulation time in seconds.
        private double simulationTimeSeconds = 0.0;
        // Loaded dataset and lookup tables.
        private SolarSystemJsonLoader.Result? json_database;

        // Spawned SolarObject instances keyed by id.
        private readonly Dictionary<string, SolarObject> instancesById =
            new(StringComparer.OrdinalIgnoreCase);

        // Prefabs found in Resources by name.
        private readonly Dictionary<string, GameObject> prefabsByName =
            new(StringComparer.OrdinalIgnoreCase);

        // Global visual scaling and defaults shared by all solar objects.
        private readonly SolarObject.VisualContext visualContext = new();

        // Guard for runtime controls initialization.
        private bool guiInitialized = false;

        // Time label update throttle.
        private float timeLabelUpdateTimer = 0.0f;
        #endregion

        #region Runtime Control Levels
        // Human-readable labels for slider levels.
        private readonly string[] timeScaleLevelNames = { "Standard", "Accelerated", "Hyper", "Maximum" };
        private readonly string[] visualPresetLevelNames = { "Normal", "Minimal" };

        // Level values mapped to slider indices.
        private readonly float[] timeScaleLevelValues = new float[4];
        private readonly float[] visualPresetDistanceValues = new float[2];
        private readonly float[] visualPresetRadiusValues = new float[2];
        private readonly int[] visualPresetOrbitSegmentsValues = new int[2];

        // Active indices for each slider level.
        private int timeScaleLevelIndex = 0;
        private int visualPresetLevelIndex = 0;
        #endregion

        #region Unity Lifecycle
        /// <summary>
        /// Load data, build context, and spawn all solar objects.
        /// </summary>
        private void Awake()
        {
            Application.targetFrameRate = 60;

            json_database = SolarSystemJsonLoader.LoadOrLog(resourcesJsonPathWithoutExtension);
            if (json_database == null)
            {
                HelpLogs.Error(
                    "Simulator",
                    $"Failed to load dataset '{resourcesJsonPathWithoutExtension}'. Simulator is not active."
                );
                enabled = false;
                return;
            }

            BuildVisualContext(json_database);
            LoadPrefabsFromResources();

            SpawnAll(json_database);
            InitializeAllTwoPass(json_database);

            HelpLogs.Log("Simulator", $"Ready. Objects spawned: {instancesById.Count}");
        }

        /// <summary>
        /// Subscribe to runtime UI events.
        /// </summary>
        private void OnEnable()
        {
            if (!enableRuntimeControls)
            {
                return;
            }

            Gui.TimeScaleLevelChanged += HandleTimeScaleLevelChanged;
            Gui.VisualPresetLevelChanged += HandleVisualPresetLevelChanged;
            Gui.OrbitLinesToggled += HandleOrbitLinesToggled;
            Gui.SpinAxisToggled += HandleSpinAxisToggled;
            Gui.WorldUpToggled += HandleWorldUpToggled;
        }

        /// <summary>
        /// Initialize runtime controls after scene objects are ready.
        /// </summary>
        private void Start()
        {
            if (!enableRuntimeControls)
            {
                return;
            }

            Gui.AllocateInteractionWidgets();
            SetupRuntimeGui();
            guiInitialized = true;

            UpdateAppVersionText();
            ApplyVisualPresetLevel(visualPresetLevelIndex, true);
            UpdateTimeScaleText();
        }

        /// <summary>
        /// Cleanup runtime UI references.
        /// Unsubscribe from runtime UI events.
        /// </summary>
        private void OnDestroy()
        {
            if (!enableRuntimeControls)
            {
                return;
            }

            Gui.TimeScaleLevelChanged -= HandleTimeScaleLevelChanged;
            Gui.VisualPresetLevelChanged -= HandleVisualPresetLevelChanged;
            Gui.OrbitLinesToggled -= HandleOrbitLinesToggled;
            Gui.SpinAxisToggled -= HandleSpinAxisToggled;
            Gui.WorldUpToggled -= HandleWorldUpToggled;

            Gui.DeallocateInteractionWidgets();
        }

        /// <summary>
        /// Advance simulation and update runtime labels.
        /// </summary>
        private void Update()
        {
            if (json_database == null)
            {
                return;
            }

            // Advance simulation clock and update solar objects.
            simulationTimeSeconds += Time.deltaTime * timeScale;

            foreach (KeyValuePair<string, SolarObject> _pair in instancesById)
            {
                _pair.Value.Simulate(simulationTimeSeconds);
            }

            if (enableRuntimeControls && guiInitialized)
            {
                timeLabelUpdateTimer += Time.deltaTime;
                if (timeLabelUpdateTimer >= 1.0f)
                {
                    timeLabelUpdateTimer = 0.0f;
                    UpdateTimeScaleText();
                }
            }
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Build global visual settings from the dataset.
        /// </summary>
        private void BuildVisualContext(SolarSystemJsonLoader.Result _db)
        {
            GlobalVisualDefaultsData? _defaults = _db.Data.GlobalVisualDefaults;
            if (_defaults != null)
            {
                visualContext.DistanceKmPerUnityUnit = _defaults.DistanceKmPerUnityUnit;
                visualContext.GlobalDistanceMultiplier = _defaults.GlobalDistanceMultiplier;
                visualContext.GlobalRadiusMultiplier = _defaults.GlobalRadiusMultiplier;
                visualContext.OrbitPathSegments = Math.Max(64, _defaults.OrbitPathSegmentsDefault);
                visualContext.MoonClearanceUnity = _defaults.MoonClearanceUnity;
            }

            if (_db.ById.TryGetValue("sun", out SolarObjectData _sunData))
            {
                visualContext.ReferenceSolarObjectRadiusKm = _sunData.TruthPhysical?.MeanRadiusKm ?? 695700.0;
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
            instancesById.Clear();

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

                instancesById[_data.Id] = _solarObject;
            }
        }

        /// <summary>
        /// Initialize SolarObject instances (reference first, then dependents).
        /// </summary>
        private void InitializeAllTwoPass(SolarSystemJsonLoader.Result _db)
        {
            // Pass 1: reference solar object (Sun).
            foreach (KeyValuePair<string, SolarObject> _pair in instancesById)
            {
                SolarObjectData _data = _db.ById[_pair.Key];
                if (!_data.IsReference)
                {
                    continue;
                }

                _pair.Value.Initialize(_data, null, visualContext);
            }

            if (instancesById.TryGetValue("sun", out SolarObject _sun))
            {
                visualContext.ReferenceSolarObjectDiameterUnity = _sun.transform.localScale.x;
            }

            // Pass 2: all other solar objects, resolved to their primary transforms.
            foreach (KeyValuePair<string, SolarObject> _pair in instancesById)
            {
                SolarObjectData _data = _db.ById[_pair.Key];
                if (_data.IsReference)
                {
                    continue;
                }

                Transform? _primaryTransform = null;
                if (!string.IsNullOrWhiteSpace(_data.PrimaryId) &&
                    instancesById.TryGetValue(_data.PrimaryId, out SolarObject _primary))
                {
                    _primaryTransform = _primary.transform;
                }

                _pair.Value.Initialize(_data, _primaryTransform, visualContext);
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

        #region Runtime Controls
        /// <summary>
        /// Configure runtime control slider ranges and defaults.
        /// </summary>
        private void SetupRuntimeGui()
        {
            if (!enableRuntimeControls)
            {
                return;
            }

            // Discrete slider levels that map to curated values.
            timeScaleLevelValues[0] = timeScale;
            timeScaleLevelValues[1] = 10_000.0f;
            timeScaleLevelValues[2] = 100_000.0f;
            timeScaleLevelValues[3] = 10_000_000.0f;

            timeScaleLevelIndex = 1;
            timeScale = timeScaleLevelValues[timeScaleLevelIndex];

            visualPresetDistanceValues[0] = (float)visualContext.GlobalDistanceMultiplier;
            visualPresetDistanceValues[1] = 0.02f;

            visualPresetRadiusValues[0] = (float)visualContext.GlobalRadiusMultiplier;
            visualPresetRadiusValues[1] = 0.25f;

            visualPresetOrbitSegmentsValues[0] = 128;
            visualPresetOrbitSegmentsValues[1] = 64;

            if (Gui.TimeScaleSlider != null)
            {
                Gui.TimeScaleSlider.wholeNumbers = true;
                Gui.TimeScaleSlider.minValue = 0;
                Gui.TimeScaleSlider.maxValue = timeScaleLevelValues.Length - 1;
                Gui.TimeScaleSlider.value = timeScaleLevelIndex;
            }

            if (Gui.VisualPresetSlider != null)
            {
                Gui.VisualPresetSlider.wholeNumbers = true;
                Gui.VisualPresetSlider.minValue = 0;
                Gui.VisualPresetSlider.maxValue = visualPresetLevelNames.Length - 1;
                visualPresetLevelIndex = 1;
                Gui.VisualPresetSlider.value = visualPresetLevelIndex;
            }

            if (Gui.OrbitLinesToggle != null)
            {
                Gui.OrbitLinesToggle.isOn = visualContext.ShowOrbitLines;
            }

            if (Gui.SpinAxisToggle != null)
            {
                Gui.SpinAxisToggle.isOn = visualContext.ShowSpinAxisLines;
            }

            if (Gui.WorldUpToggle != null)
            {
                Gui.WorldUpToggle.isOn = visualContext.ShowWorldUpLines;
            }
        }

        /// <summary>
        /// Apply a time scale level from the runtime slider.
        /// </summary>
        private void HandleTimeScaleLevelChanged(int _levelIndex)
        {
            if (!guiInitialized)
            {
                return;
            }

            int _clamped = Mathf.Clamp(_levelIndex, 0, timeScaleLevelValues.Length - 1);
            if (_clamped == timeScaleLevelIndex)
            {
                return;
            }

            timeScaleLevelIndex = _clamped;
            timeScale = timeScaleLevelValues[timeScaleLevelIndex];
            UpdateTimeScaleText();
        }

        /// <summary>
        /// Apply a visual preset level from the runtime slider.
        /// </summary>
        private void HandleVisualPresetLevelChanged(int _levelIndex)
        {
            if (!guiInitialized)
            {
                return;
            }

            ApplyVisualPresetLevel(_levelIndex, true);
        }

        /// <summary>
        /// Toggle orbit line rendering for all objects.
        /// </summary>
        private void HandleOrbitLinesToggled(bool _enabled)
        {
            if (!guiInitialized)
            {
                return;
            }

            visualContext.ShowOrbitLines = _enabled;
            MarkAllLineStylesDirty();
        }

        /// <summary>
        /// Toggle spin axis line rendering for all objects.
        /// </summary>
        private void HandleSpinAxisToggled(bool _enabled)
        {
            if (!guiInitialized)
            {
                return;
            }

            visualContext.ShowSpinAxisLines = _enabled;
            MarkAllLineStylesDirty();
        }

        /// <summary>
        /// Toggle world-up line rendering for all objects.
        /// </summary>
        private void HandleWorldUpToggled(bool _enabled)
        {
            if (!guiInitialized)
            {
                return;
            }

            visualContext.ShowWorldUpLines = _enabled;
            MarkAllLineStylesDirty();
        }

        /// <summary>
        /// Re-apply visual scaling to all objects.
        /// </summary>
        private void RefreshAllVisuals()
        {
            foreach (KeyValuePair<string, SolarObject> _pair in instancesById)
            {
                _pair.Value.RefreshVisuals(visualContext);
            }
        }

        /// <summary>
        /// Mark runtime line widths as dirty for all objects.
        /// </summary>
        private void MarkAllLineStylesDirty()
        {
            foreach (KeyValuePair<string, SolarObject> _pair in instancesById)
            {
                _pair.Value.MarkLineStylesDirty();
            }
        }

        /// <summary>
        /// Update the time scale label.
        /// </summary>
        private void UpdateTimeScaleText()
        {
            double _simDays = simulationTimeSeconds / 86400.0;

            if (Gui.TimeScaleValueText != null)
            {
                string _label = timeScaleLevelNames[
                    Mathf.Clamp(timeScaleLevelIndex, 0, timeScaleLevelNames.Length - 1)
                ];
                Gui.TimeScaleValueText.text =
                    $"Time Scale: {_label} ({timeScale:0.##}x, Sim {_simDays:0.00} d)";
            }
        }

        /// <summary>
        /// Update the application version label once at startup.
        /// </summary>
        private void UpdateAppVersionText()
        {
            if (Gui.AppVersionText == null)
            {
                return;
            }

            string _version = string.IsNullOrWhiteSpace(Application.version) ? "0.0.0" : Application.version;
            Gui.AppVersionText.text = $"Version: {_version}";
        }

        /// <summary>
        /// Update the visual preset label with all current settings.
        /// </summary>
        private void UpdateVisualPresetText()
        {
            if (Gui.VisualPresetValueText == null)
            {
                return;
            }

            string _label = visualPresetLevelNames[
                Mathf.Clamp(visualPresetLevelIndex, 0, visualPresetLevelNames.Length - 1)
            ];

            Gui.VisualPresetValueText.text =
                $"Visual Preset: {_label}\n" +
                $"Distance km/unit: {visualContext.DistanceKmPerUnityUnit:0}\n" +
                $"Global Distance: {visualContext.GlobalDistanceMultiplier:0.###}\n" +
                $"Global Radius: {visualContext.GlobalRadiusMultiplier:0.###}\n" +
                $"Orbit Segments: {visualContext.OrbitPathSegments}\n" +
                $"Moon Clearance: {visualContext.MoonClearanceUnity:0.###}";
        }

        /// <summary>
        /// Apply a visual preset and optionally refresh visuals.
        /// </summary>
        private void ApplyVisualPresetLevel(int _levelIndex, bool _refreshVisuals)
        {
            int _clamped = Mathf.Clamp(_levelIndex, 0, visualPresetLevelNames.Length - 1);
            if (_clamped == visualPresetLevelIndex && !_refreshVisuals)
            {
                return;
            }

            visualPresetLevelIndex = _clamped;
            visualContext.GlobalDistanceMultiplier = visualPresetDistanceValues[visualPresetLevelIndex];
            visualContext.GlobalRadiusMultiplier = visualPresetRadiusValues[visualPresetLevelIndex];
            visualContext.OrbitPathSegments = visualPresetOrbitSegmentsValues[visualPresetLevelIndex];
            visualContext.RuntimeLineWidthScale = visualPresetLevelIndex == 0 ? 1.0f : 0.25f;

            if (_refreshVisuals)
            {
                RefreshAllVisuals();
            }

            MarkAllLineStylesDirty();
            UpdateVisualPresetText();
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Find the nearest index in a list of level values.
        /// </summary>
        private static int GetClosestLevelIndex(float _value, float[] _levels)
        {
            int _bestIndex = 0;
            float _bestDistance = float.MaxValue;
            for (int _i = 0; _i < _levels.Length; _i++)
            {
                float _dist = Mathf.Abs(_value - _levels[_i]);
                if (_dist < _bestDistance)
                {
                    _bestDistance = _dist;
                    _bestIndex = _i;
                }
            }

            return _bestIndex;
        }
        #endregion
    }
}
