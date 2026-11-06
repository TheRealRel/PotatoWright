// The mannequins in the build menu: one fold-open entry, "Mannequins", that opens to Ask: Poses, Ask: Emotes,
// Embla: Poses and Embla: Emotes (the user's layout, 2026-11-06), instead of 80 pieces in a tab.
//
// Valheim 1.0's build menu lists its sidebar entries from ByUsagePieceList: one per usage tag present in the tool's
// table (Piece.UsageTagFlags indexes); Jötunn adds its custom categories with ids past the game's (their position in the
// list). The same way, entries of our own are added with ids from 1000, for any table holding mannequin pieces (the
// hammer). The mannequins carry no usage tag (MannequinPieces.ClearUsage), and they are taken out of every other entry
// but "Show all": with Potato Hammer, which puts each mod's pieces on a tab of its own, the Potato Wright tab keeps the
// ropes and chains and the mannequins stay under Mannequins. (The same mechanism as the Weapon Crate 0.1.2's sections.)
// Where the ropes and chains have a tab of their own (Potato Hammer's "Potato Wright"), the Mannequins entry sits right
// under that tab, indented, as part of it (the user, 2026-11-06); otherwise it comes last.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PotatoWright
{
    [HarmonyPatch]
    internal static class MannequinMenu
    {
        private const int Header = 1000;

        // Header + 1 + body * 2 + (emotes ? 1 : 0).
        private static readonly string[] Labels = { "$potatowright_menu_ask_poses", "$potatowright_menu_ask_emotes", "$potatowright_menu_embla_poses", "$potatowright_menu_embla_emotes" };

        private static readonly HashSet<ByUsagePieceList> s_lists = new HashSet<ByUsagePieceList>();

        private static bool s_open;

        // Lists whose Mannequins entry sits under the mod's own tab (drawn indented).
        private static readonly HashSet<ByUsagePieceList> s_nested = new HashSet<ByUsagePieceList>();

        private static readonly List<Piece> s_scratch = new List<Piece>();

        // The position of the tab holding the ropes and chains, if it is one of Jötunn's tabs (Potato Hammer's mod tabs
        // are); -1 when they sit in the game's own tabs. Jötunn's tags are filled in its own postfix, which runs first.
        private static int ModTabIndex(ByUsagePieceList list, PieceTable pieceTable)
        {
            for (int i = 0; i < list.m_availableTags.Count; i++)
            {
                int id = list.m_availableTags[i];
                if (id < list.m_usageTags.Length || IsOurs(id))
                {
                    continue;
                }
                s_scratch.Clear();
                list.GetAvailablePiecesWithTag(id, pieceTable, s_scratch);
                foreach (Piece piece in s_scratch)
                {
                    if (piece != null && piece.GetComponent<Swag>() != null)
                    {
                        s_scratch.Clear();
                        return i;
                    }
                }
            }
            s_scratch.Clear();
            return -1;
        }

        private static bool s_redrawPending;

        private static bool IsOurs(int id)
        {
            return id >= Header && id <= Header + Labels.Length;
        }

        private static MannequinPose PoseOf(Piece piece)
        {
            return (piece != null) ? piece.GetComponent<MannequinPose>() : null;
        }

        private static bool InEntry(MannequinPose pose, int id)
        {
            if (pose == null)
            {
                return false;
            }
            if (id == Header)
            {
                return true;
            }
            int sub = id - Header - 1;
            return pose.m_body == sub / 2 && (pose.m_emote >= 0) == (sub % 2 == 1);
        }

        [HarmonyPatch(typeof(ByUsagePieceList), "UpdateAvailableTags")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        private static void UpdateAvailableTags_Postfix(ByUsagePieceList __instance, PieceTable pieceTable)
        {
            if (pieceTable == null)
            {
                s_lists.Remove(__instance);
                return;
            }
            bool[] present = new bool[Labels.Length];
            bool any = false;
            foreach (Piece piece in pieceTable.m_availablePieces)
            {
                MannequinPose pose = PoseOf(piece);
                if (pose == null)
                {
                    continue;
                }
                any = true;
                present[pose.m_body * 2 + ((pose.m_emote >= 0) ? 1 : 0)] = true;
            }
            if (!any)
            {
                s_lists.Remove(__instance);
                return;
            }
            s_lists.Add(__instance);
            MannequinPieces.EnsureEmoteIcons();
            List<int> ours = new List<int> { Header };
            if (s_open)
            {
                for (int i = 0; i < Labels.Length; i++)
                {
                    if (present[i])
                    {
                        ours.Add(Header + 1 + i);
                    }
                }
            }
            int tab = ModTabIndex(__instance, pieceTable);
            if (tab >= 0)
            {
                s_nested.Add(__instance);
                __instance.m_availableTags.InsertRange(tab + 1, ours);
            }
            else
            {
                s_nested.Remove(__instance);
                __instance.m_availableTags.AddRange(ours);
            }
        }

        [HarmonyPatch(typeof(ByUsagePieceList), "GetTagDisplayName")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        private static bool GetTagDisplayName_Prefix(ByUsagePieceList __instance, int index, ref string __result)
        {
            if (!s_lists.Contains(__instance) || index < 0 || index >= __instance.m_availableTags.Count)
            {
                return true;
            }
            int id = __instance.m_availableTags[index];
            if (!IsOurs(id))
            {
                return true;
            }
            // The header: bold, "+" folded or "-" open; the entries indented under it.
            string indent = s_nested.Contains(__instance) ? "     " : "";
            __result = (id == Header)
                ? (indent + "<b>" + (s_open ? "- " : "+ ") + Localization.instance.Localize("$potatowright_menu_mannequins") + "</b>")
                : (indent + "     " + Localization.instance.Localize(Labels[id - Header - 1]));
            return false;
        }

        [HarmonyPatch(typeof(ByUsagePieceList), "GetAvailablePiecesWithTag")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        private static bool GetAvailablePiecesWithTag_Prefix(ByUsagePieceList __instance, int tagId, PieceTable pieceTable, IList<Piece> resultOut)
        {
            if (!IsOurs(tagId) || pieceTable == null)
            {
                return true;
            }
            foreach (Piece piece in pieceTable.m_availablePieces)
            {
                if (piece.m_repairPiece || piece.m_removePiece || InEntry(PoseOf(piece), tagId))
                {
                    resultOut.Add(piece);
                }
            }
            return false;
        }

        // Every other entry but Show all (-1): no mannequins (Jötunn's and Potato Hammer's tabs fill by category).
        [HarmonyPatch(typeof(ByUsagePieceList), "GetAvailablePiecesWithTag")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void GetAvailablePiecesWithTag_Postfix(int tagId, IList<Piece> resultOut)
        {
            if (tagId == -1 || IsOurs(tagId) || resultOut == null)
            {
                return;
            }
            for (int i = resultOut.Count - 1; i >= 0; i--)
            {
                if (PoseOf(resultOut[i]) != null)
                {
                    resultOut.RemoveAt(i);
                }
            }
        }

        // Picking the Mannequins entry or one of its entries opens it; picking any other entry folds it. The list is
        // redrawn on the next BuildUi.Update, not inside the click that recycles the buttons.
        [HarmonyPatch(typeof(BuildUi), "OnSelectTag")]
        [HarmonyPostfix]
        private static void OnSelectTag_Postfix(BuildUi __instance, int tagId)
        {
            if (tagId == -10)
            {
                return;
            }
            bool open = IsOurs(tagId);
            if (open != s_open)
            {
                s_open = open;
                s_redrawPending = true;
            }
        }

        [HarmonyPatch(typeof(BuildUi), "Update")]
        [HarmonyPostfix]
        private static void BuildUi_Update_Postfix(BuildUi __instance)
        {
            if (!s_redrawPending)
            {
                return;
            }
            s_redrawPending = false;
            try
            {
                if (!__instance.gameObject.activeInHierarchy)
                {
                    return;
                }
                __instance.UpdateTagButtons(refreshOnly: true);
                __instance.ConfigureButtonNavigation();
                if (__instance.TryGetTagButtonById(__instance.m_currentTagId, out BuildUiTagButton button) && button != null)
                {
                    button.GetComponent<UnityEngine.UI.Selectable>()?.Select();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(Plugin.Tag + " Build menu Mannequins entry redraw failed: " + ex);
            }
        }
    }
}
