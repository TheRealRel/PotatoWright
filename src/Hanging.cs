// Things hung on a rope or chain (the user's choices, 2026-11-06): the hanging pieces themselves are made in
// HangPieces.cs; this file has the piece's own behaviour (Hanging: strings drawn from their ZDO, the check that its rope
// or chain is still there) and the placing (HangPlacing: aim at a rope or chain).
// Each one falls (drops its materials) when its rope or chain is gone, checked by the owner every few seconds like a
// swag's ends. Strings are built once from their ZDO, no physics, nothing updated per frame. Colliders on piece_nonsolid
// (aimed at and removed with the hammer, walked through).
// Saved in worlds: never rename the ZDO keys.
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace PotatoWright
{
    // On every hanging piece.
    public sealed class Hanging : MonoBehaviour, IPlaced
    {
        public int m_kind;

        internal bool IsString => m_kind == HangPieces.Bunting || m_kind == HangPieces.Fey;

        internal static readonly int EndKey = "PotatoWright_hangEnd".GetStableHashCode();
        internal static readonly int SagKey = "PotatoWright_hangSag".GetStableHashCode();
        internal static readonly int StyleKey = "PotatoWright_hangStyle".GetStableHashCode();
        private static readonly int ShipParentKey = "ShipBuild_ParentUUID".GetStableHashCode();

        // Set by the placing code just before the game places a string; read once by OnPlaced.
        internal static bool s_pending;
        internal static Vector3 s_pendingEnd;
        internal static float s_pendingSag;
        internal static int s_pendingStyle;

        private static int s_ropeMask;
        private static readonly Collider[] s_hits = new Collider[16];
        private static MaterialPropertyBlock s_red;

        private ZNetView m_nview;
        private WearNTear m_wear;
        private GameObject m_visual;
        private Vector3 m_end;
        private float m_sag = -1f;
        private int m_style = -1;
        private int m_misses;
        private float m_start;
        private Transform m_lastParent;
        private bool m_parentKnown;
        private bool m_invalid;

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            m_wear = GetComponent<WearNTear>();
            m_start = Time.time;
            if (s_ropeMask == 0) s_ropeMask = LayerMask.GetMask("piece_nonsolid");
            ZDO zdo = (m_nview != null) ? m_nview.GetZDO() : null;
            if (zdo == null)
            {
                if (IsString) Preview(new Vector3(0f, 0f, 1.6f), 0.15f, 0);
                return;
            }
            if (IsString)
            {
                Vector3 end = zdo.GetVec3(EndKey, Vector3.zero);
                if (end != Vector3.zero) Build(end, zdo.GetFloat(SagKey, 0.15f), zdo.GetInt(StyleKey, 0), true);
            }
            InvokeRepeating(nameof(CheckHold), Random.Range(3f, 6f), 3f);
        }

        public void OnPlaced()
        {
            if (!IsString || !s_pending || m_nview == null || !m_nview.IsValid()) return;
            s_pending = false;
            ZDO zdo = m_nview.GetZDO();
            zdo.Set(EndKey, s_pendingEnd);
            zdo.Set(SagKey, s_pendingSag);
            zdo.Set(StyleKey, s_pendingStyle);
            Build(s_pendingEnd, s_pendingSag, s_pendingStyle, true);
            Plugin.Debug("Hanging " + name + " placed along a rope of " + s_pendingEnd.magnitude.ToString("0.00") + " m, sag " + s_pendingSag.ToString("0.00") + ", style " + s_pendingStyle);
        }

        internal void Preview(Vector3 end, float sag, int style)
        {
            if (m_visual != null && (end - m_end).sqrMagnitude < 0.0001f && Mathf.Abs(sag - m_sag) < 0.001f && style == m_style) return;
            Build(end, sag, style, false);
        }

        private void Build(Vector3 end, float sag, int style, bool placed)
        {
            m_end = end;
            m_sag = sag;
            m_style = style;
            if (m_visual != null) Destroy(m_visual);
            m_invalid = false;
            m_visual = new GameObject("string");
            m_visual.transform.SetParent(transform, false);
            List<Vector3> curve = Swag.Points(Vector3.zero, end, Vector3.down, sag, Mathf.Clamp(Mathf.CeilToInt(end.magnitude * 8f), 16, 520));
            Renderer shown = null;
            if (!GUIManager.IsHeadless())
            {
                if (m_kind == HangPieces.Bunting)
                {
                    Material[] mats = HangPieces.BuntingMaterials(style);
                    if (mats != null && mats.Length > 0 && mats[0] != null)
                    {
                        Mesh mesh = HangPieces.BuntingMesh(curve, style, out int flags);
                        MeshRenderer r = (MeshRenderer)HangPieces.MeshPart(m_visual, "flags", mesh, null, Vector3.zero, Quaternion.identity, Vector3.one);
                        r.sharedMaterials = mats;
                        shown = r;
                        if (placed) Plugin.Debug("Bunting " + name + ": " + flags + " flags, look " + style + " (" + HangPieces.LookName(style) + ")");
                    }
                }
                else if (HangPieces.FeyTemplate)
                {
                    shown = Fey(m_visual, curve, placed);
                }
            }
            if (shown != null)
            {
                LODGroup lod = m_visual.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(0.01f, new[] { shown }) });
                lod.RecalculateBounds();
            }
            if (placed)
            {
                // Hammer targets just under the rope (flags, wisps), so the rope itself stays aimable.
                int layer = LayerMask.NameToLayer("piece_nonsolid");
                float drop = m_kind == HangPieces.Bunting ? HangPieces.FlagLength * 0.5f : 0.12f;
                int from = 0;
                for (int i = 1; i < curve.Count; i++)
                {
                    bool last = i == curve.Count - 1;
                    if (!last && Vector3.Distance(curve[from], curve[i]) < 1f) continue;
                    Vector3 a = curve[from] + Vector3.down * drop, b = curve[i] + Vector3.down * drop;
                    GameObject box = new GameObject("collider");
                    box.layer = layer;
                    box.transform.SetParent(m_visual.transform, false);
                    box.transform.localPosition = (a + b) / 2f;
                    box.transform.localRotation = Quaternion.LookRotation(b - a);
                    box.AddComponent<BoxCollider>().size = new Vector3(0.12f, m_kind == HangPieces.Bunting ? HangPieces.FlagLength * 0.7f : 0.12f, Vector3.Distance(a, b));
                    from = i;
                }
            }
        }

        // The game's fey light wisps, spawned along the rope instead of the garland's box; their number and rate grow
        // with the length (the garland is about 4 m). Paused when not drawn.
        private Renderer Fey(GameObject parent, List<Vector3> curve, bool placed)
        {
            GameObject go = Instantiate(HangPieces.FeyTemplate, parent.transform, false);
            go.name = "fey";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            Light light = go.GetComponent<Light>();
            if (light) DestroyImmediate(light);
            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            if (ps == null) return null;
            List<Vector3> points = Swag.Resample(curve, 0.15f);
            float length = 0f;
            for (int i = 1; i < points.Count; i++) length += Vector3.Distance(points[i - 1], points[i]);
            Mesh mesh = new Mesh { name = "potatowright_fey" };
            mesh.SetVertices(points);
            mesh.SetIndices(Enumerable.Range(0, points.Count).ToArray(), MeshTopology.Points, 0);
            mesh.RecalculateBounds();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            float factor = Mathf.Max(0.3f, length / 4f);
            ParticleSystem.MainModule main = ps.main;
            main.maxParticles = Mathf.Max(4, Mathf.RoundToInt(main.maxParticles * factor));
            main.cullingMode = ParticleSystemCullingMode.Pause;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTimeMultiplier = emission.rateOverTimeMultiplier * factor;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Mesh;
            shape.meshShapeType = ParticleSystemMeshShapeType.Vertex;
            shape.mesh = mesh;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
            shape.scale = Vector3.one;
            ps.Play(true);
            if (placed) Plugin.Debug("Fey lights " + name + ": " + length.ToString("0.0") + " m, max " + main.maxParticles + ", rate x" + factor.ToString("0.00"));
            return go.GetComponent<ParticleSystemRenderer>();
        }

        internal void SetInvalid(bool invalid)
        {
            if (invalid == m_invalid || m_visual == null) return;
            m_invalid = invalid;
            if (s_red == null)
            {
                s_red = new MaterialPropertyBlock();
                s_red.SetColor("_Color", Color.red);
                s_red.SetColor("_EmissionColor", Color.red * 0.7f);
            }
            foreach (MeshRenderer r in m_visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.SetPropertyBlock(invalid ? s_red : null);
            }
        }

        // Owner only: the rope or chain is still there (a swag's collider at the hook, or at the middle of a string).
        private void CheckHold()
        {
            if (m_nview == null || !m_nview.IsValid() || !m_nview.IsOwner() || Time.time - m_start < 10f) return;
            if (IsString && m_sag < 0f) return;
            if (ZNetScene.instance == null || ZNetScene.instance.OutsideActiveArea(transform.position) || ZoneSystem.instance == null || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoBuildingFall)) return;
            if (!m_parentKnown || transform.parent != m_lastParent)
            {
                m_parentKnown = true;
                m_lastParent = transform.parent;
                m_start = Time.time;
                m_misses = 0;
                return;
            }
            Ship ship = GetComponentInParent<Ship>();
            if (ship == null && !string.IsNullOrEmpty(m_nview.GetZDO().GetString(ShipParentKey))) return;
            if (ship != null) Physics.SyncTransforms();
            Vector3 point = IsString ? transform.TransformPoint(Swag.Points(Vector3.zero, m_end, Vector3.down, m_sag, 3)[1]) : transform.position;
            if (OnRope(point))
            {
                m_misses = 0;
                return;
            }
            m_misses++;
            Plugin.Debug("Hanging " + name + ": no rope at " + point + " (" + m_misses + ")");
            if (m_misses >= 2 && m_wear != null)
            {
                Plugin.Debug("Hanging " + name + " falls: its rope or chain is gone.");
                m_wear.Remove();
            }
        }

        private static bool OnRope(Vector3 point)
        {
            int n = Physics.OverlapSphereNonAlloc(point, 0.2f, s_hits, s_ropeMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                if (s_hits[i] != null && s_hits[i].GetComponentInParent<Swag>() != null) return true;
            }
            return !ZNetScene.instance.IsAreaReady(point);
        }
    }

    // Aiming a hanging at a rope or chain.
    [HarmonyPatch]
    internal static class HangPlacing
    {
        private static Swag s_target;
        private static Vector3 s_end;
        private static float s_sag;
        private static int s_style, s_step, s_turn;
        // A spot hanging's turn on its rope (the user, test-f: "The hooded lantern needs to be rotatable"), in the game's
        // own building step.
        private const float TurnStep = 22.5f;

        // Aimed at a placed rope or chain this frame (the camera keeps its zoom then: WheelNoZoom).
        internal static bool Locked => s_target != null;
        private static float s_wheel;
        private static Player.PlacementStatus s_lastStatus = (Player.PlacementStatus)(-1);

        private static Hanging GhostHanging(Player player)
        {
            return (player != null && player.m_placementGhost != null) ? player.m_placementGhost.GetComponent<Hanging>() : null;
        }

        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        [HarmonyPostfix]
        private static void UpdatePlacementGhost_Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            Hanging hanging = GhostHanging(__instance);
            if (hanging == null) return;
            GameObject ghost = __instance.m_placementGhost;
            s_target = null;
            if (!ghost.activeSelf || GameCamera.instance == null) return;
            if (__instance.m_placementStatus != s_lastStatus)
            {
                s_lastStatus = __instance.m_placementStatus;
                Plugin.Debug("Hanging ghost: the game's placement status " + s_lastStatus);
            }
            Transform cam = GameCamera.instance.transform;
            Swag swag = null;
            RaycastHit hit = default;
            if (Physics.Raycast(cam.position, cam.forward, out hit, 50f, __instance.m_placeRayMask))
            {
                swag = hit.collider.GetComponentInParent<Swag>();
            }
            if (swag == null || !swag.Placed)
            {
                if (hanging.IsString) hanging.Preview(new Vector3(0f, 0f, 1.6f), 0.15f, s_style);
                if (__instance.m_placementStatus == Player.PlacementStatus.Valid) __instance.m_placementStatus = Player.PlacementStatus.Invalid;
                __instance.SetPlacementGhostValid(false);
                if (hanging.IsString) hanging.SetInvalid(true);
                return;
            }
            Transform st = swag.transform;
            Vector3 up = st.up;
            if (hanging.IsString)
            {
                ghost.transform.position = st.position;
                ghost.transform.rotation = st.rotation;
                s_end = swag.End;
                s_sag = swag.Sag;
                hanging.Preview(s_end, s_sag, s_style);
            }
            else
            {
                List<Vector3> curve = Swag.Points(Vector3.zero, swag.End, Vector3.down, swag.Sag, Mathf.Clamp(Mathf.CeilToInt(swag.End.magnitude * 20f), 16, 1300));
                Vector3 local = st.InverseTransformPoint(hit.point);
                int best = 0;
                float bestD = float.MaxValue;
                for (int i = 0; i < curve.Count; i++)
                {
                    float d = (curve[i] - local).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = i; }
                }
                Vector3 tangent = curve[Mathf.Min(best + 1, curve.Count - 1)] - curve[Mathf.Max(best - 1, 0)];
                Vector3 forward = Vector3.ProjectOnPlane(st.TransformDirection(tangent), up);
                ghost.transform.position = st.TransformPoint(curve[best]);
                ghost.transform.rotation = (forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward, up) : st.rotation) * Quaternion.Euler(0f, s_turn * TurnStep, 0f);
            }
            s_target = swag;
            if (__instance.m_placementStatus == Player.PlacementStatus.Valid)
            {
                Vector3 at = ghost.transform.position;
                if (Location.IsInsideNoBuildLocation(at)) __instance.m_placementStatus = Player.PlacementStatus.NoBuildZone;
                else if (!PrivateArea.CheckAccess(at, 0f, false, false)) __instance.m_placementStatus = Player.PlacementStatus.PrivateZone;
            }
            __instance.SetPlacementGhostValid(__instance.m_placementStatus == Player.PlacementStatus.Valid);
            if (hanging.IsString) hanging.SetInvalid(__instance.m_placementStatus != Player.PlacementStatus.Valid);
        }

        // While aimed at a rope or chain, the mouse wheel steps the bunting through its looks (HangPieces.LookOrder) and turns
        // a spot hanging (lanterns, brazier, herbs) on the rope, up forward and down back. Not aimed at one, the wheel is
        // the camera's.
        [HarmonyPatch(typeof(Player), "UpdatePlacement")]
        [HarmonyPostfix]
        private static void UpdatePlacement_Postfix(Player __instance, bool takeInput)
        {
            if (__instance != Player.m_localPlayer || !takeInput) return;
            Hanging hanging = GhostHanging(__instance);
            if (hanging == null || Hud.IsPieceSelectionVisible() || (hanging.IsString && hanging.m_kind != HangPieces.Bunting)) return;
            if (s_target == null)
            {
                s_wheel = 0f;
                return;
            }
            s_wheel += ZInput.GetMouseScrollWheel();
            if (Mathf.Abs(s_wheel) < __instance.m_scrollAmountThreshold) return;
            int dir = s_wheel > 0f ? 1 : -1;
            s_wheel = 0f;
            if (hanging.m_kind != HangPieces.Bunting)
            {
                s_turn = (s_turn + dir + 16) % 16;
                Plugin.Debug("Hanging turn " + (s_turn * TurnStep) + " degrees");
                return;
            }
            int n = HangPieces.LookOrder.Length;
            s_step = ((s_step + dir) % n + n) % n;
            s_style = HangPieces.LookOrder[s_step];
            __instance.Message(MessageHud.MessageType.Center, "$potatowright_flags_" + s_style);
            Plugin.Debug("Bunting look " + s_style + " (" + HangPieces.LookName(s_style) + ")");
        }

        [HarmonyPatch(typeof(Player), "TryPlacePiece")]
        [HarmonyPrefix]
        private static bool TryPlacePiece_Prefix(Player __instance, Piece piece, ref bool __result)
        {
            if (__instance != Player.m_localPlayer || piece == null) return true;
            Hanging hanging = piece.GetComponent<Hanging>();
            if (hanging == null) return true;
            __instance.UpdatePlacementGhost(false);
            if (s_target == null)
            {
                __instance.Message(MessageHud.MessageType.Center, "$potatowright_hang_aim");
                Plugin.Debug("Hanging " + piece.name + ": not aimed at a rope or chain");
                __result = false;
                return false;
            }
            if (hanging.IsString)
            {
                Hanging.s_pending = true;
                Hanging.s_pendingEnd = s_end;
                Hanging.s_pendingSag = s_sag;
                Hanging.s_pendingStyle = hanging.m_kind == HangPieces.Bunting ? s_style : 0;
            }
            Plugin.Debug("Hanging " + piece.name + ": placing on " + s_target.name + " at " + __instance.m_placementGhost.transform.position + " (status " + __instance.m_placementStatus + ")");
            return true;
        }

        [HarmonyPatch(typeof(Player), "TryPlacePiece")]
        [HarmonyPostfix]
        private static void TryPlacePiece_Postfix(Piece piece)
        {
            if (piece != null && piece.GetComponent<Hanging>() != null) Hanging.s_pending = false;
        }
    }
}
