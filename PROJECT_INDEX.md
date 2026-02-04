# ActionBar Mod - Project Index

**Project:** Outward Mod - ActionBar (formerly ActionUI)  
**Author:** fierrof  
**Current Version:** 1.1.2  
**Purpose:** Expand QuickSlots with configurable Action Slots, customize hotbar layout, reposition HUD elements

## Build & Distribution

- **Build Script:** `build_mod.ps1` - reads version from manifest.json, outputs to `bin/fierrof-ActionBar-{version}.zip`
- **Manifest:** `ActionUI.Plugin/manifest.json` - version control (currently 1.0.3)
- **Asset Bundle:** Pre-built Unity bundle at `Assets/asset-bundles/action-ui` (copied during build, not rebuilt)
- **Install Location:** r2modman Default profile (`C:\Users\fierr\AppData\Roaming\r2modmanPlus-local\OutwardDe\profiles\Default\`)

## Solution Structure

### ActionUI/ (Core Library - netstandard2.0)
- **Controllers/**: HotbarsController, ActionSlotController interfaces and implementations
- **Data/**: Profile services (Hotbar, Equipment Sets, Skill Chains, Positions), ProfileManager
- **Extensions/**: Unity component extensions (GameObject, Transform, Event, etc.)
- **Models/**: Enums (EquipmentEnums, HotkeyCategories), interfaces (IActionMenu, IActionSlotConfig)
- **MonoBehaviours/**: Unity components
  - **SettingViews/**: SettingsView.cs - UI toggle bindings (durability toggle hidden at lines 57-61)
  - `PlayerActionMenus.cs` - Main menu container, references DurabilityDisplay (line 16)
  - `DurabilityDisplay.cs` - Durability UI component (disabled)
  - `UIPositionScreen.cs` - Move UI Elements feature
- **Services/**: Core services logic

### ActionUI.Plugin/ (BepInEx Plugin - netstandard2.0)
- **Entry Point:** `ActionUIPlugin.cs` - BepInEx plugin initialization
- **Startup:** 
  - `Startup.cs` - Service registration and asset bundle loading (line 116)
  - `HotbarsStartup.cs` - Hotbar system initialization
  - `InventoryStartup.cs` - Inventory integration (localization registration after scene load)
  - `DurabilityDisplayStartup.cs` - DISABLED (lines 81-86 removed, 100-101 commented)
- **Services/**:
  - `PlayerMenuService.cs` - Injects PlayerActionMenus into character UI (line 122)
  - `PositionsService.cs` - Handles Move UI Elements feature, blocklist at lines 24-27
  - `DurabilityDisplayService.cs` - DISABLED service (not registered, not started)
- **Patches/**: Harmony patches
  - `EquipmentPatches.cs` - Equipment events (still used by non-durability features)
- **Settings/**: `ActionUISettings.cs` - Default profile (DurabilityDisplayEnabled = false, line 29)
- **DataModels/**: `ActionUIProfile.cs` - Profile data structure
- **Trackers/**: DurabilityTracker, DurabilityActionSlotTracker (unused, not removed)

### Unity.ActionUI.AssetBundles/ (Unity Project)
- **Assets/Prefabs/**:
  - `ActionUI.prefab` - Main prefab with PlayerActionMenus component, references DurabilityDisplay (line 1113)
  - `Durability.prefab` - Standalone durability UI prefab (contains PositionableBg)
  - `PositionableBg.prefab` - Frame/border shown in Move UI mode
- **Note:** Asset bundles are pre-built and copied, not rebuilt during mod compilation

### Outward.Shared/ModifAmorphic.Outward.Shared/ (Shared Library)
- Common utilities, logging, coroutines, localization
- `LocalizationModule.cs` - Handles game localization registration
- **DLL References:** Point to r2modman Default profile cache

## Key Features

### Hotbars System (Active)
- Configurable action slots
- Hotkey management
- Skill chain support
- Equipment set management

### Move UI Elements (Active)
- **Service:** `PositionsService.cs`
- **Blocklist:** Lines 24-27 - excludes specific HUD elements from repositioning
- **Currently Blocked:** CorruptionSmog, PanicOverlay, TargetingFlare, CharacterBars, LowHealth, LowStamina, Chat - Panel, Durability
- **Implementation:** Adds `PositionableUI` component to HUD children, shows frame/border in positioning mode

### Durability Display (REMOVED in v1.0.1+)
- **Status:** Completely disabled
- **Changes Made:**
  - Service registration removed from Startup.cs (lines 81-86, removed in v1.0.5)
  - Startup disabled (lines 100-101 commented)
  - GameObject explicitly disabled in PlayerMenuService.cs after instantiation (v1.0.5)
  - Default setting changed to false (ActionUISettings.cs line 29)
  - UI toggle hidden (SettingsView.cs lines 57-61)
  - Blocklist entry added to prevent frame in Move UI mode (PositionsService.cs line 27)
- **Remaining Code:** Service/tracker files not removed (no runtime impact if not instantiated)

## Common Tasks

### Change Version
1. Edit `ActionUI.Plugin/manifest.json` line 3
2. Run `.\build_mod.ps1`
3. Output: `bin/fierrof-ActionBar-{version}.zip`

### Add HUD Element to Move UI Blocklist
- Edit `ActionUI.Plugin/Services/PositionsService.cs` lines 24-27
- Add element HUD GameObject name to HashSet

### Disable a Feature
1. Remove/comment service registration in `ActionUI.Plugin/Startup.cs`
2. Remove/comment startup call in `ActionUI.Plugin/Startup.cs`
3. Update default settings in `ActionUI.Plugin/Settings/ActionUISettings.cs`
4. Hide UI toggles in relevant SettingsView files

## Known Issues & Notes

### Localization Timing
- LocalizationManager.Instance not ready during Unity Awake
- Solution: Register localizations in scene loaded callback (InventoryStartup.cs)

### Asset Bundle Modifications
- Require Unity project rebuild
- Pre-built bundle copied from `Assets/asset-bundles/action-ui`
- Prefab changes (like removing DurabilityDisplay) need Unity Editor

### DLL References
- Point to r2modman Default profile: `C:\Users\fierr\AppData\Roaming\r2modmanPlus-local\OutwardDe\`
- BepInEx, Outward game DLLs, dependency mods (SideLoader, etc.)

## File Patterns

### Startup Services
- Pattern: `{Feature}Startup.cs` in ActionUI.Plugin/
- Registered in `Startup.cs` services container
- Started via `Starter.TryStart()` call

### UI Components (MonoBehaviours)
- Core components in `ActionUI/MonoBehaviours/`
- Unity ScriptComponent attribute for prefab binding
- Instantiated from asset bundle prefabs

### Settings & Profiles
- Interfaces in `ActionUI/Data/I{Feature}Profile.cs`
- Implementations in `ActionUI.Plugin/DataModels/{Feature}Profile.cs`
- Services in `ActionUI/Data/I{Feature}ProfileService.cs` and implementations

### Harmony Patches
- Located in `ActionUI.Plugin/Patches/`
- Pattern: `{GameClass}Patches.cs`
- Events exposed for service subscriptions
