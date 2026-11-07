// Ropes and chains take no wear or support damage: they hang from their two ends, which the swag checks itself
// (Swag.CheckEnds, which removes it when an end goes). Proven in test-m (log 2026-11-06 18:21): on a ship, Build On Ship
// (Skarif) turns on support wear for every piece, the support of a swag (touching nothing) came out 0, and
// WearNTear.UpdateWear then called ApplyDamage(full health) on the first wear update after placing. Several installed
// mods touch WearNTear.UpdateSupport (Build On Ship, Valheim Performance Optimizations, Valheim Community Patch,
// Potato Tweaks).
// 1. SupportOff: before each wear update, a swag's support wear is switched back off (Priority.Last, so after Build On
//    Ship's own UpdateWear prefix switches it on), so no support is worked out for it at all (test-n still did, every
//    few seconds per swag on a ship, and threw it away; the user agreed to remove that, 2026-11-06).
// 2. SwagWear, the backstop: damage without a hit (support, rain, snow, ash, lava, Build On Ship's recheck of a whole
//    ship when a piece on it is destroyed) is ignored for swags. Hits from weapons and creatures (RPC_Damage passes its
//    HitData) still count, the hammer still removes them, and a destroyed ship still destroys them (Build On Ship calls
//    WearNTear.Destroy directly).
// PotatoWright patches only the game here, not Build On Ship's own code, so an update of that mod can't break it.
// The same holds for every piece carrying Steady: the swags, the scaffold (never falls) and the hangings (they fall
// only with their rope, Hanging.CheckHold).
using HarmonyLib;

namespace PotatoWright
{
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.ApplyDamage))]
    internal static class SwagWear
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(WearNTear __instance, HitData hitData, ref bool __result)
        {
            if (hitData != null || __instance == null || __instance.GetComponent<Steady>() == null)
            {
                return true;
            }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.UpdateWear))]
    internal static class SupportOff
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(WearNTear __instance)
        {
            if (__instance != null && __instance.m_noSupportWear && __instance.GetComponent<Steady>() != null)
            {
                __instance.m_noSupportWear = false;
            }
        }
    }
}

namespace PotatoWright
{
    // Marks the pieces that take no support or weather wear (SwagWear, SupportOff).
    public sealed class Steady : UnityEngine.MonoBehaviour
    {
    }
}
