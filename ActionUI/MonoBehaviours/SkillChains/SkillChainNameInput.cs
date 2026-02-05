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

namespace ModifAmorphic.Outward.Unity.ActionMenus
{
    [UnityScriptComponent]
    public class SkillChainNameInput : MonoBehaviour
    {
        public InputField NameInput;
        public Button OkButton;
        public Text Caption;
        public Text DisplayText;

        public SkillChainMenu SkillChainMenu;

        public bool IsShowing => gameObject.activeSelf;

        public UnityEvent OnShow { get; } = new UnityEvent();

        public UnityEvent OnHide { get; } = new UnityEvent();


        private void Awake()
        {
            Hide(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            OnShow?.TryInvoke();
        }

        public void Show(string chainName)
        {
            gameObject.SetActive(true);
            OnShow?.TryInvoke();
        }

        public void Hide() => Hide(true);
        private void Hide(bool raiseEvent)
        {
            gameObject.SetActive(false);
            if (raiseEvent)
            {
                OnHide?.TryInvoke();
            }
        }

    }
}