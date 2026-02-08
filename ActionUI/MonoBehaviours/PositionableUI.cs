using ModifAmorphic.Outward.Unity.ActionUI;
using ModifAmorphic.Outward.Unity.ActionUI.Data;
using ModifAmorphic.Outward.Unity.ActionUI.Extensions;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModifAmorphic.Outward.Unity.ActionMenus
{
    [UnityScriptComponent]
    public class PositionableUI : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        public Image BackgroundImage;
        public Button ResetButton;

        public string TransformPath => transform.GetPath();
        public string NormalizedTransformPath => transform.GetNormalizedPath();

        public RectTransform RectTransform => GetComponent<RectTransform>();

        private UIPosition _originPosition;
        public UIPosition OriginPosition => _originPosition;

        public bool HasMoved => _startPositionSet && (!Mathf.Approximately(StartPosition.x, RectTransform.anchoredPosition.x) || !Mathf.Approximately(StartPosition.y, RectTransform.anchoredPosition.y));

        public UnityEvent<bool> OnIsPositionableChanged { get; private set; } = new UnityEvent<bool>();

        private bool _positioningEnabled = false;
        public bool IsPositionable => _positioningEnabled;

        private IPositionsProfileService _positionsProfileService;
        private bool profileChangeEventNeeded = false;
        private bool _buttonInit;
        private bool _raycasterAdded;
        private bool _canvasAdded;

        private Vector2 _startPosition;
        private bool _startPositionSet;
        public Vector2 StartPosition
        {
            get => _startPosition;
            private set
            {
                _startPosition = value;
                _startPositionSet = true;
            }
        }
        public Vector2 DynamicOffset { get; set; } = Vector2.zero;
        private Vector2 _logicalPosition;
        private bool _logicalPositionInit = false;
        private Vector2 _offset;

        public UnityEvent<PositionableUI> UIElementMoved { get; } = new UnityEvent<PositionableUI>();

        private void InitializeLogicalPosition()
        {
            if (!_logicalPositionInit)
            {
                _logicalPosition = RectTransform.anchoredPosition;
                _logicalPositionInit = true;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
        private void Start()
        {
            InitializeLogicalPosition();
            if (ResetButton != null && !_buttonInit)
            {
                ResetButton.onClick.AddListener(ResetToOrigin);
                _buttonInit = true;
            }
            if (_originPosition == default)
            {
                _originPosition = RectTransform.ToRectTransformPosition();
            }

            // Capture origin BEFORE applying profile if not already set or if it was set to current position which might be wrong
             StartPosition = new Vector2(RectTransform.anchoredPosition.x, RectTransform.anchoredPosition.y);
             _logicalPosition = StartPosition;

            if (_positionsProfileService != null)
                SetPositionFromProfile(_positionsProfileService.GetProfile());


            if (BackgroundImage != null)
                BackgroundImage.gameObject.SetActive(_positioningEnabled);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
        private void Update()
        {
            // Apply dynamic offset every frame to ensure smoothness and responsiveness to offset changes
            if (_logicalPositionInit)
            {
                RectTransform.anchoredPosition = _logicalPosition + DynamicOffset;
            }

            if (profileChangeEventNeeded && _positionsProfileService != null)
            {
                profileChangeEventNeeded = false;
                DebugLogger.Log($"[Debug  :ActionMenus] PositionableUI{{{name}}}::Update: Adding OnProfileChanged listener.");
                _positionsProfileService.OnProfileChanged += OnProfileChanged;
            }
        }

        public void SetPositionsService(IPositionsProfileService positionsProfileService)
        {
            _positionsProfileService = positionsProfileService;
            profileChangeEventNeeded = true;
            if (_positionsProfileService != null)
            {
                DebugLogger.Log($"[Debug  :ActionMenus] PositionableUI{{{name}}}::SetPositionsService: Adding OnProfileChanged listener.");
                _positionsProfileService.OnProfileChanged += OnProfileChanged;
                profileChangeEventNeeded = false;
                SetPositionFromProfile(_positionsProfileService.GetProfile());
            }
        }

        public void EnableMovement()
        {
            _positioningEnabled = true;
            if (BackgroundImage != null)
                BackgroundImage.gameObject.SetActive(_positioningEnabled);

            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                gameObject.AddComponent<Canvas>();
                _canvasAdded = true;
            }
            var raycaster = GetComponent<GraphicRaycaster>();
            if (canvas == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
                _raycasterAdded = true;
            }
            OnIsPositionableChanged.Invoke(_positioningEnabled);
        }

        public void DisableMovement()
        {
            _positioningEnabled = false;
            if (BackgroundImage != null)
                BackgroundImage.gameObject.SetActive(_positioningEnabled);

            if (_raycasterAdded)
            {
                var raycaster = GetComponent<GraphicRaycaster>();
                if (raycaster != null)
                    UnityEngine.Object.Destroy(raycaster);

                _raycasterAdded = false;
            }
            if (_canvasAdded)
            {
                var canvas = gameObject.GetComponent<Canvas>();
                if (canvas != null)
                    UnityEngine.Object.Destroy(canvas);

                _canvasAdded = false;
            }
            OnIsPositionableChanged.Invoke(_positioningEnabled);
        }

        public void SetPosition(float x, float y) 
        {
            InitializeLogicalPosition();
            _logicalPosition = new Vector2(x, y);
            RectTransform.anchoredPosition = _logicalPosition + DynamicOffset;
        }

        public void SetPosition(UIPosition position) => SetPosition(position.AnchoredPosition.X, position.AnchoredPosition.Y);

        public void SetPositionFromProfile(PositionsProfile profile)
        {
            // Use normalized path for matching so positions work across different characters
            var normalizedPath = NormalizedTransformPath;
            var position = profile.Positions?.FirstOrDefault(p => TransformExtensions.NormalizePath(p.TransformPath) == normalizedPath);
            if (position != default)
            {
                DebugLogger.Log($"[Debug  :ActionMenus] PositionableUI{{{name}}}: Setting position of PositionableUI {name} to modified position of ({position.ModifiedPosition.AnchoredPosition.X}, {position.ModifiedPosition.AnchoredPosition.Y}).");
                SetPosition(position.ModifiedPosition);
                // _originPosition = position.OriginPosition; // Do not overwrite origin from profile, trust local startup origin
            }
        }

        private void OnProfileChanged(PositionsProfile profile)
        {
            DebugLogger.Log($"[Debug  :ActionMenus] PositionableUI{{{name}}}: OnProfileChanged for PositionableUI {name}.");
            ResetToOrigin();
            SetPositionFromProfile(profile);
        }

        public void ResetToOrigin()
        {
            if (_originPosition != null)
            {
                DebugLogger.Log($"[Debug  :ActionMenus] PositionableUI{{{name}}}: Setting position of PositionableUI {name} to origin position of ({_originPosition.AnchoredPosition.X}, {_originPosition.AnchoredPosition.Y}).");
                SetPosition(_originPosition);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_positioningEnabled)
            {
                transform.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y) - _offset;
                _logicalPosition = RectTransform.anchoredPosition - DynamicOffset;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_positioningEnabled)
            {
                DebugLogger.Log("Dragging started.");
                if (_originPosition == default)
                {
                    _originPosition = RectTransform.ToRectTransformPosition();
                }
                
                // Keep logical position up to date
                InitializeLogicalPosition();
                StartPosition = _logicalPosition;
                
                _offset = eventData.position - new Vector2(RectTransform.position.x, RectTransform.position.y);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_positioningEnabled)
            {
                if (HasMoved)
                    UIElementMoved?.TryInvoke(this);
                DebugLogger.Log("Dragging Done.");
            }
        }

        /// <summary>
        /// Gets a new instance of a <see cref="UIPosition"/> using this <see cref="PositionableUI"/>'s <see cref="RectTransform"/>.
        /// </summary>
        /// <returns>New instance of a <see cref="UIPosition"/></returns>
        public UIPositions GetUIPositions() =>
          new UIPositions()
          {
              ModifiedPosition = RectTransform.ToRectTransformPosition(),
              OriginPosition = _originPosition,
              TransformPath = transform.GetNormalizedPath()  // Use normalized path for global matching
          };
    }
}
