using ModifAmorphic.Outward.Unity.ActionUI;
using ModifAmorphic.Outward.Unity.ActionUI.Data;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ModifAmorphic.Outward.Unity.ActionMenus
{
    [UnityScriptComponent]
    public class StorageSettingsView : MonoBehaviour, ISettingsView
    {
        public MainSettingsMenu MainSettingsMenu;

        public Toggle DisplayCurrencyToggle;

        public Toggle StashInventoryToggle;
        public Toggle StashInventoryAnywhereToggle;

        public Toggle MerchantStashToggle;
        public Toggle MerchantStashAnywhereToggle;

        public Toggle CraftFromStashToggle;
        public Toggle CraftFromStashAnywhereToggle;

        public Toggle PreserveFoodToggle;
        public InputField PreserveFoodAmount;

        public bool IsShowing => gameObject.activeSelf;

        public UnityEvent OnShow;

        public UnityEvent OnHide;

        private void Awake()
        {
            if (OnShow == null)
                OnShow = new UnityEvent();
            if (OnHide == null)
                OnHide = new UnityEvent();
        }

        private void Start()
        {
        }

        public void Show()
        {
            gameObject.SetActive(true);
            OnShow?.Invoke();
        }
        public void Hide()
        {
            gameObject.SetActive(false);
            OnHide?.Invoke();
        }
    }
}