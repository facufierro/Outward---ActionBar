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

        // ── Exact-name matches (always attached regardless of depth) ──
        // Names taken directly from the CharacterUI hierarchy log
        public static readonly Dictionary<string, string> KnownElements = new Dictionary<string, string>
        {
            // All bars as a group (L3)
            { "MainCharacterBars",          "Health / Mana / Stamina" },

            // HUD direct children (L3)
            { "Stability",                  "Stability" },
            { "QuiverDisplay",              "Arrows" },
            { "StatusEffect - Panel",       "Status Effects" },
            { "InteractionDisplay",         "Interact Tooltip" },
            { "Compass",                    "Compass" },
            { "TemperatureSensor",          "Temperature" },
            { "Tutorialization_DropBag",    "Backpack" },
            { "Tutorialization_UseBandage", "Bandage" },
        };

        // ── Names that should NEVER get a HudMover (root containers, our stuff) ──
        private static readonly HashSet<string> Blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Canvas",
            "CharacterUI",
            "GameplayPanels",
            "SafeFrame",
            "GeneralPanels",
            "DebugPanels",
            "DropPanel",
            "ActionBar_Root",
            "CharacterMainCharacterBars",
            "SlotContainer",
            "QuickSlot",
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
                foreach (var m in _movers)
                {
                    if (m != null) ForceVisible(m.gameObject);
                }
                foreach (var m in _movers) m.EnableEditVisuals();
                _wasEditMode = true;
            }
            else if (!SlotDropHandler.IsEditMode && _wasEditMode)
            {
                foreach (var m in _movers) m.DisableEditVisuals();
                _wasEditMode = false;
            }
        }

        // LateUpdate runs AFTER the game's own Update/LateUpdate scripts,
        // so our force-visibility wins even if the game deactivates elements.
        void LateUpdate()
        {
            if (SlotDropHandler.IsEditMode)
            {
                foreach (var m in _movers)
                {
                    if (m != null) ForceVisible(m.gameObject);
                }
            }
            else
            {
                // Enforce Hidden states when out of Edit Mode
                foreach (var m in _movers)
                {
                    if (m == null) continue;

                    if (m.ElementId == "Backpack" && Plugin.HideBackpack.Value)
                    {
                        if (m.gameObject.activeSelf) m.gameObject.SetActive(false);
                    }
                    else if (m.ElementId == "Bandage" && Plugin.HideBandage.Value)
                    {
                        if (m.gameObject.activeSelf) m.gameObject.SetActive(false);
                    }
                }
            }
        }

        public void UpdateVisibilityOnConfigChange()
        {
            if (SlotDropHandler.IsEditMode) return;

            foreach (var m in _movers)
            {
                if (m == null) continue;

                if (m.ElementId == "Backpack")
                {
                    if (Plugin.HideBackpack.Value && m.gameObject.activeSelf)
                        m.gameObject.SetActive(false);
                    else if (!Plugin.HideBackpack.Value && !m.gameObject.activeSelf)
                        m.gameObject.SetActive(true); 
                }
                else if (m.ElementId == "Bandage")
                {
                    if (Plugin.HideBandage.Value && m.gameObject.activeSelf)
                        m.gameObject.SetActive(false);
                    else if (!Plugin.HideBandage.Value && !m.gameObject.activeSelf)
                        m.gameObject.SetActive(true);
                }
            }
        }
        private void ForceVisible(GameObject go)
        {
            if (!go.activeSelf) go.SetActive(true);

            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null && cg.alpha < 0.05f) cg.alpha = 1f;
        }

        private void DiscoverAndAttach(CharacterUI characterUI)
        {
            var root = characterUI.transform;
            Plugin.Log.LogMessage("=== HUD Discovery: Scanning CharacterUI children ===");

            // Scan deep – game nests HUD elements 5-8 levels in
            DiscoverRecursive(root, 0, 10);

            LoadPositions();

            try { ApplyConfigScales(); }
            catch (Exception ex) { Plugin.Log.LogWarning($"Failed to apply HUD scales: {ex.Message}"); }

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

                bool isBlacklisted = Blacklist.Contains(name);

                // Only attach to EXACT matches in KnownElements
                string friendlyName;
                bool shouldAttach = KnownElements.TryGetValue(name, out friendlyName);

                if (shouldAttach && !isBlacklisted)
                {
                    var mover = child.GetComponent<HudMover>();
                    if (mover == null)
                    {
                        mover = child.gameObject.AddComponent<HudMover>();
                        mover.ElementId = friendlyName;

                        if (name.StartsWith("Tutorialization"))
                            mover.AnchorBottom = true;
                    }

                    _movers.Add(mover);
                    Plugin.Log.LogMessage($"  >>> Attached HudMover: '{friendlyName}' ({child.name})");
                    // Don't recurse into matched elements — we move the whole thing
                    continue;
                }

                // ALWAYS recurse into children (even if blacklisted root panel)
                DiscoverRecursive(child, depth + 1, maxDepth);
            }
        }

        // ── Scale ─────────────────────────────────────────────

        public void ApplyConfigScales()
        {
            foreach (var m in _movers)
            {
                if (m == null) continue;
                if (Plugin.HudElementScale.TryGetValue(m.ElementId, out var entry))
                    m.SetScale(entry.Value);
            }
        }

        public void ApplyScale(string elementId, int scalePercent)
        {
            foreach (var m in _movers)
            {
                if (m == null) continue;
                if (m.ElementId == elementId)
                {
                    m.SetScale(scalePercent);
                    SavePositions();
                    return;
                }
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
                    // Format: ElementId=X,Y,Scale
                    lines.Add($"{m.ElementId}={pos.x:F2},{pos.y:F2},{m.ScalePercent}");
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

                var scales = new Dictionary<string, int>();

                // Parse format: "ElementId=x,y[,scale]" per line
                foreach (var line in lines)
                {
                    var parts = line.Split('=');
                    if (parts.Length != 2) continue;

                    var coords = parts[1].Split(',');
                    if (coords.Length < 2) continue;

                    if (float.TryParse(coords[0].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(coords[1].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float y))
                    {
                        string key = parts[0].Trim();
                        positions[key] = new Vector2(x, y);

                        if (coords.Length >= 3 && int.TryParse(coords[2].Trim(), out int scale))
                            scales[key] = scale;
                    }
                }

                foreach (var m in _movers)
                {
                    if (m == null) continue;
                    if (positions.TryGetValue(m.ElementId, out Vector2 pos))
                    {
                        m.SetPosition(pos.x, pos.y);
                        Plugin.Log.LogMessage($"HUD '{m.ElementId}': restored to ({pos.x:F1}, {pos.y:F1}).");
                    }
                    if (scales.TryGetValue(m.ElementId, out int s))
                    {
                        m.SetScale(s);
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
            {
                if (m == null) continue;
                m.ResetToOriginal();
                m.SetScale(100);

                if (Plugin.HudElementScale.TryGetValue(m.ElementId, out var entry))
                    entry.Value = 100;
            }

            if (File.Exists(SavePath))
                File.Delete(SavePath);

            Plugin.Log.LogMessage("HUD positions and scales reset to defaults.");
        }

        // We need to re-discover if scene changes
        public void OnSceneUnloaded()
        {
            _movers.Clear();
            _attached = false;
        }
    }
}
