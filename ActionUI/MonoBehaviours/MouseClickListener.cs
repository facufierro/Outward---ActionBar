using ModifAmorphic.Outward.Unity.ActionUI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace ModifAmorphic.Outward.Unity.ActionMenus
{
    [UnityScriptComponent]
    public class MouseClickListener : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public UnityEvent OnLeftClick = new UnityEvent();
        public UnityEvent OnRightClick = new UnityEvent();
        public UnityEvent OnMiddleClick = new UnityEvent();

        private bool _isPointerOver;
        private float _lastMiddleClickInvokeTime;
        private const float MiddleClickDebounceSeconds = 0.08f;

        private void Update()
        {
            if ((_isPointerOver || IsPointerInsideSelf()) && (Input.GetMouseButtonDown(2) || Input.GetKeyDown(KeyCode.Mouse2)))
            {
                InvokeMiddleClick();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                OnLeftClick?.Invoke();
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                OnRightClick?.Invoke();
            }
            else if (eventData.button == PointerEventData.InputButton.Middle)
            {
                InvokeMiddleClick();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Middle)
            {
                InvokeMiddleClick();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerOver = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerOver = false;
        }

        private void InvokeMiddleClick()
        {
            if (Time.unscaledTime - _lastMiddleClickInvokeTime < MiddleClickDebounceSeconds)
                return;

            _lastMiddleClickInvokeTime = Time.unscaledTime;
            OnMiddleClick?.Invoke();
        }

        private bool IsPointerInsideSelf()
        {
            if (!(transform is RectTransform rectTransform))
                return false;

            Camera eventCamera = null;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                eventCamera = canvas.worldCamera;

            return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition, eventCamera);
        }
    }
}
