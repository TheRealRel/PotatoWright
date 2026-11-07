// The scaffold: a 2 x 2 x 2 m wooden frame to build from (the user's choices, 2026-11-06: "Stackable frame", "Never falls,
// holds nothing"). One half is the game's wooden stepladder (its steps, colliders and climbing triggers, moved 0.5 m to
// the -x side), climbing from the front (+z) at the bottom to the back (-z) at the top; the other half is a plank deck at
// the top (the game's 2 x 2 wood floor, squeezed to 1 x 2 m). Four corner posts and three rails (the game's wood pole
// look). A full deck would leave no headroom over the ladder.
// - Snap points at its eight corners: frames stack and line up side by side.
// - No support or weather wear (m_noSupportWear and m_noRoofWear off, and Steady for SwagWear on ships). It falls when
//   what it stands on is removed (the user, test-c: "the scaffold should crash down when the bottom is removed"):
//   ScaffoldMark checks under its feet after a piece near it is destroyed.
// - Holds nothing: m_supports off, so nothing else rests on it (pieces built on it need their own support).
// Saved in worlds: never rename the prefab.
using System.Collections.Generic;
using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace PotatoWright
{
    internal static class ScaffoldPiece
    {
        internal const string Name = "PotatoWright_Scaffold";
        private const float Post = 0.14f, Rail = 0.1f, TopBeam = 0.08f;

        internal static void Create()
        {
            CustomPiece custom = new CustomPiece(Name, "wood_stepladder", new PieceConfig
            {
                Name = "$piece_potatowright_scaffold",
                Description = "$piece_potatowright_scaffold_description",
                PieceTable = "Hammer",
                Category = "Furniture",
                CraftingStation = "piece_workbench",
                Requirements = new[] { new RequirementConfig("Wood", 4, 0, true) },
            });
            GameObject prefab = custom.PiecePrefab;
            if (!prefab)
            {
                Debug.LogWarning(Plugin.Tag + " wood_stepladder not found; no scaffold.");
                return;
            }
            // The ladder takes the -x half; its own snap points go (the frame's corners replace them).
            int moved = 0;
            foreach (Transform child in prefab.transform.Cast<Transform>().ToList())
            {
                if (child.name.StartsWith("$hud_snappoint"))
                {
                    Object.DestroyImmediate(child.gameObject);
                    continue;
                }
                child.localPosition += new Vector3(-0.5f, 0f, 0f);
                moved++;
            }
            GameObject pole = PrefabManager.Instance.GetPrefab("wood_pole2");
            Transform poleNew = pole ? pole.transform.Find("New") : null;
            Mesh cube = poleNew ? poleNew.GetComponent<MeshFilter>()?.sharedMesh : null;
            Material wood = poleNew ? poleNew.GetComponent<MeshRenderer>()?.sharedMaterial : null;
            GameObject floor = PrefabManager.Instance.GetPrefab("wood_floor");
            Transform floorNew = floor ? floor.transform.Find("New/_Combined Mesh [high]") : null;
            Mesh deckMesh = floorNew ? floorNew.GetComponent<MeshFilter>()?.sharedMesh : null;
            Material deckMat = floorNew ? floorNew.GetComponent<MeshRenderer>()?.sharedMaterial : null;
            if (!cube || !wood || !deckMesh || !deckMat)
            {
                Debug.LogWarning(Plugin.Tag + " Scaffold parts missing: pole mesh " + (bool)cube + ", pole wood " + (bool)wood + ", deck mesh " + (bool)deckMesh + ", deck wood " + (bool)deckMat + ".");
            }
            int layer = prefab.layer;
            GameObject frame = new GameObject("frame");
            frame.transform.SetParent(prefab.transform, false);
            frame.layer = layer;
            List<Renderer> renderers = new List<Renderer>();
            // Posts (the pole's cube is long along its x, so turned upright). None at the ladder's foot (front, ladder side):
            // it stood in the corner of the bottom step (the user, test-a: "the first beam, on the bottom right of the
            // ladder"); the ladder's own side rail is that side's brace.
            float c = 1f - Post / 2f;
            foreach (Vector2 corner in new[] { new Vector2(c, c), new Vector2(c, -c), new Vector2(-c, -c) })
            {
                renderers.Add(Beam(frame, "post", cube, wood, new Vector3(corner.x, 1f, corner.y), Quaternion.Euler(0f, 0f, 90f), new Vector3(2f, Post, Post)));
            }
            // A post at the deck's front inner corner, beside the ladder's foot (outside its steps): it holds the inner
            // ends of the front rail and the front top beam, and the beam under the deck's inner edge.
            renderers.Add(Beam(frame, "post_middle", cube, wood, new Vector3(Post / 2f, 1f, c), Quaternion.Euler(0f, 0f, 90f), new Vector3(2f, Post, Post)));
            // Rails halfway up: the back and the deck side all along, the front under the deck only (the ladder's foot
            // stays open). None on the ladder side: the ladder's own sides are there.
            float r = 1f - Post / 2f;
            renderers.Add(Beam(frame, "rail_back", cube, wood, new Vector3(0f, 1f, -r), Quaternion.identity, new Vector3(2f, Rail, Rail)));
            renderers.Add(Beam(frame, "rail_side", cube, wood, new Vector3(r, 1f, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector3(2f, Rail, Rail)));
            renderers.Add(Beam(frame, "rail_front", cube, wood, new Vector3(0.5f, 1f, r), Quaternion.identity, new Vector3(1f, Rail, Rail)));
            // Top beams just under the deck (the user's choice, test-a: "Top beams all round"): the back (the ladder's top
            // step rests on it), the deck side, the front under the deck only (one over the ladder's foot would be in the
            // way of the climber's head), and one under the deck's inner edge. The ladder side has no front post to hold
            // a top beam; its ladder rail runs from the foot up to the back post.
            const float top = 2f - TopBeam;
            float e = 1f - TopBeam / 2f;
            renderers.Add(Beam(frame, "top_back", cube, wood, new Vector3(0f, top, -e), Quaternion.identity, new Vector3(2f, Rail, TopBeam)));
            renderers.Add(Beam(frame, "top_deck_side", cube, wood, new Vector3(e, top, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector3(2f, Rail, TopBeam)));
            renderers.Add(Beam(frame, "top_front", cube, wood, new Vector3(0.5f, top, e), Quaternion.identity, new Vector3(1f, Rail, TopBeam)));
            renderers.Add(Beam(frame, "top_middle", cube, wood, new Vector3(TopBeam / 2f, top, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector3(2f, Rail, TopBeam)));
            // The deck: the wood floor's planks squeezed to 1 m across, its top level with the ladder's top step (2.05 m).
            if (deckMesh)
            {
                Bounds b = deckMesh.bounds;
                GameObject deck = new GameObject("deck");
                deck.layer = layer;
                deck.transform.SetParent(frame.transform, false);
                deck.transform.localScale = new Vector3(0.5f, 1f, 1f);
                deck.transform.localPosition = new Vector3(0.5f - 0.5f * b.center.x, 2.05f - b.max.y, -b.center.z);
                deck.AddComponent<MeshFilter>().sharedMesh = deckMesh;
                MeshRenderer dr = deck.AddComponent<MeshRenderer>();
                dr.sharedMaterial = deckMat;
                renderers.Add(dr);
            }
            GameObject deckCollider = new GameObject("deck_collider");
            deckCollider.layer = layer;
            deckCollider.transform.SetParent(frame.transform, false);
            deckCollider.transform.localPosition = new Vector3(0.5f, 2f, 0f);
            deckCollider.AddComponent<BoxCollider>().size = new Vector3(1f, 0.1f, 2f);
            LODGroup lod = frame.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.015f, renderers.Where(x => x != null).ToArray()) });
            lod.RecalculateBounds();
            // Snap points at the eight corners.
            foreach (float y in new[] { 0f, 2f })
            {
                foreach (float x in new[] { -1f, 1f })
                {
                    foreach (float z in new[] { -1f, 1f })
                    {
                        GameObject snap = new GameObject("$hud_snappoint_corner");
                        snap.transform.SetParent(prefab.transform, false);
                        snap.transform.localPosition = new Vector3(x, y, z);
                        snap.tag = "snappoint";
                        snap.SetActive(false);
                    }
                }
            }
            WearNTear wear = prefab.GetComponent<WearNTear>();
            if (wear)
            {
                wear.m_noSupportWear = false;
                wear.m_noRoofWear = false;
                wear.m_supports = false;
            }
            prefab.AddComponent<Steady>();
            prefab.AddComponent<ScaffoldMark>();
            Piece piece = prefab.GetComponent<Piece>();
            if (piece) Plugin.Debug("Scaffold: the stepladder's noClipping was " + piece.m_noClipping + ", now off");
            if (piece) piece.m_noClipping = false;
            Sprite icon = PieceIcons.FromRenderers(Name, prefab, Quaternion.Euler(18f, 35f, 0f));
            if (icon && piece) piece.m_icon = icon;
            PieceManager.Instance.AddPiece(custom);
            Plugin.Debug("Scaffold piece " + Name + " (wood_stepladder copy, " + moved + " ladder parts moved, layer " + layer + ", icon " + (bool)icon + ", Wood x4 at the workbench)");
        }

        private static Renderer Beam(GameObject parent, string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            GameObject go = new GameObject(name);
            go.layer = parent.layer;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            go.AddComponent<BoxCollider>();
            if (!mesh) return null;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            if (material) r.sharedMaterial = material;
            return r;
        }
    }
}

namespace PotatoWright
{
    // On the scaffold: scaffolds can be placed while aiming at one (AimAtHolders), and a frame falls when what it stands
    // on goes (the user, test-c: "the scaffold should crash down when the bottom is removed").
    // Nothing runs while nothing changes: when any piece is destroyed (WearNTear.Destroy, on the client that destroys
    // it), the scaffolds standing just above it look under their feet a moment later (the destroyed piece's colliders
    // are gone by then). A frame with nothing under its feet (ground, rock, a building piece, a ship, another frame) is
    // removed the way the game breaks a piece (break effect, its wood dropped), which in turn makes the frames above it
    // look, so a stack comes down one frame after another.
    public sealed class ScaffoldMark : UnityEngine.MonoBehaviour
    {
        private static readonly System.Collections.Generic.List<ScaffoldMark> s_all = new System.Collections.Generic.List<ScaffoldMark>();
        private static readonly UnityEngine.Collider[] s_hits = new UnityEngine.Collider[32];
        private static int s_mask;
        private const float CheckDelay = 0.3f;
        private bool m_falling;

        private void OnEnable() => s_all.Add(this);

        private void OnDisable() => s_all.Remove(this);

        // A piece near was destroyed: the frames standing within reach above it look under their feet.
        internal static void PieceGone(WearNTear gone)
        {
            if (gone == null || s_all.Count == 0)
            {
                return;
            }
            UnityEngine.Vector3 at = gone.transform.position;
            int looking = 0;
            foreach (ScaffoldMark s in s_all)
            {
                if (s == null || s.gameObject == gone.gameObject || s.m_falling)
                {
                    continue;
                }
                UnityEngine.Vector3 d = s.transform.position - at;
                if (UnityEngine.Mathf.Abs(d.x) > 5f || UnityEngine.Mathf.Abs(d.z) > 5f || d.y < -1f || d.y > 6f)
                {
                    continue;
                }
                ZNetView nview = s.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid())
                {
                    continue;
                }
                s.CancelInvoke(nameof(CheckFeet));
                s.Invoke(nameof(CheckFeet), CheckDelay);
                looking++;
            }
            if (looking > 0)
            {
                Plugin.Debug("Scaffold: " + gone.name + " destroyed at " + at + "; " + looking + " frame(s) near look under their feet");
            }
        }

        private void CheckFeet()
        {
            ZNetView nview = GetComponent<ZNetView>();
            WearNTear wear = GetComponent<WearNTear>();
            if (m_falling || nview == null || !nview.IsValid() || wear == null)
            {
                return;
            }
            if (s_mask == 0)
            {
                // The game's support mask, plus vehicle so a frame on a ship's deck stands (as Swag's ends).
                s_mask = UnityEngine.LayerMask.GetMask("piece", "Default", "static_solid", "Default_small", "terrain", "vehicle");
            }
            // Its feet: the 2 x 2 m base, from 0.25 m below to 0.1 m above, kept 5 cm in from the edges so frames beside
            // it don't count.
            UnityEngine.Vector3 center = transform.TransformPoint(new UnityEngine.Vector3(0f, -0.075f, 0f));
            int n = UnityEngine.Physics.OverlapBoxNonAlloc(center, new UnityEngine.Vector3(0.95f, 0.175f, 0.95f), s_hits, transform.rotation, s_mask, UnityEngine.QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                UnityEngine.Collider c = s_hits[i];
                if (c != null && !c.transform.IsChildOf(transform))
                {
                    Plugin.Debug("Scaffold at " + transform.position + " stands on " + c.name + " (layer " + UnityEngine.LayerMask.LayerToName(c.gameObject.layer) + ")");
                    return;
                }
            }
            m_falling = true;
            Plugin.Debug("Scaffold at " + transform.position + ": nothing under its feet, falls");
            wear.Remove();
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(WearNTear), "Destroy")]
    internal static class ScaffoldFall
    {
        private static void Postfix(WearNTear __instance)
        {
            ScaffoldMark.PieceGone(__instance);
        }
    }
}
