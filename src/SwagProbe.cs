// TEST ONLY (test-m): find what breaks ropes and chains on ships (the user, test-l: "broke immediately", with Build On
// Ship's support calculation already skipped for swags and no end-check line in the log). Every way a piece can be
// damaged, broken, removed or deleted logs once for a swag with the call stack, and the first wear update of each swag
// logs its support state. Removed before the release.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PotatoWright
{
    [HarmonyPatch]
    internal static class SwagProbe
    {
        private static readonly HashSet<int> s_wearLogged = new HashSet<int>();
        private static readonly HashSet<int> s_supportLogged = new HashSet<int>();
        private static int s_supportCalls;

        private static bool Prepare()
        {
            return Plugin.TestBuild;
        }

        private static string Where()
        {
            // Skip the probe's own frames.
            string[] lines = Environment.StackTrace.Split('\n');
            return string.Join(" | ", lines, Math.Min(3, lines.Length), Math.Max(0, Math.Min(14, lines.Length - 3))).Replace("\r", "").Replace("  at ", "");
        }

        private static string State(WearNTear wear)
        {
            ZNetView view = wear.GetComponent<ZNetView>();
            ZDO zdo = (view != null && view.IsValid()) ? view.GetZDO() : null;
            return "health " + wear.m_health.ToString("0") + ", zdo health " + (zdo != null ? zdo.GetFloat(ZDOVars.s_health, -1f).ToString("0") : "-")
                + ", support " + wear.m_support.ToString("0") + " (min " + wear.GetMinSupport().ToString("0") + "), noSupportWear " + wear.m_noSupportWear
                + ", parent " + (wear.transform.parent != null ? wear.transform.parent.name : "none") + ", owner " + (view != null && view.IsValid() && view.IsOwner());
        }

        private static bool IsSwag(Component c)
        {
            return c != null && c.GetComponent<Swag>() != null;
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.ApplyDamage))]
        [HarmonyPrefix]
        private static void ApplyDamage(WearNTear __instance, float damage)
        {
            if (IsSwag(__instance))
            {
                Plugin.Debug("PROBE swag " + __instance.name + " ApplyDamage " + damage.ToString("0") + "; " + State(__instance) + "; from " + Where());
            }
        }

        [HarmonyPatch(typeof(WearNTear), "RPC_Damage")]
        [HarmonyPrefix]
        private static void RpcDamage(WearNTear __instance, HitData hit)
        {
            if (IsSwag(__instance))
            {
                Plugin.Debug("PROBE swag " + __instance.name + " RPC_Damage " + (hit != null ? hit.GetTotalDamage().ToString("0") + " by " + hit.m_hitType : "null") + "; " + State(__instance) + "; from " + Where());
            }
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Remove))]
        [HarmonyPrefix]
        private static void Remove(WearNTear __instance)
        {
            if (IsSwag(__instance))
            {
                Plugin.Debug("PROBE swag " + __instance.name + " Remove; " + State(__instance) + "; from " + Where());
            }
        }

        [HarmonyPatch(typeof(WearNTear), "Destroy")]
        [HarmonyPrefix]
        private static void Destroy(WearNTear __instance)
        {
            if (IsSwag(__instance))
            {
                Plugin.Debug("PROBE swag " + __instance.name + " Destroy; " + State(__instance) + "; from " + Where());
            }
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Destroy))]
        [HarmonyPrefix]
        private static void SceneDestroy(GameObject go)
        {
            if (go != null && go.GetComponent<Swag>() != null)
            {
                Plugin.Debug("PROBE swag " + go.name + " ZNetScene.Destroy; from " + Where());
            }
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.UpdateWear))]
        [HarmonyPostfix]
        private static void UpdateWear(WearNTear __instance)
        {
            if (IsSwag(__instance) && s_wearLogged.Add(__instance.GetInstanceID()))
            {
                Plugin.Debug("PROBE swag " + __instance.name + " first wear update; " + State(__instance));
            }
        }
    
        // test-o: support should no longer be worked out for swags at all.
        [HarmonyPatch(typeof(WearNTear), "UpdateSupport")]
        [HarmonyPrefix]
        private static void UpdateSupport(WearNTear __instance)
        {
            if (!IsSwag(__instance))
            {
                return;
            }
            s_supportCalls++;
            if (s_supportLogged.Add(__instance.GetInstanceID()))
            {
                Plugin.Debug("PROBE swag " + __instance.name + " UpdateSupport called (" + s_supportCalls + " so far); " + State(__instance) + "; from " + Where());
            }
        }
    }
}
