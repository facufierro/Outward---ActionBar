# Changelog
## 2.1.1
* Improved mouse labels
* Fixed slot saving, was saving to global
* Fixed font size for cd and hotkey labels

## 2.1.0

* Added resizing for every hud element
* Fixed Interaction element not being able to be moved
* Fixed slot saving and dynmic slots not behaving as they should

## 2.0.1

* Fixed droppable skils, If you dropped a skill and it disappeared, you can relearn it using Debug Mode (There are mods that activate it).

## 2.0.0

* Remade the whole thing from scratch

## 1.2.4

- **Fixed**: Profiles not saving completely

## 1.2.3

- **Fixed**: Configs not refreshing when changed
- **Fixed**: Some UI elements reseting on reload

## 1.2.2

- **Fixed**: Dynamic slots now work correctly with modded equipment that adds non-standard hand slots (e.g. Classfixes).
- **Fixed**: Main-hand non-weapon equipment (shields, off-hand items held in the main hand) no longer shares context IDs with offhand equipment, preventing preset mixups.
- **Fixed**: Equipment context resolution now falls back to slot-based slot scanning when standard property names are unavailable, improving compatibility with mods that expose equipment differently.
- **Fixed**: Non-weapon equipment context IDs are now derived from a stable UID or name hash, preventing context collisions across sessions.

## 1.2.1

- **Fixed**: Dynamic slot context now resolves both main-hand and off-hand equipment correctly (including non-weapon off-hand items like torches).
- **Fixed**: Combo context fallback behavior now restores expected slot assignments when the off-hand item is unequipped.
- **Fixed**: Slot edits made while both hands are equipped now save to the combo preset context instead of incorrectly saving only to the main-hand context.

## 1.2.0

- **Added**: Per-slot empty style cycling in slot configuration mode (`Hidden → Image → Transparent`).
- **Added**: Dynamic slots with weapon-based presets:
  - Toggle dynamic state with middle-click in slot configuration mode.
  - Equipping weapons applies saved dynamic slot presets when available.
  - Drag/drop updates the active weapon preset for dynamic slots.
  - Clearing a dynamic slot removes that slot from the active weapon preset.

## 1.1.2

- **Fixed**: Interaction tooltip appearing at the top of the screen. Implemented auto-detection to identify the container holding the interaction prompt and exclude it from positioning, ensuring it remains in its correct place.

## 1.1.1

- **Fixed**: Bug where the interaction tooltip was on the top.

## 1.1.0

- **Added**: Precise UI Positioning:
  - X/Y Sliders in Config Manager.
  - Two-way binding between Config Manager and Visual Drag Editor.
  - "Center X" and "Center Y" buttons to alignment.
- **Fixed**: UI Dragging no longer snaps the bar back to incorrect positions.
- **Fixed**: "Reset Positions" button now correctly resets both UI and configuration values.
- **Fixed**: Configuration changes (Rows, Slots) apply immediately and reliably.
- **Removed**: "Equipment Sets" completely removed this time.

## 1.0.2

- **Documentation**: Fixed README formatting and deduplication.
- **Documentation**: Fixed Changelog display on Thunderstore.

## 1.0.1

- **Documentation**: Added critical warning about incompatibility with legacy ActionUI mod.
- **Documentation**: Added instructions to uninstall the old mod.

## 1.0.0

- Initial release of the revamped Action Bar mod.
- **Added**:
  - Hide/Disable Slots: Right-click in Hotkey Mode to toggle slot visibility.
  - Global Layout Config: Rows, slots, scale, disabled slots shared across all characters.
  - Config Manager Integration: Uses Mefino's Config Manager.
- **Fixed**:
  - Addressed significant stability issues regarding bar resets and item assignment.
  - Fixed vanilla keyboard quickslots overlapping.
