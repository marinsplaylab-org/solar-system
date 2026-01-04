#nullable enable
using System.Collections.Generic;
using Assets.Scripts.Guis;
using Assets.Scripts.Helpers.Debugging;
using Assets.Scripts.Loading;
using UnityEngine;

namespace Assets.Scripts.Runtime
{
    public sealed partial class SolarSystemSimulator
    {
        #region Unity Lifecycle
        /// <summary>
        /// Load data, build context, and spawn all solar objects.
        /// </summary>
        private void Awake()
        {
            Application.targetFrameRate = 60;

            realismLevel = Mathf.Clamp01(realismLevel);
            activeDatabase = SolarSystemJsonLoader.LoadOrLog(resourcesJsonPathWithoutExtension);
            if (activeDatabase == null)
            {
                HelpLogs.Error(
                    "Simulator",
                    $"Failed to load dataset '{resourcesJsonPathWithoutExtension}'. Simulator is not active."
                );
                enabled = false;
                return;
            }

            LoadPrefabsFromResources();

            ApplyDatabase(activeDatabase, true);
            SolarObjectsReady?.Invoke(solarObjectsOrdered);

            HelpLogs.Log("Simulator", $"Ready. Objects spawned: {solarObjectsById.Count}");
        }

        /// <summary>
        /// Subscribe to runtime UI events.
        /// </summary>
        private void OnEnable()
        {
            Gui.TimeScaleStepRequested += HandleTimeScaleStepRequested;
            Gui.RealismStepRequested += HandleRealismStepRequested;
        }

        /// <summary>
        /// Initialize runtime controls after scene objects are ready.
        /// </summary>
        private void Start()
        {
            Gui.Initialize();
            SetupRuntimeGui();
            runtimeControlsInitialized = true;

            UpdateAppVersionText();
            ApplyRealismLevel(realismLevel, true);
            UpdateTimeScaleText();
        }

        /// <summary>
        /// Cleanup runtime UI references.
        /// Unsubscribe from runtime UI events.
        /// </summary>
        private void OnDestroy()
        {
            Gui.TimeScaleStepRequested -= HandleTimeScaleStepRequested;
            Gui.RealismStepRequested -= HandleRealismStepRequested;

            Gui.UnInitialize();
        }

        /// <summary>
        /// Advance simulation and update runtime labels.
        /// </summary>
        private void Update()
        {
            if (activeDatabase == null)
            {
                return;
            }

            // Advance simulation clock and update solar objects.
            simulationTimeSeconds += Time.deltaTime * timeScale;

            for (int _i = 0; _i < solarObjectsOrdered.Count; _i++)
            {
                SolarObject _object = solarObjectsOrdered[_i];
                _object.Simulate(simulationTimeSeconds);
            }

            if (runtimeControlsInitialized)
            {
                timeLabelRefreshTimer += Time.deltaTime;
                if (timeLabelRefreshTimer >= 1.0f)
                {
                    timeLabelRefreshTimer = 0.0f;
                    UpdateTimeScaleText();
                }
            }
        }
        #endregion
    }
}
