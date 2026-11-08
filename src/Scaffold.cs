// The scaffold: a 2 x 2 x 2 m wooden frame to build from (the user's choices, 2026-11-06: "Stackable frame", "Never falls,
// holds nothing"). One half is the game's wooden stepladder (its steps, colliders and climbing triggers, moved 0.5 m to
// the -x side), climbing from the front (+z) at the bottom to the back (-z) at the top; the other half is a plank deck at
// the top (the game's 2 x 2 wood floor, squeezed to 1 x 2 m). Four corner posts and three rails (the game's wood pole
// look). A full deck would leave no headroom over the ladder.
// - Snap points at its eight corners: frames stack and line up side by side, by the game's own snapping (and the
//   player's own snap point choice). The piece's origin is the middle of the deck (ScaffoldPiece.Origin), so aiming at
//   a deck stacks the next frame straight on it. (test-g's own placing of a frame aimed at a frame is gone: it fought
//   the game's snapping; the user: "snap points were broken".)
// - Its shape follows the frames around it (ScaffoldMark): one post where frames meet, open sides between frames, a
//   guard rail on the top frame only.
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

        // The posts' spots (x, z): the deck's front and back outer corners, the ladder side's back corner (none at the
        // ladder's foot: it stood in the corner of the bottom step; the user, test-a: "the first beam, on the bottom right
        // of the ladder"; the ladder's own side rail is that side's brace), and the deck's front inner corner beside the
        // ladder's foot (outside its steps), which holds the inner ends of the front rail, the front top beam and the beam
        // under the deck's inner edge. Part names post_<i> and guard_post_<i> follow this order (ScaffoldMark).
        // Where the frame's own middle (x and z) sits from the piece's origin, which is the middle of the deck.
        internal static readonly Vector3 Origin = new Vector3(-0.5f, 0f, 0f);

        internal static readonly Vector2[] PostSpots =
        {
            new Vector2(1f - Post / 2f, 1f - Post / 2f), new Vector2(1f - Post / 2f, -(1f - Post / 2f)),
            new Vector2(-(1f - Post / 2f), -(1f - Post / 2f)), new Vector2(Post / 2f, 1f - Post / 2f),
        };

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
            // Posts on PostSpots (the pole's cube is long along its x, so turned upright).
            for (int i = 0; i < PostSpots.Length; i++)
            {
                renderers.Add(Beam(frame, "post_" + i, cube, wood, new Vector3(PostSpots[i].x, 1f, PostSpots[i].y), Quaternion.Euler(0f, 0f, 90f), new Vector3(2f, Post, Post)));
            }
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
            // The guard rail on the top frame of a stack (the user, test-e: "Guard rail on top"): short posts on the four
            // post spots from the deck up, and rails 1 m above the deck at the back (all along), the deck side and the
            // front (deck half). None on the ladder side or the deck's inner edge: the climber steps off the ladder there.
            // ScaffoldMark hides it when a frame stands on top (that frame's own rails are at the same height).
            const float guardBottom = 2.05f, guardTop = 3.05f, guardRail = 3f;
            for (int i = 0; i < PostSpots.Length; i++)
            {
                renderers.Add(Beam(frame, "guard_post_" + i, cube, wood, new Vector3(PostSpots[i].x, (guardBottom + guardTop) / 2f, PostSpots[i].y), Quaternion.Euler(0f, 0f, 90f), new Vector3(guardTop - guardBottom, Post, Post)));
            }
            renderers.Add(Beam(frame, "guard_back", cube, wood, new Vector3(0f, guardRail, -r), Quaternion.identity, new Vector3(2f, Rail, Rail)));
            renderers.Add(Beam(frame, "guard_side", cube, wood, new Vector3(r, guardRail, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector3(2f, Rail, Rail)));
            renderers.Add(Beam(frame, "guard_front", cube, wood, new Vector3(0.5f, guardRail, r), Quaternion.identity, new Vector3(1f, Rail, Rail)));
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
            // The piece's origin at the middle of the deck, not of the frame (the user, test-f and test-g: stacking was
            // frustrating). The game snaps a piece only when its own snap point comes within 0.5 m of another's
            // (Player.FindClosestSnapPoints), and the ghost's origin sits where the player aims; a frame lines up on top
            // of another only when the aim is within 0.5 m of that frame's origin raised 2 m. With the origin at the
            // frame's middle that was the deck's inner edge over the ladder; at the deck's middle, aiming anywhere on the
            // deck stacks it straight. Everything (ladder, frame, snap points) moves by Origin.
            foreach (Transform child in prefab.transform)
            {
                child.localPosition += Origin;
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
    // On the scaffold: scaffolds can be placed while aiming at one (AimAtHolders); a frame takes its shape from the frames
    // around it; and a frame falls when what it stands on goes.
    // Shape (the user, test-e: "No doubled posts", "Open between frames", "Guard rail on top"), worked out by every
    // client for itself from where the frames stand (nothing sent or saved), when a frame is loaded or placed and when
    // one near it goes:
    // - Where frames meet, a post spot with another frame's post beside it (within 0.25 m, same level) shows only the
    //   post of the frame with the lowest ZDOID, so the same one shows for everyone.
    // - A side with a frame beside it (same level, turned by a multiple of 90 degrees) loses its rail halfway up and its
    //   guard rail, so the frames open into each other.
    // - Only a frame with no frame on top of it and none a level up beside it shows the guard rail.
    // - A side with a frame one level up or down beside it (a stair of frames) opens the guard rail or the rail halfway
    //   up there.
    // - A leg with nothing under its foot (within 1 m) is hidden with the rails halfway up that it holds (the user,
    //   test-i: "Hide them"), and a guard post shows only while a guard rail it holds shows. Frames near a building piece
    //   that loads or is placed look at their legs again (WearNTear.Awake), and so do frames near one that is destroyed.
    // - Every third step up a stair (counted from the frame on the ground; Step) is a leg frame: its legs over nothing
    //   reach down as plain posts to whatever is below (up to 30 m), and hold it up when the frames under it go. Its
    //   step is saved in its ZDO (PotatoWright_scaffoldStep), so it goes on counting as that step with no frame under it.
    // Parts are hidden by switching their objects off (VPO 2.0's indirect renderer drops a renderer on OnDisable).
    // Falling (the user, test-c: "the scaffold should crash down when the bottom is removed"): nothing runs while nothing
    // changes. When any piece is destroyed (WearNTear.Destroy, on the client that destroys it), the scaffolds standing just
    // above it look under their feet a moment later (the destroyed piece's colliders are gone by then). A frame with
    // nothing under its feet (ground, rock, a building piece, a ship, another frame) is removed the way the game breaks a
    // piece (break effect, its wood dropped), which in turn makes the frames above it look, so a stack comes down one
    // frame after another.
    public sealed class ScaffoldMark : UnityEngine.MonoBehaviour
    {
        private static readonly System.Collections.Generic.List<ScaffoldMark> s_all = new System.Collections.Generic.List<ScaffoldMark>();
        private static readonly UnityEngine.Collider[] s_hits = new UnityEngine.Collider[32];
        private static readonly UnityEngine.RaycastHit[] s_casts = new UnityEngine.RaycastHit[32];
        private static int s_mask;
        private const float CheckDelay = 0.3f, Level = 0.2f, Beside = 0.25f;
        // A leg shows when something lies under its foot within this reach (the user, test-i: legs that stand on nothing,
        // like the far legs of a frame stepped up a stair, are hidden). A frame placed on sloping ground keeps the leg
        // on the downhill side as long as the ground is this close.
        private const float LegReach = 1f;

        // The game's support mask, plus vehicle so a frame on a ship's deck stands (as Swag's ends).
        private static int Mask
        {
            get
            {
                if (s_mask == 0)
                {
                    s_mask = UnityEngine.LayerMask.GetMask("piece", "Default", "static_solid", "Default_small", "terrain", "vehicle");
                }
                return s_mask;
            }
        }
        private bool m_falling, m_listed, m_hasLong;
        private int m_step;
        private ZNetView m_nview;
        private UnityEngine.GameObject[] m_long;
        private float[] m_longLength;
        private UnityEngine.Renderer[] m_lodBase;
        private float m_lodHeight;
        // Long legs (the user, test-j: "every third scaffold placed reached down for leg placement"; chose every third
        // step up, counted from the frame on the ground, plain posts, and the legs hold the frame up): how far down they
        // may reach, and the step a leg frame keeps in its ZDO, so it stays a leg frame when the frames under it go.
        private const float LongReach = 30f;
        private static readonly int s_stepKey = "PotatoWright_scaffoldStep".GetStableHashCode();
        private static readonly float[] s_underX = { 0f, 2f, -2f, 0f, 0f }, s_underZ = { 0f, 0f, 0f, 2f, -2f };
        private UnityEngine.GameObject[] m_posts, m_guardPosts;
        private UnityEngine.GameObject m_railBack, m_railSide, m_railFront, m_guardBack, m_guardSide, m_guardFront;

        private void Start()
        {
            m_nview = GetComponent<ZNetView>();
            if (m_nview == null || !m_nview.IsValid())
            {
                return; // the placing preview
            }
            int n = ScaffoldPiece.PostSpots.Length;
            m_posts = new UnityEngine.GameObject[n];
            m_guardPosts = new UnityEngine.GameObject[n];
            m_long = new UnityEngine.GameObject[n];
            m_longLength = new float[n];
            for (int i = 0; i < n; i++)
            {
                m_posts[i] = Part("post_" + i);
                m_guardPosts[i] = Part("guard_post_" + i);
            }
            m_railBack = Part("rail_back");
            m_railSide = Part("rail_side");
            m_railFront = Part("rail_front");
            m_guardBack = Part("guard_back");
            m_guardSide = Part("guard_side");
            m_guardFront = Part("guard_front");
            s_all.Add(this);
            m_listed = true;
            ReshapeNear(this, true);
        }

        private void OnDestroy()
        {
            if (!m_listed)
            {
                return;
            }
            s_all.Remove(this);
            m_listed = false;
            ReshapeNear(this, false);
        }

        // Build On Ship attaches a frame to its ship once the ship has loaded (it can load after the frame): take the shape
        // again then, so its legs go by the ship (OnSameShip).
        private void OnTransformParentChanged()
        {
            if (Live)
            {
                CancelInvoke(nameof(Reshape));
                Invoke(nameof(Reshape), CheckDelay);
            }
        }

        private UnityEngine.GameObject Part(string name)
        {
            UnityEngine.Transform t = transform.Find("frame/" + name);
            return t ? t.gameObject : null;
        }

        private bool Live => m_listed && this != null && m_nview != null && m_nview.IsValid();

        // The frame's own middle at its foot (the piece's origin is the middle of the deck: ScaffoldPiece.Origin).
        private UnityEngine.Vector3 Middle => transform.TransformPoint(ScaffoldPiece.Origin);

        // The frames within reach of one that came or went take their shape again (and it too, when it came).
        private static void ReshapeNear(ScaffoldMark changed, bool self)
        {
            UnityEngine.Vector3 at = changed.Middle;
            foreach (ScaffoldMark s in s_all)
            {
                if (s == null || (!self && s == changed) || !s.Live)
                {
                    continue;
                }
                UnityEngine.Vector3 d = s.Middle - at;
                if (UnityEngine.Mathf.Abs(d.x) < 3f && UnityEngine.Mathf.Abs(d.z) < 3f && UnityEngine.Mathf.Abs(d.y) < 2.5f)
                {
                    s.Reshape();
                }
            }
        }

        // Where another frame stands, in this frame's own axes.
        private UnityEngine.Vector3 Local(ScaffoldMark other) => transform.InverseTransformDirection(other.Middle - Middle);

        // Turned by a multiple of 90 degrees from this frame.
        private bool Square(ScaffoldMark other)
        {
            float a = UnityEngine.Mathf.Repeat(other.transform.eulerAngles.y - transform.eulerAngles.y + 45f, 90f) - 45f;
            return UnityEngine.Mathf.Abs(a) < 5f;
        }

        private bool HasFrameOnTop()
        {
            foreach (ScaffoldMark o in s_all)
            {
                if (o == this || o == null || !o.Live) continue;
                UnityEngine.Vector3 d = Local(o);
                if (UnityEngine.Mathf.Abs(d.x) < Level && UnityEngine.Mathf.Abs(d.z) < Level && UnityEngine.Mathf.Abs(d.y - 2f) < Level) return true;
            }
            return false;
        }

        // A frame beside this one on that side (x, z between the frames' middles), its foot y above this one's (0 the same
        // level, 2 one level up, -2 one level down), turned by a multiple of 90 degrees.
        private bool FrameBeside(float x, float z, float y)
        {
            foreach (ScaffoldMark o in s_all)
            {
                if (o == this || o == null || !o.Live || !Square(o)) continue;
                UnityEngine.Vector3 d = Local(o);
                if (UnityEngine.Mathf.Abs(d.y - y) < Level && UnityEngine.Mathf.Abs(d.x - x) < Level && UnityEngine.Mathf.Abs(d.z - z) < Level) return true;
            }
            return false;
        }

        // A frame one level up has a post beside this one's post i (a guard post there would stand doubled beside it).
        private bool PostAbove(int i)
        {
            UnityEngine.Vector3 mine = Spot(i);
            foreach (ScaffoldMark o in s_all)
            {
                if (o == this || o == null || !o.Live) continue;
                UnityEngine.Vector3 apart = o.Middle - Middle;
                if (UnityEngine.Mathf.Abs(apart.y - 2f) > Level || UnityEngine.Mathf.Abs(apart.x) > 3f || UnityEngine.Mathf.Abs(apart.z) > 3f) continue;
                for (int j = 0; j < ScaffoldPiece.PostSpots.Length; j++)
                {
                    UnityEngine.Vector3 d = o.Spot(j) - mine;
                    d.y = 0f;
                    if (d.magnitude < Beside) return true;
                }
            }
            return false;
        }

        private UnityEngine.Vector3 Spot(int i) => transform.TransformPoint(ScaffoldPiece.Origin + new UnityEngine.Vector3(ScaffoldPiece.PostSpots[i].x, 0f, ScaffoldPiece.PostSpots[i].y));

        // Something under the foot of post i: a thin box a little wider than the post (so the edge beam of a frame below,
        // which the post stands just beside, counts) swept down from just above the foot to LegReach below it. Anything
        // the sweep meets counts; of what the box already touches before it moves, only things other than scaffolds
        // count (ground or a rock around a leg that sinks into a slope), since other frames' posts and rails there stand
        // beside the leg, not under it. The frame's own parts are skipped.
        private bool StandsOnSomething(int i, out string on)
        {
            UnityEngine.Vector3 up = transform.up;
            UnityEngine.Vector3 start = Spot(i) + up * 0.15f;
            int n = UnityEngine.Physics.BoxCastNonAlloc(start, new UnityEngine.Vector3(0.17f, 0.05f, 0.17f), -up, s_casts, transform.rotation, 0.1f + LegReach, Mask, UnityEngine.QueryTriggerInteraction.Ignore);
            Ship ship = GetComponentInParent<Ship>();
            for (int k = 0; k < n; k++)
            {
                UnityEngine.Collider c = s_casts[k].collider;
                if (c == null || c.transform.IsChildOf(transform) || !OnSameShip(c, ship))
                {
                    continue;
                }
                if (s_casts[k].distance <= 0f && c.GetComponentInParent<ScaffoldMark>() != null)
                {
                    continue;
                }
                on = c.name;
                return true;
            }
            on = null;
            return false;
        }

        // Which step up this frame is: 1 for a frame with no frame under it, otherwise one more than the lowest frame it
        // rests on (one level down, under it or beside it, turned by a multiple of 90 degrees). A frame with no frame
        // under it that was a leg frame keeps the step it saved (KeepStep), so it goes on standing on its long legs.
        private int Step(System.Collections.Generic.Dictionary<ScaffoldMark, int> memo)
        {
            if (memo.TryGetValue(this, out int known))
            {
                return known;
            }
            memo[this] = 1;
            int best = 0;
            foreach (ScaffoldMark o in s_all)
            {
                if (o == this || o == null || !o.Live || !Square(o)) continue;
                UnityEngine.Vector3 d = Local(o);
                if (UnityEngine.Mathf.Abs(d.y + 2f) >= Level) continue;
                bool under = false;
                for (int k = 0; k < s_underX.Length; k++)
                {
                    if (UnityEngine.Mathf.Abs(d.x - s_underX[k]) < Level && UnityEngine.Mathf.Abs(d.z - s_underZ[k]) < Level) under = true;
                }
                if (!under) continue;
                int s = o.Step(memo) + 1;
                if (best == 0 || s < best) best = s;
            }
            if (best == 0)
            {
                int kept = m_nview.GetZDO().GetInt(s_stepKey, 0);
                best = kept > 0 ? kept : 1;
            }
            memo[this] = best;
            return best;
        }

        // A frame on a ship (Build On Ship parents it to the ship) moves with the ship, so only the ship and what is built on
        // it can be under its legs (test-m: a leg frame on a longship reached 8 m down to the seabed). On land, anything.
        private static bool OnSameShip(UnityEngine.Collider c, Ship ship)
        {
            return ship == null || c.GetComponentInParent<Ship>() == ship;
        }

        // How far below the foot of post i the nearest thing is (the same box as StandsOnSomething, swept down to
        // LongReach; what it touches before it moves stands beside the leg, not under it).
        private bool LongLeg(int i, out float length, out string on)
        {
            UnityEngine.Vector3 up = transform.up;
            UnityEngine.Vector3 start = Spot(i) + up * 0.15f;
            int n = UnityEngine.Physics.BoxCastNonAlloc(start, new UnityEngine.Vector3(0.17f, 0.05f, 0.17f), -up, s_casts, transform.rotation, 0.1f + LongReach, Mask, UnityEngine.QueryTriggerInteraction.Ignore);
            float best = -1f;
            on = null;
            Ship ship = GetComponentInParent<Ship>();
            for (int k = 0; k < n; k++)
            {
                UnityEngine.Collider c = s_casts[k].collider;
                if (c == null || c.transform.IsChildOf(transform) || s_casts[k].distance <= 0f || !OnSameShip(c, ship))
                {
                    continue;
                }
                if (best < 0f || s_casts[k].distance < best)
                {
                    best = s_casts[k].distance;
                    on = c.name;
                }
            }
            length = UnityEngine.Mathf.Max(0f, best - 0.1f);
            return best >= 0f;
        }

        // The long part of leg i, from its foot down (0: none). Built of 2 m posts like the frame's own (the last one
        // shorter), with one collider the whole length, under a switched-off object and then switched on (VPO 2.0's
        // indirect renderer reads a renderer when it is switched on); the frame's LOD group takes its renderers.
        private void SetLong(int i, float length)
        {
            if (length <= 0f)
            {
                Show(m_long[i], false);
                return;
            }
            if (m_long[i] == null || UnityEngine.Mathf.Abs(m_longLength[i] - length) > 0.05f)
            {
                UnityEngine.Transform frame = transform.Find("frame");
                UnityEngine.MeshFilter postMesh = m_posts[i] ? m_posts[i].GetComponent<UnityEngine.MeshFilter>() : null;
                UnityEngine.MeshRenderer postRenderer = m_posts[i] ? m_posts[i].GetComponent<UnityEngine.MeshRenderer>() : null;
                if (frame == null || postMesh == null || postRenderer == null)
                {
                    return;
                }
                if (m_long[i] != null)
                {
                    UnityEngine.Object.Destroy(m_long[i]);
                }
                const float post = 0.14f;
                UnityEngine.Vector2 spot = ScaffoldPiece.PostSpots[i];
                UnityEngine.GameObject leg = new UnityEngine.GameObject("long_leg_" + i);
                leg.SetActive(false);
                leg.layer = frame.gameObject.layer;
                leg.transform.SetParent(frame, false);
                leg.transform.localPosition = new UnityEngine.Vector3(spot.x, -length / 2f, spot.y);
                leg.AddComponent<UnityEngine.BoxCollider>().size = new UnityEngine.Vector3(post, length, post);
                int pieces = UnityEngine.Mathf.CeilToInt(length / 2f - 0.001f);
                for (int k = 0; k < pieces; k++)
                {
                    float piece = UnityEngine.Mathf.Min(2f, length - k * 2f);
                    UnityEngine.GameObject part = new UnityEngine.GameObject("piece_" + k);
                    part.layer = leg.layer;
                    part.transform.SetParent(leg.transform, false);
                    part.transform.localPosition = new UnityEngine.Vector3(0f, length / 2f - k * 2f - piece / 2f, 0f);
                    part.transform.localRotation = UnityEngine.Quaternion.Euler(0f, 0f, 90f);
                    part.transform.localScale = new UnityEngine.Vector3(piece, post, post);
                    part.AddComponent<UnityEngine.MeshFilter>().sharedMesh = postMesh.sharedMesh;
                    part.AddComponent<UnityEngine.MeshRenderer>().sharedMaterial = postRenderer.sharedMaterial;
                }
                m_long[i] = leg;
                m_longLength[i] = length;
                UpdateLod(frame);
            }
            Show(m_long[i], true);
        }

        private void UpdateLod(UnityEngine.Transform frame)
        {
            UnityEngine.LODGroup lod = frame.GetComponent<UnityEngine.LODGroup>();
            if (lod == null)
            {
                return;
            }
            if (m_lodBase == null)
            {
                UnityEngine.LOD[] lods = lod.GetLODs();
                if (lods.Length == 0) return;
                m_lodBase = lods[0].renderers;
                m_lodHeight = lods[0].screenRelativeTransitionHeight;
            }
            System.Collections.Generic.List<UnityEngine.Renderer> all = new System.Collections.Generic.List<UnityEngine.Renderer>();
            foreach (UnityEngine.Renderer r in m_lodBase) if (r != null) all.Add(r);
            foreach (UnityEngine.GameObject leg in m_long) if (leg != null) all.AddRange(leg.GetComponentsInChildren<UnityEngine.Renderer>(true));
            lod.SetLODs(new[] { new UnityEngine.LOD(m_lodHeight, all.ToArray()) });
            lod.RecalculateBounds();
        }

        // A leg frame saves its step in its ZDO (the owner does, a moment after it became one, so a frame that is a
        // leg frame only while the frames under it fall in turn doesn't save it).
        private void KeepStep()
        {
            if (!Live || !m_hasLong || !m_nview.IsOwner())
            {
                return;
            }
            int step = Step(new System.Collections.Generic.Dictionary<ScaffoldMark, int>());
            if (step % 3 == 0 && m_nview.GetZDO().GetInt(s_stepKey, 0) != step)
            {
                m_nview.GetZDO().Set(s_stepKey, step);
                Plugin.Debug("Scaffold at " + transform.position + ": leg frame, step " + step + " kept");
            }
        }

        // Another frame on the same level, with a lower ZDOID, has a post beside this one's post i (guard: only frames
        // with no frame on top, whose guard posts show).
        private bool Yields(int i, bool guard)
        {
            UnityEngine.Vector3 mine = Spot(i);
            ZDOID id = m_nview.GetZDO().m_uid;
            foreach (ScaffoldMark o in s_all)
            {
                if (o == this || o == null || !o.Live) continue;
                UnityEngine.Vector3 apart = o.Middle - Middle;
                if (UnityEngine.Mathf.Abs(apart.y) > Level || UnityEngine.Mathf.Abs(apart.x) > 3f || UnityEngine.Mathf.Abs(apart.z) > 3f) continue;
                ZDOID other = o.m_nview.GetZDO().m_uid;
                if (other.UserID > id.UserID || (other.UserID == id.UserID && other.ID >= id.ID)) continue;
                if (guard && o.HasFrameOnTop()) continue;
                for (int j = 0; j < ScaffoldPiece.PostSpots.Length; j++)
                {
                    UnityEngine.Vector3 d = o.Spot(j) - mine;
                    d.y = 0f;
                    if (d.magnitude < Beside) return true;
                }
            }
            return false;
        }

        private void Reshape()
        {
            if (!Live)
            {
                return;
            }
            bool top = !HasFrameOnTop();
            // Legs over nothing (the user, test-i: "Hide them"), and the rails halfway up that would hang from them.
            if (GetComponentInParent<Ship>() != null)
            {
                UnityEngine.Physics.SyncTransforms(); // a moving ship's colliders lag their transforms
            }
            int step = Step(new System.Collections.Generic.Dictionary<ScaffoldMark, int>());
            bool legFrame = step % 3 == 0;
            bool[] leg = new bool[m_posts.Length];
            bool[] stand = new bool[m_posts.Length];
            float[] longLength = new float[m_posts.Length];
            string legs = "";
            for (int i = 0; i < m_posts.Length; i++)
            {
                leg[i] = StandsOnSomething(i, out string on);
                stand[i] = leg[i];
                if (!leg[i] && legFrame && LongLeg(i, out longLength[i], out string below))
                {
                    stand[i] = true;
                    legs += " " + i + " long " + longLength[i].ToString("0.00") + " m to " + below;
                }
                else
                {
                    legs += " " + i + (leg[i] ? " on " + on : " over nothing");
                }
            }
            // Open sides (the user, test-h: frames built as a stair, each one beside and one level up from the last, kept
            // the lower frame's guard rail between its deck and the next frame's ladder). A frame beside on the same level
            // opens both rails on that side; one a level up opens the guard rail (its foot is at this deck); one a level
            // down opens the rail halfway up (this frame's foot is at its deck).
            bool[] same = { FrameBeside(2f, 0f, 0f), FrameBeside(0f, -2f, 0f), FrameBeside(0f, 2f, 0f) };
            bool[] up = { FrameBeside(2f, 0f, 2f), FrameBeside(0f, -2f, 2f), FrameBeside(0f, 2f, 2f) };
            bool[] down = { FrameBeside(2f, 0f, -2f), FrameBeside(0f, -2f, -2f), FrameBeside(0f, 2f, -2f) };
            // Each rail runs between two posts (PostSpots: 0 deck front outer, 1 deck back outer, 2 ladder back, 3 deck
            // front inner): the deck side 0-1, the back 1-2, the front 3-0.
            Show(m_railSide, !same[0] && !down[0] && stand[0] && stand[1]);
            Show(m_railBack, !same[1] && !down[1] && stand[1] && stand[2]);
            Show(m_railFront, !same[2] && !down[2] && stand[3] && stand[0]);
            // The guard rail only on a frame with no frame on top and none a level up beside it (the user, test-j: "The
            // rail gets in the way when rotated 90 degrees. Just get rid of it when a scaffold is stacked.").
            bool railOn = top && !up[0] && !up[1] && !up[2];
            bool guardSide = railOn && !same[0];
            bool guardBack = railOn && !same[1];
            bool guardFront = railOn && !same[2];
            Show(m_guardSide, guardSide);
            Show(m_guardBack, guardBack);
            Show(m_guardFront, guardFront);
            // A guard post shows only while a guard rail it holds shows (test-i: one left alone at the top of the ladder
            // once the guard rail beside it opened for the next frame of a stair).
            bool[] held = { guardSide || guardFront, guardBack || guardSide, guardBack, guardFront };
            string hidden = "";
            bool hasLong = false;
            for (int i = 0; i < m_posts.Length; i++)
            {
                bool post = stand[i] && !Yields(i, false);
                bool guard = top && held[i] && !Yields(i, true) && !PostAbove(i);
                Show(m_posts[i], post);
                Show(m_guardPosts[i], guard);
                bool longLeg = post && !leg[i];
                SetLong(i, longLeg ? longLength[i] : 0f);
                hasLong |= longLeg;
                if (!post) hidden += " post " + i;
                if (top && !guard) hidden += " guard post " + i;
            }
            string[] sides = { "deck side", "back", "front" };
            string beside = "";
            for (int k = 0; k < 3; k++)
            {
                if (same[k]) beside += ", frame at the " + sides[k];
                if (up[k]) beside += ", frame a level up at the " + sides[k];
                if (down[k]) beside += ", frame a level down at the " + sides[k];
            }
            Plugin.Debug("Scaffold at " + transform.position + " (middle " + Middle + ", turned " + transform.eulerAngles.y.ToString("0.0") + "): " + (top ? "top" : "frame on top") + (railOn ? ", guard rail" : "") + beside + ", step " + step + (hidden.Length > 0 ? ", hidden:" + hidden : "") + "; legs:" + legs);
            m_hasLong = hasLong;
            if (hasLong)
            {
                CancelInvoke(nameof(KeepStep));
                Invoke(nameof(KeepStep), 2f);
            }
            // The frames resting on this one count their steps from it.
            if (step != m_step)
            {
                m_step = step;
                ReshapeNear(this, false);
            }
        }

        private static void Show(UnityEngine.GameObject part, bool on)
        {
            if (part != null && part.activeSelf != on) part.SetActive(on);
        }

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
                if (s == null || s.gameObject == gone.gameObject || s.m_falling || !s.Live)
                {
                    continue;
                }
                UnityEngine.Vector3 d = s.Middle - at;
                if (UnityEngine.Mathf.Abs(d.x) > 5f || UnityEngine.Mathf.Abs(d.z) > 5f || d.y < -1f || d.y > 6f)
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

        // A building piece came (loaded or placed, on every client): the frames within reach above it take their shape
        // again a moment later, so a leg over a piece that loaded after the frame shows. The ground is always there
        // first (ZNetScene.CreateObjectsSorted creates nothing until ZoneSystem.IsActiveAreaLoaded); other pieces load
        // in the same pass as the frames, in no set order. Frames themselves are handled in Start.
        internal static void PieceCame(WearNTear came)
        {
            if (s_all.Count == 0 || came == null || came.m_nview == null || !came.m_nview.IsValid() || came.GetComponent<ScaffoldMark>() != null)
            {
                return;
            }
            UnityEngine.Vector3 at = came.transform.position;
            foreach (ScaffoldMark s in s_all)
            {
                if (s == null || s.m_falling || !s.Live)
                {
                    continue;
                }
                UnityEngine.Vector3 d = s.Middle - at;
                if (UnityEngine.Mathf.Abs(d.x) > 5f || UnityEngine.Mathf.Abs(d.z) > 5f || d.y < -1f || d.y > 6f)
                {
                    continue;
                }
                s.CancelInvoke(nameof(Reshape));
                s.Invoke(nameof(Reshape), CheckDelay);
            }
        }

        private void CheckFeet()
        {
            WearNTear wear = GetComponent<WearNTear>();
            if (m_falling || !Live || wear == null)
            {
                return;
            }
            // Its feet: the 2 x 2 m base, from 0.25 m below to 0.1 m above, kept 5 cm in from the edges so frames beside
            // it don't count.
            UnityEngine.Vector3 center = transform.TransformPoint(ScaffoldPiece.Origin + new UnityEngine.Vector3(0f, -0.075f, 0f));
            int n = UnityEngine.Physics.OverlapBoxNonAlloc(center, new UnityEngine.Vector3(0.95f, 0.175f, 0.95f), s_hits, transform.rotation, Mask, UnityEngine.QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                UnityEngine.Collider c = s_hits[i];
                if (c != null && !c.transform.IsChildOf(transform))
                {
                    Plugin.Debug("Scaffold at " + transform.position + " stands on " + c.name + " (layer " + UnityEngine.LayerMask.LayerToName(c.gameObject.layer) + ")");
                    // It stays, but what was under one of its legs may be gone.
                    Reshape();
                    return;
                }
            }
            // A leg on something holds it: a frame resting only on the edge beam of the frame below (a stair turned 90
            // degrees at the lower frame's deck side) has nothing inside the base box above (test-k: the fourth step fell
            // while the third stood on its legs). The same check that shows the legs.
            for (int i = 0; i < m_posts.Length; i++)
            {
                if (StandsOnSomething(i, out string under))
                {
                    Plugin.Debug("Scaffold at " + transform.position + " stands on " + under + " (leg " + i + ")");
                    Reshape();
                    return;
                }
            }
            // Long legs hold it (the user, test-j: "Yes, legs hold it up").
            for (int i = 0; i < m_posts.Length; i++)
            {
                if (m_long[i] != null && m_long[i].activeSelf && LongLeg(i, out float length, out string below))
                {
                    Plugin.Debug("Scaffold at " + transform.position + " stands on its long legs (leg " + i + ", " + length.ToString("0.00") + " m to " + below + ")");
                    Reshape();
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

    [HarmonyLib.HarmonyPatch(typeof(WearNTear), "Awake")]
    internal static class ScaffoldLegs
    {
        private static void Postfix(WearNTear __instance)
        {
            ScaffoldMark.PieceCame(__instance);
        }
    }
}
