using ModifAmorphic.Outward.Unity.ActionUI;
using ModifAmorphic.Outward.Unity.ActionUI.Data;
using ModifAmorphic.Outward.Unity.ActionUI.EquipmentSets;
using ModifAmorphic.Outward.Unity.ActionUI.Extensions;
using ModifAmorphic.Outward.Unity.ActionUI.Models.EquipmentSets;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.UI.Dropdown;

namespace ModifAmorphic.Outward.Unity.ActionMenus
{
    public enum EquipmentSetTypes
    {
        Weapon,
        Armor
    }
    [UnityScriptComponent]
    public class EquipmentSetView : MonoBehaviour
    {
        public PlayerActionMenus PlayerMenu;

        public EquipmentSetTypes EquipmentSetType;

        public Dropdown EquipmentSetDropdown;
        public Button NewSetButton;
        public Button RenameSetButton;
        public Button SaveSetButton;
        public Button DeleteSetButton;
        public Dropdown EquipmentIconDropdown;

        public ActionItemView EquipmentIcon;

        public EquipmentSetNameInput SetNamePanel;
        public ConfirmationPanel ConfirmationPanel;

        private void Awake()
        {
        }

        void Start()
        {
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Refresh()
        {
        }
    }
}