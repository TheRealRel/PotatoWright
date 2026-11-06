// Ropes and chains on ships built with Build On Ship (Skarif, com.Skarif.shipbuildmod; read from 4.0.19).
// That mod gives every piece on a ship its own support: full when the piece sits on the hull or within 0.8 m of the deck,
// otherwise from supporting pieces whose bounds touch it within 4.2 m (WearNTearHelper.CalculateShipSupport). A swag
// hangs in the air between its two ends and touches neither, so it got no support: the game's support wear (which that
// mod turns on for ship pieces) breaks it, and so does that mod's recheck of the whole ship whenever a piece on it is
// destroyed (support below the minimum -> 100x its health in damage). The user, test-k: "support was not taking I think.
// It would break immediately." A swag needs no support: it hangs from its two ends, which the swag checks itself
// (Swag.CheckEnds). So for swags that calculation is skipped and their support kept at the maximum.
// Ship destroyed: that mod destroys attached pieces with WearNTear.Destroy directly, which this doesn't touch.
using System.Reflection;
using HarmonyLib;

namespace PotatoWright
{
    [HarmonyPatch]
    internal static class ShipCompat
    {
        internal const string BuildOnShipGuid = "com.Skarif.shipbuildmod";

        private static MethodBase s_target;

        private static bool Prepare()
        {
            System.Type helper = AccessTools.TypeByName("ShipBuildingMod.WearNTearHelper");
            s_target = (helper != null) ? AccessTools.Method(helper, "CalculateShipSupport") : null;
            if (helper != null && s_target == null)
            {
                UnityEngine.Debug.LogWarning(Plugin.Tag + " Build On Ship is installed but its support code wasn't found: ropes and chains on ships may break.");
            }
            else if (s_target != null)
            {
                Plugin.Debug("Build On Ship found: ropes and chains keep full support on ships.");
            }
            return s_target != null;
        }

        private static MethodBase TargetMethod()
        {
            return s_target;
        }

        // __0: the piece (WearNTear).
        private static bool Prefix(WearNTear __0)
        {
            if (__0 == null || __0.GetComponent<Swag>() == null)
            {
                return true;
            }
            float max = __0.GetMaxSupport();
            __0.m_support = max;
            ZNetView view = __0.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && view.IsOwner())
            {
                view.GetZDO().Set(ZDOVars.s_support, max);
            }
            return false;
        }
    }
}
