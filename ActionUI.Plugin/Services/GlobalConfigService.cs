using BepInEx.Configuration;
//using ModifAmorphic.Outward.ActionUI.Config; // Removed invalid namespace
using ModifAmorphic.Outward.ActionUI.Settings;
using ModifAmorphic.Outward.Unity.ActionUI.Data;
using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

namespace ModifAmorphic.Outward.ActionUI.Services
{
    public class GlobalConfigService
    {
        public static GlobalConfigService Instance { get; private set; }

        public PositionsProfile PositionsProfile { get; private set; }
        public IHotbarProfile NavPanelProfile { get; private set; } // Will need to adapt this

        public GlobalConfigService()
        {
            Instance = this;
            LoadPositions();
            // Hotbars will be loaded/parsed as needed or here
        }

        public void LoadPositions()
        {
            if (string.IsNullOrEmpty(ActionUISettings.SerializedPositions.Value))
            {
                PositionsProfile = new PositionsProfile();
            }
            else
            {
                try
                {
                    PositionsProfile = JsonConvert.DeserializeObject<PositionsProfile>(ActionUISettings.SerializedPositions.Value);
                }
                catch
                {
                    PositionsProfile = new PositionsProfile();
                }
            }
        }

        public void SavePositions()
        {
            if (PositionsProfile == null) return;
            ActionUISettings.SerializedPositions.Value = JsonConvert.SerializeObject(PositionsProfile, Formatting.None);
        }
        
        // Similar for Hotbars, need to check IHotbarProfile implementation
    }
}
