// The camera zooms on the mouse wheel in build mode unless the held piece can be turned (GameCamera.UpdateCamera asks
// Player.CanRotatePiece, which reads the selected piece's m_canRotate). Ropes, chains and hangings can't be turned the
// game's way, so the wheel moved the camera while it changed the bunting's look (the user, test-f: "The mouse wheel needs
// to not zoom the camera when it's locked onto a rope/chain"). While the wheel is ours (a rope's second end being chosen,
// a hanging aimed at a rope or chain), the held piece counts as turnable for the camera. The game's own turning still
// reads the ghost's m_canRotate, which stays off, so nothing else turns.
using HarmonyLib;

namespace PotatoWright
{
    [HarmonyPatch(typeof(Player), nameof(Player.CanRotatePiece))]
    internal static class WheelNoZoom
    {
        private static void Postfix(Player __instance, ref bool __result)
        {
            if (__result || __instance != Player.m_localPlayer || !__instance.InPlaceMode() || __instance.m_placementGhost == null)
            {
                return;
            }
            if (SwagPlacing.Choosing && __instance.m_placementGhost.GetComponent<Swag>() != null)
            {
                __result = true;
                return;
            }
            Hanging hanging = __instance.m_placementGhost.GetComponent<Hanging>();
            if (hanging != null && HangPlacing.Locked && (!hanging.IsString || hanging.m_kind == HangPieces.Bunting))
            {
                __result = true;
            }
        }
    }
}
