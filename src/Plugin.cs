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
        public const string Id = "potato.wright", Version = "1.1.0";

        // Test builds log what they do (Debug); releases don't.
        internal const bool TestBuild = true;

        internal const string Tag = "[PotatoWright]";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            SwagPieces.Longest = Config.Bind("1 - Ropes and chains", "Ropes and chains: longest", false, new BepInEx.Configuration.ConfigDescription(
                "Ropes and chains reach up to 64 m instead of 48 m. 64 m is the longest that still shows for every player at either end, whatever their simulation distance (the game only creates a rope while the area of its first end is loaded around the player).",
                null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
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
            try
            {
                ScaffoldPiece.Create();
            }
            catch (System.Exception e)
            {
                Log.LogError("Scaffold not added: " + e);
            }
            try
            {
                HangPieces.Create();
            }
            catch (System.Exception e)
            {
                Log.LogError("Hangings not added: " + e);
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
                { "piece_potatowright_swag_description", "Hangs between two points. Click the first end, roll the mouse wheel to set the sag, then click the second end (up to 48 m, or 64 m when the server allows it). It falls if either end loses what it hangs from." },
                { "potatowright_swag_first", "First end set. Wheel: sag. Click the second end." },
                { "potatowright_swag_cancel", "First end dropped" },
                { "potatowright_swag_toolong", "Too long: up to" },
                { "potatowright_swag_tooshort", "Too short" },
                { "potatowright_swag_sag", "Sag" },
                { "piece_potatowright_scaffold", "Scaffold" },
                { "piece_potatowright_scaffold_description", "A wooden frame to build from: a ladder on one side, a plank deck on the other. Frames stack and line up at their corners, open into each other where they meet, and the top one has a guard rail. A frame falls when what it stands on is removed, and nothing else rests on it." },
                { "piece_potatowright_hang_lantern", "Hanging lantern: Dvergr" },
                { "piece_potatowright_hang_lantern_description", "Aim at a rope or chain and click: it hangs there and lights up like the game's lanterns. It falls when its rope or chain goes." },
                { "piece_potatowright_hang_lantern_hooded", "Hanging lantern: hooded" },
                { "piece_potatowright_hang_lantern_hooded_description", "A hooded lantern that throws its beam out and down. Aim at a rope or chain, turn it with the mouse wheel and click. It falls when its rope or chain goes." },
                { "piece_potatowright_hang_lantern_deepnorth", "Hanging lantern: Deep North" },
                { "piece_potatowright_hang_lantern_deepnorth_description", "The Deep North's iron lamp. Aim at a rope or chain and click. It falls when its rope or chain goes." },
                { "piece_potatowright_hang_brazier", "Hanging brazier" },
                { "piece_potatowright_hang_brazier_description", "The game's hanging brazier, hung from a rope or chain by the top of its chains: aim at it and click. Burns coal and warms like the game's. It falls when its rope or chain goes." },
                { "piece_potatowright_hang_fish", "Drying fish" },
                { "piece_potatowright_hang_fish_description", "Fish hung by their tails." },
                { "piece_potatowright_hang_herbs", "Drying herbs" },
                { "piece_potatowright_hang_herbs_description", "Thistles and dandelions hung heads down. Aim at a rope or chain and click: they hang there. They fall when their rope or chain goes." },
                { "piece_potatowright_bunting", "Pennant bunting" },
                { "piece_potatowright_bunting_description", "Flags along a whole rope or chain: aim at it and click. Mouse wheel: many colours, banner cloth or one colour, with or without runes. It falls when its rope or chain goes." },
                { "piece_potatowright_fey", "Fey lights on a rope" },
                { "piece_potatowright_fey_description", "Fey lights drifting along a rope." },
                { "potatowright_hang_aim", "Aim at a rope or chain" },
            });
            LocalizationManager.Instance.GetLocalization().AddTranslation("English", HangPieces.LookTranslations());
        }
    }
}
