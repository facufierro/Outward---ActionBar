using ModifAmorphic.Outward.Unity.ActionUI;
using ModifAmorphic.Outward.Unity.ActionUI.Controllers;
using ModifAmorphic.Outward.Unity.ActionUI.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;



namespace ModifAmorphic.Outward.Unity.ActionMenus
{
    [UnityScriptComponent]
    public class HotbarsContainer : MonoBehaviour
    {
        private RectTransform _leftDisplay;

        // Cached reference to the vanilla panel
        private KeyboardQuickSlotPanel _vanillaKeyboardPanel;

        private IHotbarController _controller;
        public IHotbarController Controller { get => _controller; }

        //      private Button _settingsButton;
        //public Button SettingsButton => _settingsButton;
        public PlayerActionMenus PlayerActionMenus;
        public List<CanvasGroup> VanillaOverlayTargets = new List<CanvasGroup>();
        public List<GameObject> VanillaSuppressionTargets = new List<GameObject>();
        //public ActionsViewer ActionsViewer;

        public bool HotbarsEnabled => _actionBarsCanvas.gameObject.activeSelf;

        private Canvas _actionBarsCanvas;
        public Canvas ActionBarsCanvas => _actionBarsCanvas;

        //private LeftHotbarNav _leftHotbarNav;
        public LeftHotbarNav LeftHotbarNav; // => _leftHotbarNav;

        private Canvas _baseHotbarCanvas;
        internal Canvas BaseHotbarCanvas => _baseHotbarCanvas;

        private GridLayoutGroup _baseGrid;
        internal GridLayoutGroup BaseGrid => _baseGrid;

        private GridLayoutGroup[] _hotbarGrid;
        internal GridLayoutGroup[] HotbarGrid => _hotbarGrid;

        private GameObject _baseActionSlot;
        internal GameObject BaseActionSlot => _baseActionSlot;

        private ActionSlot[][] _hotbars;
        /// <summary>
        /// [Hotbar Index][Slot Index]
        /// </summary>
        public ActionSlot[][] Hotbars => _hotbars;

        private Dictionary<int, ActionSlot> _actionSlots = new Dictionary<int, ActionSlot>();
        public Dictionary<int, ActionSlot> ActionSlots => _actionSlots;

        private int _selectedHotbar;
        public int SelectedHotbar
        {
            get => _selectedHotbar;
            internal set
            {
                _selectedHotbar = value;
            }
        }

        //private Text posText;
        //private RectTransform rectTransform;

        //private Text canvasPosText;
        //private RectTransform canvasRectTransform;

        //private Text gridPosText;

        public bool IsInHotkeyEditMode { get; internal set; }
        public bool IsInActionSlotEditMode { get; internal set; }

        private bool _hasChanges = false;
        public bool HasChanges
        {
            get => _hasChanges;
            internal set
            {
                var oldValue = _hasChanges;
                _hasChanges = value;
                if (value && !oldValue)
                {
                    OnHasChanges.Invoke();
                }
            }
        }
        public void ClearChanges() => _hasChanges = false;

        public event Action OnAwake;
        public UnityEvent OnHasChanges { get; } = new UnityEvent();
        public bool IsAwake { get; private set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
        private void Awake()
        {
            SetComponents();
            _controller = new HotbarsController(this);
            PlayerActionMenus.OnPlayerIdAssigned.AddListener(OnPlayerAssigned);

            //posText = transform.parent.GetComponentsInChildren<Text>().First(t => t.name == "HotbarsPosText");
            //rectTransform = GetComponent<RectTransform>();

            //canvasPosText = transform.parent.GetComponentsInChildren<Text>().First(t => t.name == "Hotbar0CanvasPosText");
            //gridPosText = transform.parent.GetComponentsInChildren<Text>().First(t => t.name == "Hotbar0GridPosText");
            IsAwake = true;
            OnAwake?.Invoke();
            
            // Link to Configs
            // Logic moved to HotbarService to avoid circular dependency
        }

        private void OnPlayerAssigned(PlayerActionMenus menus)
        {
             if (menus == PlayerActionMenus)
             {
                 var hotbarService = menus.ServicesProvider.GetService<IHotbarProfileService>();
                 if (hotbarService != null)
                 {
                     hotbarService.OnProfileChanged += OnProfileChanged;
                 }
             }
        }

        private void OnProfileChanged(IHotbarProfile profile, HotbarProfileChangeTypes type)
        {
            _controller.ConfigureHotbars(profile);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
        private void Update()
        {
            //canvasRectTransform = GetComponentsInChildren<Canvas>()?.FirstOrDefault(c => c.name == "HotbarCanvas0")?
            //	.GetComponent<RectTransform>();

            //posText.text = $"Hotbars Pos: {rectTransform.position.x}, {rectTransform.position.y}. Size {rectTransform.sizeDelta.x}, {rectTransform.sizeDelta.y}";
            //if (canvasRectTransform != null)
            //	canvasPosText.text = $"Hotbars0 Canvas Pos: {canvasRectTransform.position.x}, {canvasRectTransform.position.y}. Size {canvasRectTransform.sizeDelta.x}, {canvasRectTransform.sizeDelta.y}";

            //if (HotbarGrid?.Length > 0)
            //{
            //	var grid = HotbarGrid[0].GetComponent<RectTransform>();
            //	gridPosText.text = $"HotbarsGrid0 Pos: {grid.position.x}, {grid.position.y}. Size {grid.sizeDelta.x}, {grid.sizeDelta.y}";
            //}

            // Handle dynamic positioning based on vanilla UI
            float maxOffset = 0f;
            if (VanillaOverlayTargets != null)
            {
                foreach (var target in VanillaOverlayTargets)
                {
                    if (target != null && target.gameObject.activeInHierarchy && target.alpha > 0.01f)
                    {
                        var rect = target.GetComponent<RectTransform>();
                        if (rect != null)
                        {
                            // Convert target height to world space then to local space to ensure correct offset regardless of scaling differences
                            float worldHeight = rect.rect.height * target.transform.lossyScale.y;
                            if (transform.lossyScale.y > 0.0001f)
                                maxOffset = Mathf.Max(maxOffset, worldHeight / transform.lossyScale.y);
                            else
                                maxOffset = Mathf.Max(maxOffset, rect.rect.height);
                        }
                    }
                }
            }

            // Brute-force override for Vanilla Keyboard Slots
            // We search for it if we haven't found it, or if the reference was lost/destroyed
            if (_vanillaKeyboardPanel == null)
            {
                // Look in parent (CharacterUI) for the panel
                _vanillaKeyboardPanel = transform.root.GetComponentInChildren<KeyboardQuickSlotPanel>(true);
            }

            if (_vanillaKeyboardPanel != null && _vanillaKeyboardPanel.gameObject.activeSelf)
            {
                // Force disable it
                _vanillaKeyboardPanel.gameObject.SetActive(false);
            }

            // Enforce suppression of other targets (like Gamepad slots if needed, or if added via Service)
            if (VanillaSuppressionTargets != null)
            {
                for (int i = 0; i < VanillaSuppressionTargets.Count; i++)
                {
                    var target = VanillaSuppressionTargets[i];
                    if (target != null)
                    {
                        if (target.activeSelf) target.SetActive(false);
                         // Also squash it just in case
                        if (target.transform.localScale != Vector3.zero) target.transform.localScale = Vector3.zero;
                    }
                }
            }

            var positionable = GetComponent<PositionableUI>();
            if (positionable != null)
            {
               positionable.DynamicOffset = new Vector2(0, maxOffset);
            }

            Controller.HotbarsContainerUpdate();
        }
        private void SetComponents()
        {
            _actionBarsCanvas = transform.parent.GetComponent<Canvas>();
            LeftHotbarNav = GetComponentInChildren<LeftHotbarNav>();
            _leftDisplay = transform.Find("LeftDisplay").GetComponent<RectTransform>();
            //_settingsButton = _leftDisplay.Find("Settings").GetComponent<Button>();

            _baseHotbarCanvas = transform.Find("BaseHotbarCanvas").GetComponent<Canvas>();

            _baseGrid = _baseHotbarCanvas.GetComponentsInChildren<GridLayoutGroup>().First(g => g.name == "BaseHotbarGrid");
            _baseActionSlot = _baseGrid.GetComponentInChildren<ActionSlot>().gameObject;
            _baseHotbarCanvas.gameObject.SetActive(false);
            _baseGrid.gameObject.SetActive(false);
            _baseActionSlot.SetActive(false);
            
            // Set Anchor to Bottom-Right (Vanilla Position)
            var rect = GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
            }
        }

        internal void ResetCollections()
        {
            _hotbarGrid = new GridLayoutGroup[0];
            _hotbars = new ActionSlot[0][];
            _actionSlots = new Dictionary<int, ActionSlot>();
        }
        internal void Resize(float hotbarWidth, float hotbarHeight)
        {
            GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, hotbarWidth + _leftDisplay.rect.width);
            GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, hotbarHeight);
        }

        internal void ConfigureHotbars(int barsAmount)
        {
            //DestroyHotbars();

            _hotbarGrid = new GridLayoutGroup[barsAmount];
            _hotbars = new ActionSlot[barsAmount][];
        }

        internal void ConfigureActionSlots(int bar, int slotsAmount)
        {
            _hotbars[bar] = new ActionSlot[slotsAmount];
        }

        //private void DestroyHotbars()
        //{
        //    if (_hotbarGrid != null)
        //    {
        //        for (int g = 0; g < _hotbarGrid.Length; g++)
        //        {
        //            if (_hotbarGrid[g] != null)
        //                _hotbarGrid[g].gameObject.Destroy();
        //        }
        //    }
        //    _hotbarGrid = null;

        //    if (_hotbars != null)
        //    {
        //        for (int h = 0; h < _hotbars.Length; h++)
        //        {
        //            if (_hotbars[h] != null)
        //            {
        //                for (int s = 0; s < _hotbars[h].Length; s++)
        //                {
        //                    if (_hotbars[h][s].Controller is IDisposable disposable)
        //                        disposable?.Dispose();
        //                    _hotbars[h][s].gameObject.Destroy();
        //                }
        //                _hotbars[h] = null;
        //            }
        //        }
        //    }
        //    _hotbars = null;
        //}
    }
}