#nullable enable
using System;
using Assets.Scripts.Data;
using Assets.Scripts.Helpers.Debugging;
using UnityEngine;

namespace Assets.Scripts.Runtime
{
    public sealed partial class SolarObject
    {
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

        /// <summary>
        /// Resolve the camera used for line distance scaling.
        /// </summary>
        private static Camera? GetLineScaleCamera()
        {
            if (lineScaleCamera != null && lineScaleCamera.enabled)
            {
                return lineScaleCamera;
            }

            Camera _camera = Camera.main;
            if (_camera != null)
            {
                lineScaleCamera = _camera;
                return _camera;
            }

            Camera[] _all = Resources.FindObjectsOfTypeAll<Camera>();
            for (int _i = 0; _i < _all.Length; _i++)
            {
                Camera _candidate = _all[_i];
                if (_candidate == null)
                {
                    continue;
                }

                if (!_candidate.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (!_candidate.enabled || !_candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                lineScaleCamera = _candidate;
                return _candidate;
            }

            return null;
        }

        /// <summary>
        /// Solve Kepler's equation for eccentric anomaly.
        /// </summary>
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

        private CameraFocusProfile ResolveCameraFocusProfile(SolarObjectData _data)
        {
            string _raw = _data.CameraFocusProfile ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_raw))
            {
                HelpLogs.Error("SolarObject", $"'{_data.Id}' missing camera_focus_profile.");
                return CameraFocusProfile.Auto;
            }

            if (TryParseCameraFocusProfile(_raw, out CameraFocusProfile _profile))
            {
                return _profile;
            }

            HelpLogs.Error("SolarObject", $"'{_data.Id}' camera_focus_profile '{_raw}' is incorrect.");
            return CameraFocusProfile.Auto;
        }

        private static bool TryParseCameraFocusProfile(string _value, out CameraFocusProfile _profile)
        {
            _profile = CameraFocusProfile.Auto;
            string _normalized = _value.Trim().Replace("-", "_").ToLowerInvariant();
            switch (_normalized)
            {
                case "auto":
                    _profile = CameraFocusProfile.Auto;
                    return true;
                case "moon":
                    _profile = CameraFocusProfile.Moon;
                    return true;
                case "dwarf_planet":
                case "dwarfplanet":
                    _profile = CameraFocusProfile.DwarfPlanet;
                    return true;
                case "terrestrial":
                    _profile = CameraFocusProfile.Terrestrial;
                    return true;
                case "gas_giant":
                case "gasgiant":
                    _profile = CameraFocusProfile.GasGiant;
                    return true;
                case "ice_giant":
                case "icegiant":
                    _profile = CameraFocusProfile.IceGiant;
                    return true;
                case "star":
                    _profile = CameraFocusProfile.Star;
                    return true;
                default:
                    return false;
            }
        }
        #endregion
    }
}
