using System.Collections.Generic;
using Assets.Scripts.Helpers.Debugging;
using UnityEngine;

namespace Assets.Scripts.Guis
{
    /// <summary>
    /// Named panel identifiers for canvas children.
    /// Make sure this names are matching canvas panel names identically
    /// </summary>
    public enum Panel : sbyte
    {
        None = -1,
        Start_Intro = 0,
        Simulation
    }

    public static partial class Gui
    {
        #region Panel Cache
        // Cached panel game objects found on the active canvas.
        private static readonly List<GameObject> panels = new();
        #endregion

        #region Show and Hide
        /// <summary>
        /// Show the requested panel, optionally hiding all others.
        /// </summary>
        public static void Show(Panel _panelToShow, bool _hideAllOtherPanels = true)
        {
            if (panels.Count == 0)
            {
                HelpLogs.Warn("Gui", "Panels not allocated; can not show panel.");
                return;
            }

            if (_hideAllOtherPanels)
            {
                Hide();
            }

            foreach (GameObject _panel in panels)
            {
                if (_panel.name == _panelToShow.ToString())
                {
                    _panel.SetActive(true);
                }
            }
        }

        /// <summary>
        /// Hide a specific panel by name.
        /// </summary>
        public static void Hide(Panel _panelToHide)
        {
            if (panels.Count == 0)
            {
                HelpLogs.Warn("Gui", "Panels not allocated; can not hide panel.");
                return;
            }

            foreach (GameObject _panel in panels)
            {
                if (_panel.name == _panelToHide.ToString())
                {
                    _panel.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Hide all panels.
        /// </summary>
        public static void Hide()
        {
            if (panels.Count == 0)
            {
                HelpLogs.Warn("Gui", "Panels not allocated; can not hide panels.");
                return;
            }

            foreach (GameObject _panel in panels)
            {
                _panel.SetActive(false);
            }
        }
        #endregion

        #region Allocate and Deallocate
        /// <summary>
        /// Cache all panel game objects from the active canvas.
        /// </summary>
        private static void AllocatePanels()
        {
            if (panels.Count > 0)
            {
                HelpLogs.Warn("Gui", "Panels already allocated; skipping duplicate allocation.");
                return;
            }

            Canvas _canvas = GameObject.FindFirstObjectByType<Canvas>();

            if (_canvas == null)
            {
                HelpLogs.Warn("Gui", "Can not locate canvas on this scene");
                return;
            }

            foreach (Transform _canvasChild in _canvas.transform)
            {
                if (_canvasChild.GetComponent<RectTransform>() != null)
                {
                    HelpLogs.Log(
                        "Gui",
                        $"Allocator found {_canvasChild.name} (as RectTransform) on {_canvas.name}"
                    );
                    panels.Add(_canvasChild.gameObject);
                }
            }

            HelpLogs.Log("Gui", $"Allocated {panels.Count} panels on canvas {_canvas.name}");
        }

        /// <summary>
        /// Clear cached panels.
        /// </summary>
        private static void DeallocatePanels()
        {
            panels.Clear();
            HelpLogs.Log("Gui", "Deallocated panels");
        }
        #endregion
    }
}
