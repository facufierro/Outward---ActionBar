using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace fierrof.ActionBar
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("com.sinai.SideLoader", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID    = "fierrof.actionbar";
        public const string NAME    = "ActionBar";
        public const string VERSION = "2.0.21";

        public static ManualLogSource Log;

        public static ConfigEntry<int>   SlotCount;
        public static ConfigEntry<float> PositionX;
        public static ConfigEntry<float> PositionY;
        public static ConfigEntry<float> Scale;

        void Awake()
        {
            Log = Logger;

            SlotCount = Config.Bind("ActionBar", "SlotCount", 8,
                new ConfigDescription("Number of quickslot buttons displayed",
                    new AcceptableValueRange<int>(1, 20)));

            PositionX = Config.Bind("ActionBar", "PositionX", 0.5f,
                new ConfigDescription("Horizontal position (0 = left, 1 = right)",
                    new AcceptableValueRange<float>(0f, 1f)));

            PositionY = Config.Bind("ActionBar", "PositionY", 0.05f,
                new ConfigDescription("Vertical position (0 = bottom, 1 = top)",
                    new AcceptableValueRange<float>(0f, 1f)));

            Scale = Config.Bind("ActionBar", "Scale", 1.0f,
                new ConfigDescription("Size multiplier for the action bar",
                    new AcceptableValueRange<float>(0.5f, 3f)));

            new Harmony(GUID).PatchAll();
            Log.LogMessage($"{NAME} v{VERSION} loaded.");
        }
    }
}
