using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Attached to a game HUD element to make it draggable in Edit Mode.
    /// Stores the original position so it can be reset.
    /// </summary>
    public class HudMover : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public string ElementId;
        public bool AnchorBottom;
        public int SortPriority;

        private RectTransform _rect;
        private Vector2 _originalAnchoredPos;
        private Vector2 _dragOffset;
        private bool _dragging;

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
        private bool _hasPositionOverride; // true if a game script fights our position
        private Vector2 _positionOffset;   // user offset applied in LateUpdate

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _originalAnchoredPos = _rect.anchoredPosition;
        }

        public Vector2 OriginalPosition => _originalAnchoredPos;

        private int _scalePercent = 100;
        public int ScalePercent => _scalePercent;

        public void SetScale(int percent)
        {
            _scalePercent = percent;
            if (_rect == null) return;
            float s = percent / 100f;
            _rect.localScale = new Vector3(s, s, 1f);
        }

        // ── Drag handling ──────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (ElementId == "Interact Tooltip")
                Plugin.Log.LogMessage($"[HudMover DEBUG] OnBeginDrag fired for {ElementId}, editMode={SlotDropHandler.IsEditMode}, button={eventData.button}, _rect null={_rect == null}");

            if (!SlotDropHandler.IsEditMode) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;

            _dragging = true;
            _dragOffset = eventData.position - new Vector2(_rect.position.x, _rect.position.y);
        }

        void Update()
        {
            if (SlotDropHandler.IsEditMode)
            {
                // Keep CanvasGroup alpha up (manager handles gameObject.SetActive)
                if (_canvasGroup != null && _canvasGroup.alpha < 0.05f) _canvasGroup.alpha = 1f;
            }
        }

        void LateUpdate()
        {
            // Apply user offset after game scripts reposition the element
            if (_hasPositionOverride && !SlotDropHandler.IsEditMode && !_dragging && _rect != null)
            {
                // The game script sets anchoredPosition each frame; we add our offset on top
                // Only apply if offset is non-zero to avoid unnecessary writes
                if (_positionOffset.sqrMagnitude > 0.01f)
                    _rect.anchoredPosition = _originalAnchoredPos + _positionOffset;
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

            if (_hasPositionOverride)
                _positionOffset = _rect.anchoredPosition - _originalAnchoredPos;

            var pos = GetPosition();
            Plugin.Log.LogMessage($"HUD '{ElementId}': moved to ({pos.x:F1}, {pos.y:F1}).");
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
                    _hasPositionOverride = true;
                    break;
                }
            }

            // ── Debug dump for troubleshooting handle visibility ──
            if (ElementId == "Interact Tooltip")
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[HudMover DEBUG] === {ElementId} ===");
                sb.AppendLine($"  GO active: {gameObject.activeSelf}, activeInHierarchy: {gameObject.activeInHierarchy}");
                sb.AppendLine($"  _rect null: {_rect == null}");
                if (_rect != null)
                {
                    sb.AppendLine($"  _rect.rect: w={_rect.rect.width}, h={_rect.rect.height}");
                    sb.AppendLine($"  _rect.sizeDelta: {_rect.sizeDelta}");
                    sb.AppendLine($"  _rect.anchorMin: {_rect.anchorMin}, anchorMax: {_rect.anchorMax}");
                    sb.AppendLine($"  _rect.pivot: {_rect.pivot}");
                    sb.AppendLine($"  _rect.localScale: {_rect.localScale}");
                    sb.AppendLine($"  _rect.position: {_rect.position}");
                    sb.AppendLine($"  _rect.anchoredPosition: {_rect.anchoredPosition}");
                }
                sb.AppendLine($"  AnchorBottom: {AnchorBottom}");
                // Dump all components on this GO
                var comps = gameObject.GetComponents<Component>();
                sb.AppendLine($"  Components on GO ({comps.Length}):");
                foreach (var c in comps)
                    sb.AppendLine($"    - {c.GetType().Name}");
                // Dump children
                sb.AppendLine($"  Children ({transform.childCount}):");
                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i);
                    var cRect = child.GetComponent<RectTransform>();
                    string size = cRect != null ? $"w={cRect.rect.width}, h={cRect.rect.height}, sizeDelta={cRect.sizeDelta}" : "no RectTransform";
                    var childComps = child.GetComponents<Component>();
                    string compNames = "";
                    foreach (var cc in childComps) compNames += cc.GetType().Name + ", ";
                    sb.AppendLine($"    [{i}] '{child.name}' active={child.gameObject.activeSelf} | {size} | {compNames}");
                }
                // Dump parent info
                if (transform.parent != null)
                {
                    var pRect = transform.parent.GetComponent<RectTransform>();
                    sb.AppendLine($"  Parent: '{transform.parent.name}'");
                    if (pRect != null)
                        sb.AppendLine($"    parent rect: w={pRect.rect.width}, h={pRect.rect.height}");
                    var pComps = transform.parent.GetComponents<Component>();
                    sb.AppendLine($"    parent components:");
                    foreach (var pc in pComps) sb.AppendLine($"      - {pc.GetType().Name}");
                }
                Plugin.Log.LogMessage(sb.ToString());
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
            if (_hasPositionOverride)
                _positionOffset = new Vector2(x, y) - _originalAnchoredPos;
            else
                _rect.anchoredPosition = new Vector2(x, y);
        }

        public Vector2 GetPosition()
        {
            if (_hasPositionOverride)
                return _originalAnchoredPos + _positionOffset;
            return _rect.anchoredPosition;
        }

        public void ResetToOriginal()
        {
            _positionOffset = Vector2.zero;
            _hasPositionOverride = false;
            _rect.anchoredPosition = _originalAnchoredPos;
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
