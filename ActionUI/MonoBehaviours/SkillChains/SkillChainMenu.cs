using ModifAmorphic.Outward.Unity.ActionUI;
using ModifAmorphic.Outward.Unity.ActionUI.Data;
using ModifAmorphic.Outward.Unity.ActionUI.EquipmentSets;
using ModifAmorphic.Outward.Unity.ActionUI.Extensions;
using ModifAmorphic.Outward.Unity.ActionUI.Models.EquipmentSets;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static UnityEngine.UI.Dropdown;

namespace ModifAmorphic.Outward.Unity.ActionMenus
{

    [UnityScriptComponent]
    public class SkillChainMenu : MonoBehaviour, IActionMenu
    {
        public RectTransform ParentTransform;
        public Button ToggleChainsButton;

        public PlayerActionMenus PlayerMenus;

        public Dropdown SkillChainsDropdown;
        public Button NewChainButton;
        public Button RenameChainButton;
        public Button SaveChainButton;
        public Button DeleteChainButton;

        public ActionItemView ActionPrefab;
        public GridLayoutGroup ActionsGrid;

        public SkillChainNameInput SetNamePanel;
        public ConfirmationPanel ConfirmationPanel;


        public bool IsShowing => gameObject.activeSelf;

        public UnityEvent OnShow { get; private set; } = new UnityEvent();

        public UnityEvent OnHide { get; private set; } = new UnityEvent();


        private void Awake()
        {
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide() => Hide(false);

        public void Hide(bool forceHide)
        {
            gameObject.SetActive(false);
        }

        public void ToggleMenu()
        {
            if (!IsShowing)
                Show();
            else
                Hide(true);
        }
    }
}