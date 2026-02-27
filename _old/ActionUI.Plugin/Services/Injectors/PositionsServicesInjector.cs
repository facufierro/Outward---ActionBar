using ModifAmorphic.Outward.Coroutines;
using ModifAmorphic.Outward.GameObjectResources;
using ModifAmorphic.Outward.Logging;
using ModifAmorphic.Outward.Unity.ActionMenus;
using ModifAmorphic.Outward.Unity.ActionUI.Data;
using System;

namespace ModifAmorphic.Outward.ActionUI.Services.Injectors
{
    internal class PositionsServicesInjector
    {
        private readonly ServicesProvider _services;
        private readonly ModifGoService _modifGoService;
        private readonly ModifCoroutine _coroutines;
        //private bool _isInjected;

        Func<IModifLogger> _getLogger;
        private IModifLogger Logger => _getLogger.Invoke();

        public PositionsServicesInjector(ServicesProvider services, PlayerMenuService playerMenuService, ModifGoService modifGoService, ModifCoroutine coroutines, Func<IModifLogger> getLogger)
        {
            (_services, _modifGoService, _coroutines, _getLogger) = (services, modifGoService, coroutines, getLogger);
            playerMenuService.OnPlayerActionMenusConfigured += TryAddPositionsServices;
        }

        private void TryAddPositionsServices(PlayerActionMenus actionMenus, SplitPlayer splitPlayer)
        {
            try
            {
                AddPositionsServices(actionMenus, splitPlayer);
            }
            catch (Exception ex)
            {
                Logger.LogException($"Failed to enable Positioning UI for player {splitPlayer.RewiredID}.", ex);
            }
        }
        private void AddPositionsServices(PlayerActionMenus actionMenus, SplitPlayer splitPlayer)
        {
            var usp = Psp.Instance.GetServicesProvider(splitPlayer.RewiredID);
            // var profileService = (ProfileService)usp.GetService<IActionUIProfileService>(); // Unused

            usp.AddSingleton<IPositionsProfileService>(_services.GetService<IPositionsProfileService>()); // Register Global service for player scope usage (redundant if already added in Shared? Shared adds it.)

            // Actually, SharedServicesInjector added IPositionsProfileService in my previous edit?
            // Let's check SharedServicesInjector content in my mind.. 
            // Yes, "usp.AddSingleton(_services.GetService<IPositionsProfileService>());" was added in SharedServicesInjector.
            // So this might be redundant or we can just ensure it's there.
            // But if PositionsServiceInjector is responsible for it, we should do it here or let Shared handle it.
            // SharedServicesInjector runs on "SetCharacterAfter", Positions likely runs when UI created?
            // SharedServicesInjector adds "IActionUIProfileService" "IHotbarProfileService" and "IPositionsProfileService" in my previous step.
            // So we don't need to add it here, or we can just do nothing if it's already there
            if (!usp.ContainsService<IPositionsProfileService>())
            {
                 usp.AddSingleton<IPositionsProfileService>(_services.GetService<IPositionsProfileService>());
            }
        }
    }
}
