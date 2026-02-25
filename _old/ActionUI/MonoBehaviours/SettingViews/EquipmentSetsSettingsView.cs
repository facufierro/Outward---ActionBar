using ModifAmorphic.Outward.Unity.ActionUI;
using ModifAmorphic.Outward.Unity.ActionUI.Data;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModifAmorphic.Outward.Unity.ActionMenus
{
    [UnityScriptComponent]
    public class EquipmentSetsSettingsView : MonoBehaviour, ISettingsView
    {

        public Toggle ArmorSetsCombat;
        public Toggle SkipWeaponAnimation;
        public Toggle EquipFromStash;
        public Toggle StashEquipAnywhere;
        public Toggle UnequipToStash;
        public Toggle StashUnequipAnywhere;

        public MainSettingsMenu MainSettingsMenu;

        public bool IsShowing => gameObject.activeSelf;

        public UnityEvent OnShow { get; } = new UnityEvent();

        public UnityEvent OnHide { get; } = new UnityEvent();


        private void Awake()
        {
        }

        private void Start()
        {
        }

        public void Show()
        {
            gameObject.SetActive(true);
            OnShow?.Invoke();
        }

        public void Hide() => gameObject.SetActive(false);

    }
}