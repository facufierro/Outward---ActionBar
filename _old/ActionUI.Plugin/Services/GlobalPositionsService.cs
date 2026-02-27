using ModifAmorphic.Outward.ActionUI.Settings;
using ModifAmorphic.Outward.Unity.ActionUI.Data;
using Newtonsoft.Json;
using System;
using System.Linq;

namespace ModifAmorphic.Outward.ActionUI.Services
{
    public class GlobalPositionsService : IPositionsProfileService
    {
        private PositionsProfile _cachedProfile;
        public event Action<PositionsProfile> OnProfileChanged;

        public GlobalPositionsService()
        {
            LoadProfile();
        }

        private void LoadProfile()
        {
            var json = ActionUISettings.SerializedPositions.Value;
            if (string.IsNullOrEmpty(json))
            {
                _cachedProfile = new PositionsProfile();
            }
            else
            {
                try
                {
                    _cachedProfile = JsonConvert.DeserializeObject<PositionsProfile>(json);
                }
                catch
                {
                    _cachedProfile = new PositionsProfile();
                }
            }
        }

        public PositionsProfile GetProfile()
        {
            if (_cachedProfile == null) LoadProfile();
            return _cachedProfile;
        }

        public void Save()
        {
            SaveNew(GetProfile());
        }

        public void SaveNew(PositionsProfile positionsProfile)
        {
             var json = JsonConvert.SerializeObject(positionsProfile, Formatting.None);
             if (ActionUISettings.SerializedPositions.Value != json)
             {
                 ActionUISettings.SerializedPositions.Value = json;
             }
             OnProfileChanged?.Invoke(positionsProfile);
        }

        public void AddOrUpdate(UIPositions position)
        {
             GetProfile().AddOrReplacePosition(position);
             Save();
        }

        public void Remove(UIPositions position)
        {
            if (GetProfile().RemovePosition(position))
            {
                Save();
            }
        }
    }
}
