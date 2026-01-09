#nullable enable
using System;
using System.Collections.Generic;
using Assets.Scripts.Guis;
using Assets.Scripts.Helpers.Debugging;
using Assets.Scripts.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Assets.Scripts.Cameras
{
    /// <summary>
    /// Select solar objects by raycasting from the screen position.
    /// </summary>
    public sealed class SolarObjectSelectionInput : MonoBehaviour
    {
        #region Serialized Fields
        [Header("Selection")]
        private Camera? raycastCamera;
        private SolarSystemCamera? cameraController;
        [Tooltip("Max selection distance in world units. Higher = reach farther objects. Example: 5000")]
        [Range(1f, 100000f)]
        [SerializeField] private float maxRayDistance = 5000f;
        [Tooltip("Layer mask for selectable solar objects. Example: Everything")]
        [SerializeField] private LayerMask selectionLayerMask = ~0;
        [Tooltip("Ignore clicks/taps over UI. Example: true")]
        [SerializeField] private bool ignoreUi = true;
        [Tooltip("Focus the camera on the selected solar object. Example: true")]
        [SerializeField] private bool focusOnSelect = true;
        [Tooltip("Log selection debug messages. Example: true")]
        [SerializeField] private bool logSelections = true;
        [Tooltip("Seconds allowed between taps for a double-click selection. Example: 0.35")]
        [Range(0.05f, 1.0f)]
        [SerializeField] private float doubleClickMaxSeconds = 0.35f;

        [Header("Drag Orbit")]
        [Tooltip("Enable drag to orbit the camera. Example: true")]
        [SerializeField] private bool enableDragOrbit = true;
        [Tooltip("Drag distance in pixels before a drag is active. Example: 8")]
        [Range(1f, 200f)]
        [SerializeField] private float dragStartThresholdPixels = 8f;
        [Tooltip("Orbit sensitivity per pixel drag. Example: 0.021")]
        [Range(0.001f, 0.2f)]
        [SerializeField] private float dragOrbitSensitivity = 0.02125f;
        [Tooltip("Extra orbit sensitivity multiplier for touch input. Example: 1.04")]
        [Range(0.1f, 5f)]
        [SerializeField] private float touchOrbitSensitivityMultiplier = 1.04f;
        [Tooltip("Invert horizontal drag direction. Example: true")]
        [SerializeField] private bool invertDragX = true;
        [Tooltip("Invert vertical drag direction. Example: false")]
        [SerializeField] private bool invertDragY = false;

        [Header("Zoom Input")]
        [Tooltip("Enable mouse wheel zoom. Example: true")]
        [SerializeField] private bool enableScrollZoom = true;
        [Tooltip("Zoom steps per mouse wheel unit. Higher = faster zoom. Example: 1.87")]
        [Range(0.1f, 10f)]
        [SerializeField] private float scrollZoomStepsPerUnit = 1.87f;
        [Tooltip("Invert mouse wheel zoom direction. Example: true")]
        [SerializeField] private bool invertScrollZoom = true;
        [Tooltip("Enable pinch zoom on touch devices. Example: true")]
        [SerializeField] private bool enablePinchZoom = true;
        [Tooltip("Zoom steps per pixel of pinch delta. Higher = faster zoom. Example: 0.029")]
        [Range(0.001f, 0.2f)]
        [SerializeField] private float pinchZoomStepsPerPixel = 0.0287f;
        [Tooltip("Minimum pinch delta in pixels before zoom steps apply. Example: 2")]
        [Range(0.1f, 50f)]
        [SerializeField] private float pinchZoomDeadZonePixels = 2f;
        [Tooltip("Invert pinch zoom direction. Example: true")]
        [SerializeField] private bool invertPinchZoom = true;
        #endregion

        #region Runtime State
        private bool isPointerDown = false;
        private bool isDragging = false;
        private bool pointerBlockedByUi = false;
        private bool pointerDownOverBadge = false;
        private int activePointerId = -1;
        private Vector2 pointerDownScreenPos = Vector2.zero;
        private Vector2 lastPointerScreenPos = Vector2.zero;
        private float scrollZoomAccumulator = 0f;
        private float pinchZoomAccumulator = 0f;
        private bool isPinching = false;
        private float lastPinchDistance = 0f;
        private SolarObject? lastSelectedSolarObject = null;
        private float lastSelectTime = -1f;
        private readonly List<RaycastResult> uiRaycastResults = new();
        #endregion

        #region Public API
        /// <summary>
        /// Layer mask used for solar object selection raycasts.
        /// </summary>
        public LayerMask SelectionLayerMask => selectionLayerMask;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            raycastCamera = Camera.main;

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<SolarSystemCamera>();
                if (cameraController == null)
                {
                    HelpLogs.Warn("Selection", "SolarSystemCamera not found. Selection will only log hits.");
                }
            }
        }

        private void Update()
        {
            HandleScrollZoom();

            if (HandlePinchZoom())
            {
                if (isPointerDown)
                {
                    ResetPointerState();
                }

                return;
            }

            if (!isPointerDown)
            {
                if (TryGetPointerDown(out Vector2 _screenPos, out int _pointerId))
                {
                    StartPointer(_screenPos, _pointerId);
                }

                return;
            }

            if (TryGetPointerHold(activePointerId, out Vector2 _holdPos))
            {
                HandlePointerDrag(_holdPos);
            }

            if (TryGetPointerUp(activePointerId, out Vector2 _upPos))
            {
                HandlePointerUp(_upPos);
            }
        }
        #endregion

        #region Pointer Handling
        private void StartPointer(Vector2 _screenPos, int _pointerId)
        {
            activePointerId = _pointerId;
            pointerDownScreenPos = _screenPos;
            lastPointerScreenPos = _screenPos;
            isPointerDown = true;
            isDragging = false;
            pointerDownOverBadge = IsPointerOverBadge(_pointerId, _screenPos);
            pointerBlockedByUi = ignoreUi && IsPointerOverUi(_pointerId) && !pointerDownOverBadge;
        }

        private void HandlePointerDrag(Vector2 _screenPos)
        {
            if (!isPointerDown || pointerBlockedByUi)
            {
                return;
            }

            Vector2 _totalDelta = _screenPos - pointerDownScreenPos;
            float _threshold = Mathf.Max(1f, dragStartThresholdPixels);
            if (!isDragging && _totalDelta.sqrMagnitude >= _threshold * _threshold)
            {
                isDragging = true;
            }

            if (isDragging && enableDragOrbit)
            {
                Vector2 _frameDelta = _screenPos - lastPointerScreenPos;
                Vector2 _orbitDelta = GetOrbitDeltaFromDrag(_frameDelta, IsTouchPointer(activePointerId));
                if (_orbitDelta != Vector2.zero)
                {
                    Gui.NotifyCameraOrbitStepRequested(_orbitDelta);
                }
            }

            lastPointerScreenPos = _screenPos;
        }

        private void HandlePointerUp(Vector2 _screenPos)
        {
            if (isPointerDown && !pointerBlockedByUi && !isDragging && !pointerDownOverBadge)
            {
                TrySelectAtPosition(_screenPos);
            }

            ResetPointerState();
        }

        private void ResetPointerState()
        {
            isPointerDown = false;
            isDragging = false;
            pointerBlockedByUi = false;
            pointerDownOverBadge = false;
            activePointerId = -1;
            pointerDownScreenPos = Vector2.zero;
            lastPointerScreenPos = Vector2.zero;
        }

        private Vector2 GetOrbitDeltaFromDrag(Vector2 _dragDelta, bool _isTouch)
        {
            float _touchMultiplier = _isTouch ? Mathf.Max(0.1f, touchOrbitSensitivityMultiplier) : 1.0f;
            float _scale = Mathf.Max(0.0001f, dragOrbitSensitivity) * _touchMultiplier;
            float _x = _dragDelta.x * _scale;
            float _y = _dragDelta.y * _scale;

            if (invertDragX)
            {
                _x = -_x;
            }

            if (invertDragY)
            {
                _y = -_y;
            }

            return new Vector2(_x, _y);
        }

        private static bool IsTouchPointer(int _pointerId)
        {
            return _pointerId >= 0;
        }

        private void HandleScrollZoom()
        {
            if (!enableScrollZoom)
            {
                return;
            }

            if (!TryGetScrollDelta(out float _scrollDelta))
            {
                return;
            }

            if (Mathf.Approximately(_scrollDelta, 0f))
            {
                return;
            }

            if (invertScrollZoom)
            {
                _scrollDelta = -_scrollDelta;
            }

            float _stepsPerUnit = Mathf.Max(0.01f, scrollZoomStepsPerUnit);
            scrollZoomAccumulator += _scrollDelta * _stepsPerUnit;
            EmitZoomStepsFromAccumulator(ref scrollZoomAccumulator);
        }

        private bool HandlePinchZoom()
        {
            if (!enablePinchZoom)
            {
                ResetPinchState();
                return false;
            }

            if (!TryGetPinchState(
                    out float _distance,
                    out int _pointerIdA,
                    out int _pointerIdB,
                    out Vector2 _posA,
                    out Vector2 _posB
                ))
            {
                ResetPinchState();
                return false;
            }

            if (ignoreUi && (IsPointerBlockedByUi(_pointerIdA, _posA) || IsPointerBlockedByUi(_pointerIdB, _posB)))
            {
                ResetPinchState();
                return true;
            }

            if (!isPinching)
            {
                isPinching = true;
                lastPinchDistance = _distance;
                return true;
            }

            float _delta = _distance - lastPinchDistance;
            lastPinchDistance = _distance;

            float _deadZone = Mathf.Max(0.01f, pinchZoomDeadZonePixels);
            if (Mathf.Abs(_delta) < _deadZone)
            {
                return true;
            }

            if (invertPinchZoom)
            {
                _delta = -_delta;
            }

            float _stepsPerPixel = Mathf.Max(0.0001f, pinchZoomStepsPerPixel);
            pinchZoomAccumulator += _delta * _stepsPerPixel;
            EmitZoomStepsFromAccumulator(ref pinchZoomAccumulator);
            return true;
        }

        private void EmitZoomStepsFromAccumulator(ref float _accumulator)
        {
            int _steps = Mathf.FloorToInt(Mathf.Abs(_accumulator));
            if (_steps <= 0)
            {
                return;
            }

            int _direction = _accumulator >= 0f ? 1 : -1;
            for (int _i = 0; _i < _steps; _i++)
            {
                Gui.NotifyCameraZoomStepRequested(_direction);
            }

            _accumulator -= _direction * _steps;
        }

        private void ResetPinchState()
        {
            isPinching = false;
            lastPinchDistance = 0f;
        }

        private void TrySelectAtPosition(Vector2 _screenPos)
        {
            if (raycastCamera == null)
            {
                raycastCamera = Camera.main;
                if (raycastCamera == null)
                {
                    HelpLogs.Warn("Selection", "No camera available for raycasting.");
                    return;
                }
            }

            Ray _ray = raycastCamera.ScreenPointToRay(_screenPos);
            bool _hit = Physics.Raycast(
                _ray,
                out RaycastHit _hitInfo,
                Mathf.Max(0.01f, maxRayDistance),
                selectionLayerMask,
                QueryTriggerInteraction.Ignore
            );

            if (!_hit)
            {
                if (logSelections)
                {
                    HelpLogs.Log("Selection", "No solar object hit.");
                }
                lastSelectedSolarObject = null;
                lastSelectTime = -1f;
                return;
            }

            SolarObject? _solarObject = _hitInfo.collider.GetComponentInParent<SolarObject>();
            if (_solarObject == null)
            {
                if (logSelections)
                {
                    HelpLogs.Log("Selection", $"Hit '{_hitInfo.collider.name}' but no SolarObject found.");
                }
                lastSelectedSolarObject = null;
                lastSelectTime = -1f;
                return;
            }

            float _now = Time.unscaledTime;
            float _elapsed = _now - lastSelectTime;
            bool _isDoubleClick =
                lastSelectedSolarObject != null &&
                lastSelectedSolarObject == _solarObject &&
                _elapsed >= 0f &&
                _elapsed <= doubleClickMaxSeconds;
            lastSelectedSolarObject = _solarObject;
            lastSelectTime = _now;

            if (logSelections)
            {
                HelpLogs.Log("Selection", $"Selected '{_solarObject.name}' ({_solarObject.Id}).");
            }

            if (focusOnSelect && cameraController != null)
            {
                cameraController.FocusOn(_solarObject);
                if (_isDoubleClick)
                {
                    cameraController.ToggleFocusAxisLines(_solarObject);
                }
            }
        }
        #endregion

        #region Input Helpers
        private static bool IsPointerOverUi(int _pointerId)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            if (_pointerId >= 0)
            {
                return EventSystem.current.IsPointerOverGameObject(_pointerId);
            }

            return EventSystem.current.IsPointerOverGameObject();
        }

        private bool IsPointerBlockedByUi(int _pointerId, Vector2 _screenPos)
        {
            if (!IsPointerOverUi(_pointerId))
            {
                return false;
            }

            return !IsPointerOverBadge(_pointerId, _screenPos);
        }

        private bool IsPointerOverBadge(int _pointerId, Vector2 _screenPos)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            PointerEventData _eventData = new PointerEventData(EventSystem.current)
            {
                position = _screenPos,
                pointerId = _pointerId,
            };

            uiRaycastResults.Clear();
            EventSystem.current.RaycastAll(_eventData, uiRaycastResults);
            for (int _i = 0; _i < uiRaycastResults.Count; _i++)
            {
                GameObject _hit = uiRaycastResults[_i].gameObject;
                if (_hit == null)
                {
                    continue;
                }

                if (_hit.GetComponent<Button>() == null)
                {
                    continue;
                }

                if (_hit.name.StartsWith("Badge_", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetPointerDown(out Vector2 _screenPos, out int _pointerId)
        {
            _screenPos = Vector2.zero;
            _pointerId = -1;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                _screenPos = Mouse.current.position.ReadValue();
                return true;
            }

            if (Touchscreen.current != null)
            {
                foreach (TouchControl _touch in Touchscreen.current.touches)
                {
                    if (_touch == null)
                    {
                        continue;
                    }

                    if (_touch.press.wasPressedThisFrame)
                    {
                        _screenPos = _touch.position.ReadValue();
                        _pointerId = _touch.touchId.ReadValue();
                        return true;
                    }
                }
            }
#else
            if (Input.GetMouseButtonDown(0))
            {
                _screenPos = Input.mousePosition;
                return true;
            }

            Touch[] _touches = Input.touches;
            for (int _i = 0; _i < _touches.Length; _i++)
            {
                Touch _touch = _touches[_i];
                if (_touch.phase == TouchPhase.Began)
                {
                    _screenPos = _touch.position;
                    _pointerId = _touch.fingerId;
                    return true;
                }
            }
#endif

            return false;
        }

        private static bool TryGetScrollDelta(out float _delta)
        {
            _delta = 0f;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null)
            {
                return false;
            }

            _delta = Mouse.current.scroll.ReadValue().y;
#else
            _delta = Input.mouseScrollDelta.y;
#endif
            return !Mathf.Approximately(_delta, 0f);
        }

        private static bool TryGetPinchState(
            out float _distance,
            out int _pointerIdA,
            out int _pointerIdB,
            out Vector2 _posA,
            out Vector2 _posB
        )
        {
            _distance = 0f;
            _pointerIdA = -1;
            _pointerIdB = -1;
            _posA = Vector2.zero;
            _posB = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current == null)
            {
                return false;
            }

            TouchControl? _first = null;
            TouchControl? _second = null;
            foreach (TouchControl _touch in Touchscreen.current.touches)
            {
                if (_touch == null || !_touch.press.isPressed)
                {
                    continue;
                }

                if (_first == null)
                {
                    _first = _touch;
                }
                else
                {
                    _second = _touch;
                    break;
                }
            }

            if (_first == null || _second == null)
            {
                return false;
            }

            _posA = _first.position.ReadValue();
            _posB = _second.position.ReadValue();
            _pointerIdA = _first.touchId.ReadValue();
            _pointerIdB = _second.touchId.ReadValue();
            _distance = Vector2.Distance(_posA, _posB);
            return true;
#else
            Touch[] _touches = Input.touches;
            if (_touches == null || _touches.Length < 2)
            {
                return false;
            }

            Touch _first = _touches[0];
            Touch _second = _touches[1];
            if (_first.phase == TouchPhase.Canceled || _first.phase == TouchPhase.Ended)
            {
                return false;
            }

            if (_second.phase == TouchPhase.Canceled || _second.phase == TouchPhase.Ended)
            {
                return false;
            }

            _posA = _first.position;
            _posB = _second.position;
            _pointerIdA = _first.fingerId;
            _pointerIdB = _second.fingerId;
            _distance = Vector2.Distance(_posA, _posB);
            return true;
#endif
        }

        private static bool TryGetPointerHold(int _pointerId, out Vector2 _screenPos)
        {
            _screenPos = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (_pointerId < 0 && Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                _screenPos = Mouse.current.position.ReadValue();
                return true;
            }

            if (_pointerId >= 0 && Touchscreen.current != null)
            {
                foreach (TouchControl _touch in Touchscreen.current.touches)
                {
                    if (_touch == null)
                    {
                        continue;
                    }

                    int _id = _touch.touchId.ReadValue();
                    if (_id != _pointerId)
                    {
                        continue;
                    }

                    if (_touch.press.isPressed)
                    {
                        _screenPos = _touch.position.ReadValue();
                        return true;
                    }
                }
            }
#else
            if (_pointerId < 0 && Input.GetMouseButton(0))
            {
                _screenPos = Input.mousePosition;
                return true;
            }

            Touch[] _touches = Input.touches;
            for (int _i = 0; _i < _touches.Length; _i++)
            {
                Touch _touch = _touches[_i];
                if (_touch.fingerId != _pointerId)
                {
                    continue;
                }

                if (_touch.phase == TouchPhase.Moved ||
                    _touch.phase == TouchPhase.Stationary ||
                    _touch.phase == TouchPhase.Began)
                {
                    _screenPos = _touch.position;
                    return true;
                }
            }
#endif

            return false;
        }

        private static bool TryGetPointerUp(int _pointerId, out Vector2 _screenPos)
        {
            _screenPos = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (_pointerId < 0 && Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                _screenPos = Mouse.current.position.ReadValue();
                return true;
            }

            if (_pointerId >= 0 && Touchscreen.current != null)
            {
                foreach (TouchControl _touch in Touchscreen.current.touches)
                {
                    if (_touch == null)
                    {
                        continue;
                    }

                    int _id = _touch.touchId.ReadValue();
                    if (_id != _pointerId)
                    {
                        continue;
                    }

                    if (_touch.press.wasReleasedThisFrame)
                    {
                        _screenPos = _touch.position.ReadValue();
                        return true;
                    }
                }
            }
#else
            if (_pointerId < 0 && Input.GetMouseButtonUp(0))
            {
                _screenPos = Input.mousePosition;
                return true;
            }

            Touch[] _touches = Input.touches;
            for (int _i = 0; _i < _touches.Length; _i++)
            {
                Touch _touch = _touches[_i];
                if (_touch.fingerId != _pointerId)
                {
                    continue;
                }

                if (_touch.phase == TouchPhase.Ended || _touch.phase == TouchPhase.Canceled)
                {
                    _screenPos = _touch.position;
                    return true;
                }
            }
#endif

            return false;
        }
        #endregion
    }
}
