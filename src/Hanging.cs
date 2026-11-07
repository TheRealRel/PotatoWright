// Things hung on a rope or chain (the user's choices, 2026-11-06): lanterns, drying fish and drying herbs hang where you
// aim on a placed rope or chain; pennant bunting and fey lights cover the whole rope or chain you aim at. Each one falls
// (drops its materials) when its rope or chain is gone, checked by the owner every few seconds like a swag's ends.
// - Lantern: the game's Dvergr lantern (the item's lantern body, lit material) on a short iron chain, with the game's
//   placed Dvergr lantern light and glow (piece_dvergr_lantern: point light with LightLod, so it switches off at a
//   distance and follows the Point lights setting like every lamp in the game).
// - Drying fish: the game's perch and pike models hung by the tail on cords; drying herbs: three thistles, heads down.
// - Pennant bunting: small triangle flags hanging from the rope, in the game's banner colours (plain) or cut from the
//   game's banner cloth (the banners' own pictures); the mouse wheel switches between the two while placing. Both use the
//   banners' cloth shader.
// - Fey lights: the game's Fey Lights wisps (piece_FairylightGarland's particle system) spread along the rope; they only
//   glow (the garland's light is left out, the user's choice "Lanterns light, strings glow").
// Built once (spot hangings in the prefab, strings from their ZDO), no physics, nothing updated per frame. Colliders on
// piece_nonsolid (aimed at and removed with the hammer, walked through).
// Saved in worlds: never rename the prefabs or ZDO keys.
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace PotatoWright
{
    internal static class HangPieces
    {
        internal const int Lantern = 0, Fish = 1, Herbs = 2, Bunting = 3, Fey = 4;

        // The game's banner colours, read from each banner's picture (main colour of its cloth): red, yellow, blue,
        // white, green, orange, purple, black (banners 04, 08, 02, 11, 05, 10, 09, 01).
        private static readonly Color32[] Palette =
        {
            new Color32(171, 12, 1, 255), new Color32(186, 146, 0, 255), new Color32(5, 68, 151, 255), new Color32(210, 210, 210, 255),
            new Color32(33, 112, 16, 255), new Color32(209, 110, 8, 255), new Color32(138, 67, 188, 255), new Color32(45, 45, 45, 255),
        };
        // Banner cloth order along the string (piece_bannerNN).
        private static readonly int[] ClothOrder = { 4, 8, 2, 11, 5, 10, 9, 1, 3, 6, 7 };

        internal static Material PlainFlags;
        internal static Material[] ClothFlags;
        internal static GameObject FeyTemplate;
        internal static int PaletteCount => Palette.Length;

        internal static void Create()
        {
            if (!GUIManager.IsHeadless())
            {
                Flags();
                GameObject garland = PrefabManager.Instance.GetPrefab("piece_FairylightGarland");
                Transform ps = garland ? garland.transform.Find("Particle System") : null;
                FeyTemplate = ps ? ps.gameObject : null;
                if (FeyTemplate)
                {
                    ParticleSystem p = FeyTemplate.GetComponent<ParticleSystem>();
                    if (p)
                    {
                        ParticleSystem.MainModule main = p.main;
                        ParticleSystem.ShapeModule shape = p.shape;
                        ParticleSystem.EmissionModule emission = p.emission;
                        Plugin.Debug("Fey template: max " + main.maxParticles + ", lifetime " + main.startLifetime.constantMin + "-" + main.startLifetime.constantMax + ", size " + main.startSize.constantMin + "-" + main.startSize.constantMax + ", space " + main.simulationSpace + ", culling " + main.cullingMode + ", rate " + emission.rateOverTime.constant + "/" + emission.rateOverTime.constantMax + " (" + emission.rateOverTime.mode + "), shape " + shape.shapeType + " scale " + shape.scale + " pos " + shape.position + ", renderer " + (p.GetComponent<ParticleSystemRenderer>()?.sharedMaterial?.name ?? "none"));
                    }
                }
                else
                {
                    Debug.LogWarning(Plugin.Tag + " piece_FairylightGarland's particles not found; fey lights won't show.");
                }
            }
            AddSpot("PotatoWright_HangLantern", "piece_potatowright_hang_lantern", Lantern, "forge", new[] { new RequirementConfig("Bronze", 1, 0, true), new RequirementConfig("Resin", 2, 0, true) });
            AddSpot("PotatoWright_HangFish", "piece_potatowright_hang_fish", Fish, "piece_workbench", new[] { new RequirementConfig("FishRaw", 2, 0, true) });
            AddSpot("PotatoWright_HangHerbs", "piece_potatowright_hang_herbs", Herbs, "piece_workbench", new[] { new RequirementConfig("Thistle", 1, 0, true), new RequirementConfig("Dandelion", 2, 0, true) });
            AddSpot("PotatoWright_HangBunting", "piece_potatowright_bunting", Bunting, "piece_workbench", new[] { new RequirementConfig("LeatherScraps", 2, 0, true), new RequirementConfig("Raspberry", 2, 0, true) });
            // Fey lights cost what the game's Fey Lights cost, at the same station.
            GameObject feyPiece = PrefabManager.Instance.GetPrefab("piece_FairylightGarland");
            Piece fp = feyPiece ? feyPiece.GetComponent<Piece>() : null;
            RequirementConfig[] feyCost = (fp != null && fp.m_resources != null && fp.m_resources.Length > 0)
                ? fp.m_resources.Where(r => r.m_resItem != null).Select(r => new RequirementConfig(r.m_resItem.name, r.m_amount, 0, true)).ToArray()
                : new[] { new RequirementConfig("Resin", 2, 0, true) };
            string feyStation = (fp != null && fp.m_craftingStation != null) ? fp.m_craftingStation.name : "piece_workbench";
            Plugin.Debug("Fey lights cost (the game's Fey Lights): " + string.Join(", ", feyCost.Select(r => r.Item + " x" + r.Amount)) + " at " + feyStation);
            AddSpot("PotatoWright_HangFey", "piece_potatowright_fey", Fey, feyStation, feyCost, fp != null ? fp.m_icon : null);
        }

        // The flag materials: copies of the banners' cloth material (Custom/Vegetation, two-sided), one with a palette
        // picture, and each banner's own.
        private static void Flags()
        {
            List<Material> cloth = new List<Material>();
            foreach (int n in ClothOrder)
            {
                GameObject banner = PrefabManager.Instance.GetPrefab("piece_banner" + n.ToString("00"));
                Material m = banner ? banner.transform.Find("default")?.GetComponent<MeshRenderer>()?.sharedMaterial : null;
                if (m) cloth.Add(m);
            }
            ClothFlags = cloth.ToArray();
            if (ClothFlags.Length == 0)
            {
                Debug.LogWarning(Plugin.Tag + " Banner cloth not found; bunting won't show.");
                return;
            }
            Texture2D palette = new Texture2D(Palette.Length, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "potatowright_flags_d" };
            for (int i = 0; i < Palette.Length; i++) palette.SetPixel(i, 0, Palette[i]);
            palette.Apply();
            PlainFlags = new Material(ClothFlags[0]) { name = "potatowright_flags_plain" };
            PlainFlags.SetTexture("_MainTex", palette);
            if (PlainFlags.HasProperty("_BumpMap")) PlainFlags.SetTexture("_BumpMap", null);
            Plugin.Debug("Bunting: " + ClothFlags.Length + " banner cloths (" + string.Join(", ", ClothFlags.Select(m => m.name)) + "), shader " + ClothFlags[0].shader.name);
        }

        private static void AddSpot(string name, string token, int kind, string station, RequirementConfig[] cost, Sprite borrowedIcon = null)
        {
            CustomPiece custom = new CustomPiece(name, "wood_pole", new PieceConfig
            {
                Name = "$" + token,
                Description = "$" + token + "_description",
                PieceTable = "Hammer",
                Category = "Furniture",
                CraftingStation = station,
                Requirements = cost,
            });
            GameObject prefab = custom.PiecePrefab;
            if (!prefab)
            {
                Debug.LogWarning(Plugin.Tag + " wood_pole not found; no " + name + ".");
                return;
            }
            foreach (Transform child in prefab.transform.Cast<Transform>().ToList())
            {
                Object.DestroyImmediate(child.gameObject);
            }
            foreach (Component c in prefab.GetComponents<Component>())
            {
                if (!(c is Transform) && !(c is ZNetView) && !(c is Piece) && !(c is WearNTear))
                {
                    Object.DestroyImmediate(c);
                }
            }
            Piece piece = prefab.GetComponent<Piece>();
            piece.m_canRotate = false;
            piece.m_clipEverything = true;
            piece.m_noClipping = false;
            piece.m_groundPiece = false;
            piece.m_groundOnly = false;
            WearNTear wear = prefab.GetComponent<WearNTear>();
            if (wear)
            {
                wear.m_noSupportWear = false;
                wear.m_noRoofWear = false;
                wear.m_supports = false;
                wear.m_new = null;
                wear.m_worn = null;
                wear.m_broken = null;
                wear.m_fragmentRoots = null;
                wear.m_autoCreateFragments = false;
                wear.m_materialType = kind == Lantern ? WearNTear.MaterialType.Iron : WearNTear.MaterialType.Wood;
            }
            prefab.AddComponent<Steady>();
            Hanging hanging = prefab.AddComponent<Hanging>();
            hanging.m_kind = kind;
            Sprite icon = borrowedIcon;
            if (!GUIManager.IsHeadless())
            {
                if (kind <= Herbs)
                {
                    GameObject visual = SpotVisual(prefab, kind);
                    if (visual) icon = PieceIcons.FromRenderers(name, visual, kind == Lantern ? Quaternion.Euler(10f, 30f, 0f) : Quaternion.Euler(10f, 60f, 0f)) ?? icon;
                }
                else if (kind == Bunting)
                {
                    icon = BuntingIcon(name) ?? icon;
                }
            }
            if (icon) piece.m_icon = icon;
            PieceManager.Instance.AddPiece(custom);
            Plugin.Debug("Hanging piece " + name + " (kind " + kind + ", " + string.Join(", ", cost.Select(r => r.Item + " x" + r.Amount)) + " at " + station + ", icon " + (bool)icon + ")");
        }

        // The model of a spot hanging, under "visual", hanging from the piece's origin (the point on the rope). Local z
        // runs along the rope, y up.
        private static GameObject SpotVisual(GameObject prefab, int kind)
        {
            GameObject visual = new GameObject("visual");
            visual.transform.SetParent(prefab.transform, false);
            List<Renderer> renderers = new List<Renderer>();
            Vector3 colliderCentre, colliderSize;
            if (kind == Lantern)
            {
                // A short iron chain, then the lantern (the Dvergr lantern item's body, its lit material), with the placed
                // Dvergr lantern's light and glow at the lantern's flame.
                const float chain = 0.16f, scale = 0.85f;
                if (SwagPieces.LinkMetal[1])
                {
                    renderers.Add(MeshPart(visual, "chain", Swag.LinkMesh(new List<Vector3> { Vector3.zero, new Vector3(0f, -chain, 0f) }, false), SwagPieces.LinkMetal[1], Vector3.zero, Quaternion.identity, Vector3.one));
                }
                GameObject item = PrefabManager.Instance.GetPrefab("Lantern");
                MeshFilter body = item ? item.transform.Find("default")?.GetComponent<MeshFilter>() : null;
                Material lit = PrefabManager.Cache.GetPrefab<Material>("DvergrTownLantern_mat");
                float top = body ? body.sharedMesh.bounds.max.y : 0.663f;
                float bottom = -chain - top * scale;
                if (body && lit)
                {
                    renderers.Add(MeshPart(visual, "lantern", body.sharedMesh, lit, new Vector3(0f, bottom, 0f), Quaternion.identity, Vector3.one * scale));
                }
                else
                {
                    Debug.LogWarning(Plugin.Tag + " Lantern model missing: body " + (bool)body + ", DvergrTownLantern_mat " + (bool)lit + ".");
                }
                GameObject source = PrefabManager.Instance.GetPrefab("piece_dvergr_lantern");
                Transform light = source ? source.transform.Find("New/Point Light") : null;
                Transform flare = source ? source.transform.Find("New/flare") : null;
                if (light)
                {
                    GameObject l = Object.Instantiate(light.gameObject, visual.transform, false);
                    l.name = "Point Light";
                    l.transform.localPosition = new Vector3(0f, bottom + 0.253f * scale, 0f);
                    Light lc = l.GetComponent<Light>();
                    Plugin.Debug("Lantern light: range " + (lc ? lc.range : 0f) + ", intensity " + (lc ? lc.intensity : 0f) + ", shadows " + (lc ? lc.shadows.ToString() : "-") + ", components " + string.Join(", ", l.GetComponents<Component>().Select(x => x.GetType().Name)));
                }
                else
                {
                    Debug.LogWarning(Plugin.Tag + " piece_dvergr_lantern's light not found; the lantern won't light.");
                }
                if (flare)
                {
                    GameObject f = Object.Instantiate(flare.gameObject, visual.transform, false);
                    f.name = "flare";
                    f.transform.localPosition = new Vector3(0f, bottom + 0.223f * scale, 0f);
                }
                colliderCentre = new Vector3(0f, (bottom + 0f) / 2f, 0f);
                colliderSize = new Vector3(0.35f, -bottom, 0.35f);
            }
            else if (kind == Fish)
            {
                // Perch, pike, perch on cords, hung by the tail: the fish model's head is +x, its back +z, its side y
                // (tail tip at x -0.4); turned head down, side facing out from the rope.
                GameObject perch = PrefabManager.Instance.GetPrefab("Fish1"), pike = PrefabManager.Instance.GetPrefab("Fish2");
                MeshRenderer perchR = perch ? perch.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.GetComponent<MeshFilter>()) : null;
                MeshRenderer pikeR = pike ? pike.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.GetComponent<MeshFilter>()) : null;
                const float cord = 0.08f;
                float[] along = { -0.2f, 0f, 0.2f };
                for (int i = 0; i < 3; i++)
                {
                    MeshRenderer src = (i == 1) ? pikeR : perchR;
                    Vector3 s = (i == 1) ? new Vector3(0.78f, 0.62f, 0.62f) : new Vector3(0.5f, 0.5f, 0.5f);
                    float z = along[i];
                    if (SwagPieces.Rope)
                    {
                        renderers.Add(MeshPart(visual, "cord", Swag.Tube(new List<Vector3> { new Vector3(0f, 0.01f, z), new Vector3(0f, -cord - 0.02f, z) }, 6, false, 0.007f), SwagPieces.Rope, Vector3.zero, Quaternion.identity, Vector3.one));
                    }
                    if (src)
                    {
                        Mesh m = src.GetComponent<MeshFilter>().sharedMesh;
                        renderers.Add(MeshPart(visual, i == 1 ? "pike" : "perch", m, src.sharedMaterial, new Vector3(-0.047f * s.y, -cord - 0.4f * s.x, z), Quaternion.Euler(0f, 0f, -90f), s));
                    }
                }
                if (!perchR || !pikeR) Debug.LogWarning(Plugin.Tag + " Fish models missing: perch " + (bool)perchR + ", pike " + (bool)pikeR + ".");
                colliderCentre = new Vector3(0f, -0.4f, 0f);
                colliderSize = new Vector3(0.15f, 0.65f, 0.6f);
            }
            else
            {
                // Three thistles tied at the stems, heads down, fanned a little.
                GameObject thistle = PrefabManager.Instance.GetPrefab("Thistle");
                MeshRenderer src = thistle ? thistle.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.GetComponent<MeshFilter>()) : null;
                const float cord = 0.05f, scale = 0.14f;
                if (SwagPieces.Rope)
                {
                    renderers.Add(MeshPart(visual, "cord", Swag.Tube(new List<Vector3> { new Vector3(0f, 0.01f, 0f), new Vector3(0f, -cord - 0.03f, 0f) }, 6, false, 0.008f), SwagPieces.Rope, Vector3.zero, Quaternion.identity, Vector3.one));
                }
                if (src)
                {
                    Mesh m = src.GetComponent<MeshFilter>().sharedMesh;
                    for (int i = 0; i < 3; i++)
                    {
                        Quaternion q = Quaternion.AngleAxis(i * 120f + 20f, Vector3.up) * Quaternion.AngleAxis(12f, Vector3.right) * Quaternion.Euler(0f, 0f, 180f);
                        renderers.Add(MeshPart(visual, "thistle", m, src.sharedMaterial, new Vector3(0f, -cord, 0f), q, Vector3.one * scale));
                    }
                }
                else
                {
                    Debug.LogWarning(Plugin.Tag + " Thistle model missing.");
                }
                colliderCentre = new Vector3(0f, -0.28f, 0f);
                colliderSize = new Vector3(0.3f, 0.5f, 0.3f);
            }
            renderers.RemoveAll(r => r == null);
            LODGroup lod = visual.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.008f, renderers.ToArray()) });
            lod.RecalculateBounds();
            GameObject box = new GameObject("collider");
            box.layer = LayerMask.NameToLayer("piece_nonsolid");
            box.transform.SetParent(prefab.transform, false);
            box.transform.localPosition = colliderCentre;
            box.AddComponent<BoxCollider>().size = colliderSize;
            return visual;
        }

        internal static Renderer MeshPart(GameObject parent, string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (!mesh) return null;
            GameObject go = new GameObject(name);
            go.layer = parent.layer;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            if (material) r.sharedMaterial = material;
            return r;
        }

        // Three plain flags on a short rope.
        private static Sprite BuntingIcon(string name)
        {
            if (!PlainFlags) return null;
            GameObject go = new GameObject(name + "_icon");
            try
            {
                List<Vector3> curve = Swag.Points(new Vector3(0f, 0f, -0.5f), new Vector3(0f, 0f, 0.5f), Vector3.down, 0.12f, 24);
                MeshPart(go, "flags", BuntingMesh(curve, 0, out _), PlainFlags, Vector3.zero, Quaternion.identity, Vector3.one);
                if (SwagPieces.Rope) MeshPart(go, "rope", Swag.Tube(Swag.Resample(curve, 0.02f), 8, false), SwagPieces.Rope, Vector3.zero, Quaternion.identity, Vector3.one);
                return PieceIcons.Draw(name, go, Quaternion.Euler(0f, 90f, 0f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        internal const float FlagSpacing = 0.34f, FlagHalf = 0.12f, FlagLength = 0.3f;

        // Triangle flags along the curve: the top edge on the rope, the tip hanging straight down. Plain style: one
        // submesh, each flag one palette colour. Cloth style: one submesh per banner; the flag shows the top half of
        // that banner's picture, edge to middle to edge (the banner picture holds half the banner, mirrored at the
        // middle: u 0.057 at the edge to 0.246 at the middle, v 0.968 at the top).
        internal static Mesh BuntingMesh(List<Vector3> curve, int style, out int flags)
        {
            List<Vector3> fine = Swag.Resample(curve, 0.01f);
            float total = 0f;
            for (int i = 1; i < fine.Count; i++) total += Vector3.Distance(fine[i - 1], fine[i]);
            float step = total / (fine.Count - 1);
            flags = Mathf.Max(1, Mathf.FloorToInt((total - 2f * FlagHalf) / FlagSpacing) + 1);
            float start = (total - (flags - 1) * FlagSpacing) / 2f;
            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            int subs = style == 0 ? 1 : Mathf.Max(1, ClothFlags != null ? ClothFlags.Length : 1);
            List<int>[] tris = new List<int>[subs];
            for (int k = 0; k < subs; k++) tris[k] = new List<int>();
            for (int f = 0; f < flags; f++)
            {
                float s = start + f * FlagSpacing;
                Vector3 a = At(fine, step, s - FlagHalf), m = At(fine, step, s), b = At(fine, step, s + FlagHalf);
                Vector3 tip = m + Vector3.down * FlagLength;
                Vector3 normal = Vector3.Cross(b - a, Vector3.down).normalized;
                int o = v.Count;
                v.Add(a); v.Add(m); v.Add(b); v.Add(tip);
                for (int k = 0; k < 4; k++) n.Add(normal);
                if (style == 0)
                {
                    float u = (f % Palette.Length + 0.5f) / Palette.Length;
                    for (int k = 0; k < 4; k++) uv.Add(new Vector2(u, 0.5f));
                }
                else
                {
                    uv.Add(new Vector2(0.057f, 0.968f)); uv.Add(new Vector2(0.246f, 0.968f)); uv.Add(new Vector2(0.057f, 0.968f)); uv.Add(new Vector2(0.246f, 0.502f));
                }
                List<int> t = tris[style == 0 ? 0 : f % subs];
                t.Add(o); t.Add(o + 1); t.Add(o + 3);
                t.Add(o + 1); t.Add(o + 2); t.Add(o + 3);
            }
            Mesh mesh = new Mesh { name = "potatowright_bunting" };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = subs;
            for (int k = 0; k < subs; k++) mesh.SetTriangles(tris[k], k);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static Vector3 At(List<Vector3> fine, float step, float s)
        {
            float x = Mathf.Clamp(s / step, 0f, fine.Count - 1);
            int i = Mathf.Min(Mathf.FloorToInt(x), fine.Count - 2);
            return Vector3.Lerp(fine[i], fine[i + 1], x - i);
        }
    }

    // On every hanging piece.
    public sealed class Hanging : MonoBehaviour, IPlaced
    {
        public int m_kind;

        internal bool IsString => m_kind >= HangPieces.Bunting;

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
            List<Vector3> curve = Swag.Points(Vector3.zero, end, Vector3.down, sag, Mathf.Clamp(Mathf.CeilToInt(end.magnitude * 8f), 16, 160));
            Renderer shown = null;
            if (!GUIManager.IsHeadless())
            {
                if (m_kind == HangPieces.Bunting)
                {
                    Material[] mats = style == 0 ? new[] { HangPieces.PlainFlags } : HangPieces.ClothFlags;
                    if (mats != null && mats.Length > 0 && mats[0] != null)
                    {
                        Mesh mesh = HangPieces.BuntingMesh(curve, style, out int flags);
                        MeshRenderer r = (MeshRenderer)HangPieces.MeshPart(m_visual, "flags", mesh, null, Vector3.zero, Quaternion.identity, Vector3.one);
                        r.sharedMaterials = mats;
                        shown = r;
                        if (placed) Plugin.Debug("Bunting " + name + ": " + flags + " flags, style " + (style == 0 ? "plain" : "banner cloth"));
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
        private static int s_style;
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
                List<Vector3> curve = Swag.Points(Vector3.zero, swag.End, Vector3.down, swag.Sag, Mathf.Clamp(Mathf.CeilToInt(swag.End.magnitude * 20f), 16, 320));
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
                ghost.transform.rotation = forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward, up) : st.rotation;
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

        // Bunting: the mouse wheel switches plain colours and banner cloth.
        [HarmonyPatch(typeof(Player), "UpdatePlacement")]
        [HarmonyPostfix]
        private static void UpdatePlacement_Postfix(Player __instance, bool takeInput)
        {
            if (__instance != Player.m_localPlayer || !takeInput) return;
            Hanging hanging = GhostHanging(__instance);
            if (hanging == null || hanging.m_kind != HangPieces.Bunting || Hud.IsPieceSelectionVisible()) return;
            s_wheel += ZInput.GetMouseScrollWheel();
            if (Mathf.Abs(s_wheel) < __instance.m_scrollAmountThreshold) return;
            s_wheel = 0f;
            s_style = 1 - s_style;
            __instance.Message(MessageHud.MessageType.Center, s_style == 0 ? "$potatowright_flags_plain" : "$potatowright_flags_cloth");
            Plugin.Debug("Bunting style " + (s_style == 0 ? "plain" : "banner cloth"));
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
