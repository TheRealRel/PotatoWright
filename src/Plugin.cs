// PotatoWright - generic building pieces for Valheim: wooden mannequins (Ask and Embla) in every pose and emote, and
// hanging ropes and chains (swags) between any two points.
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace PotatoWright
{
    // Every piece here needs to be on the server and every player: no process filter, so dedicated servers load it and
    // know the pieces (a server deletes pieces it doesn't know).
    [BepInPlugin(Id, "PotatoWright", Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Id = "potato.wright", Version = "1.0.0";

        // Test builds log what they do (Debug); releases don't.
        internal const bool TestBuild = true;

        internal const string Tag = "[PotatoWright]";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            AddTranslations();
            PrefabManager.OnVanillaPrefabsAvailable += AddPieces;
            new Harmony(Id).PatchAll(Assembly.GetExecutingAssembly());
        }

        internal static void Debug(string message)
        {
            if (TestBuild)
            {
                Log.LogInfo(message);
            }
        }

        private static void AddPieces()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= AddPieces;
            try
            {
                MannequinPieces.Create();
            }
            catch (System.Exception e)
            {
                Log.LogError("Mannequins not added: " + e);
            }
            try
            {
                SwagPieces.Create();
            }
            catch (System.Exception e)
            {
                Log.LogError("Ropes and chains not added: " + e);
            }
        }

        private static void AddTranslations()
        {
            LocalizationManager.Instance.GetLocalization().AddTranslation("English", new Dictionary<string, string>
            {
                { "potatowright_mannequin_male", "Ask" },
                { "potatowright_mannequin_female", "Embla" },
                { "potatowright_emote", "emote" },
                { "potatowright_mannequin_description", "A wooden mannequin to dress in armor and weapons. Its color, and whether it stands on its wooden base, are set in the panel beside this menu; place it on a mannequin to recolor that one." },
                { "potatowright_mannequin_color", "Mannequin color" },
                { "potatowright_hue", "Hue" },
                { "potatowright_saturation", "Saturation" },
                { "potatowright_brightness", "Brightness" },
                { "potatowright_natural_wood", "Natural wood" },
                { "potatowright_recolor", "Recolor" },
                { "potatowright_base", "Base" },
                { "potatowright_base_on", "On" },
                { "potatowright_base_off", "Off" },
                { "potatowright_menu_mannequins", "Mannequins" },
                { "potatowright_menu_ask_poses", "Ask: Poses" },
                { "potatowright_menu_ask_emotes", "Ask: Emotes" },
                { "potatowright_menu_embla_poses", "Embla: Poses" },
                { "potatowright_menu_embla_emotes", "Embla: Emotes" },
                { "piece_potatowright_swag_rope", "Rope" },
                { "piece_potatowright_swag_iron", "Iron chain" },
                { "piece_potatowright_swag_bronze", "Bronze chain" },
                { "piece_potatowright_swag_blackmetal", "Black metal chain" },
                { "piece_potatowright_swag_test_iron", "TEST Iron chain (links)" },
                { "piece_potatowright_swag_test_bronze", "TEST Bronze chain (links)" },
                { "piece_potatowright_swag_test_blackmetal", "TEST Black metal chain (links)" },
                { "piece_potatowright_swag_description", "Hangs between two points. Click the first end, roll the mouse wheel to set the sag, then click the second end (up to 16 m). It falls if either end loses what it hangs from." },
                { "potatowright_swag_first", "First end set. Wheel: sag. Click the second end." },
                { "potatowright_swag_cancel", "Swag cancelled" },
                { "potatowright_swag_toolong", "Too long: up to 16 m" },
                { "potatowright_swag_tooshort", "Too short" },
                { "potatowright_swag_sag", "Sag" },
            });
        }
    }
}
