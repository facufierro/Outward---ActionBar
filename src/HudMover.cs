using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Attached to a game HUD element to make it draggable in Edit Mode.
    /// Stores the original position so it can be reset.
    /// </summary>
    public class HudMover : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public string ElementId;
        public bool AnchorBottom;
        public int SortPriority;

        private RectTransform _rect;
        private Vector2 _originalAnchoredPos;
        private bool _originalPosCaptured;
        private Vector2 _dragOffset;
        private bool _dragging;

        // Persistent target position — enforced every LateUpdate so the game can't overwrite it
        private Vector2 _targetPosition;
        private bool _hasTargetPosition;

        private GameObject _handleObj;   // contains both highlight + label
        private Canvas _addedCanvas;
        private GraphicRaycaster _addedRaycaster;
        private CanvasGroup _canvasGroup;

        // State tracking to restore after edit mode
        private bool _wasActive;
        private float _originalAlpha = -1f;
        private LayoutGroup _layoutGroup;
        private ContentSizeFitter _sizeFitter;
        private bool _hadLayoutGroup;
        private bool _hadSizeFitter;
        private MonoBehaviour _gameScript; // e.g. InteractionDisplay — repositions element each frame
        private bool _hadGameScript;

        /// <summary>
        /// Lazily initializes _rect and captures the original position.
        /// Safe to call multiple times — only captures on first successful init.
        /// Needed because Awake() is deferred on inactive GameObjects.
        /// </summary>
        private void EnsureInit()
        {
            if (_rect == null)
                _rect = GetComponent<RectTransform>();
            if (_rect != null && !_originalPosCaptured)
            {
                _originalAnchoredPos = _rect.anchoredPosition;
                _originalPosCaptured = true;
            }
        }

        void Awake()
        {
            EnsureInit();
        }

        public Vector2 OriginalPosition => _originalAnchoredPos;

        /// <summary>True if the user has moved or scaled this element from its default.</summary>
        public bool IsCustomized => _hasTargetPosition || _scalePercent != 100;

        private int _scalePercent = 100;
        public int ScalePercent => _scalePercent;

        public void SetScale(int percent)
        {
            _scalePercent = percent;
            EnsureInit();
            if (_rect == null) return;
            float s = percent / 100f;
            _rect.localScale = new Vector3(s, s, 1f);
        }

        // ── Drag handling ──────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!SlotDropHandler.IsEditMode) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;

            _dragging = true;
            _dragOffset = eventData.position - new Vector2(_rect.position.x, _rect.position.y);
        }

        void Update()
        {
            if (!SlotDropHandler.IsEditMode) return;

            // Keep CanvasGroup alpha up (manager handles gameObject.SetActive)
            if (_canvasGroup != null && _canvasGroup.alpha < 0.05f) _canvasGroup.alpha = 1f;
        }

        public bool IsMouseOver()
        {
            if (_rect == null) return false;
            var canvas = _rect.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(_rect, Input.mousePosition, cam);
        }

        void LateUpdate()
        {
            // Lazy init — catches elements that were inactive when AddComponent ran
            EnsureInit();

            // Persistently enforce saved position so the game's layout system can't overwrite it
            if (_hasTargetPosition && !_dragging && _rect != null)
            {
                if ((_rect.anchoredPosition - _targetPosition).sqrMagnitude > 0.01f)
                    _rect.anchoredPosition = _targetPosition;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _rect.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y) - _dragOffset;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;

            // Lock in the new position so LateUpdate enforces it
            _targetPosition = _rect.anchoredPosition;
            _hasTargetPosition = true;

            // Tell parent LayoutGroups to ignore this element
            var le = GetComponent<LayoutElement>();
            if (le == null) le = gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            var pos = GetPosition();
            Plugin.Log.LogMessage($"HUD '{ElementId}': moved to ({pos.x:F1}, {pos.y:F1}).");
            HudMoverManager.Instance?.SavePositions();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!SlotDropHandler.IsEditMode) return;

            int step = eventData.scrollDelta.y > 0 ? 5 : -5;
            int newScale = Mathf.Clamp(_scalePercent + step, 10, 500);
            SetScale(newScale);

            // Sync the config entry if one exists
            if (Plugin.HudElementScale.TryGetValue(ElementId, out var entry))
                entry.Value = newScale;

            HudMoverManager.Instance?.SavePositions();
        }

        // ── Edit mode visuals ──────────────────────────────

        public void EnableEditVisuals()
        {
            // Add Canvas + GraphicRaycaster so drag events work on this element
            if (_addedCanvas == null)
            {
                _addedCanvas = gameObject.GetComponent<Canvas>();
                if (_addedCanvas == null)
                    _addedCanvas = gameObject.AddComponent<Canvas>();
            }
            // Always force high sort order so our handle is on top
            _addedCanvas.overrideSorting = true;
            _addedCanvas.sortingOrder = 100 + SortPriority;

            if (_addedRaycaster == null)
            {
                _addedRaycaster = gameObject.GetComponent<GraphicRaycaster>();
                if (_addedRaycaster == null)
                    _addedRaycaster = gameObject.AddComponent<GraphicRaycaster>();
            }

            // Capture state for restoring
            _wasActive = gameObject.activeSelf;
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup != null)
            {
                _originalAlpha = _canvasGroup.alpha;
            }

            // Force visibility and interactability right now
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (_canvasGroup != null)
            {
                if (_canvasGroup.alpha < 0.05f) _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = true;
            }

            // Disable LayoutGroup/ContentSizeFitter so they don't fight dragging
            _layoutGroup = GetComponent<LayoutGroup>();
            _sizeFitter = GetComponent<ContentSizeFitter>();
            if (_layoutGroup != null) { _hadLayoutGroup = _layoutGroup.enabled; _layoutGroup.enabled = false; }
            if (_sizeFitter != null) { _hadSizeFitter = _sizeFitter.enabled; _sizeFitter.enabled = false; }

            // Disable game scripts that reposition the element each frame
            foreach (var mb in GetComponents<MonoBehaviour>())
            {
                if (mb == this) continue; // skip ourselves
                if (mb is HudMover) continue;
                if (mb is GraphicRaycaster) continue;
                // Disable scripts like InteractionDisplay that override position
                if (mb.GetType().Name == "InteractionDisplay")
                {
                    _gameScript = mb;
                    _hadGameScript = mb.enabled;
                    mb.enabled = false;
                    break;
                }
            }

            // Build the handle (highlight + label in one GameObject)
            if (_handleObj == null)
            {
                _handleObj = new GameObject("HudMover_Handle");
                _handleObj.transform.SetParent(transform, false);
                _handleObj.transform.SetAsLastSibling();

                // Ignore parent LayoutGroups so our handle keeps its own size
                var layoutElem = _handleObj.AddComponent<LayoutElement>();
                layoutElem.ignoreLayout = true;

                // ── Semi-transparent black highlight background ──
                var img = _handleObj.AddComponent<Image>();
                img.color = new Color(0.18f, 0.18f, 0.18f, 0.75f);
                img.raycastTarget = true; // needed for drag events

                var hRect = _handleObj.GetComponent<RectTransform>();

                hRect.anchorMin = new Vector2(0.5f, 0.5f);
                hRect.anchorMax = new Vector2(0.5f, 0.5f);
                hRect.pivot     = new Vector2(0.5f, 0.5f);

                // For tall tutorial rects, find the actual icon child and match it
                var contentRect = AnchorBottom ? FindContentChild(_rect) : null;
                var sizeRef = contentRect ?? _rect;
                float w = Mathf.Clamp(sizeRef.rect.width + 10f, 60f, 150f);
                float h = Mathf.Clamp(sizeRef.rect.height + 10f, 60f, 150f);
                hRect.sizeDelta = new Vector2(w, h);

                if (contentRect != null)
                    hRect.position = contentRect.position;
                else
                    hRect.anchoredPosition = Vector2.zero;

                // ── Label text sits just above the highlight ──
                var labelGO = new GameObject("Label");
                labelGO.transform.SetParent(_handleObj.transform, false);

                var text = labelGO.AddComponent<Text>();
                text.text = ElementId;
                text.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
                text.fontSize = 12;
                text.alignment = TextAnchor.LowerCenter;
                text.color = Color.yellow;
                text.raycastTarget = false;

                var outline = labelGO.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(1f, -1f);

                var labelRect = labelGO.GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0f, 1f);
                labelRect.anchorMax = new Vector2(1f, 1f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.anchoredPosition = new Vector2(0f, 2f);
                labelRect.sizeDelta = new Vector2(0f, 16f);

                // ── Visual Proxy (for hidden elements like Backpack/Bandage) ──
                if (contentRect != null)
                {
                    var sourceImage = contentRect.GetComponent<Image>();
                    var sourceRaw = contentRect.GetComponent<RawImage>();

                    if (sourceImage != null && sourceImage.sprite != null)
                    {
                        var proxyGO = new GameObject("VisualProxy");
                        proxyGO.transform.SetParent(_handleObj.transform, false);
                        var proxyImg = proxyGO.AddComponent<Image>();
                        proxyImg.sprite = sourceImage.sprite;
                        proxyImg.color = new Color(1f, 1f, 1f, 0.7f); // slightly transparent
                        proxyImg.raycastTarget = false;

                        var proxyRect = proxyGO.GetComponent<RectTransform>();
                        proxyRect.anchorMin = new Vector2(0.5f, 0.5f);
                        proxyRect.anchorMax = new Vector2(0.5f, 0.5f);
                        proxyRect.pivot = new Vector2(0.5f, 0.5f);
                        proxyRect.sizeDelta = contentRect.rect.size;
                        proxyRect.anchoredPosition = Vector2.zero;
                    }
                    else if (sourceRaw != null && sourceRaw.texture != null)
                    {
                        var proxyGO = new GameObject("VisualProxy_Raw");
                        proxyGO.transform.SetParent(_handleObj.transform, false);
                        var proxyRaw = proxyGO.AddComponent<RawImage>();
                        proxyRaw.texture = sourceRaw.texture;
                        proxyRaw.color = new Color(1f, 1f, 1f, 0.7f); // slightly transparent
                        proxyRaw.raycastTarget = false;

                        var proxyRect = proxyGO.GetComponent<RectTransform>();
                        proxyRect.anchorMin = new Vector2(0.5f, 0.5f);
                        proxyRect.anchorMax = new Vector2(0.5f, 0.5f);
                        proxyRect.pivot = new Vector2(0.5f, 0.5f);
                        proxyRect.sizeDelta = contentRect.rect.size;
                        proxyRect.anchoredPosition = Vector2.zero;
                    }
                }
            }
            _handleObj.SetActive(true);
        }

        public void DisableEditVisuals()
        {
            if (_handleObj != null) _handleObj.SetActive(false);

            // Remove added Canvas/Raycaster to not interfere with game UI
            if (_addedRaycaster != null)
            {
                Destroy(_addedRaycaster);
                _addedRaycaster = null;
            }
            if (_addedCanvas != null)
            {
                Destroy(_addedCanvas);
                _addedCanvas = null;
            }

            // Restore LayoutGroup/ContentSizeFitter
            if (_layoutGroup != null) _layoutGroup.enabled = _hadLayoutGroup;
            if (_sizeFitter != null) _sizeFitter.enabled = _hadSizeFitter;

            // Restore game script — it will reposition the element, so we apply offset in LateUpdate
            if (_gameScript != null)
            {
                _gameScript.enabled = _hadGameScript;
                _gameScript = null;
            }

            // Restore visibility
            if (!_wasActive) gameObject.SetActive(false);
            if (_canvasGroup != null && _originalAlpha >= 0f) _canvasGroup.alpha = _originalAlpha;
        }

        // ── Position management ────────────────────────────

        public void SetPosition(float x, float y)
        {
            EnsureInit();
            _targetPosition = new Vector2(x, y);
            _hasTargetPosition = true;
            if (_rect == null) return;
            _rect.anchoredPosition = _targetPosition;

            // Tell parent LayoutGroups to ignore this element so they don't fight our position
            var le = GetComponent<LayoutElement>();
            if (le == null) le = gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        public Vector2 GetPosition()
        {
            EnsureInit();
            if (_rect == null) return _targetPosition;
            return _hasTargetPosition ? _targetPosition : _rect.anchoredPosition;
        }

        public void ResetToOriginal()
        {
            EnsureInit();
            _hasTargetPosition = false;
            if (_rect != null) _rect.anchoredPosition = _originalAnchoredPos;

            // Re-enable parent layout control
            var le = GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = false;
        }

        private static RectTransform FindContentChild(RectTransform parent)
        {
            RectTransform fallback = null;
            return FindContentChildRecursive(parent, ref fallback) ?? fallback;
        }

        private static RectTransform FindContentChildRecursive(RectTransform parent, ref RectTransform fallback)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i).GetComponent<RectTransform>();
                if (child == null || child.name == "HudMover_Handle") continue;

                var img = child.GetComponent<Image>();
                var raw = child.GetComponent<RawImage>();

                if (img != null || raw != null)
                {
                    // In Outward, the GameObject might just be called "Image", but the actual Sprite
                    // usually has "Icon" or specific names like "tex_men_equipmentIconEmptyBag" in it.
                    string spriteName = img != null && img.sprite != null ? img.sprite.name : "";
                    string texName = raw != null && raw.texture != null ? raw.texture.name : "";

                    string ident = child.name + spriteName + texName;

                    if (ident.IndexOf("Icon", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        ident.IndexOf("EmptyBag", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        ident.IndexOf("Bandage", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return child;
                    }

                    if (fallback == null) fallback = child;
                }

                var deeper = FindContentChildRecursive(child, ref fallback);
                if (deeper != null) return deeper;
            }
            return null;
        }
    }
}
