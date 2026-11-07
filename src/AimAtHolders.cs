// The game refuses to place any piece while aiming at a piece that holds nothing (Player.UpdatePlacementGhost:
// "if (wearNTear && !wearNTear.m_supports) Invalid", wearNTear being the aimed-at piece's). The swags and the scaffold
// hold nothing, so test-a could neither hang anything on a rope (log: "placing on ... (status Invalid)" every time) nor
// stack scaffolds. For the one placement step, the aimed-at piece is let through when it is what the held piece goes
// on: a hanging aimed at a rope or chain, a scaffold aimed at a scaffold. Everything else still can't be placed while
// aiming at them (the user's choice, 2026-11-06: "Keep the game's rule"), and nothing rests on them (support is worked
// out elsewhere, never here).
using System;
using HarmonyLib;
using UnityEngine;

namespace PotatoWright
{
    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    internal static class AimAtHolders
    {
        private static WearNTear s_opened;

        [HarmonyPriority(Priority.First)]
        private static void Prefix(Player __instance)
        {
            Restore();
            if (__instance != Player.m_localPlayer || __instance.m_placementGhost == null || GameCamera.instance == null)
            {
                return;
            }
            GameObject ghost = __instance.m_placementGhost;
            bool hanging = ghost.GetComponent<Hanging>() != null;
            bool scaffold = !hanging && ghost.GetComponent<ScaffoldMark>() != null;
            if (!hanging && !scaffold)
            {
                return;
            }
            Transform cam = GameCamera.instance.transform;
            if (!Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, 50f, __instance.m_placeRayMask))
            {
                return;
            }
            WearNTear wear = hit.collider.GetComponentInParent<WearNTear>();
            if (wear == null || wear.m_supports)
            {
                return;
            }
            if (hanging ? wear.GetComponent<Swag>() == null : wear.GetComponent<ScaffoldMark>() == null)
            {
                return;
            }
            wear.m_supports = true;
            s_opened = wear;
        }

        private static Exception Finalizer(Exception __exception)
        {
            Restore();
            return __exception;
        }

        private static void Restore()
        {
            if (s_opened != null)
            {
                s_opened.m_supports = false;
            }
            s_opened = null;
        }
    }
}
