# Action Bar

A complete overhaul of the Outward Action Bar functionality, focused on stability and configuration.

## Features

### Added
- **Hide/Disable Slots**: Right-click in Hotkey Mode to toggle slot visibility.
- **Per-Character Skill Saving**: Each character saves their own skill/item assignments.
- **Global Layout Config**: Rows, slots, scale, disabled slots shared across all characters.
- **Config Manager Integration**: Uses Mefino's Config Manager instead of custom menu.

### Removed
- Equipment Sets
- Skill Chains
- Durability Display functions
- Right-click "Add Skill" context menu

## ⚠️ INCOMPATIBILITY NOTICE ⚠️
**This mod (`ActionBar_Redux`) is a complete rewrite and replacement.**

**It is NOT COMPATIBLE with:**
1. The old `ActionUI` mod.
2. The old deprecated `ActionBar` mod.

> **YOU MUST DELETE BOTH OF THESE OLD MODS IF INSTALLED.**
> **FAILING TO REMOVE THEM WILL CAUSE CRITICAL CONFLICTS.**

## Installation
1. Ensure you have BepInEx installed.
2. Extract the `ActionBar` folder into your `BepInEx/plugins` directory.

## Configuration
Access the configuration menu via **Config Manager** (F5 by default) in-game.
- **General**: Toggle the mod, adjust scale, and HUD visibility.
- **Layout**: Configure rows and slots per row.
- **Hotkeys**: Set up your custom keybindings.

To disable/hide specific slots:
1. Enter **Hotkey Mode** (default binding in controls).
2. **Right-click** on any slot to toggle it disabled/hidden.