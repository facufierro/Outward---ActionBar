using ModifAmorphic.Outward.ActionUI.Patches;
using ModifAmorphic.Outward.Coroutines;
using ModifAmorphic.Outward.Extensions;
using ModifAmorphic.Outward.Logging;
using ModifAmorphic.Outward.Unity.ActionMenus;
using ModifAmorphic.Outward.Unity.ActionUI.Data;
using ModifAmorphic.Outward.Unity.ActionUI.EquipmentSets;
using System;

namespace ModifAmorphic.Outward.ActionUI.Services
{
    internal class ResetActionUIsService
    {
        private IModifLogger Logger => _getLogger.Invoke();
        private readonly Func<IModifLogger> _getLogger;

        private readonly ServicesProvider _services;
        private readonly LevelCoroutines _coroutine;


        public ResetActionUIsService(
                                ServicesProvider services,
                                LevelCoroutines coroutine,
                                Func<IModifLogger> getLogger)
        {
            _services = services;
            _coroutine = coroutine;
            _getLogger = getLogger;

            CharacterUIPatches.BeforeReleaseUI += ResetUIs;
            LobbySystemPatches.BeforeClearPlayerSystems += ResetAllPlayerUIs;
        }

        private void ResetAllPlayerUIs(LobbySystem lobbySystem)
        {
            var players = lobbySystem.PlayersInLobby.FindAll(p => p.IsLocalPlayer);
            foreach (var p in players)
            {
                ResetUIs(p.ControlledCharacter.CharacterUI, p.PlayerID);
            }
        }

        /*
        private void SaveProfiles(CharacterUI characterUI, int rewiredId)
        {
            // Implementation commented out due to removal of ProfileManager and JSON services.
            // Saving is now handled by BepInEx ConfigurationManager.
        }

        private void SaveProfile(ISavableProfile profileService, string characterUID)
        {
             // Implementation commented out.
        }
        */

        private void ResetUIs(CharacterUI characterUI, int rewiredId)
        {
            try
            {
                //SaveProfiles(characterUI, rewiredId);
            }
            catch (Exception ex)
            {
                Logger.LogException($"Unexpected error occured when saving profiles for character {characterUI.TargetCharacter.UID}.", ex);
            }

            Logger.LogDebug($"Destroying Action UIs for player {rewiredId}");
            if (Psp.Instance.TryGetServicesProvider(rewiredId, out var usp) && usp.TryGetService<PositionsService>(out var posService))
            {
                try
                {
                    posService.DestroyPositionableUIs(characterUI);
                }
                catch (Exception ex)
                {
                    Logger.LogException("Dispose of PositionableUIs failed.", ex);
                }
            }

            Psp.Instance.TryDisposeServicesProvider(rewiredId);
            CharacterUIPatches.GetIsMenuFocused.TryRemove(rewiredId, out _);

            try
            {
                characterUI.GetComponentInChildren<EquipmentSetMenu>(true).gameObject.Destroy();
            }
            catch (Exception ex)
            {
                Logger.LogException($"Dispose of {nameof(EquipmentSetMenu)} gameobject failed.", ex);
            }

            try
            {
                characterUI.GetComponentInChildren<PlayerActionMenus>(true).gameObject.Destroy();
            }
            catch (Exception ex)
            {
                Logger.LogException($"Dispose of {nameof(PlayerActionMenus)} gameobject failed.", ex);
            }
        }
    }
}
