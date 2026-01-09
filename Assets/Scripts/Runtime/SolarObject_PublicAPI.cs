#nullable enable
using Assets.Scripts.Data;
using UnityEngine;

namespace Assets.Scripts.Runtime
{
    public sealed partial class SolarObject
    {
        #region Public API
        /// <summary>
        /// Initialize from dataset and shared visual context.
        /// </summary>
        public void Initialize(
            SolarObjectData _data,
            Transform? _primaryTransform,
            SolarObject? _primarySolarObject,
            VisualContext _visualContext
        )
        {
            primaryTransform = _primaryTransform;
            primarySolarObject = _primarySolarObject;
            visualContext = _visualContext;
            lineStylesDirty = true;

            id = _data.Id;
            isReference = _data.IsReference;
            isHypothetical = _data.IsHypothetical;
            type = _data.Type ?? string.Empty;
            primaryId = _data.PrimaryId ?? string.Empty;
            orderFromSun = _data.OrderFromSun ?? -1;
            alignOrbitToPrimaryTilt = _data.AlignOrbitToPrimaryTilt;
            tidalLockOverride = _data.TidalLock;
            cameraFocusProfile = ResolveCameraFocusProfile(_data);

            name = string.IsNullOrWhiteSpace(_data.DisplayName) ? _data.Id : _data.DisplayName;

            dataRadiusMultiplier = _data.VisualDefaults?.RadiusMultiplier ?? 1.0;
            dataDistanceMultiplier = _data.VisualDefaults?.DistanceMultiplier ?? 1.0;

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

            lastSimulationTimeSeconds = _simulationTimeSeconds;

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

        /// <summary>
        /// Enable or disable spin-axis, world-up, and spin-direction lines for this object.
        /// </summary>
        public void SetAxisLinesEnabled(bool _enabled)
        {
            showSpinAxisLinesLocal = _enabled;
            showWorldUpLinesLocal = _enabled;
            showSpinDirectionLinesLocal = _enabled;
            lineStylesDirty = true;
        }

        /// <summary>
        /// True when axis, world-up, or spin-direction lines are enabled locally.
        /// </summary>
        public bool AreAxisLinesEnabled =>
            showSpinAxisLinesLocal || showWorldUpLinesLocal || showSpinDirectionLinesLocal;

        /// <summary>
        /// Enable or disable orbit line rendering for this object.
        /// </summary>
        public void SetOrbitLinesEnabled(bool _enabled)
        {
            showOrbitLinesLocal = _enabled;
            lineStylesDirty = true;
        }
        #endregion
    }
}
