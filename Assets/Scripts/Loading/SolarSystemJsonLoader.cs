#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using Assets.Scripts.Data;
using Assets.Scripts.Helpers.Debugging;

namespace Assets.Scripts.Loading
{
    /// <summary>
    /// Loads the solar system JSON from Resources and validates required fields.
    /// </summary>
    public static class SolarSystemJsonLoader
    {
        #region Types
        /// <summary>
        /// Parsed dataset and an id lookup table.
        /// </summary>
        public sealed class Result
        {
            // Parsed dataset root.
            public SolarSystemData Data = new();
            // Lookup table by solar object id.
            public Dictionary<string, SolarObjectData> ById =
                new(StringComparer.OrdinalIgnoreCase);
        }
        #endregion

        #region Public API
        /// <summary>
        /// Load and validate the dataset from a Resources path (no extension).
        /// Logs errors and returns null on failure.
        /// </summary>
        public static Result? LoadOrLog(string _resourcesPathWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(_resourcesPathWithoutExtension))
            {
                HelpLogs.Error("JsonLoader", "Resources path is null/empty.");
                return null;
            }

            TextAsset _asset = Resources.Load<TextAsset>(_resourcesPathWithoutExtension);
            if (_asset == null)
            {
                HelpLogs.Error(
                    "JsonLoader",
                    $"Missing JSON TextAsset at Resources path '{_resourcesPathWithoutExtension}' (no extension)."
                );
                return null;
            }

            SolarSystemData? _data;
            try
            {
                _data = JsonConvert.DeserializeObject<SolarSystemData>(_asset.text);
            }
            catch (Exception _ex)
            {
                HelpLogs.Error("JsonLoader", $"JSON parse failed. {_ex.Message}");
                return null;
            }

            if (_data == null || _data.SolarObjects == null || _data.SolarObjects.Count == 0)
            {
                HelpLogs.Error("JsonLoader", "JSON parsed but has no solar_objects.");
                return null;
            }

            Result _result = new Result { Data = _data };

            for (int _i = 0; _i < _data.SolarObjects.Count; _i++)
            {
                SolarObjectData _o = _data.SolarObjects[_i];
                if (string.IsNullOrWhiteSpace(_o.Id))
                {
                    HelpLogs.Error("JsonLoader", $"solar_objects[{_i}] has empty id.");
                    return null;
                }

                if (_result.ById.ContainsKey(_o.Id))
                {
                    HelpLogs.Error("JsonLoader", $"Duplicate id '{_o.Id}'.");
                    return null;
                }

                _result.ById.Add(_o.Id, _o);
            }

            if (!_result.ById.ContainsKey("sun"))
            {
                HelpLogs.Error("JsonLoader", "Missing object with id 'sun'.");
                return null;
            }

            // Hard validation for required fields used by the simulation.
            foreach (KeyValuePair<string, SolarObjectData> _pair in _result.ById)
            {
                SolarObjectData _o = _pair.Value;

                if (_o.TruthPhysical == null || !_o.TruthPhysical.MeanRadiusKm.HasValue)
                {
                    HelpLogs.Error(
                        "JsonLoader",
                        $"'{_o.Id}' missing truth_physical.mean_radius_km."
                    );
                    return null;
                }

                if (_o.TruthSpin == null ||
                    (!_o.TruthSpin.SiderealRotationPeriodDays.HasValue &&
                     !_o.TruthSpin.SiderealRotationPeriodHours.HasValue))
                {
                    HelpLogs.Error("JsonLoader", $"'{_o.Id}' missing truth_spin rotation period.");
                    return null;
                }

                if (!_o.IsReference)
                {
                    string _primaryId = _o.PrimaryId ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(_primaryId))
                    {
                        HelpLogs.Error("JsonLoader", $"'{_o.Id}' missing primary_id.");
                        return null;
                    }

                    if (!_result.ById.ContainsKey(_primaryId))
                    {
                        HelpLogs.Error(
                            "JsonLoader",
                            $"'{_o.Id}' primary_id '{_primaryId}' not found in dataset."
                        );
                        return null;
                    }

                    if (_o.TruthOrbit == null)
                    {
                        HelpLogs.Error("JsonLoader", $"'{_o.Id}' missing truth_orbit.");
                        return null;
                    }

                    bool _hasA = _o.TruthOrbit.SemiMajorAxisKm.HasValue ||
                        _o.TruthOrbit.SemiMajorAxisAU.HasValue;
                    bool _hasP = _o.TruthOrbit.OrbitalPeriodDays.HasValue ||
                        _o.TruthOrbit.OrbitalPeriodYears.HasValue;
                    if (!_hasA)
                    {
                        HelpLogs.Error("JsonLoader", $"'{_o.Id}' missing semi_major_axis_km/AU.");
                        return null;
                    }

                    if (!_hasP)
                    {
                        HelpLogs.Error("JsonLoader", $"'{_o.Id}' missing orbital_period_days/years.");
                        return null;
                    }

                    string _model = _o.TruthOrbit.Model ?? string.Empty;
                    if (!string.Equals(_model, "keplerian", StringComparison.OrdinalIgnoreCase))
                    {
                        HelpLogs.Error(
                            "JsonLoader",
                            $"'{_o.Id}' orbit model '{_model}' is not supported. Use 'keplerian'."
                        );
                        return null;
                    }

                    if (!_o.TruthOrbit.Eccentricity.HasValue ||
                        !_o.TruthOrbit.InclinationDeg.HasValue ||
                        !_o.TruthOrbit.LongitudeAscendingNodeDeg.HasValue ||
                        !_o.TruthOrbit.ArgumentPeriapsisDeg.HasValue ||
                        !_o.TruthOrbit.MeanAnomalyDeg.HasValue)
                    {
                        HelpLogs.Error("JsonLoader", $"'{_o.Id}' keplerian orbit missing elements.");
                        return null;
                    }
                }

                string _focusProfile = _o.CameraFocusProfile ?? string.Empty;
                if (string.IsNullOrWhiteSpace(_focusProfile))
                {
                    HelpLogs.Error("JsonLoader", $"'{_o.Id}' missing camera_focus_profile.");
                    return null;
                }

                if (!IsValidCameraFocusProfile(_focusProfile))
                {
                    HelpLogs.Error(
                        "JsonLoader",
                        $"'{_o.Id}'camera_focus_profile '{_focusProfile}' is incorrect."
                    );
                    return null;
                }
            }

            return _result;
        }
        #endregion

        #region Validation Helpers
        private static bool IsValidCameraFocusProfile(string _value)
        {
            string _normalized = _value.Trim().Replace("-", "_").ToLowerInvariant();
            switch (_normalized)
            {
                case "moon":
                case "dwarf_planet":
                case "dwarfplanet":
                case "terrestrial":
                case "gas_giant":
                case "gasgiant":
                case "ice_giant":
                case "icegiant":
                case "star":
                    return true;
                default:
                    return false;
            }
        }
        #endregion
    }
}
