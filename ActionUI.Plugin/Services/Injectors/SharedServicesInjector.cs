using ModifAmorphic.Outward.ActionUI.Patches;
using ModifAmorphic.Outward.ActionUI.Settings;
using ModifAmorphic.Outward.Logging;
using ModifAmorphic.Outward.Unity.ActionMenus;
using ModifAmorphic.Outward.Unity.ActionUI.Data;
using Rewired;
using System;
using System.IO;

namespace ModifAmorphic.Outward.ActionUI.Services.Injectors
{
    public class SharedServicesInjector
    {
        private readonly ServicesProvider _services;

        Func<IModifLogger> _getLogger;
        private IModifLogger Logger => _getLogger.Invoke();

        public delegate void SharedServicesInjectedDelegate(int playerID, string characterUID);
        public event SharedServicesInjectedDelegate OnSharedServicesInjected;

        public SharedServicesInjector(ServicesProvider services, Func<IModifLogger> getLogger)
        {
            (_services, _getLogger) = (services, getLogger);

            //NetworkInstantiateManagerPatches.BeforeAddLocalPlayer += (manager, playerId, save) => AddProfileManager(playerId, save.CharacterUID);
            SplitPlayerPatches.SetCharacterAfter += AddSharedServices;
        }

        private void AddSharedServices(SplitPlayer splitPlayer, Character character)
        {
            var usp = Psp.Instance.GetServicesProvider(splitPlayer.RewiredID);
            var player = ReInput.players.GetPlayer(splitPlayer.RewiredID);

            // Register Global Services to Player Provider so they can be resolved by player components
            usp.AddSingleton(_services.GetService<IHotbarProfileService>());
            usp.AddSingleton(_services.GetService<IPositionsProfileService>()); // Assuming added in Startup
            usp.AddSingleton(_services.GetService<IActionUIProfileService>());

            usp
                .AddSingleton(new SlotDataService(player
                                        , splitPlayer.AssignedCharacter
                                        , usp.GetService<IHotbarProfileService>()
                                        , _getLogger));
        }

    }
}
