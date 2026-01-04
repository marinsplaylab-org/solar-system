#nullable enable
using System;
using System.Collections.Generic;
using Assets.Scripts.Helpers.Debugging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.Guis
{
    /// <summary>
    /// Binds button events to the runtime GUI notification hooks.
    /// </summary>
    public sealed class Gui_RuntimeControlEvents : MonoBehaviour
    {
        #region Serialized Fields
        [Tooltip("Optional override. Leave empty to auto-bind Gui.TimeScaleMinusButton. Example: TimeScaleMinusButton")]
        [SerializeField] private Button? timeScaleMinusButton;
        [Tooltip("Optional override. Leave empty to auto-bind Gui.TimeScalePlusButton. Example: TimeScalePlusButton")]
        [SerializeField] private Button? timeScalePlusButton;
        [Tooltip("Optional override. Leave empty to auto-bind Gui.RealismMinusButton. Example: RealismMinusButton")]
        [SerializeField] private Button? realismMinusButton;
        [Tooltip("Optional override. Leave empty to auto-bind Gui.RealismPlusButton. Example: RealismPlusButton")]
        [SerializeField] private Button? realismPlusButton;
        [Header("Hold Repeat")]
        [Tooltip("Allow press-and-hold repeat for buttons. Example: true")]
        [SerializeField] private bool enableHoldRepeat = true;
        [Tooltip("Seconds before repeat starts. Higher = longer delay, lower = faster repeat start. Example: 0.4")]
        [Range(0.05f, 2f)]
        [SerializeField] private float holdRepeatInitialDelay = 0.4f;
        [Tooltip("Seconds between repeats while held. Lower = faster repeats, higher = slower. Example: 0.1")]
        [Range(0.02f, 0.5f)]
        [SerializeField] private float holdRepeatInterval = 0.1f;
        #endregion

        #region Runtime State
        private bool isBound = false;
        private readonly List<HoldRepeatState> holdRepeats = new();
        #endregion

        #region Hold Repeat Types
        private sealed class HoldRepeatState
        {
            public Button? Button;
            public Action? Action;
            public bool IsHeld;
            public float NextRepeatTime;
            public EventTrigger? Trigger;
            public EventTrigger.Entry? PointerDownEntry;
            public EventTrigger.Entry? PointerUpEntry;
            public EventTrigger.Entry? PointerExitEntry;
        }
        #endregion

        #region Unity Lifecycle
        /// <summary>
        /// Bind UI events once the scene is ready.
        /// </summary>
        private void Start()
        {
            Bind();
        }

        /// <summary>
        /// Unbind UI events on teardown.
        /// </summary>
        private void OnDestroy()
        {
            Unbind();
        }

        private void Update()
        {
            if (!enableHoldRepeat || holdRepeats.Count == 0)
            {
                return;
            }

            float _now = Time.unscaledTime;
            float _interval = Mathf.Max(0.01f, holdRepeatInterval);
            for (int _i = 0; _i < holdRepeats.Count; _i++)
            {
                HoldRepeatState _state = holdRepeats[_i];
                if (!_state.IsHeld || _state.Action == null)
                {
                    continue;
                }

                if (_state.Button == null ||
                    !_state.Button.interactable ||
                    !_state.Button.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (_now < _state.NextRepeatTime)
                {
                    continue;
                }

                _state.Action.Invoke();
                _state.NextRepeatTime = _now + _interval;
            }
        }
        #endregion

        #region Binding
        /// <summary>
        /// Bind button events once.
        /// </summary>
        public void Bind()
        {
            if (isBound)
            {
                return;
            }

            if (!Gui.EnsureRuntimeWidgets())
            {
                HelpLogs.Warn("Gui", "Runtime widgets not found; can not bind button events.");
                return;
            }

            if (timeScaleMinusButton == null)
            {
                timeScaleMinusButton = Gui.TimeScaleMinusButton;
            }

            if (timeScalePlusButton == null)
            {
                timeScalePlusButton = Gui.TimeScalePlusButton;
            }

            if (realismMinusButton == null)
            {
                realismMinusButton = Gui.RealismMinusButton;
            }

            if (realismPlusButton == null)
            {
                realismPlusButton = Gui.RealismPlusButton;
            }

            bool timeScaleButtonsBound = false;
            if (timeScaleMinusButton != null)
            {
                timeScaleMinusButton.onClick.AddListener(HandleTimeScaleMinus);
                timeScaleButtonsBound = true;
                RegisterHoldRepeat(timeScaleMinusButton, HandleTimeScaleMinus);
            }

            if (timeScalePlusButton != null)
            {
                timeScalePlusButton.onClick.AddListener(HandleTimeScalePlus);
                timeScaleButtonsBound = true;
                RegisterHoldRepeat(timeScalePlusButton, HandleTimeScalePlus);
            }

            if (!timeScaleButtonsBound)
            {
                HelpLogs.Warn("Gui", "Missing time scale buttons.");
            }

            bool realismButtonsBound = false;
            if (realismMinusButton != null)
            {
                realismMinusButton.onClick.AddListener(HandleRealismMinus);
                realismButtonsBound = true;
                RegisterHoldRepeat(realismMinusButton, HandleRealismMinus);
            }

            if (realismPlusButton != null)
            {
                realismPlusButton.onClick.AddListener(HandleRealismPlus);
                realismButtonsBound = true;
                RegisterHoldRepeat(realismPlusButton, HandleRealismPlus);
            }

            if (!realismButtonsBound)
            {
                HelpLogs.Warn("Gui", "Missing realism buttons.");
            }

            isBound = timeScaleButtonsBound || realismButtonsBound;
        }

        /// <summary>
        /// Unbind button events when this component is destroyed.
        /// </summary>
        public void Unbind()
        {
            if (!isBound)
            {
                return;
            }

            if (timeScaleMinusButton != null)
            {
                timeScaleMinusButton.onClick.RemoveListener(HandleTimeScaleMinus);
            }

            if (timeScalePlusButton != null)
            {
                timeScalePlusButton.onClick.RemoveListener(HandleTimeScalePlus);
            }

            if (realismMinusButton != null)
            {
                realismMinusButton.onClick.RemoveListener(HandleRealismMinus);
            }

            if (realismPlusButton != null)
            {
                realismPlusButton.onClick.RemoveListener(HandleRealismPlus);
            }

            ClearHoldRepeats();
            isBound = false;
        }
        #endregion

        #region Hold Repeat Helpers
        private void RegisterHoldRepeat(Button? _button, Action _action)
        {
            if (!enableHoldRepeat || _button == null)
            {
                return;
            }

            for (int _i = 0; _i < holdRepeats.Count; _i++)
            {
                if (holdRepeats[_i].Button == _button)
                {
                    return;
                }
            }

            HoldRepeatState _state = new HoldRepeatState
            {
                Button = _button,
                Action = _action,
                IsHeld = false,
                NextRepeatTime = 0f
            };

            EventTrigger _trigger = _button.GetComponent<EventTrigger>();
            if (_trigger == null)
            {
                _trigger = _button.gameObject.AddComponent<EventTrigger>();
            }

            if (_trigger.triggers == null)
            {
                _trigger.triggers = new List<EventTrigger.Entry>();
            }

            _state.Trigger = _trigger;
            _state.PointerDownEntry = AddTrigger(_trigger, EventTriggerType.PointerDown, () => StartHold(_state));
            _state.PointerUpEntry = AddTrigger(_trigger, EventTriggerType.PointerUp, () => StopHold(_state));
            _state.PointerExitEntry = AddTrigger(_trigger, EventTriggerType.PointerExit, () => StopHold(_state));
            holdRepeats.Add(_state);
        }

        private void StartHold(HoldRepeatState _state)
        {
            if (_state.Button == null || !_state.Button.interactable)
            {
                return;
            }

            _state.IsHeld = true;
            _state.NextRepeatTime = Time.unscaledTime + Mathf.Max(0f, holdRepeatInitialDelay);
        }

        private void StopHold(HoldRepeatState _state)
        {
            _state.IsHeld = false;
        }

        private void ClearHoldRepeats()
        {
            for (int _i = 0; _i < holdRepeats.Count; _i++)
            {
                HoldRepeatState _state = holdRepeats[_i];
                _state.IsHeld = false;

                if (_state.Trigger == null || _state.Trigger.triggers == null)
                {
                    continue;
                }

                if (_state.PointerDownEntry != null)
                {
                    _state.Trigger.triggers.Remove(_state.PointerDownEntry);
                }

                if (_state.PointerUpEntry != null)
                {
                    _state.Trigger.triggers.Remove(_state.PointerUpEntry);
                }

                if (_state.PointerExitEntry != null)
                {
                    _state.Trigger.triggers.Remove(_state.PointerExitEntry);
                }
            }

            holdRepeats.Clear();
        }

        private static EventTrigger.Entry AddTrigger(EventTrigger _trigger, EventTriggerType _type, Action _action)
        {
            EventTrigger.Entry _entry = new EventTrigger.Entry
            {
                eventID = _type
            };
            _entry.callback.AddListener(_ => _action());
            _trigger.triggers.Add(_entry);
            return _entry;
        }
        #endregion

        #region Event Handlers
        private void HandleTimeScaleMinus()
        {
            Gui.NotifyTimeScaleStepRequested(-1);
        }

        private void HandleTimeScalePlus()
        {
            Gui.NotifyTimeScaleStepRequested(1);
        }

        private void HandleRealismMinus()
        {
            Gui.NotifyRealismStepRequested(-1);
        }

        private void HandleRealismPlus()
        {
            Gui.NotifyRealismStepRequested(1);
        }

        #endregion
    }
}
