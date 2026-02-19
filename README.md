# Action Bar

A complete overhaul of the Outward Action Bar functionality, focused on stability and configuration, including per-slot empty-style cycling and weapon-based dynamic slots.

## Features

### Added
- **Per-Slot Empty Style**: Right-click in Hotkey Mode to cycle slot style (Hidden → Image → Transparent).
- **Dynamic Slots**: Middle-click in slot configuration mode to mark slots as dynamic and swap assignments by equipped weapon presets.
- **Per-Character Skill Saving**: Each character saves their own skill/item assignments.
- **Global Layout Config**: Rows, slots, and scale shared across all characters.
- **UI Positioning**: Position Hotbars with X/Y sliders or Drag & Drop.
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

**YOU MUST DELETE BOTH OF THESE OLD MODS IF INSTALLED.**
**FAILING TO REMOVE THEM WILL CAUSE CRITICAL CONFLICTS.**

## Installation
1. Ensure you have BepInEx installed.
2. Extract the `ActionBar` folder into your `BepInEx/plugins` directory.

## Configuration
Access the configuration menu via **Config Manager** (F5 by default) in-game.
- **General**: Toggle the mod, adjust scale, and HUD visibility.
- **Layout**: Configure rows and slots per row.
- **Hotkeys**: Set up your custom keybindings.

To set specific empty slot styles:
1. Enter **Hotkey Mode** (default binding in controls).
2. **Right-click** on any slot to cycle `Hidden → Image → Transparent`.

To set dynamic slots:
1. Enter slot configuration mode.
2. **Middle-click** a slot to toggle Dynamic mode.
3. Equip a weapon and drag skills/items into dynamic slots to save that weapon preset.
4. Clearing a dynamic slot while that weapon is equipped removes that slot from that weapon preset.