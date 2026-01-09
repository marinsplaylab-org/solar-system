#nullable enable
using System.Collections.Generic;
using Assets.Scripts.Cameras;
using Assets.Scripts.Helpers.Debugging;
using Assets.Scripts.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Guis
{
    /// <summary>
    /// Renders clickable icon badges above solar objects.
    /// </summary>
    public sealed class SolarObjectBadgeManager : MonoBehaviour
    {
        private static float SmoothStep01(float _t) => _t * _t * (3f - 2f * _t);
        #region Serialized Fields
        [Header("Badge Layout")]
        [Tooltip("Base badge size in pixels before scaling. Example: 42")]
        [Range(8f, 256f)]
        [SerializeField] private float badgeBaseSizePixels = 42f;
        [Tooltip("Base vertical offset above the object center in Unity units. Example: 0.05")]
        [SerializeField] private float badgeWorldOffset = 0f;
        [Tooltip("Extra vertical offset based on object diameter. Example: 0.6")]
        [Range(0f, 5f)]
        [SerializeField] private float badgeWorldOffsetDiameterMultiplier = 0f;
        [Tooltip("Padding beyond screen edges before hiding badges. Example: 12")]
        [Range(0f, 200f)]
        [SerializeField] private float screenEdgePaddingPixels = 12f;
        [Tooltip("Render badges behind other UI elements on the same canvas. Example: true")]
        [SerializeField] private bool renderBadgesBehindUi = true;
        [Tooltip("Sort visible badges by camera distance so nearer badges render on top. Example: true")]
        [SerializeField] private bool sortBadgesByDistance = true;

        [Header("Badge Style")]
        [Tooltip("Sprite brightness multiplier for all badges. Example: 3")]
        [Range(0.1f, 5f)]
        [SerializeField] private float badgeSpriteBrightness = 3.0f;
        [Tooltip("Visible badge alpha. Example: 1")]
        [Range(0f, 1f)]
        [SerializeField] private float badgeMinAlpha = 1.0f;
        [Tooltip("Force opaque alpha for badge sprites. Requires Read/Write enabled on badge circle textures. Example: true")]
        [SerializeField] private bool forceOpaqueSpriteAlpha = false;

        [Header("Badge Visibility")]
        [Tooltip("Enable or disable all badge UI. Example: true")]
        [SerializeField] private bool badgesEnabled = true;

        [Header("Badge Circle")]
        [Tooltip("Optional circle sprite override for badges. Example: CircleSprite")]
        [SerializeField] private Sprite? badgeCircleSprite;
        [Tooltip("Circle color tint. Example: (1, 1, 1, 0.9)")]
        [SerializeField] private Color badgeCircleColor = new Color(1f, 1f, 1f, 0.9f);

        [Header("Badge Label")]
        [Tooltip("Show object name label under the badge. Example: true")]
        [SerializeField] private bool badgeLabelEnabled = true;
        [Tooltip("Optional TMP font for badge labels. Example: Oxanium SDF")]
        [SerializeField] private TMP_FontAsset? badgeLabelFont;
        [Tooltip("Badge label font size in pixels. Example: 16")]
        [Range(6f, 48f)]
        [SerializeField] private float badgeLabelFontSize = 16f;
        [Tooltip("Badge label color. Example: (1, 1, 1, 1)")]
        [SerializeField] private Color badgeLabelColor = Color.white;
        [Tooltip("Badge label offset from the badge center in pixels. Example: (0, -30)")]
        [SerializeField] private Vector2 badgeLabelOffsetPixels = new Vector2(0f, -30f);
        [Tooltip("Max label width in pixels before overflow. Example: 140")]
        [Range(40f, 400f)]
        [SerializeField] private float badgeLabelMaxWidthPixels = 140f;

        [Header("Badge Toggle Label")]
        [Tooltip("Optional toggle button name to auto-find the label. Example: Solar_Object_Badges_Toggle_Button")]
        [SerializeField] private string badgesToggleButtonName = "Solar_Object_Badges_Toggle_Button";
        [Tooltip("Optional TMP label for the badge toggle. Example: Text")]
        [SerializeField] private TMP_Text? badgesToggleLabel;
        [Tooltip("Optional legacy UI Text label for the badge toggle. Example: Text")]
        [SerializeField] private Text? badgesToggleLabelLegacy;
        [Tooltip("Label text when badges are enabled. Example: UI Badges (On)")]
        [SerializeField] private string badgesToggleLabelOn = "UI Badges (On)";
        [Tooltip("Label text when badges are disabled. Example: UI Badges (Off)")]
        [SerializeField] private string badgesToggleLabelOff = "UI Badges (Off)";

        [Header("Focus Visibility")]
        [Tooltip("Hide badges when focus zoom normalized is at or above this fraction. Example: 0.98")]
        [Range(0f, 1f)]
        [SerializeField] private float focusZoomHideThreshold = 0.99f;

        [Header("Focus Non-Sun Visibility")]
        [Tooltip("Hide non-sun badges when focus zoom normalized is at or above this fraction. Example: 0.85")]
        [Range(0f, 1f)]
        [SerializeField] private float hideNonSunFocusZoomThreshold = 0.85f;
        [Tooltip("Enable hiding non-sun badges when zoomed far out in focus mode. Example: true")]
        [SerializeField] private bool hideNonSunBadgesOnFocusZoomOut = true;
        [Tooltip("Scale multiplier for moon badges when their planet is focused. Example: 0.6")]
        [Range(0.1f, 1f)]
        [SerializeField] private float focusMoonBadgeScaleMultiplier = 0.6f;
        [Tooltip("Show focus planet moon badges only when the camera is within this focus zoom fraction. Example: 0.4")]
        [Range(0f, 1f)]
        [SerializeField] private float focusMoonShowZoomThreshold = 0.2f;
        [Tooltip("Scale multiplier for moon badges when focused planet is zoomed in. Example: 2.5")]
        [Range(0.1f, 5f)]
        [SerializeField] private float focusMoonBadgeCloseScaleMultiplier = 2.5f;

        [Header("Overview Visibility")]
        [Tooltip("Hide non-sun badges when overview zoom normalized is at or above this fraction. Example: 0.42")]
        [Range(0f, 1f)]
        [SerializeField] private float hideNonSunOverviewZoomThreshold = 0.42f;
        [Tooltip("Enable hiding non-sun badges when zoomed far out in overview. Example: true")]
        [SerializeField] private bool hideNonSunBadgesOnOverviewZoomOut = true;
        [Tooltip("Keep planet badges visible in overview regardless of zoom distance. Example: true")]
        [SerializeField] private bool alwaysShowPlanetBadgesInOverview = true;
        [Tooltip("Hide planet badges when overview zoom normalized is at or above this fraction. Example: 0.95")]
        [Range(0f, 1f)]
        [SerializeField] private float overviewPlanetHideZoomThreshold = 0.95f;

        [Header("Visibility Scaling")]
        [Tooltip("Distance multiplier applied to hide/show thresholds. Higher hides earlier. Example: 3")]
        [Range(0.1f, 10f)]
        [SerializeField] private float badgeVisibilityDistanceScale = 8f;

        [Header("Sun Scaling")]
        [Tooltip("Base scale multiplier for the Sun badge. Higher = larger Sun badge at all scales. Example: 1.25")]
        [Range(1f, 10f)]
        [SerializeField] private float sunBadgeScaleMultiplier = 1.25f;
        [Tooltip("Minimum scale multiplier for the Sun badge before applying the base scale. Example: 1")]
        [Range(1f, 5f)]
        [SerializeField] private float sunBadgeMinScaleMultiplier = 1.0f;
        [Tooltip("Minimum distance-based scale for the Sun badge. Example: 1")]
        [Range(0.1f, 5f)]
        [SerializeField] private float sunBadgeMinDistanceScale = 1.0f;
        [Tooltip("Hide the Sun badge sooner as the camera gets closer. Example: 1.6")]
        [Range(1f, 5f)]
        [SerializeField] private float sunBadgeHideDistanceMultiplier = 2.0f;
        [Tooltip("Only show the Sun circle when non-sun badges are hidden. Example: true")]
        [SerializeField] private bool showSunCircleOnlyWhenNonSunHidden = true;

        [Header("Sun Priority")]
        [Tooltip("Prefer the Sun badge to render on top when zoomed out. Example: true")]
        [SerializeField] private bool enableSunPriorityOnZoomOut = false;
        [Tooltip("Overview zoom normalized threshold for Sun render priority. Example: 0.6")]
        [Range(0f, 1f)]
        [SerializeField] private float sunBadgePriorityOverviewZoomNormalized = 0.6f;
        [Tooltip("Focus zoom normalized threshold for Sun render priority. Example: 0.85")]
        [Range(0f, 1f)]
        [SerializeField] private float sunBadgePriorityFocusZoomNormalized = 0.85f;

        [Header("Sun Max Zoom Scale")]
        [Tooltip("Sun badge scale multiplier at max zoom. Example: 2")]
        [Range(1f, 10f)]
        [SerializeField] private float sunBadgeMaxZoomScaleMultiplier = 2f;
        [Tooltip("Overview zoom normalized threshold for Sun max zoom scaling. Example: 0.95")]
        [Range(0f, 1f)]
        [SerializeField] private float sunBadgeMaxZoomOverviewThreshold = 0.95f;
        [Tooltip("Focus zoom normalized threshold for Sun max zoom scaling. Example: 0.95")]
        [Range(0f, 1f)]
        [SerializeField] private float sunBadgeMaxZoomFocusThreshold = 0.95f;

        [Header("Badge Material")]
        [Tooltip("Optional material override for badge images. Example: UI/BadgeOpaque material")]
        [SerializeField] private Material? badgeMaterial;
        [Tooltip("Alpha clip threshold for the opaque badge shader. Example: 0.01")]
        [Range(0f, 1f)]
        [SerializeField] private float badgeAlphaClipThreshold = 0.25f;

        [Header("Occlusion")]
        [Tooltip("Hide badges when another solar object blocks the view ray. Example: true")]
        [SerializeField] private bool enableOcclusionCheck = true;
        [Tooltip("Layer mask for occlusion raycasts. 0 = use selection mask. Example: Everything")]
        [SerializeField] private LayerMask occlusionLayerMask = 0;
        [Tooltip("Extra distance added to the ray length for occlusion checks. Example: 0.05")]
        [Range(0f, 2f)]
        [SerializeField] private float occlusionDistancePadding = 0.05f;
        [Tooltip("Seconds between occlusion checks. 0 = every update. Example: 0.05")]
        [Range(0f, 1f)]
        [SerializeField] private float occlusionCheckIntervalSeconds = 0.05f;

        [Header("Distance Scaling")]
        [Tooltip("Minimum scale near the camera. Example: 0.8")]
        [Range(0.1f, 2f)]
        [SerializeField] private float badgeMinScale = 0.8f;
        [Tooltip("Maximum scale when far away. Example: 1.6")]
        [Range(0.2f, 5f)]
        [SerializeField] private float badgeMaxScale = 1.6f;
        [Tooltip("Hide badge when camera distance is below diameter * multiplier. Example: 2.5")]
        [Range(0.1f, 50f)]
        [SerializeField] private float badgeHideDistanceMultiplier = 2.5f;
        [Tooltip("Max scale when camera distance is above diameter * multiplier. Example: 20")]
        [Range(1f, 200f)]
        [SerializeField] private float badgeShowDistanceMultiplier = 20f;
        [Tooltip("Absolute minimum hide distance in Unity units. Example: 0.25")]
        [Range(0.01f, 10f)]
        [SerializeField] private float badgeMinHideDistance = 0.25f;
        [Tooltip("Absolute minimum show distance in Unity units. Example: 6")]
        [Range(0.1f, 200f)]
        [SerializeField] private float badgeMinShowDistance = 6f;

        [Header("Size Scaling")]
        [Tooltip("Minimum size scale from object diameter. Example: 0.7")]
        [Range(0.1f, 5f)]
        [SerializeField] private float badgeDiameterMinScale = 0.7f;
        [Tooltip("Maximum size scale from object diameter. Example: 1.8")]
        [Range(0.1f, 10f)]
        [SerializeField] private float badgeDiameterMaxScale = 1.8f;
        [Tooltip("Exponent for diameter scaling curve. Lower = less extreme. Example: 0.7")]
        [Range(0.1f, 3f)]
        [SerializeField] private float badgeDiameterScaleExponent = 0.7f;
        [Tooltip("Hard cap for final badge scale. Example: 2")]
        [Range(0.1f, 10f)]
        [SerializeField] private float badgeMaxFinalScale = 1.4f;
        [Tooltip("Hard cap for non-sun badge scale. Example: 1")]
        [Range(0.1f, 10f)]
        [SerializeField] private float badgeMaxFinalScaleNonSun = 1.0f;

        [Header("Moon Overlap")]
        [Tooltip("Hide moon badges if they overlap primary badges within this pixel radius. Example: 40")]
        [Range(4f, 200f)]
        [SerializeField] private float moonOverlapHidePixels = 40f;
        [Tooltip("Only hide moon badges when far (distance >= diameter * multiplier). Example: 8")]
        [Range(1f, 200f)]
        [SerializeField] private float moonOverlapHideDistanceMultiplier = 8f;
        [Tooltip("Absolute minimum overlap distance for hiding moons. Example: 4")]
        [Range(0.1f, 200f)]
        [SerializeField] private float moonOverlapMinDistance = 4f;

        [Header("Performance")]
        [Tooltip("Seconds between badge updates. 0 = every frame. Example: 0.05")]
        [Range(0f, 1f)]
        [SerializeField] private float updateIntervalSeconds = 0f;
        #endregion

        #region Runtime State
        private sealed class BadgeEntry
        {
            public SolarObject SolarObject = null!;
            public RectTransform RectTransform = null!;
            public Image Image = null!;
            public TMP_Text? Label = null;
            public CanvasGroup CanvasGroup = null!;
            public Vector2 ScreenPosition;
            public float Scale;
            public float CameraDistance;
            public bool Visible;
            public bool IsOccluded;
        }

        private readonly List<BadgeEntry> badges = new();
        private readonly Dictionary<SolarObject, BadgeEntry> badgesByObject = new();
        private readonly Dictionary<Sprite, Sprite> opaqueSpriteCache = new();
        private readonly HashSet<Sprite> opaqueSpriteFailures = new();
        private readonly List<Texture2D> opaqueSpriteTextures = new();
        private readonly RaycastHit[] occlusionHits = new RaycastHit[32];
        private readonly List<BadgeEntry> orderedBadges = new();
        private Material? runtimeBadgeMaterial;
        private Sprite? fallbackCircleSprite;
        private SolarSystemSimulator? simulator;
        private SolarSystemCamera? cameraController;
        private Camera? worldCamera;
        private Canvas? badgeCanvas;
        private RectTransform? badgeRoot;
        private bool isInitialized = false;
        private bool opaqueSpriteWarningLogged = false;
        private bool missingCircleSpriteLogged = false;
        private float updateTimer = 0f;
        private float occlusionTimer = 0f;
        private float minDiameterUnity = 1f;
        private float maxDiameterUnity = 1f;
        private static readonly int AlphaThresholdId = Shader.PropertyToID("_AlphaThreshold");
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            ResolveSceneReferences();
            UpdateBadgesToggleLabel();
        }

        private void OnEnable()
        {
            BindSimulator();
        }

        private void Start()
        {
            TryBuildFromSimulator();
        }

        private void OnDestroy()
        {
            ReleaseOpaqueSprites();
            ReleaseBadgeMaterial();
        }

        private void OnDisable()
        {
            UnbindSimulator();
        }

        private void LateUpdate()
        {
            if (!isInitialized)
            {
                return;
            }

            if (!badgesEnabled)
            {
                return;
            }

            if (updateIntervalSeconds > 0f)
            {
                updateTimer += Time.unscaledDeltaTime;
                if (updateTimer < updateIntervalSeconds)
                {
                    return;
                }

                updateTimer = 0f;
            }

            UpdateBadges();
        }
        #endregion

        #region Public API
        /// <summary>
        /// True when badge UI is enabled.
        /// </summary>
        public bool BadgesEnabled => badgesEnabled;

        /// <summary>
        /// Enable or disable all badge UI.
        /// </summary>
        public void SetBadgesEnabled(bool _enabled)
        {
            if (badgesEnabled == _enabled)
            {
                return;
            }

            badgesEnabled = _enabled;
            ApplyBadgesEnabledState();
        }

        /// <summary>
        /// Toggle badge UI on or off.
        /// </summary>
        public void ToggleBadgesEnabled()
        {
            SetBadgesEnabled(!badgesEnabled);
        }
        #endregion

        #region Setup
        private void ResolveSceneReferences()
        {
            if (simulator == null)
            {
                simulator = FindFirstObjectByType<SolarSystemSimulator>();
            }

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<SolarSystemCamera>();
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (occlusionLayerMask.value == 0)
            {
                SolarObjectSelectionInput? _input = FindFirstObjectByType<SolarObjectSelectionInput>();
                if (_input != null)
                {
                    occlusionLayerMask = _input.SelectionLayerMask;
                }

                if (occlusionLayerMask.value == 0)
                {
                    occlusionLayerMask = ~0;
                }
            }

            ResolveBadgesToggleLabel();
        }

        private void BindSimulator()
        {
            if (simulator == null)
            {
                return;
            }

            simulator.SolarObjectsReady += HandleSolarObjectsReady;
        }

        private void UnbindSimulator()
        {
            if (simulator == null)
            {
                return;
            }

            simulator.SolarObjectsReady -= HandleSolarObjectsReady;
        }

        private void HandleSolarObjectsReady(IReadOnlyList<SolarObject> _objects)
        {
            BuildBadges(_objects);
        }

        private void TryBuildFromSimulator()
        {
            if (simulator == null)
            {
                simulator = FindFirstObjectByType<SolarSystemSimulator>();
                if (simulator == null)
                {
                    HelpLogs.Warn("Badges", "SolarSystemSimulator not found for badge setup.");
                    return;
                }
            }

            if (simulator.OrderedSolarObjects.Count == 0)
            {
                return;
            }

            BuildBadges(simulator.OrderedSolarObjects);
        }

        private void BuildBadges(IReadOnlyList<SolarObject> _objects)
        {
            if (_objects.Count == 0)
            {
                return;
            }

            EnsureCanvasAndRoot();
            if (badgeRoot == null)
            {
                HelpLogs.Warn("Badges", "Badge root not available.");
                return;
            }

            ClearBadges();

            minDiameterUnity = float.MaxValue;
            maxDiameterUnity = 0f;
            for (int _i = 0; _i < _objects.Count; _i++)
            {
                SolarObject _object = _objects[_i];
                if (_object == null)
                {
                    continue;
                }

                float _diameter = Mathf.Max(0.0001f, _object.BaseDiameterUnity);
                if (_diameter < minDiameterUnity)
                {
                    minDiameterUnity = _diameter;
                }

                if (_diameter > maxDiameterUnity)
                {
                    maxDiameterUnity = _diameter;
                }
            }

            if (minDiameterUnity == float.MaxValue)
            {
                minDiameterUnity = 1f;
                maxDiameterUnity = 1f;
            }

            for (int _i = 0; _i < _objects.Count; _i++)
            {
                SolarObject _object = _objects[_i];
                if (_object == null)
                {
                    continue;
                }

                BadgeEntry _entry = CreateBadgeEntry(_object, badgeRoot);
                badges.Add(_entry);
                badgesByObject[_object] = _entry;
            }

            ApplyBadgesEnabledState();
            isInitialized = true;
        }

        private void ClearBadges()
        {
            for (int _i = 0; _i < badges.Count; _i++)
            {
                BadgeEntry _entry = badges[_i];
                if (_entry.RectTransform != null)
                {
                    Destroy(_entry.RectTransform.gameObject);
                }
            }

            badges.Clear();
            badgesByObject.Clear();
        }

        private void EnsureCanvasAndRoot()
        {
            if (badgeCanvas == null)
            {
                Canvas _existing = FindFirstObjectByType<Canvas>();
                if (_existing != null && _existing.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    badgeCanvas = _existing;
                }
                else
                {
                    GameObject _canvasObject = new GameObject(
                        "SolarObjectBadges_Canvas",
                        typeof(Canvas),
                        typeof(CanvasScaler),
                        typeof(GraphicRaycaster)
                    );
                    badgeCanvas = _canvasObject.GetComponent<Canvas>();
                    badgeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    badgeCanvas.sortingOrder = renderBadgesBehindUi ? -10 : 50;

                    CanvasScaler _scaler = _canvasObject.GetComponent<CanvasScaler>();
                    _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    _scaler.referenceResolution = new Vector2(1920f, 1080f);
                    _scaler.matchWidthOrHeight = 0.5f;
                }
            }

            if (badgeCanvas != null && badgeCanvas.GetComponent<GraphicRaycaster>() == null)
            {
                badgeCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            if (badgeRoot == null && badgeCanvas != null)
            {
                GameObject _root = new GameObject("SolarObjectBadges_Root", typeof(RectTransform));
                _root.transform.SetParent(badgeCanvas.transform, false);
                RectTransform _rect = _root.GetComponent<RectTransform>();
                _rect.anchorMin = Vector2.zero;
                _rect.anchorMax = Vector2.one;
                _rect.pivot = new Vector2(0.5f, 0.5f);
                _rect.sizeDelta = Vector2.zero;
                badgeRoot = _rect;
            }

            ApplyBadgeRootOrder();
        }

        private void ApplyBadgesEnabledState()
        {
            ResolveBadgesToggleLabel();
            bool _enabled = badgesEnabled;
            for (int _i = 0; _i < badges.Count; _i++)
            {
                BadgeEntry _entry = badges[_i];
                if (_entry.RectTransform == null)
                {
                    continue;
                }

                _entry.RectTransform.gameObject.SetActive(_enabled);
                if (!_enabled)
                {
                    _entry.CanvasGroup.alpha = 0f;
                    _entry.CanvasGroup.interactable = false;
                    _entry.CanvasGroup.blocksRaycasts = false;
                }
            }

            UpdateBadgesToggleLabel();
        }

        private void ResolveBadgesToggleLabel()
        {
            if (badgesToggleLabel != null || badgesToggleLabelLegacy != null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(badgesToggleButtonName))
            {
                return;
            }

            Transform? _buttonTransform = FindTransformByName(badgesToggleButtonName);
            if (_buttonTransform == null)
            {
                return;
            }

            badgesToggleLabel = _buttonTransform.GetComponentInChildren<TMP_Text>(true);
            if (badgesToggleLabel != null)
            {
                return;
            }

            badgesToggleLabelLegacy = _buttonTransform.GetComponentInChildren<Text>(true);
        }

        private void UpdateBadgesToggleLabel()
        {
            ResolveBadgesToggleLabel();

            string _label = badgesEnabled ? badgesToggleLabelOn : badgesToggleLabelOff;
            if (badgesToggleLabel != null && badgesToggleLabel.text != _label)
            {
                badgesToggleLabel.text = _label;
            }

            if (badgesToggleLabelLegacy != null && badgesToggleLabelLegacy.text != _label)
            {
                badgesToggleLabelLegacy.text = _label;
            }
        }

        private Transform? FindTransformByName(string _name)
        {
            Canvas[] _canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int _canvasIndex = 0; _canvasIndex < _canvases.Length; _canvasIndex++)
            {
                Canvas _canvas = _canvases[_canvasIndex];
                if (_canvas == null)
                {
                    continue;
                }

                Transform[] _children = _canvas.GetComponentsInChildren<Transform>(true);
                for (int _i = 0; _i < _children.Length; _i++)
                {
                    Transform _child = _children[_i];
                    if (string.Equals(_child.name, _name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return _child;
                    }
                }
            }

            return null;
        }

        private BadgeEntry CreateBadgeEntry(SolarObject _object, RectTransform _parent)
        {
            GameObject _badgeObject = new GameObject(
                $"Badge_{_object.Id}",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image),
                typeof(Button)
            );
            _badgeObject.transform.SetParent(_parent, false);

            RectTransform _rect = _badgeObject.GetComponent<RectTransform>();
            _rect.anchorMin = new Vector2(0.5f, 0.5f);
            _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.sizeDelta = new Vector2(badgeBaseSizePixels, badgeBaseSizePixels);

            CanvasGroup _group = _badgeObject.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            _group.ignoreParentGroups = true;

            Image _image = _badgeObject.GetComponent<Image>();
            Sprite? _sprite = ResolveBadgeSprite();
            _image.sprite = _sprite;
            _image.color = GetBadgeSpriteColor(_object);
            _image.preserveAspect = true;
            _image.raycastTarget = true;
            _image.material = ResolveBadgeMaterial();

            GameObject _labelObject = new GameObject(
                "BadgeLabel",
                typeof(RectTransform),
                typeof(TextMeshProUGUI)
            );
            _labelObject.transform.SetParent(_badgeObject.transform, false);

            RectTransform _labelRect = _labelObject.GetComponent<RectTransform>();
            _labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _labelRect.pivot = new Vector2(0.5f, 0.5f);
            _labelRect.anchoredPosition = badgeLabelOffsetPixels;
            _labelRect.sizeDelta = new Vector2(badgeLabelMaxWidthPixels, badgeLabelFontSize * 1.4f);

            TextMeshProUGUI _tmp = _labelObject.GetComponent<TextMeshProUGUI>();
            _tmp.text = ResolveBadgeLabelText(_object);
            _tmp.fontSize = badgeLabelFontSize;
            _tmp.color = badgeLabelColor;
            _tmp.alignment = TextAlignmentOptions.Center;
            _tmp.textWrappingMode = TextWrappingModes.NoWrap;
            _tmp.overflowMode = TextOverflowModes.Overflow;
            _tmp.raycastTarget = false;
            _tmp.enabled = badgeLabelEnabled;
            if (badgeLabelFont != null)
            {
                _tmp.font = badgeLabelFont;
            }

            TMP_Text _label = _tmp;

            Button _button = _badgeObject.GetComponent<Button>();
            _button.transition = Selectable.Transition.None;
            _button.targetGraphic = _image;
            _button.onClick.AddListener(() => HandleBadgeClicked(_object));

            return new BadgeEntry
            {
                SolarObject = _object,
                RectTransform = _rect,
                Image = _image,
                Label = _label,
                CanvasGroup = _group,
                ScreenPosition = Vector2.zero,
                Scale = 1f,
                CameraDistance = 0f,
                Visible = false,
                IsOccluded = false,
            };
        }
        #endregion

        #region Updates
        private void UpdateBadges()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
                if (worldCamera == null)
                {
                    return;
                }
            }

            if (badgeRoot == null || badgeCanvas == null)
            {
                return;
            }

            Camera? _uiCamera = badgeCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : badgeCanvas.worldCamera;

            bool _hideForFocusZoomOut = ShouldHideBadgesForFocusZoomOut();
            bool _runOcclusionCheck = ShouldRunOcclusionCheck();
            bool _hideNonSunForFocusZoomOut = ShouldHideNonSunBadgesForFocusZoomOut();
            bool _hideNonSunForOverviewZoomOut = ShouldHideNonSunBadgesForOverviewZoomOut();
            ApplyBadgeRootOrder();

            for (int _i = 0; _i < badges.Count; _i++)
            {
                BadgeEntry _entry = badges[_i];
                UpdateBadgeState(
                    _entry,
                    worldCamera,
                    _hideForFocusZoomOut,
                    _hideNonSunForFocusZoomOut,
                    _hideNonSunForOverviewZoomOut,
                    _runOcclusionCheck
                );
            }

            for (int _i = 0; _i < badges.Count; _i++)
            {
                BadgeEntry _entry = badges[_i];
                ApplyMoonOverlapRule(_entry);
            }

            for (int _i = 0; _i < badges.Count; _i++)
            {
                BadgeEntry _entry = badges[_i];
                ApplyBadgeState(_entry, _uiCamera);
            }

            UpdateBadgeRenderOrder();
            SyncBadgeMaterialSettings();
        }

        private void ApplyBadgeRootOrder()
        {
            if (badgeRoot == null)
            {
                return;
            }

            if (renderBadgesBehindUi)
            {
                badgeRoot.SetAsFirstSibling();
            }
            else
            {
                badgeRoot.SetAsLastSibling();
            }
        }

        private void UpdateBadgeRenderOrder()
        {
            if (!sortBadgesByDistance || badgeRoot == null)
            {
                return;
            }

            orderedBadges.Clear();
            for (int _i = 0; _i < badges.Count; _i++)
            {
                BadgeEntry _entry = badges[_i];
                if (_entry.Visible)
                {
                    orderedBadges.Add(_entry);
                }
            }

            if (orderedBadges.Count <= 1)
            {
                return;
            }

            orderedBadges.Sort((BadgeEntry _a, BadgeEntry _b) =>
                _b.CameraDistance.CompareTo(_a.CameraDistance));

            if (ShouldPrioritizeSunBadge())
            {
                BadgeEntry? _sunEntry = null;
                for (int _i = 0; _i < orderedBadges.Count; _i++)
                {
                    BadgeEntry _entry = orderedBadges[_i];
                    if (IsSunBadge(_entry.SolarObject))
                    {
                        _sunEntry = _entry;
                        orderedBadges.RemoveAt(_i);
                        break;
                    }
                }

                if (_sunEntry != null)
                {
                    orderedBadges.Add(_sunEntry);
                }
            }

            for (int _i = 0; _i < orderedBadges.Count; _i++)
            {
                orderedBadges[_i].RectTransform.SetSiblingIndex(_i);
            }
        }

        private bool ShouldHideBadgesForFocusZoomOut()
        {
            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<SolarSystemCamera>();
                if (cameraController == null)
                {
                    return false;
                }
            }

            if (!cameraController.IsFocusMode)
            {
                return false;
            }

            float _threshold = Mathf.Clamp01(focusZoomHideThreshold);
            return cameraController.FocusZoomNormalized >= _threshold;
        }

        private bool ShouldHideNonSunBadgesForOverviewZoomOut()
        {
            if (!hideNonSunBadgesOnOverviewZoomOut)
            {
                return false;
            }

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<SolarSystemCamera>();
                if (cameraController == null)
                {
                    return false;
                }
            }

            if (!cameraController.IsOverviewMode)
            {
                return false;
            }

            float _threshold = Mathf.Clamp01(hideNonSunOverviewZoomThreshold);
            return cameraController.OverviewZoomNormalized >= _threshold;
        }

        private bool ShouldHideNonSunBadgesForFocusZoomOut()
        {
            if (!hideNonSunBadgesOnFocusZoomOut)
            {
                return false;
            }

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<SolarSystemCamera>();
                if (cameraController == null)
                {
                    return false;
                }
            }

            if (!cameraController.IsFocusMode)
            {
                return false;
            }

            float _threshold = Mathf.Clamp01(hideNonSunFocusZoomThreshold);
            return cameraController.FocusZoomNormalized >= _threshold;
        }

        private bool ShouldRunOcclusionCheck()
        {
            if (!enableOcclusionCheck)
            {
                return false;
            }

            if (occlusionCheckIntervalSeconds <= 0f)
            {
                return true;
            }

            occlusionTimer -= Time.unscaledDeltaTime;
            if (occlusionTimer > 0f)
            {
                return false;
            }

            occlusionTimer = occlusionCheckIntervalSeconds;
            return true;
        }

        private void UpdateBadgeState(
            BadgeEntry _entry,
            Camera _camera,
            bool _hideForFocusZoomOut,
            bool _hideNonSunForFocusZoomOut,
            bool _hideNonSunForOverviewZoomOut,
            bool _runOcclusionCheck
        )
        {
            SolarObject _object = _entry.SolarObject;
            if (_object == null || !_object.gameObject.activeInHierarchy)
            {
                _entry.Visible = false;
                return;
            }

            bool _isSun = IsSunBadge(_object);
            bool _isFocusMoon = IsFocusMoonBadge(_object);
            bool _forcePlanetOverview = ShouldForcePlanetBadgeInOverview(_object);

            Sprite? _resolvedSprite = ResolveBadgeSprite();
            if (_entry.Image.sprite != _resolvedSprite)
            {
                _entry.Image.sprite = _resolvedSprite;
            }

            if (_resolvedSprite == null)
            {
                _entry.Visible = false;
                return;
            }

            Color _targetColor = GetBadgeSpriteColor(_object);
            if (_isSun && showSunCircleOnlyWhenNonSunHidden && !_hideNonSunForOverviewZoomOut)
            {
                _targetColor.a = 0f;
            }
            if (_entry.Image.color != _targetColor)
            {
                _entry.Image.color = _targetColor;
            }

            Material? _material = ResolveBadgeMaterial();
            if (_entry.Image.material != _material)
            {
                _entry.Image.material = _material;
            }

            UpdateBadgeLabel(_entry, _object);

            float _diameter = Mathf.Max(0.001f, _object.DiameterUnity);
            Vector3 _worldPos = _object.transform.position + Vector3.up *
                (badgeWorldOffset + _diameter * badgeWorldOffsetDiameterMultiplier);
            Vector3 _screenPos = _camera.WorldToScreenPoint(_worldPos);
            _entry.ScreenPosition = new Vector2(_screenPos.x, _screenPos.y);

            if (_screenPos.z <= 0f)
            {
                _entry.Visible = false;
                return;
            }

            float _padding = Mathf.Max(0f, screenEdgePaddingPixels);
            if (_screenPos.x < -_padding || _screenPos.x > Screen.width + _padding ||
                _screenPos.y < -_padding || _screenPos.y > Screen.height + _padding)
            {
                _entry.Visible = false;
                return;
            }

            float _distance = Vector3.Distance(_camera.transform.position, _object.transform.position);
            _entry.CameraDistance = _distance;

            bool _allowPlanetForFocus = ShouldShowPlanetBadgeBeyondFocusMax(_object, _distance);
            if (_hideForFocusZoomOut && !_allowPlanetForFocus)
            {
                _entry.Visible = false;
                return;
            }

            if (!_isSun && !_isFocusMoon &&
                (_hideNonSunForOverviewZoomOut || (_hideNonSunForFocusZoomOut && !_allowPlanetForFocus)) &&
                !_forcePlanetOverview)
            {
                _entry.Visible = false;
                return;
            }

            float _baseHide = Mathf.Max(badgeMinHideDistance, _diameter * badgeHideDistanceMultiplier);
            float _baseShow = Mathf.Max(_baseHide + 0.01f, _diameter * badgeShowDistanceMultiplier);
            _baseShow = Mathf.Max(_baseShow, badgeMinShowDistance);

            float _hideDistance;
            float _showDistance;

            float _visibilityScale = Mathf.Max(0.1f, badgeVisibilityDistanceScale);
            _hideDistance = _baseHide * _visibilityScale;
            _showDistance = _baseShow * _visibilityScale;

            if (_isFocusMoon)
            {
                _hideDistance = 0f;
                if (_showDistance <= _hideDistance)
                {
                    _showDistance = _hideDistance + 0.01f;
                }
            }

            if (_forcePlanetOverview)
            {
                _hideDistance = 0f;
                if (_showDistance <= _hideDistance)
                {
                    _showDistance = _hideDistance + 0.01f;
                }
            }

            if (_isSun)
            {
                float _sunHideMultiplier = Mathf.Max(1f, sunBadgeHideDistanceMultiplier);
                _hideDistance *= _sunHideMultiplier;
                if (_showDistance <= _hideDistance)
                {
                    _showDistance = _hideDistance + 0.01f;
                }
            }

            if (_distance <= _hideDistance)
            {
                _entry.Visible = false;
                return;
            }

            float _t = Mathf.InverseLerp(_hideDistance, _showDistance, _distance);
            float _distanceScale = Mathf.Lerp(badgeMinScale, badgeMaxScale, _t);
            if (_isSun)
            {
                float _sunMinScale = badgeMinScale * Mathf.Max(1f, sunBadgeMinScaleMultiplier);
                float _sunDistanceMin = Mathf.Max(_sunMinScale, Mathf.Max(0.1f, sunBadgeMinDistanceScale));
                _distanceScale = Mathf.Max(_distanceScale, _sunDistanceMin);
            }
            float _diameterScale = GetDiameterScale(_object);
            float _finalScale = _distanceScale * _diameterScale;
            float _sunMaxScale = Mathf.Max(0.01f, badgeMaxFinalScale);
            float _nonSunMaxScale = Mathf.Max(0.01f, badgeMaxFinalScaleNonSun);
            float _maxScale = _isSun
                ? _sunMaxScale
                : Mathf.Min(_sunMaxScale, _nonSunMaxScale);
            float _sunBaseMultiplier = _isSun
                ? Mathf.Max(1f, sunBadgeScaleMultiplier)
                : 1f;
            _finalScale *= _sunBaseMultiplier;
            _maxScale *= _sunBaseMultiplier;
            float _sunZoomBoost = 1f;

            if (_isSun && ShouldBoostSunBadgeScale())
            {
                _sunZoomBoost = GetSunZoomScaleBoost();
                _finalScale *= _sunZoomBoost;
                float _minBoostedCap = _nonSunMaxScale * _sunZoomBoost * _sunBaseMultiplier;
                _maxScale = Mathf.Max(_maxScale, _minBoostedCap);
            }

            if (_isFocusMoon)
            {
                float _moonScale = Mathf.Clamp(focusMoonBadgeScaleMultiplier, 0.1f, 3f);
                if (cameraController != null && cameraController.IsFocusMode)
                {
                    float _closeScale = Mathf.Clamp(focusMoonBadgeCloseScaleMultiplier, 0.1f, 5f);
                    float _zoomT = Mathf.Clamp01(cameraController.FocusZoomNormalized);
                    _moonScale = Mathf.Lerp(_closeScale, _moonScale, _zoomT);
                }

                _finalScale *= _moonScale;
                _maxScale = Mathf.Max(0.01f, _maxScale * _moonScale);
            }

            _entry.Scale = Mathf.Min(_finalScale, _maxScale);
            _entry.Visible = true;

            if (_runOcclusionCheck)
            {
                _entry.IsOccluded = IsBadgeOccluded(_object, _camera, _distance);
            }

            if (_entry.IsOccluded)
            {
                _entry.Visible = false;
            }
        }

        private void ApplyMoonOverlapRule(BadgeEntry _entry)
        {
            if (!_entry.Visible)
            {
                return;
            }

            SolarObject _object = _entry.SolarObject;
            if (_object == null || !_object.IsMoon)
            {
                return;
            }

            if (IsFocusMoonBadge(_object))
            {
                return;
            }

            SolarObject? _primary = _object.PrimarySolarObject;
            if (_primary == null)
            {
                return;
            }

            if (!badgesByObject.TryGetValue(_primary, out BadgeEntry _primaryEntry))
            {
                return;
            }

            if (!_primaryEntry.Visible)
            {
                return;
            }

            float _primaryDiameter = Mathf.Max(0.001f, _primary.DiameterUnity);
            float _overlapDistance = Mathf.Max(moonOverlapMinDistance, _primaryDiameter * moonOverlapHideDistanceMultiplier);
            if (_entry.CameraDistance < _overlapDistance)
            {
                return;
            }

            float _pixelDistance = Vector2.Distance(_entry.ScreenPosition, _primaryEntry.ScreenPosition);
            float _pixelThreshold = moonOverlapHidePixels * Mathf.Max(0.1f, _primaryEntry.Scale);
            if (_pixelDistance <= _pixelThreshold)
            {
                _entry.Visible = false;
            }
        }

        private void ApplyBadgeState(BadgeEntry _entry, Camera? _uiCamera)
        {
            if (!_entry.Visible)
            {
                _entry.CanvasGroup.alpha = 0f;
                _entry.CanvasGroup.interactable = false;
                _entry.CanvasGroup.blocksRaycasts = false;
                if (_entry.Label != null)
                {
                    _entry.Label.enabled = false;
                }
                return;
            }

            if (badgeRoot == null)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    badgeRoot,
                    _entry.ScreenPosition,
                    _uiCamera,
                    out Vector2 _local)
                )
            {
                _entry.RectTransform.anchoredPosition = _local;
            }

            float _scale = Mathf.Max(0.01f, _entry.Scale);
            _entry.RectTransform.localScale = new Vector3(_scale, _scale, 1f);

            float _minAlpha = Mathf.Clamp01(badgeMinAlpha);

            _entry.CanvasGroup.alpha = _minAlpha;
            _entry.CanvasGroup.interactable = true;
            _entry.CanvasGroup.blocksRaycasts = true;
        }
        #endregion

        #region Actions
        private void HandleBadgeClicked(SolarObject _object)
        {
            if (_object == null)
            {
                return;
            }

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<SolarSystemCamera>();
            }

            if (cameraController == null)
            {
                HelpLogs.Warn("Badges", "SolarSystemCamera not found for badge selection.");
                return;
            }

            cameraController.FocusOn(_object);
        }
        #endregion

        #region Materials
        private Material? ResolveBadgeMaterial()
        {
            if (runtimeBadgeMaterial != null)
            {
                return runtimeBadgeMaterial;
            }

            Shader? _shader = null;
            if (badgeMaterial != null)
            {
                _shader = badgeMaterial.shader;
            }
            else
            {
                _shader = Shader.Find("UI/BadgeOpaque");
            }

            if (_shader == null)
            {
                return null;
            }

            runtimeBadgeMaterial = badgeMaterial != null
                ? new Material(badgeMaterial)
                : new Material(_shader);
            runtimeBadgeMaterial.SetFloat(AlphaThresholdId, Mathf.Clamp01(badgeAlphaClipThreshold));
            return runtimeBadgeMaterial;
        }

        private void SyncBadgeMaterialSettings()
        {
            if (runtimeBadgeMaterial == null)
            {
                return;
            }

            float _threshold = Mathf.Clamp01(badgeAlphaClipThreshold);
            if (!Mathf.Approximately(runtimeBadgeMaterial.GetFloat(AlphaThresholdId), _threshold))
            {
                runtimeBadgeMaterial.SetFloat(AlphaThresholdId, _threshold);
            }
        }

        private void ReleaseBadgeMaterial()
        {
            if (runtimeBadgeMaterial == null)
            {
                return;
            }

            Destroy(runtimeBadgeMaterial);
            runtimeBadgeMaterial = null;
        }
        #endregion

        #region Occlusion
        private bool IsBadgeOccluded(SolarObject _object, Camera _camera, float _distance)
        {
            if (occlusionLayerMask.value == 0)
            {
                return false;
            }

            Vector3 _origin = _camera.transform.position;
            Vector3 _targetPos = _object.transform.position;
            Vector3 _direction = _targetPos - _origin;
            float _length = Mathf.Max(0.0001f, _distance);
            if (_direction.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            _direction /= _length;
            float _rayDistance = _length + Mathf.Max(0f, occlusionDistancePadding);

            int _hitCount = Physics.RaycastNonAlloc(
                _origin,
                _direction,
                occlusionHits,
                _rayDistance,
                occlusionLayerMask,
                QueryTriggerInteraction.Ignore
            );

            if (_hitCount <= 0)
            {
                return false;
            }

            SolarObject? _closestObject = null;
            float _closestDistance = float.MaxValue;
            for (int _i = 0; _i < _hitCount; _i++)
            {
                Collider _collider = occlusionHits[_i].collider;
                if (_collider == null)
                {
                    continue;
                }

                SolarObject? _hitObject = _collider.GetComponentInParent<SolarObject>();
                if (_hitObject == null)
                {
                    continue;
                }

                float _hitDistance = occlusionHits[_i].distance;
                if (_hitDistance < _closestDistance)
                {
                    _closestDistance = _hitDistance;
                    _closestObject = _hitObject;
                }
            }

            if (_closestObject == null)
            {
                return false;
            }

            return _closestObject != _object;
        }
        #endregion

        #region Helpers
        private static bool IsSunBadge(SolarObject _object)
        {
            if (_object == null)
            {
                return false;
            }

            if (_object.IsStar)
            {
                return true;
            }

            return string.Equals(_object.Id, "sun", System.StringComparison.OrdinalIgnoreCase);
        }

        private bool IsFocusMoonBadge(SolarObject _object)
        {
            if (_object == null || !_object.IsMoon)
            {
                return false;
            }

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<SolarSystemCamera>();
                if (cameraController == null)
                {
                    return false;
                }
            }

            if (!cameraController.IsFocusMode)
            {
                return false;
            }

            SolarObject? _focused = cameraController.FocusedSolarObject;
            if (_focused == null || !_focused.IsPlanet)
            {
                return false;
            }

            float _threshold = Mathf.Clamp01(focusMoonShowZoomThreshold);
            if (cameraController.TryGetFocusZoomRange(_focused, out float _minDistance, out float _maxDistance))
            {
                Camera? _camera = worldCamera ?? Camera.main;
                if (_camera != null)
                {
                    float _showDistance = Mathf.Lerp(_minDistance, _maxDistance, _threshold);
                    float _distance = Vector3.Distance(_camera.transform.position, _focused.transform.position);
                    if (_distance > _showDistance)
                    {
                        return false;
                    }
                }
            }

            SolarObject? _primary = _object.PrimarySolarObject;
            if (_primary != null)
            {
                return _primary == _focused;
            }

            string _primaryId = _object.PrimaryId;
            if (string.IsNullOrWhiteSpace(_primaryId))
            {
                return false;
            }

            return string.Equals(_primaryId, _focused.Id, System.StringComparison.OrdinalIgnoreCase);
        }

        private bool ShouldShowPlanetBadgeBeyondFocusMax(SolarObject _object, float _distance)
        {
            if (_object == null || !_object.IsPlanet)
            {
                return false;
            }

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<SolarSystemCamera>();
                if (cameraController == null)
                {
                    return false;
                }
            }

            if (!cameraController.IsFocusMode)
            {
                return false;
            }

            if (!cameraController.TryGetFocusZoomRange(_object, out _, out float _maxDistance))
            {
                return false;
            }

            return _distance >= _maxDistance;
        }

        private bool ShouldForcePlanetBadgeInOverview(SolarObject _object)
        {
            if (!alwaysShowPlanetBadgesInOverview)
            {
                return false;
            }

            if (_object == null || !_object.IsPlanet)
            {
                return false;
            }

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<SolarSystemCamera>();
                if (cameraController == null)
                {
                    return false;
                }
            }

            if (!cameraController.IsOverviewMode)
            {
                return false;
            }

            float _threshold = Mathf.Clamp01(overviewPlanetHideZoomThreshold);
            return cameraController.OverviewZoomNormalized < _threshold;
        }

        private bool ShouldPrioritizeSunBadge()
        {
            if (!enableSunPriorityOnZoomOut)
            {
                return false;
            }

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<SolarSystemCamera>();
                if (cameraController == null)
                {
                    return false;
                }
            }

            if (cameraController.IsOverviewMode)
            {
                float _threshold = Mathf.Clamp01(sunBadgePriorityOverviewZoomNormalized);
                return cameraController.OverviewZoomNormalized >= _threshold;
            }

            float _focusThreshold = Mathf.Clamp01(sunBadgePriorityFocusZoomNormalized);
            return cameraController.FocusZoomNormalized >= _focusThreshold;
        }

        private bool ShouldBoostSunBadgeScale()
        {
            return GetSunZoomScaleBoost() > 1.0f;
        }

        private float GetSunZoomScaleBoost()
        {
            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<SolarSystemCamera>();
                if (cameraController == null)
                {
                    return 1.0f;
                }
            }

            float _threshold;
            float _zoomNormalized;

            if (cameraController.IsOverviewMode)
            {
                _threshold = Mathf.Clamp01(sunBadgeMaxZoomOverviewThreshold);
                _zoomNormalized = cameraController.OverviewZoomNormalized;
            }
            else
            {
                _threshold = Mathf.Clamp01(sunBadgeMaxZoomFocusThreshold);
                _zoomNormalized = cameraController.FocusZoomNormalized;
            }

            if (_zoomNormalized <= _threshold)
            {
                return 1.0f;
            }

            float _t = Mathf.InverseLerp(_threshold, 1.0f, Mathf.Clamp01(_zoomNormalized));
            float _ease = SmoothStep01(_t);
            float _max = Mathf.Max(1.0f, sunBadgeMaxZoomScaleMultiplier);
            return Mathf.Lerp(1.0f, _max, _ease);
        }
        #endregion

        #region Sprite Overrides
        private Sprite? ResolveBadgeSprite()
        {
            Sprite? _sprite = ResolveBadgeCircleSprite();
            if (_sprite == null || !forceOpaqueSpriteAlpha)
            {
                return _sprite;
            }

            if (opaqueSpriteCache.TryGetValue(_sprite, out Sprite _cached))
            {
                return _cached;
            }

            if (opaqueSpriteFailures.Contains(_sprite))
            {
                return _sprite;
            }

            Texture2D _texture = _sprite.texture;
            if (_texture == null || !_texture.isReadable)
            {
                if (!opaqueSpriteWarningLogged)
                {
                    HelpLogs.Warn(
                        "Badges",
                        "Opaque sprite override requires Read/Write enabled on badge circle textures."
                    );
                    opaqueSpriteWarningLogged = true;
                }

                opaqueSpriteFailures.Add(_sprite);
                return _sprite;
            }

            Sprite _opaque = CreateOpaqueSprite(_sprite);
            opaqueSpriteCache[_sprite] = _opaque;
            return _opaque;
        }

        private Sprite? ResolveBadgeCircleSprite()
        {
            if (badgeCircleSprite != null)
            {
                return badgeCircleSprite;
            }

            if (fallbackCircleSprite != null)
            {
                return fallbackCircleSprite;
            }

            fallbackCircleSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            if (fallbackCircleSprite == null)
            {
                fallbackCircleSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            }

            if (fallbackCircleSprite == null && !missingCircleSpriteLogged)
            {
                HelpLogs.Warn("Badges", "No badge circle sprite resolved. Assign one in SolarObjectBadgeManager.");
                missingCircleSpriteLogged = true;
            }

            return fallbackCircleSprite;
        }

        private Sprite CreateOpaqueSprite(Sprite _sprite)
        {
            Rect _rect = _sprite.textureRect;
            Texture2D _source = _sprite.texture;
            int _width = Mathf.Max(1, Mathf.RoundToInt(_rect.width));
            int _height = Mathf.Max(1, Mathf.RoundToInt(_rect.height));
            int _x = Mathf.Clamp(Mathf.FloorToInt(_rect.x), 0, Mathf.Max(0, _source.width - _width));
            int _y = Mathf.Clamp(Mathf.FloorToInt(_rect.y), 0, Mathf.Max(0, _source.height - _height));

            Color[] _pixels = _source.GetPixels(_x, _y, _width, _height);
            for (int _i = 0; _i < _pixels.Length; _i++)
            {
                float _alpha = _pixels[_i].a;
                if (_alpha > 0f)
                {
                    if (_alpha < 1f)
                    {
                        float _invAlpha = 1f / Mathf.Max(0.0001f, _alpha);
                        _pixels[_i].r = Mathf.Min(1f, _pixels[_i].r * _invAlpha);
                        _pixels[_i].g = Mathf.Min(1f, _pixels[_i].g * _invAlpha);
                        _pixels[_i].b = Mathf.Min(1f, _pixels[_i].b * _invAlpha);
                    }

                    _pixels[_i].a = 1f;
                }
            }

            Texture2D _copy = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
            _copy.SetPixels(_pixels);
            _copy.Apply(false, false);
            opaqueSpriteTextures.Add(_copy);

            float _pivotX = _rect.width <= 0.0001f ? 0.5f : _sprite.pivot.x / _rect.width;
            float _pivotY = _rect.height <= 0.0001f ? 0.5f : _sprite.pivot.y / _rect.height;
            Vector2 _pivot = new Vector2(_pivotX, _pivotY);

            return Sprite.Create(
                _copy,
                new Rect(0f, 0f, _width, _height),
                _pivot,
                _sprite.pixelsPerUnit,
                0,
                SpriteMeshType.Tight,
                _sprite.border
            );
        }

        private void ReleaseOpaqueSprites()
        {
            foreach (Sprite _sprite in opaqueSpriteCache.Values)
            {
                if (_sprite != null)
                {
                    Destroy(_sprite);
                }
            }

            for (int _i = 0; _i < opaqueSpriteTextures.Count; _i++)
            {
                Texture2D _texture = opaqueSpriteTextures[_i];
                if (_texture != null)
                {
                    Destroy(_texture);
                }
            }

            opaqueSpriteCache.Clear();
            opaqueSpriteFailures.Clear();
            opaqueSpriteTextures.Clear();
        }
        #endregion

        #region Styling
        private Color GetBadgeSpriteColor(SolarObject _object)
        {
            float _brightness = Mathf.Clamp(badgeSpriteBrightness, 0.1f, 5f);
            Color _orbitColor = _object.OrbitLineBaseColor;
            Color _tint = badgeCircleColor;
            Color _base = new Color(
                _orbitColor.r * _tint.r,
                _orbitColor.g * _tint.g,
                _orbitColor.b * _tint.b,
                1f
            );

            Color.RGBToHSV(_base, out float _h, out float _s, out float _v);
            _v = Mathf.Clamp01(_v * _brightness);
            Color _color = Color.HSVToRGB(_h, _s, _v);
            _color.a = _tint.a;
            return _color;
        }

        private void UpdateBadgeLabel(BadgeEntry _entry, SolarObject _object)
        {
            if (_entry.Label == null)
            {
                return;
            }

            if (!badgeLabelEnabled)
            {
                _entry.Label.enabled = false;
                return;
            }

            if (!_entry.Label.enabled)
            {
                _entry.Label.enabled = true;
            }

            string _text = ResolveBadgeLabelText(_object);
            if (_entry.Label.text != _text)
            {
                _entry.Label.text = _text;
            }

            if (badgeLabelFont != null && _entry.Label.font != badgeLabelFont)
            {
                _entry.Label.font = badgeLabelFont;
            }

            if (!Mathf.Approximately(_entry.Label.fontSize, badgeLabelFontSize))
            {
                _entry.Label.fontSize = badgeLabelFontSize;
            }

            if (_entry.Label.color != badgeLabelColor)
            {
                _entry.Label.color = badgeLabelColor;
            }

            RectTransform _labelRect = _entry.Label.rectTransform;
            if (_labelRect.anchoredPosition != badgeLabelOffsetPixels)
            {
                _labelRect.anchoredPosition = badgeLabelOffsetPixels;
            }

            Vector2 _size = new Vector2(badgeLabelMaxWidthPixels, badgeLabelFontSize * 1.4f);
            if (_labelRect.sizeDelta != _size)
            {
                _labelRect.sizeDelta = _size;
            }
        }

        private static string ResolveBadgeLabelText(SolarObject _object)
        {
            if (_object == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(_object.name))
            {
                return _object.name;
            }

            return _object.Id;
        }

        private float GetDiameterScale(SolarObject _object)
        {
            float _min = Mathf.Max(0.0001f, minDiameterUnity);
            float _max = Mathf.Max(_min, maxDiameterUnity);
            float _diameter = Mathf.Max(0.0001f, _object.BaseDiameterUnity);
            float _t = Mathf.InverseLerp(_min, _max, _diameter);
            float _curve = Mathf.Pow(_t, Mathf.Clamp(badgeDiameterScaleExponent, 0.1f, 3f));
            return Mathf.Lerp(badgeDiameterMinScale, badgeDiameterMaxScale, _curve);
        }
        #endregion
    }
}
