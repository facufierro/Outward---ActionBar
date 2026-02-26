using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Discovers game HUD elements at runtime, attaches HudMover components,
    /// and manages global save/load of positions.
    /// </summary>
    public class HudMoverManager : MonoBehaviour
    {
        public static HudMoverManager Instance { get; private set; }

        private List<HudMover> _movers = new List<HudMover>();
        private bool _attached;
        private bool _wasEditMode;

        private static string SavePath =>
            Path.Combine(BepInEx.Paths.ConfigPath, "ActionBar_HUD", "hud_positions.json");

        // Known HUD element names to look for in CharacterUI
        // We discover children recursively and match these names
        private static readonly Dictionary<string, string> KnownElements = new Dictionary<string, string>
        {
            { "HealthBar",           "Health" },
            { "ManaBar",             "Mana" },
            { "StatusEffectPanel",   "Status Effects" },
            { "QuickSlotPanel",      "Quick Slots" },
            { "Temperature",         "Temperature" },
            { "StabilityBar_1",      "Stability" },
            { "StatusIcons",         "Status Icons" },
            { "Compass",             "Compass" },
            // Broader discovery below
        };

        void Awake()
        {
            Instance = this;
        }

        void Update()
        {
            // Attempt to discover HUD elements once a character is loaded
            if (!_attached)
            {
                var character = CharacterManager.Instance?.GetFirstLocalCharacter();
                if (character?.CharacterUI != null)
                {
                    DiscoverAndAttach(character.CharacterUI);
                    _attached = true;
                }
            }

            // Handle edit mode transitions
            if (SlotDropHandler.IsEditMode && !_wasEditMode)
            {
                foreach (var m in _movers) m.EnableEditVisuals();
                _wasEditMode = true;
            }
            else if (!SlotDropHandler.IsEditMode && _wasEditMode)
            {
                foreach (var m in _movers) m.DisableEditVisuals();
                _wasEditMode = false;
            }
        }

        private void DiscoverAndAttach(CharacterUI characterUI)
        {
            var root = characterUI.transform;
            Plugin.Log.LogMessage("=== HUD Discovery: Scanning CharacterUI children ===");

            // Find all RectTransforms that are direct or near-direct children
            // and are likely HUD panels
            DiscoverRecursive(root, 0, 3); // scan up to 3 levels deep

            LoadPositions();
            Plugin.Log.LogMessage($"HUD Mover: attached to {_movers.Count} elements.");
        }

        private void DiscoverRecursive(Transform parent, int depth, int maxDepth)
        {
            if (depth > maxDepth) return;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                var rect = child.GetComponent<RectTransform>();
                if (rect == null) continue;

                string name = child.name;

                // Log everything for discovery
                string indent = new string(' ', depth * 2);
                Plugin.Log.LogMessage($"  {indent}[L{depth}] {name} (active={child.gameObject.activeSelf})");

                // Check if this is a known element or a good candidate
                string friendlyName;
                bool isKnown = KnownElements.TryGetValue(name, out friendlyName);

                // Also attach to anything with "Bar", "Panel", "Status", "Slot"
                // in the name at depth 1-3 MUST EXCLUDE root panels!
                if (!isKnown && depth <= 3)
                {
                    string lowerName = name.ToLower();
                    bool isRootPanel = lowerName.Contains("gameplay") || 
                                       lowerName.Contains("safeframe") ||
                                       lowerName.Equals("canvas") ||
                                       lowerName.Contains("menu") ||
                                       lowerName.Contains("overlay") ||
                                       lowerName.Equals("characterui") ||
                                       lowerName.Contains("droppanel") ||
                                       lowerName.Contains("actionbar_root") ||
                                       lowerName.Contains("generalpanels") ||
                                       lowerName.Contains("debugpanels");

                    if (!isRootPanel)
                    {
                        if (name.Contains("Bar") || name.Contains("Panel") || 
                            name.Contains("Status") || name.Contains("Gauge") ||
                            name.Contains("Compass") || name.Contains("Temperature") ||
                            name.Contains("Arrow"))
                        {
                            friendlyName = name;
                            isKnown = true;
                        }
                    }
                }

                if (isKnown && child.GetComponent<HudMover>() == null)
                {
                    var mover = child.gameObject.AddComponent<HudMover>();
                    mover.ElementId = friendlyName;
                    _movers.Add(mover);
                    Plugin.Log.LogMessage($"  >>> Attached HudMover: '{friendlyName}' ({child.name})");
                }

                // Continue scanning children
                DiscoverRecursive(child, depth + 1, maxDepth);
            }
        }

        // ── Save / Load ────────────────────────────────────

        public void SavePositions()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                
                var lines = new List<string>();
                foreach (var m in _movers)
                {
                    var pos = m.GetPosition();
                    // Format: ElementId=X,Y
                    lines.Add($"{m.ElementId}={pos.x:F2},{pos.y:F2}");
                }
                
                File.WriteAllLines(SavePath, lines);
                Plugin.Log.LogMessage($"HUD positions saved to {SavePath}.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to save HUD positions: {ex.Message}");
            }
        }

        public void LoadPositions()
        {
            if (!File.Exists(SavePath)) return;

            try
            {
                // Simple key=value parsing since JsonUtility doesn't handle Dictionary
                var lines = File.ReadAllLines(SavePath);
                var positions = new Dictionary<string, Vector2>();

                // Parse our simple format: "ElementId=x,y" per line
                foreach (var line in lines)
                {
                    var parts = line.Split('=');
                    if (parts.Length != 2) continue;

                    var coords = parts[1].Split(',');
                    if (coords.Length != 2) continue;

                    if (float.TryParse(coords[0].Trim(), System.Globalization.NumberStyles.Float, 
                            System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(coords[1].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float y))
                    {
                        positions[parts[0].Trim()] = new Vector2(x, y);
                    }
                }

                foreach (var m in _movers)
                {
                    if (positions.TryGetValue(m.ElementId, out Vector2 pos))
                    {
                        m.SetPosition(pos.x, pos.y);
                        Plugin.Log.LogMessage($"HUD '{m.ElementId}': restored to ({pos.x:F1}, {pos.y:F1}).");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to load HUD positions: {ex.Message}");
            }
        }

        public void ResetAllPositions()
        {
            foreach (var m in _movers)
                m.ResetToOriginal();

            // Delete save file
            if (File.Exists(SavePath))
                File.Delete(SavePath);

            Plugin.Log.LogMessage("HUD positions reset to defaults.");
        }

        // We need to re-discover if scene changes
        public void OnSceneUnloaded()
        {
            _movers.Clear();
            _attached = false;
        }
    }
}
