#nullable enable
using System;
using System.Collections.Generic;
using Assets.Scripts.Helpers.Debugging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Guis
{
    /// <summary>
    /// Runtime control UI bindings for sliders, value labels, and slider events.
    /// </summary>
    public static partial class Gui
    {
        #region Runtime Widgets
        public static TextMeshProUGUI? TimeScaleValueText { get; private set; }
        public static TextMeshProUGUI? VisualPresetValueText { get; private set; }
        public static TextMeshProUGUI? AppVersionText { get; private set; }

        public static Slider? TimeScaleSlider { get; private set; }
        public static Slider? VisualPresetSlider { get; private set; }
        public static Toggle? OrbitLinesToggle { get; private set; }
        public static Toggle? SpinAxisToggle { get; private set; }
        public static Toggle? WorldUpToggle { get; private set; }

        private static bool runtimeWidgetsAllocated = false;
        #endregion

        #region Lookups
        private static readonly Dictionary<string, TextMeshProUGUI> textsByName =
            new Dictionary<string, TextMeshProUGUI>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Slider> slidersByName =
            new Dictionary<string, Slider>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Toggle> togglesByName =
            new Dictionary<string, Toggle>(StringComparer.OrdinalIgnoreCase);
        #endregion

        #region Events
        public static event Action<int>? TimeScaleLevelChanged;
        public static event Action<int>? VisualPresetLevelChanged;
        public static event Action<bool>? OrbitLinesToggled;
        public static event Action<bool>? SpinAxisToggled;
        public static event Action<bool>? WorldUpToggled;
        #endregion

        #region Public Helpers
        /// <summary>
        /// Ensure runtime widgets are allocated at least once.
        /// </summary>
        public static bool EnsureRuntimeWidgets()
        {
            if (runtimeWidgetsAllocated)
            {
                return true;
            }

            AllocateInteractionWidgets();
            return runtimeWidgetsAllocated;
        }

        /// <summary>
        /// Relay a time scale slider change to listeners.
        /// </summary>
        public static void NotifyTimeScaleSliderChanged(float _value)
        {
            int _index = Mathf.RoundToInt(_value);
            if (TimeScaleSlider != null)
            {
                _index = Mathf.Clamp(
                    _index,
                    (int)TimeScaleSlider.minValue,
                    (int)TimeScaleSlider.maxValue
                );
            }

            TimeScaleLevelChanged?.Invoke(_index);
        }

        /// <summary>
        /// Relay a visual preset slider change to listeners.
        /// </summary>
        public static void NotifyVisualPresetSliderChanged(float _value)
        {
            int _index = Mathf.RoundToInt(_value);
            if (VisualPresetSlider != null)
            {
                _index = Mathf.Clamp(
                    _index,
                    (int)VisualPresetSlider.minValue,
                    (int)VisualPresetSlider.maxValue
                );
            }

            VisualPresetLevelChanged?.Invoke(_index);
        }

        /// <summary>
        /// Relay orbit lines toggle changes to listeners.
        /// </summary>
        public static void NotifyOrbitLinesToggled(bool _enabled)
        {
            OrbitLinesToggled?.Invoke(_enabled);
        }

        /// <summary>
        /// Relay spin axis toggle changes to listeners.
        /// </summary>
        public static void NotifySpinAxisToggled(bool _enabled)
        {
            SpinAxisToggled?.Invoke(_enabled);
        }

        /// <summary>
        /// Relay world-up line toggle changes to listeners.
        /// </summary>
        public static void NotifyWorldUpToggled(bool _enabled)
        {
            WorldUpToggled?.Invoke(_enabled);
        }
        #endregion

        #region Allocate and Deallocate
        /// <summary>
        /// Discover and cache all runtime control widgets in the active canvas.
        /// </summary>
        private static void AllocateInteractionWidgets()
        {
            Canvas _canvas = GameObject.FindFirstObjectByType<Canvas>();
            if (_canvas == null)
            {
                HelpLogs.Warn("Gui", "Can not locate canvas on this scene");
                return;
            }

            textsByName.Clear();
            slidersByName.Clear();
            togglesByName.Clear();

            TextMeshProUGUI[] _texts = _canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int _i = 0; _i < _texts.Length; _i++)
            {
                TextMeshProUGUI _text = _texts[_i];
                if (_text == null)
                {
                    continue;
                }

                if (textsByName.ContainsKey(_text.name))
                {
                    continue;
                }

                textsByName.Add(_text.name, _text);
            }

            Slider[] _sliders = _canvas.GetComponentsInChildren<Slider>(true);
            for (int _i = 0; _i < _sliders.Length; _i++)
            {
                Slider _slider = _sliders[_i];
                if (_slider == null)
                {
                    continue;
                }

                if (slidersByName.ContainsKey(_slider.name))
                {
                    continue;
                }

                slidersByName.Add(_slider.name, _slider);
            }

            Toggle[] _toggles = _canvas.GetComponentsInChildren<Toggle>(true);
            for (int _i = 0; _i < _toggles.Length; _i++)
            {
                Toggle _toggle = _toggles[_i];
                if (_toggle == null)
                {
                    continue;
                }

                if (togglesByName.ContainsKey(_toggle.name))
                {
                    continue;
                }

                togglesByName.Add(_toggle.name, _toggle);
            }

            AppVersionText = GetTextByName("AppVersionText");
            TimeScaleValueText = GetTextByName("TimeScaleValueText");
            VisualPresetValueText = GetTextByName("VisualPresetValueText");

            TimeScaleSlider = GetSliderByName("TimeScaleSlider");
            VisualPresetSlider = GetSliderByName("VisualPresetSlider");
            OrbitLinesToggle = GetToggleByName("OrbitLinesToggle");
            SpinAxisToggle = GetToggleByName("SpinAxisToggle");
            WorldUpToggle = GetToggleByName("WorldUpToggle");

            HelpLogs.Log(
                "Gui",
                $"Allocated {textsByName.Count} texts and {slidersByName.Count} sliders on canvas {_canvas.name}"
            );
            runtimeWidgetsAllocated = true;
        }

        /// <summary>
        /// Clear cached references to runtime control widgets.
        /// </summary>
        private static void DeallocateInteractionWidgets()
        {
            textsByName.Clear();
            slidersByName.Clear();
            togglesByName.Clear();

            AppVersionText = null;
            TimeScaleValueText = null;
            VisualPresetValueText = null;

            TimeScaleSlider = null;
            VisualPresetSlider = null;
            OrbitLinesToggle = null;
            SpinAxisToggle = null;
            WorldUpToggle = null;

            HelpLogs.Log("Gui", "Deallocated interaction widgets");
            runtimeWidgetsAllocated = false;
        }
        #endregion

        #region Lookup Helpers
        private static TextMeshProUGUI? GetTextByName(string _name)
        {
            if (textsByName.TryGetValue(_name, out TextMeshProUGUI _text))
            {
                return _text;
            }

            HelpLogs.Warn("Gui", $"Missing TextMeshProUGUI '{_name}' in canvas");
            return null;
        }

        private static Slider? GetSliderByName(string _name)
        {
            if (slidersByName.TryGetValue(_name, out Slider _slider))
            {
                return _slider;
            }

            HelpLogs.Warn("Gui", $"Missing Slider '{_name}' in canvas");
            return null;
        }

        private static Toggle? GetToggleByName(string _name)
        {
            if (togglesByName.TryGetValue(_name, out Toggle _toggle))
            {
                return _toggle;
            }

            HelpLogs.Warn("Gui", $"Missing Toggle '{_name}' in canvas");
            return null;
        }
        #endregion
    }
}
