# Action Bar (Modified by fierrof)

This is a modified version of the original **Action UI** mod by **ModifAmorphic**, focused strictly on the Action Bar and UI reordering features.

## Credits
- **Original Author**: [ModifAmorphic](https://github.com/ModifAmorphic)
- **Modified by**: fierrof
- **Source Code**: [GitHub Repository](https://github.com/facufierro/Outward---ActionBar)

## License
This project is licensed under the **GNU General Public License v3.0 (GPL-3.0)**, consistent with the original project's license. Source code must remain open.

## Changes in 1.2.1

**Per-Character Slot Saving:**
- Skills and items assigned to action bar slots are now saved per-character instead of globally
- Each character's slot assignments are stored in `BepInEx/config/ActionUI_CharacterSlots/{characterUID}.json`
- Disabled slot states are also saved per-character
- Layout settings (rows, slots, scale, etc.) remain global

## Changes in 1.2.0

This version fixes several critical UI refresh and settings bugs:

**UI Bug Fixes:**
- **Fixed Persistent UI Element**: Removed lingering "Skill Chain" UI window by implementing recursive GameObject destruction in PlayerActionMenus.
- **Fixed Disabled Slot Visual Bug**: Disabled slots now correctly remain hidden when keybinds are changed by prioritizing IsDisabled check in AssignEmptyAction.
- **Fixed UI Refresh Loop**: Prevented infinite refresh loop that was blocking inputs by adding recursion guard in GlobalHotbarService.
- **Fixed Disabled Slots Blinking**: Disabled slots no longer flicker when pausing the game by adding alpha change guards in AssignEmptyAction.

**Settings Fixes:**
- **Fixed "Hide Left Nav" Setting**: Now properly updates by implementing complete settings synchronization in SaveNew.
- **Fixed "Empty Slot Display" Setting**: Fixed both the initial sync issue and dropdown update issue by implementing complete settings sync and per-slot propagation.

**Code Cleanup:**
- Removed obsolete ChainedSkill.cs file that was causing build errors.

## Changes in 1.0.0
This version specifically streamlines the mod and fixes key issues:

- **Added Global Saved Configuration**: Settings now persist globally across characters and sessions.
- **Config Manager Migration**: Migrated to Mefino's Config Manager and disabled the old custom settings menu.
- **Hide Slot Functionality**: Added the ability to hide/disable specific action slots (Right-Click in Hotkey Mode).
- **Bug Fix**: Disabled right-click functionality for adding skills to specific slots to fix a bug where "Summon Will-o-Wisp" was incorrectly appearing.
- **Streamlined Features**:
    - **Disabled** Equipment Sets.
    - **Disabled** Durability functions (only the Action Bar overlay remains).
    - **Disabled** all features not related to the Action Bar and UI reordering.

***

## Highly Configurable Hotbars and Action Slots

![Hotbars](https://github.com/ModifAmorphic/outward/blob/master/ActionUI/WikiReadmeAssets/Hotbar.png?raw=true)

### Key Features
- **Multiple Hotbars**: Add additional rows and slots.
- **UI Positioning**: Reposition user interface elements.
- **Global Saving**: Configurations are now saved globally.

[ ![Assign Actions YouTube Video](https://github.com/ModifAmorphic/outward/blob/master/ActionUI/WikiReadmeAssets/AssignActionSlotVideo.png?raw=true) ](https://youtu.be/nJT76DLFIqw)