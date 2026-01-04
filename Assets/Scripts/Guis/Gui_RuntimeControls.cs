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
    /// Runtime control UI bindings for buttons and value labels.
    /// </summary>
    public static partial class Gui
    {
        #region Runtime Widgets
        public static TextMeshProUGUI? TimeScaleValueText { get; private set; }
        public static TextMeshProUGUI? RealismValueText { get; private set; }
        public static TextMeshProUGUI? AppVersionText { get; private set; }

        public static Button? TimeScaleMinusButton { get; private set; }
        public static Button? TimeScalePlusButton { get; private set; }
        public static Button? RealismMinusButton { get; private set; }
        public static Button? RealismPlusButton { get; private set; }
        private static bool runtimeWidgetsAllocated = false;
        #endregion

        #region Lookups
        private static readonly Dictionary<string, TextMeshProUGUI> textsByName =
            new Dictionary<string, TextMeshProUGUI>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Button> buttonsByName =
            new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        #endregion

        #region Events
        public static event Action<int>? TimeScaleStepRequested;
        public static event Action<int>? RealismStepRequested;
        public static event Action<Vector2>? CameraOrbitStepRequested;
        public static event Action<int>? CameraZoomStepRequested;
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
        /// Relay a time scale step request to listeners.
        /// </summary>
        public static void NotifyTimeScaleStepRequested(int _delta)
        {
            TimeScaleStepRequested?.Invoke(_delta);
        }

        /// <summary>
        /// Relay a realism step request to listeners.
        /// </summary>
        public static void NotifyRealismStepRequested(int _delta)
        {
            RealismStepRequested?.Invoke(_delta);
        }

        /// <summary>
        /// Relay a camera orbit step request to listeners.
        /// </summary>
        public static void NotifyCameraOrbitStepRequested(Vector2 _direction)
        {
            CameraOrbitStepRequested?.Invoke(_direction);
        }

        /// <summary>
        /// Relay a camera zoom step request to listeners.
        /// </summary>
        public static void NotifyCameraZoomStepRequested(int _delta)
        {
            CameraZoomStepRequested?.Invoke(_delta);
        }

        #endregion

        #region Allocate and Deallocate
        /// <summary>
        /// Discover and cache all runtime control widgets in the active canvas.
        /// </summary>
        private static void AllocateInteractionWidgets()
        {
            Canvas[] _canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            List<Canvas> _sceneCanvases = new List<Canvas>();
            for (int _i = 0; _i < _canvases.Length; _i++)
            {
                Canvas _canvas = _canvases[_i];
                if (_canvas == null)
                {
                    continue;
                }

                if (!_canvas.gameObject.scene.IsValid())
                {
                    continue;
                }

                _sceneCanvases.Add(_canvas);
            }

            if (_sceneCanvases.Count == 0)
            {
                HelpLogs.Warn("Gui", "Can not locate any canvas in this scene");
                return;
            }

            textsByName.Clear();
            buttonsByName.Clear();

            for (int _c = 0; _c < _sceneCanvases.Count; _c++)
            {
                Canvas _canvas = _sceneCanvases[_c];

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

                Button[] _buttons = _canvas.GetComponentsInChildren<Button>(true);
                for (int _i = 0; _i < _buttons.Length; _i++)
                {
                    Button _button = _buttons[_i];
                    if (_button == null)
                    {
                        continue;
                    }

                    if (buttonsByName.ContainsKey(_button.name))
                    {
                        continue;
                    }

                    buttonsByName.Add(_button.name, _button);
                }
            }

            AppVersionText = GetTextByName("AppVersionText");
            TimeScaleValueText = GetTextByName("TimeScaleValueText");
            RealismValueText = GetTextByName("RealismValueText");

            TimeScaleMinusButton = TryGetButtonByName("TimeScaleMinusButton");
            TimeScalePlusButton = TryGetButtonByName("TimeScalePlusButton");
            RealismMinusButton = TryGetButtonByName("RealismMinusButton");
            RealismPlusButton = TryGetButtonByName("RealismPlusButton");

            bool _hasTimeScaleButtons = TimeScaleMinusButton != null || TimeScalePlusButton != null;
            bool _hasRealismButtons = RealismMinusButton != null || RealismPlusButton != null;

            if (!_hasTimeScaleButtons)
            {
                HelpLogs.Warn("Gui", "Missing time scale buttons.");
            }

            if (!_hasRealismButtons)
            {
                HelpLogs.Warn("Gui", "Missing realism buttons.");
            }

            HelpLogs.Log(
                "Gui",
                $"Allocated {textsByName.Count} texts, {buttonsByName.Count} buttons " +
                $"across {_sceneCanvases.Count} canvases."
            );
            runtimeWidgetsAllocated = true;
        }

        /// <summary>
        /// Clear cached references to runtime control widgets.
        /// </summary>
        private static void DeallocateInteractionWidgets()
        {
            textsByName.Clear();
            buttonsByName.Clear();

            AppVersionText = null;
            TimeScaleValueText = null;
            RealismValueText = null;

            TimeScaleMinusButton = null;
            TimeScalePlusButton = null;
            RealismMinusButton = null;
            RealismPlusButton = null;
            HelpLogs.Log("Gui", "Deallocated interaction widgets");
            runtimeWidgetsAllocated = false;
        }
        #endregion

        #region Lookup Helpers
        /// <summary>
        /// Resolve a TextMeshProUGUI by name with a warning when missing.
        /// </summary>
        private static TextMeshProUGUI? GetTextByName(string _name)
        {
            if (textsByName.TryGetValue(_name, out TextMeshProUGUI _text))
            {
                return _text;
            }

            HelpLogs.Warn("Gui", $"Missing TextMeshProUGUI '{_name}' in canvas");
            return null;
        }

        /// <summary>
        /// Try to resolve a Button by name without warnings.
        /// </summary>
        private static Button? TryGetButtonByName(string _name)
        {
            if (buttonsByName.TryGetValue(_name, out Button _button))
            {
                return _button;
            }

            return null;
        }

        #endregion
    }
}
