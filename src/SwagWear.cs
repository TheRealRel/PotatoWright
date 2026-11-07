// Ropes and chains take no wear or support damage: they hang from their two ends, which the swag checks itself
// (Swag.CheckEnds, which removes it when an end goes). Proven in test-m (log 2026-11-06 18:21): on a ship, Build On Ship
// turns on support wear for every piece, the support of a swag (touching nothing) came out 0 even with Build On Ship's
// own support calculation skipped for swags (ShipCompat), and WearNTear.UpdateWear then called ApplyDamage(full health)
// on the first wear update after placing. Several installed mods touch WearNTear.UpdateSupport (Build On Ship,
// Valheim Performance Optimizations, Valheim Community Patch, Potato Tweaks), so the guard sits on the damage itself:
// damage without a hit (support, rain, snow, ash, lava, Build On Ship's support recheck) is ignored for swags. Hits from
// weapons and creatures (RPC_Damage passes its HitData) still count, the hammer still removes them, and a ship that is
// destroyed still destroys them (Build On Ship calls WearNTear.Destroy directly).
using HarmonyLib;

namespace PotatoWright
{
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.ApplyDamage))]
    internal static class SwagWear
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(WearNTear __instance, HitData hitData, ref bool __result)
        {
            if (hitData != null || __instance == null || __instance.GetComponent<Swag>() == null)
            {
                return true;
            }
            __result = false;
            return false;
        }
    }
}
