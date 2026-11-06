// Ropes and chains hanging between two points ("swags"). Research and decisions: project docs
// potatowright-plan.md and potatowright-swags-research.md.
//
// - Placing: click the first end (nothing is taken), aim at the second end, roll the mouse wheel to set the sag, click
//   again: the piece is placed at the first end and keeps the second end and the sag in its ZDO. Both ends snap to
//   other pieces' snap points (Shift: no snapping), as any piece does. Opening the build menu, choosing another piece or
//   walking far away drops the first end.
// - Looks: the rope is the game's own rope (a LineRenderer with the material cart_rope, like ship rigging and carts).
//   Chains are either the game's flat chain (the grappling hook's: a LineRenderer with GrapplingHookWire, tinted) or
//   3D links built here (the game's cauldron_chain metal, tinted), with the flat chain at a distance.
// - Built once from the ZDO, no physics, nothing updated per frame. Colliders are on the piece_nonsolid layer: the
//   hammer can aim at and remove them, and nothing collides with them.
// - The game's support check is off (m_noSupportWear false: a long span would always fail it) and nothing can rest on a
//   swag (m_supports false). Instead the owner checks every few seconds that both ends still touch something solid (the
//   game's support layers); when one doesn't twice running, the swag is removed and drops its materials, like a piece
//   that lost its support. The game's NoBuildingFall world setting stops that, as it stops the game's own collapse.
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace PotatoWright
{
    internal static class SwagPieces
    {
        internal const float MaxLength = 16f, MinLength = 0.5f;

        internal static readonly Color[] FlatTint =
        {
            Color.white,
            new Color(1f, 1f, 1f, 1f),
            new Color(1.6f, 1.1f, 0.6f, 1f),
            new Color(0.55f, 0.6f, 0.55f, 1f),
        };

        internal static readonly Color[] LinkTint =
        {
            Color.white,
            new Color(0.52f, 0.52f, 0.52f, 1f),
            new Color(0.8f, 0.52f, 0.28f, 1f),
            new Color(0.2f, 0.22f, 0.2f, 1f),
        };

        internal static Material Rope, Wire, Links;
        internal static readonly Material[] FlatChain = new Material[4];
        internal static readonly Material[] LinkMetal = new Material[4];

        internal static void Create()
        {
            if (!GUIManager.IsHeadless())
            {
                Rope = PrefabManager.Cache.GetPrefab<Material>("cart_rope");
                Wire = PrefabManager.Cache.GetPrefab<Material>("GrapplingHookWire");
                Links = PrefabManager.Cache.GetPrefab<Material>("cauldron_chain");
                if (!Rope || !Wire || !Links)
                {
                    UnityEngine.Debug.LogWarning(Plugin.Tag + " Rope/chain materials missing: cart_rope " + (bool)Rope + ", GrapplingHookWire " + (bool)Wire + ", cauldron_chain " + (bool)Links + ".");
                }
                for (int k = 1; k < 4; k++)
                {
                    if (Wire)
                    {
                        FlatChain[k] = new Material(Wire) { name = "potatowright_flatchain_" + k };
                        if (FlatChain[k].HasProperty("_Color")) FlatChain[k].SetColor("_Color", FlatTint[k]);
                    }
                    if (Links)
                    {
                        LinkMetal[k] = new Material(Links) { name = "potatowright_links_" + k };
                        if (LinkMetal[k].HasProperty("_Color")) LinkMetal[k].SetColor("_Color", LinkTint[k]);
                    }
                }
            }
            Sprite rope = ItemIcon("LeatherScraps"), chain = ItemIcon("Chain");
            // Saved in worlds: never rename.
            Add("PotatoWright_SwagRope", "piece_potatowright_swag_rope", 0, false, "wood_pole", "piece_workbench", "LeatherScraps", 2, rope);
            Add("PotatoWright_SwagChainIron", "piece_potatowright_swag_iron", 1, false, "iron_grate", "forge", "Iron", 1, chain);
            Add("PotatoWright_SwagChainBronze", "piece_potatowright_swag_bronze", 2, false, "iron_grate", "forge", "Bronze", 1, chain);
            Add("PotatoWright_SwagChainBlackMetal", "piece_potatowright_swag_blackmetal", 3, false, "iron_grate", "forge", "BlackMetal", 1, chain);
            if (Plugin.TestBuild)
            {
                // TEST ONLY: the 3D link look beside the flat one, to choose from. Remove placed ones before the release.
                Add("PotatoWright_TestSwagChainIronLinks", "piece_potatowright_swag_test_iron", 1, true, "iron_grate", "forge", "Iron", 1, chain);
                Add("PotatoWright_TestSwagChainBronzeLinks", "piece_potatowright_swag_test_bronze", 2, true, "iron_grate", "forge", "Bronze", 1, chain);
                Add("PotatoWright_TestSwagChainBlackMetalLinks", "piece_potatowright_swag_test_blackmetal", 3, true, "iron_grate", "forge", "BlackMetal", 1, chain);
            }
        }

        private static Sprite ItemIcon(string item)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(item);
            ItemDrop drop = prefab ? prefab.GetComponent<ItemDrop>() : null;
            Sprite[] icons = drop ? drop.m_itemData.m_shared.m_icons : null;
            return (icons != null && icons.Length > 0) ? icons[0] : null;
        }

        private static void Add(string name, string token, int kind, bool links, string baseName, string station, string item, int amount, Sprite icon)
        {
            CustomPiece custom = new CustomPiece(name, baseName, new PieceConfig
            {
                Name = "$" + token,
                Description = "$piece_potatowright_swag_description",
                PieceTable = "Hammer",
                Category = "Furniture",
                CraftingStation = station,
                Requirements = new[] { new RequirementConfig(item, amount, 0, true) },
            });
            GameObject prefab = custom.PiecePrefab;
            if (!prefab)
            {
                UnityEngine.Debug.LogWarning(Plugin.Tag + " " + baseName + " not found; no " + name + ".");
                return;
            }
            // Only the piece, its network view and its wear stay: every model, collider and child of the copied piece goes.
            foreach (Transform child in prefab.transform.Cast<Transform>().ToList())
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            foreach (Component c in prefab.GetComponents<Component>())
            {
                if (!(c is Transform) && !(c is ZNetView) && !(c is Piece) && !(c is WearNTear))
                {
                    UnityEngine.Object.DestroyImmediate(c);
                }
            }
            Piece piece = prefab.GetComponent<Piece>();
            piece.m_canRotate = false;
            piece.m_clipEverything = true;
            piece.m_noClipping = false;
            piece.m_groundPiece = false;
            piece.m_groundOnly = false;
            if (icon) piece.m_icon = icon;
            WearNTear wear = prefab.GetComponent<WearNTear>();
            if (wear)
            {
                wear.m_noSupportWear = false;
                wear.m_supports = false;
                wear.m_new = null;
                wear.m_worn = null;
                wear.m_broken = null;
                wear.m_fragmentRoots = null;
                wear.m_autoCreateFragments = false;
                wear.m_materialType = (kind == 0) ? WearNTear.MaterialType.Wood : WearNTear.MaterialType.Iron;
            }
            // The first end snaps to other pieces' snap points, as any piece does.
            GameObject snap = new GameObject("$hud_snappoint_end");
            snap.transform.SetParent(prefab.transform, false);
            snap.tag = "snappoint";
            snap.SetActive(false);
            Swag swag = prefab.AddComponent<Swag>();
            swag.m_kind = kind;
            swag.m_links = links;
            PieceManager.Instance.AddPiece(custom);
            Plugin.Debug("Swag piece " + name + " (" + baseName + " copy, " + item + " x" + amount + " at " + station + ")");
        }
    }

    // On every swag piece. Draws it from its ZDO (placed) or as the placing preview (no ZDO).
    public sealed class Swag : MonoBehaviour, IPlaced
    {
        public int m_kind;
        public bool m_links;

        internal static readonly int EndKey = "PotatoWright_swagEnd".GetStableHashCode();
        internal static readonly int SagKey = "PotatoWright_swagSag".GetStableHashCode();

        // The game's support layers (WearNTear's support test): what an end can hang from.
        private static int s_holdMask;
        private static readonly Collider[] s_hits = new Collider[8];

        // Set by the placing code just before the game places the piece; read once by OnPlaced.
        internal static bool s_pending;
        internal static Vector3 s_pendingEnd;
        internal static float s_pendingSag;

        private ZNetView m_nview;
        private WearNTear m_wear;
        private GameObject m_visual;
        private Vector3 m_end;
        private float m_sag = -1f;
        private int m_misses;
        private float m_start;

        private static MaterialPropertyBlock s_red;
        private bool m_invalid;

        private const float LinkLength = 0.12f, LinkWidth = 0.07f, LinkWire = 0.013f;
        private const float RopeWidth = 0.05f, ChainWidth = 0.12f;

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            m_wear = GetComponent<WearNTear>();
            m_start = Time.time;
            if (s_holdMask == 0)
            {
                s_holdMask = LayerMask.GetMask("piece", "Default", "static_solid", "Default_small", "terrain");
            }
            ZDO zdo = (m_nview != null) ? m_nview.GetZDO() : null;
            if (zdo == null)
            {
                // The placing preview: a short hanging stub until the first end is set.
                Preview(new Vector3(0f, -0.3f, 0f), 0f);
                return;
            }
            Vector3 end = zdo.GetVec3(EndKey, Vector3.zero);
            if (end != Vector3.zero)
            {
                Build(end, zdo.GetFloat(SagKey, 0.15f), true);
            }
            InvokeRepeating(nameof(CheckEnds), UnityEngine.Random.Range(3f, 6f), 3f);
        }

        public void OnPlaced()
        {
            if (!s_pending || m_nview == null || !m_nview.IsValid())
            {
                return;
            }
            s_pending = false;
            ZDO zdo = m_nview.GetZDO();
            zdo.Set(EndKey, s_pendingEnd);
            zdo.Set(SagKey, s_pendingSag);
            Build(s_pendingEnd, s_pendingSag, true);
            Plugin.Debug("Swag " + name + " placed at " + transform.position + ", length " + s_pendingEnd.magnitude.ToString("0.00") + " m, sag " + s_pendingSag.ToString("0.00"));
        }

        // The preview while placing: rebuilt only when the end or the sag moved.
        internal void Preview(Vector3 end, float sag)
        {
            if (m_visual != null && (end - m_end).sqrMagnitude < 0.0001f && Mathf.Abs(sag - m_sag) < 0.001f)
            {
                return;
            }
            Build(end, sag, false);
        }

        private void Build(Vector3 end, float sag, bool placed)
        {
            m_end = end;
            m_sag = sag;
            if (m_visual != null)
            {
                Destroy(m_visual);
            }
            m_visual = new GameObject("swag");
            m_visual.transform.SetParent(transform, false);
            if (placed)
            {
                m_visual.layer = gameObject.layer;
            }
            Vector3 down = transform.InverseTransformDirection(Vector3.down);
            List<Vector3> points = Points(Vector3.zero, end, down, sag, Mathf.Clamp(Mathf.CeilToInt(end.magnitude * 2f), 8, 40));
            if (!GUIManager.IsHeadless())
            {
                if (m_kind == 0)
                {
                    Line(m_visual, points, SwagPieces.Rope, RopeWidth);
                }
                else if (!m_links)
                {
                    Line(m_visual, points, SwagPieces.FlatChain[m_kind], ChainWidth);
                }
                else
                {
                    LinksWithFlatFar(m_visual, Points(Vector3.zero, end, down, sag, Mathf.Clamp(Mathf.CeilToInt(end.magnitude * 8f), 16, 160)), points);
                }
            }
            m_invalid = false;
            if (placed)
            {
                Colliders(m_visual, points);
                // A snap point at the second end too, so other swags can start from either end.
                GameObject snap = new GameObject("$hud_snappoint_end2");
                snap.transform.SetParent(transform, false);
                snap.transform.localPosition = end;
                snap.tag = "snappoint";
                snap.SetActive(false);
            }
        }

        // The preview turns red when it can't be placed. The game's highlight (MaterialMan) only colours mesh renderers it
        // found when it first coloured the preview; the swag's are line renderers and get rebuilt, so it does its own.
        internal void SetInvalid(bool invalid)
        {
            if (invalid == m_invalid || m_visual == null)
            {
                return;
            }
            m_invalid = invalid;
            if (s_red == null)
            {
                s_red = new MaterialPropertyBlock();
                s_red.SetColor("_Color", Color.red);
                s_red.SetColor("_EmissionColor", Color.red * 0.7f);
            }
            foreach (Renderer r in m_visual.GetComponentsInChildren<Renderer>(true))
            {
                r.SetPropertyBlock(invalid ? s_red : null);
            }
        }

        // The game's own sag (LineConnect): the middle hangs (distance / 2) x sag below the straight line.
        internal static List<Vector3> Points(Vector3 a, Vector3 b, Vector3 down, float sag, int count)
        {
            List<Vector3> list = new List<Vector3>(count);
            float half = Vector3.Distance(a, b) / 2f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                float k = Mathf.Abs(0.5f - t) * 2f;
                k = 1f - k * k;
                list.Add(Vector3.Lerp(a, b, t) + down * half * sag * k);
            }
            return list;
        }

        private static LineRenderer Line(GameObject parent, List<Vector3> points, Material material, float width)
        {
            GameObject go = new GameObject("line");
            go.transform.SetParent(parent.transform, false);
            go.layer = parent.layer;
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Tile;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.widthMultiplier = width;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            line.receiveShadows = true;
            if (material) line.sharedMaterial = material;
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
            return line;
        }

        // 3D links along the curve, and the flat chain beyond close range (LODGroup).
        private void LinksWithFlatFar(GameObject parent, List<Vector3> fine, List<Vector3> coarse)
        {
            GameObject go = new GameObject("links");
            go.transform.SetParent(parent.transform, false);
            go.layer = parent.layer;
            Mesh mesh = LinkMesh(fine);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            if (SwagPieces.LinkMetal[m_kind]) r.sharedMaterial = SwagPieces.LinkMetal[m_kind];
            LineRenderer far = Line(parent, coarse, SwagPieces.FlatChain[m_kind], ChainWidth);
            LODGroup lod = parent.AddComponent<LODGroup>();
            lod.SetLODs(new[]
            {
                new LOD(0.04f, new Renderer[] { r }),
                new LOD(0.004f, new Renderer[] { far }),
            });
            lod.RecalculateBounds();
        }

        // Interlocking links, each turned a quarter from the one before, a link every inner length along the curve.
        private static Mesh LinkMesh(List<Vector3> path)
        {
            float pitch = LinkLength - 2f * LinkWire;
            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> tri = new List<int>();
            float total = 0f;
            List<float> along = new List<float> { 0f };
            for (int i = 1; i < path.Count; i++)
            {
                total += Vector3.Distance(path[i - 1], path[i]);
                along.Add(total);
            }
            int links = Mathf.Max(1, Mathf.FloorToInt(total / pitch));
            const int ring = 10, sides = 4;
            float straight = LinkLength - LinkWidth, bend = LinkWidth / 2f - LinkWire;
            int seg = 0;
            for (int l = 0; l <= links; l++)
            {
                float d = Mathf.Min(l * pitch, total);
                while (seg < along.Count - 2 && along[seg + 1] < d) seg++;
                float f = (along[seg + 1] - along[seg]) > 1e-5f ? (d - along[seg]) / (along[seg + 1] - along[seg]) : 0f;
                Vector3 c = Vector3.Lerp(path[seg], path[seg + 1], f);
                Vector3 t = (path[seg + 1] - path[seg]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, t);
                if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(Vector3.forward, t);
                side.Normalize();
                Vector3 up = Vector3.Cross(t, side);
                if ((l & 1) == 1)
                {
                    Vector3 s = side;
                    side = up;
                    up = -s;
                }
                int start = v.Count;
                for (int i = 0; i < ring; i++)
                {
                    // A stadium: two half circles joined by straight sides, in the link's plane (t, side).
                    float a = (float)i / ring * Mathf.PI * 2f;
                    float x = Mathf.Cos(a) * bend + Mathf.Sign(Mathf.Cos(a)) * straight / 2f;
                    float y = Mathf.Sin(a) * bend;
                    Vector3 centre = c + t * x + side * y;
                    Vector3 outward = (t * Mathf.Cos(a) + side * Mathf.Sin(a)).normalized;
                    for (int j = 0; j < sides; j++)
                    {
                        float b = (float)j / sides * Mathf.PI * 2f;
                        Vector3 dir = outward * Mathf.Cos(b) + up * Mathf.Sin(b);
                        v.Add(centre + dir * LinkWire);
                        n.Add(dir);
                        uv.Add(new Vector2((float)i / ring, (float)j / sides));
                    }
                }
                for (int i = 0; i < ring; i++)
                {
                    int i2 = (i + 1) % ring;
                    for (int j = 0; j < sides; j++)
                    {
                        int j2 = (j + 1) % sides;
                        int p0 = start + i * sides + j, p1 = start + i2 * sides + j, p2 = start + i2 * sides + j2, p3 = start + i * sides + j2;
                        tri.Add(p0); tri.Add(p1); tri.Add(p2);
                        tri.Add(p0); tri.Add(p2); tri.Add(p3);
                    }
                }
            }
            Mesh mesh = new Mesh { name = "potatowright_links" };
            if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        // Thin boxes along the curve, about one per metre, on piece_nonsolid: aimed at and removed with the hammer,
        // walked through by everyone.
        private static void Colliders(GameObject parent, List<Vector3> points)
        {
            int layer = LayerMask.NameToLayer("piece_nonsolid");
            int from = 0;
            for (int i = 1; i < points.Count; i++)
            {
                bool last = i == points.Count - 1;
                if (!last && Vector3.Distance(points[from], points[i]) < 1f)
                {
                    continue;
                }
                Vector3 a = points[from], b = points[i];
                GameObject box = new GameObject("collider");
                box.layer = layer;
                box.transform.SetParent(parent.transform, false);
                box.transform.localPosition = (a + b) / 2f;
                box.transform.localRotation = Quaternion.LookRotation(b - a);
                BoxCollider c = box.AddComponent<BoxCollider>();
                c.size = new Vector3(0.15f, 0.15f, Vector3.Distance(a, b));
                from = i;
            }
        }

        // Owner only: both ends still touch something solid. Ends in areas not loaded yet count as held.
        private void CheckEnds()
        {
            if (m_nview == null || !m_nview.IsValid() || !m_nview.IsOwner() || m_sag < 0f || Time.time - m_start < 10f)
            {
                return;
            }
            if (ZNetScene.instance == null || ZNetScene.instance.OutsideActiveArea(transform.position) || ZoneSystem.instance == null || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoBuildingFall))
            {
                return;
            }
            Vector3 a = transform.position, b = transform.TransformPoint(m_end);
            bool heldA = Held(a), heldB = Held(b);
            if (heldA && heldB)
            {
                m_misses = 0;
                return;
            }
            m_misses++;
            Plugin.Debug("Swag " + name + ": end " + (heldA ? "B" : "A") + " holds nothing (" + m_misses + ")");
            if (m_misses >= 2 && m_wear != null)
            {
                Plugin.Debug("Swag " + name + " falls: an end lost what it hung from.");
                m_wear.Remove();
            }
        }

        private static bool Held(Vector3 point)
        {
            if (Physics.OverlapSphereNonAlloc(point, 0.15f, s_hits, s_holdMask, QueryTriggerInteraction.Ignore) > 0)
            {
                return true;
            }
            return !ZNetScene.instance.IsAreaReady(point);
        }
    }

    // The two clicks and the wheel.
    [HarmonyPatch]
    internal static class SwagPlacing
    {
        private static bool s_hasFirst;
        private static Vector3 s_first;
        private static string s_firstPiece;
        private static float s_sag = 0.15f;
        private static float s_wheel;
        private static Vector3 s_end;
        private static float s_length;
        private const float SagStep = 0.02f, SagMax = 0.6f;

        internal static void Cancel(Player player, bool tell)
        {
            if (!s_hasFirst)
            {
                return;
            }
            s_hasFirst = false;
            if (tell && player != null)
            {
                player.Message(MessageHud.MessageType.Center, "$potatowright_swag_cancel");
            }
            Plugin.Debug("Swag: first end dropped");
        }

        private static Swag GhostSwag(Player player)
        {
            return (player != null && player.m_placementGhost != null) ? player.m_placementGhost.GetComponent<Swag>() : null;
        }

        // Second end: the game has put the ghost at the aim point (snapped); move it to the first end, facing the second,
        // draw the swag between them, and check the first end too (ward, no-build zones) and the length.
        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        [HarmonyPostfix]
        private static void UpdatePlacementGhost_Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer)
            {
                return;
            }
            Swag swag = GhostSwag(__instance);
            if (swag == null)
            {
                return;
            }
            GameObject ghost = __instance.m_placementGhost;
            if (!s_hasFirst || !ghost.activeSelf)
            {
                swag.Preview(new Vector3(0f, -0.3f, 0f), 0f);
                return;
            }
            Vector3 b = ghost.transform.position;
            Vector3 flat = b - s_first;
            flat.y = 0f;
            Quaternion rot = (flat.sqrMagnitude > 0.0001f) ? Quaternion.LookRotation(flat) : Quaternion.identity;
            ghost.transform.position = s_first;
            ghost.transform.rotation = rot;
            s_end = Quaternion.Inverse(rot) * (b - s_first);
            s_length = s_end.magnitude;
            swag.Preview(s_end, s_sag);
            if (__instance.m_placementStatus == Player.PlacementStatus.Valid)
            {
                if (Location.IsInsideNoBuildLocation(s_first))
                {
                    __instance.m_placementStatus = Player.PlacementStatus.NoBuildZone;
                }
                else if (!PrivateArea.CheckAccess(s_first, 0f, false, false))
                {
                    __instance.m_placementStatus = Player.PlacementStatus.PrivateZone;
                }
                else if (s_length > SwagPieces.MaxLength || s_length < SwagPieces.MinLength)
                {
                    __instance.m_placementStatus = Player.PlacementStatus.Invalid;
                }
                __instance.SetPlacementGhostValid(__instance.m_placementStatus == Player.PlacementStatus.Valid);
            }
            swag.SetInvalid(__instance.m_placementStatus != Player.PlacementStatus.Valid);
        }

        // The wheel sets the sag while the second end is being chosen; the first end is dropped when the build menu
        // opens, another piece is chosen, or the player walks far from it.
        [HarmonyPatch(typeof(Player), "UpdatePlacement")]
        [HarmonyPostfix]
        private static void UpdatePlacement_Postfix(Player __instance, bool takeInput)
        {
            if (__instance != Player.m_localPlayer || !s_hasFirst)
            {
                return;
            }
            Piece selected = __instance.InPlaceMode() ? __instance.GetSelectedPiece() : null;
            if (selected == null || selected.name != s_firstPiece || Hud.IsPieceSelectionVisible() || Vector3.Distance(__instance.transform.position, s_first) > 40f)
            {
                Cancel(__instance, true);
                return;
            }
            if (!takeInput)
            {
                return;
            }
            s_wheel += ZInput.GetMouseScrollWheel();
            int notches = 0;
            while (s_wheel > __instance.m_scrollAmountThreshold) { s_wheel -= __instance.m_scrollAmountThreshold; notches--; }
            while (s_wheel < -__instance.m_scrollAmountThreshold) { s_wheel += __instance.m_scrollAmountThreshold; notches++; }
            if (notches != 0)
            {
                s_wheel = 0f;
                s_sag = Mathf.Clamp(s_sag + notches * SagStep, 0f, SagMax);
                __instance.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$potatowright_swag_sag") + " " + Mathf.RoundToInt(s_sag * 100f) + "%  " + s_length.ToString("0.0") + " m");
            }
        }

        // First click: keep the first end, take nothing. Second click: let the game place the piece at the first end.
        [HarmonyPatch(typeof(Player), "TryPlacePiece")]
        [HarmonyPrefix]
        private static bool TryPlacePiece_Prefix(Player __instance, Piece piece, ref bool __result)
        {
            if (__instance != Player.m_localPlayer || piece == null || piece.GetComponent<Swag>() == null)
            {
                return true;
            }
            if (!s_hasFirst)
            {
                __instance.UpdatePlacementGhost(true);
                if (__instance.m_placementStatus != Player.PlacementStatus.Valid || __instance.m_placementGhost == null)
                {
                    return true;
                }
                s_hasFirst = true;
                s_first = __instance.m_placementGhost.transform.position;
                s_firstPiece = piece.name;
                s_wheel = 0f;
                __instance.Message(MessageHud.MessageType.Center, "$potatowright_swag_first");
                Plugin.Debug("Swag " + piece.name + ": first end at " + s_first);
                __result = false;
                return false;
            }
            __instance.UpdatePlacementGhost(true);
            if (s_length > SwagPieces.MaxLength)
            {
                __instance.Message(MessageHud.MessageType.Center, "$potatowright_swag_toolong");
                __result = false;
                return false;
            }
            if (s_length < SwagPieces.MinLength)
            {
                __instance.Message(MessageHud.MessageType.Center, "$potatowright_swag_tooshort");
                __result = false;
                return false;
            }
            Swag.s_pending = true;
            Swag.s_pendingEnd = s_end;
            Swag.s_pendingSag = s_sag;
            return true;
        }

        [HarmonyPatch(typeof(Player), "TryPlacePiece")]
        [HarmonyPostfix]
        private static void TryPlacePiece_Postfix(Player __instance, Piece piece, bool __result)
        {
            if (__instance != Player.m_localPlayer || piece == null || piece.GetComponent<Swag>() == null)
            {
                return;
            }
            if (__result)
            {
                s_hasFirst = false;
            }
            Swag.s_pending = false;
        }
    }
}
