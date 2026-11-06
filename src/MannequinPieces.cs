using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Valheim.UI;

namespace PotatoWright
{
	// Mannequin pieces: the hammer's tabs Mannequins: Male and Mannequins: Female have one piece per pose: the 15 armor stand
	// poses and every player emote, on the game's wooden mannequin stands (ArmorStand_Male, ArmorStand_Female). The game
	// has these stands (with a recipe: Fine Wood 8, Bronze Nails 2, Tar 4 at a workbench) but lists them in no build menu.
	//
	// - Each pose piece is a copy of the stand prefab with its own name (registered in ZNetScene on every client, as the
	//   mod is required on all of them), its own piece name, and a MannequinPose holding the pose. Everything else is the
	//   stand's own: cost, station, slots, taking items with E, "Change pose", dropping its items when destroyed. Emote
	//   pieces use the game's emote icons. The wooden body takes the armor painted onto a body (MakePaintable).
	// - The stand starts in the pose its ZDO holds (ArmorStand.Awake: ZDOVars.s_pose, else 0). A new piece's ZDO gets the
	//   piece's pose (and emote) in an ArmorStand.Awake prefix, once (an init key), by the client that creates it.
	// - Emotes: the stand's figure gets the player's animator, the emote is started the
	//   game's way (a trigger; Sit a bool) and the figure is frozen halfway through it (state tagged "emote",
	//   normalized time 0.5, or after 6 s) with the bones kept. "Change pose" (ArmorStand.SetPose with effect) gives the
	//   stand its own animator back and drops the emote, then the stand cycles its poses as usual.
	[HarmonyPatch]
	public static class MannequinPieces
	{
		public static readonly int EmoteKey = "PotatoWright_emote".GetStableHashCode();

		private static readonly int InitKey = "PotatoWright_mannequinInit".GetStableHashCode();

		private static readonly string[] BodyPrefabs = new string[2] { "ArmorStand_Male", "ArmorStand_Female" };

		private static readonly string[] BodyLabels = new string[2] { "Male", "Female" };

		// Shown names: Ask and Embla, the first man and woman, made by the gods from two trees (Völuspá 17-18).
		private static readonly string[] BodyNames = new string[2] { "Ask", "Embla" };


		// The stand animator's "Pose" values 0 to 14 (ArmorStand_Animator in bundle c4210710).
		private static readonly string[] StandPoses = new string[15]
		{
			"A-Pose", "T-Pose", "Upright", "Wave", "Forge", "Cooking", "Equip (back)", "Equip (middle)", "Equip (loop)", "Hold the mast",
			"Kickstep", "Thumbs up", "Roar", "Warrior idle", "Sitting"
		};

		// One paintable wood material per body (see Paintable), shared by that body's pieces.
		private static readonly Material[] s_paintable = new Material[2];

		private static bool s_iconsSet;

		// Looked for at most once a second (Resources.FindObjectsOfTypeAll is slow).
		private static float s_nextIconTry;

				private static readonly List<GameObject> s_pieces = new List<GameObject>();

		// Armor is partly painted onto the body: VisEquipment copies the item's m_armorMaterial textures (_ChestTex,
		// _ChestBumpMap, _ChestMetal; _LegsTex, _LegsBumpMap, _LegsMetal) onto the body's material. The wooden stands'
		// body material (PlayerMaterialArmorStand) is Custom/Piece, which has none of those, so the paint never showed.
		// The invisible stand's (PlayerMaterialArmorStandSkin) is Custom/Player, which takes the paint, but it doesn't
		// show its main texture's colour (planks came out near white, test-q) and it cuts the whole body out by the main
		// texture's alpha, paint included (test-s). So the body is two layers (tested in test-s/t: wood, strong colours,
		// paint shown, no flicker):
		// - "PotatoWrightWoodLayer": a copy of the body renderer with the game's own wooden material: the real planks.
		//   Its colour is the planks texture multiplied by the mannequin's colour (MannequinTint).
		// - The body itself (VisEquipment's, painted as usual): a copy of the invisible stand's skin, drawn after the wood
		//   (render queue 2450), its main texture a mask of the paint's own coverage (PaintMask), so only painted armor
		//   shows from it; with no paint it is fully cut out (the skin's "transparent").
		// The game's materials and prefabs are not changed.
		public const string WoodLayerName = "PotatoWrightWoodLayer";

		// The wood layer's planks (the stands' own material's main texture): the natural colour.
		public static Texture WoodTexture;

		private static void MakePaintable(GameObject piece, int body)
		{
			ArmorStand stand = piece.GetComponent<ArmorStand>();
			VisEquipment vis = (stand != null) ? stand.m_visEquipment : null;
			if (vis == null || vis.m_bodyModel == null)
			{
				return;
			}
			Material wood = vis.m_bodyModel.sharedMaterial;
			if (s_paintable[body] == null)
			{
				GameObject invisible = PrefabManager.Instance.GetPrefab("ArmorStand");
				ArmorStand invisibleStand = (invisible != null) ? invisible.GetComponent<ArmorStand>() : null;
				Material skin = (invisibleStand != null && invisibleStand.m_visEquipment != null && invisibleStand.m_visEquipment.m_bodyModel != null) ? invisibleStand.m_visEquipment.m_bodyModel.sharedMaterial : null;
				if (skin == null || wood == null || !skin.HasProperty("_ChestTex") || !skin.HasProperty("_MainTex"))
				{
					Debug.LogWarning(Plugin.Tag + " Mannequin " + BodyLabels[body].ToLowerInvariant() + ": no paintable body material (ArmorStand skin " + ((skin != null) ? skin.shader.name : "missing") + "); painted armor will not show.");
					s_paintable[body] = wood;
				}
				else
				{
					Material m = new Material(skin);
					m.name = "PotatoWrightMannequinPaint_" + BodyLabels[body];
					m.renderQueue = 2450;
					s_paintable[body] = m;
					Plugin.Debug("Mannequin " + BodyLabels[body].ToLowerInvariant() + ": wood " + wood.name + " (" + wood.shader.name + ") under paint " + m.name + " (" + m.shader.name + ", main texture " + ((m.GetTexture("_MainTex") != null) ? m.GetTexture("_MainTex").name : "none") + "), models " + vis.m_models.Length);
				}
			}
			Material paintable = s_paintable[body];
			if (WoodTexture == null && wood != null && wood.HasProperty("_MainTex"))
			{
				WoodTexture = wood.GetTexture("_MainTex");
			}
			if (paintable == wood)
			{
				return;
			}
			// The wooden layer: the body renderer copied (same mesh and bones) with the game's wood on every slot.
			GameObject layer = UnityEngine.Object.Instantiate(vis.m_bodyModel.gameObject, vis.m_bodyModel.transform.parent, false);
			layer.name = WoodLayerName;
			foreach (Component c in layer.GetComponents<Component>())
			{
				if (!(c is Transform) && !(c is Renderer))
				{
					UnityEngine.Object.DestroyImmediate(c);
				}
			}
			foreach (Transform child in layer.transform)
			{
				UnityEngine.Object.DestroyImmediate(child.gameObject);
			}
			Renderer layerRenderer = layer.GetComponent<Renderer>();
			Material[] woodSlots = new Material[vis.m_bodyModel.sharedMaterials.Length];
			for (int i = 0; i < woodSlots.Length; i++)
			{
				woodSlots[i] = wood;
			}
			layerRenderer.sharedMaterials = woodSlots;
			// Drawn and hidden with the body at every distance: the layer joins each LOD level the body is in.
			LODGroup lodGroup = piece.GetComponentInChildren<LODGroup>(true);
			if (lodGroup != null)
			{
				LOD[] lods = lodGroup.GetLODs();
				for (int i = 0; i < lods.Length; i++)
				{
					Renderer[] renderers = lods[i].renderers ?? new Renderer[0];
					if (Array.IndexOf(renderers, vis.m_bodyModel) >= 0)
					{
						Renderer[] more = new Renderer[renderers.Length + 1];
						renderers.CopyTo(more, 0);
						more[renderers.Length] = layerRenderer;
						lods[i].renderers = more;
					}
				}
				lodGroup.SetLODs(lods);
			}
			Material[] materials = vis.m_bodyModel.sharedMaterials;
			for (int i = 0; i < materials.Length; i++)
			{
				materials[i] = paintable;
			}
			vis.m_bodyModel.sharedMaterials = materials;
			// VisEquipment.UpdateBaseModel copies the main texture and skin normal map from the model's base material.
			for (int i = 0; i < vis.m_models.Length; i++)
			{
				VisEquipment.PlayerModel model = vis.m_models[i];
				vis.m_models[i] = new VisEquipment.PlayerModel
				{
					m_mesh = (model != null) ? model.m_mesh : vis.m_bodyModel.sharedMesh,
					m_baseMaterial = paintable
				};
			}
		}

		// The emote pieces' icons: the game's own emote icons (the emote wheel's: RadialData.SO.EmoteMappings, an
		// EmoteDataMapping per emote with its Sprite; T_emote_<name> in bundle 6a33a62). The wheel's data is set up with
		// the game's UI, so this is tried while the build menu is open (MannequinPanel)
		// until it is there. The stand poses keep the stand's icon (the game has none for them).
		public static void EnsureEmoteIcons()
		{
			if (s_iconsSet || s_pieces.Count == 0 || Time.time < s_nextIconTry)
			{
				return;
			}
			s_nextIconTry = Time.time + 1f;
			try
			{
				EmoteMappings map = (RadialData.SO != null) ? RadialData.SO.EmoteMappings : null;
				if (map == null)
				{
					EmoteMappings[] loaded = Resources.FindObjectsOfTypeAll<EmoteMappings>();
					map = (loaded.Length > 0) ? loaded[0] : null;
				}
				if (map == null)
				{
					return;
				}
				int set = 0;
				int emotes = 0;
				foreach (GameObject piece in s_pieces)
				{
					MannequinPose pose = (piece != null) ? piece.GetComponent<MannequinPose>() : null;
					if (pose == null || pose.m_emote < 0)
					{
						continue;
					}
					emotes++;
					Sprite sprite = map.GetMapping((Emotes)pose.m_emote).Sprite;
					if (Plugin.TestBuild && piece.name.Contains("Male_"))
					{
						Plugin.Debug("Mannequin emote icon " + (Emotes)pose.m_emote + ": " + ((sprite != null) ? (sprite.name + " " + sprite.rect.width + "x" + sprite.rect.height + " texture " + ((sprite.texture != null) ? sprite.texture.name : "none")) : "none"));
					}
					if (sprite != null)
					{
						piece.GetComponent<Piece>().m_icon = sprite;
						set++;
					}
				}
				s_iconsSet = true;
				Plugin.Debug("Mannequin emote icons: " + set + " of " + emotes);
			}
			catch (Exception ex)
			{
				s_iconsSet = true;
				Debug.LogWarning(Plugin.Tag + " Mannequin emote icons not set: " + ex.Message);
			}
		}

		// The pieces, made once the game's prefabs exist (Jötunn's OnVanillaPrefabsAvailable): each a Jötunn copy of the
		// game's stand on the hammer, in the "Mannequins: Male" or "Mannequins: Female" tab, with the stand's own recipe.
		public static void Create()
		{
			for (int b = 0; b < BodyPrefabs.Length; b++)
			{
				GameObject stand = PrefabManager.Instance.GetPrefab(BodyPrefabs[b]);
				if (stand == null || stand.GetComponent<ArmorStand>() == null || stand.GetComponent<Piece>() == null)
				{
					Debug.LogWarning(Plugin.Tag + " " + BodyPrefabs[b] + " not found; no " + BodyLabels[b].ToLowerInvariant() + " mannequins.");
					continue;
				}
				for (int v = 0; v < StandPoses.Length; v++)
				{
					Make(b, "Pose_" + Key(StandPoses[v]), StandPoses[v], v, -1);
				}
				foreach (Emotes e in Enum.GetValues(typeof(Emotes)))
				{
					if (e != Emotes.Count)
					{
						Make(b, "Emote_" + e, Spaced(e.ToString()), 0, (int)e);
					}
				}
			}
			Plugin.Debug("Mannequin pieces: " + s_pieces.Count);
			PieceManager.OnPiecesRegistered += ClearUsage;
		}

		// Jötunn gives a piece with no usage tags the Misc tag when it adds it to the hammer; the mannequins are listed
		// only under the Mannequins entry, so they carry none.
		private static void ClearUsage()
		{
			foreach (GameObject piece in s_pieces)
			{
				Piece p = (piece != null) ? piece.GetComponent<Piece>() : null;
				if (p != null)
				{
					p.m_usage = 0;
				}
			}
		}

		internal static IEnumerable<GameObject> Pieces => s_pieces;

		private static void Make(int body, string key, string label, int standPose, int emote)
		{
			// Saved in worlds: never rename.
			string name = "PotatoWright_Mannequin" + BodyLabels[body] + "_" + key;
			string bodyLabel = Text("potatowright_mannequin_" + BodyLabels[body].ToLowerInvariant(), BodyNames[body]);
			CustomPiece custom = new CustomPiece(name, BodyPrefabs[body], new PieceConfig
			{
				Name = (emote < 0) ? (bodyLabel + ": " + label) : (bodyLabel + ", " + Text("potatowright_emote", "emote") + ": " + label),
				Description = "$potatowright_mannequin_description",
				PieceTable = "Hammer",
				// Listed under the build menu's own fold-open Mannequins entry (MannequinMenu), not a tab of their own.
				Category = "Misc",
				CraftingStation = "piece_workbench",
				Requirements = new[]
				{
					new RequirementConfig("FineWood", 8, 0, true),
					new RequirementConfig("BronzeNails", 2, 0, true),
					new RequirementConfig("Tar", 4, 0, true)
				}
			});
			GameObject piece = custom.PiecePrefab;
			if (piece == null)
			{
				Debug.LogWarning(Plugin.Tag + " Mannequin " + name + " not made.");
				return;
			}
			MakePaintable(piece, body);
			ArmorStand pieceStand = piece.GetComponent<ArmorStand>();
			int clothRemoved = CleanClothColliders((pieceStand != null) ? pieceStand.m_visEquipment : null);
			if (key == "Pose_APose" && clothRemoved > 0)
			{
				Plugin.Debug("Mannequin " + BodyPrefabs[body] + ": " + clothRemoved + " cape collider entries without a cloth collider left out of its pieces");
			}
			string cleaned = CleanEffects(piece);
			if (key == "Pose_APose" && cleaned.Length > 0)
			{
				Plugin.Debug("Mannequin " + BodyPrefabs[body] + " effect entries without a prefab, left out of its pieces:" + cleaned);
			}
			MannequinPose pose = piece.AddComponent<MannequinPose>();
			pose.m_standPose = standPose;
			pose.m_body = body;
			pose.m_emote = emote;
			piece.AddComponent<MannequinTint>();
			if (emote < 0 && !MannequinIcons.Make(piece, standPose, name))
			{
				Plugin.Debug("Stance icon for " + name + " not made");
			}
			PieceManager.Instance.AddPiece(custom);
			s_pieces.Add(piece);
		}

		// Capes (MagicaCloth) take the body's colliders from VisEquipment.m_clothColliders (VisEquipment.SetupCloth). The
		// wooden stands' list (ArmorStand_Male/_Female, bundle c4210710: 5 entries) points at their old "ClothCollider"
		// CapsuleColliders, which are not MagicaCloth colliders (those objects carry none; tools\clothcol.py, clothobj.py),
		// and a cape on a mannequin threw a NullReferenceException in MagicaCloth2.ColliderManager.AddColliderInternal
		// ("Cloth Pre-Build construction failure"). Removing only empty entries (test-q) removed none (test-u log), so the
		// copy's list is emptied, as the hammer's ArmorStand has it (0 entries).
		// What the entries were at run time is logged once.
		private static readonly System.Reflection.FieldInfo ClothColliders = AccessTools.Field(typeof(VisEquipment), "m_clothColliders");

		private static bool s_clothLogged;

		public static int CleanClothColliders(VisEquipment vis)
		{
			System.Collections.IList list = (vis != null && ClothColliders != null) ? (ClothColliders.GetValue(vis) as System.Collections.IList) : null;
			if (list == null || list.Count == 0)
			{
				return 0;
			}
			if (!s_clothLogged)
			{
				s_clothLogged = true;
				StringBuilder sb = new StringBuilder();
				foreach (object entry in list)
				{
					UnityEngine.Object o = entry as UnityEngine.Object;
					sb.Append(" ").Append((entry == null) ? "null" : ((o == null) ? (entry.GetType().Name + "(destroyed)") : (entry.GetType().Name + " " + o.name)));
				}
				Plugin.Debug("Mannequin " + vis.name + " cape collider entries at run time:" + sb);
			}
			int removed = list.Count;
			list.Clear();
			return removed;
		}

		// Effect entries without a prefab make EffectList.Create throw (the stand's own put-on, take-off and change-pose
		// effects included; the game never builds these stands). Every EffectList on the piece's components (the copy has
		// its own lists, the game's prefab is untouched) keeps only entries with a prefab.
		private static string CleanEffects(GameObject piece)
		{
			StringBuilder report = new StringBuilder();
			foreach (MonoBehaviour mb in piece.GetComponentsInChildren<MonoBehaviour>(true))
			{
				if (mb == null)
				{
					continue;
				}
				foreach (FieldInfo field in mb.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
				{
					if (field.FieldType != typeof(EffectList))
					{
						continue;
					}
					EffectList list = field.GetValue(mb) as EffectList;
					if (list == null || list.m_effectPrefabs == null)
					{
						continue;
					}
					List<EffectList.EffectData> kept = new List<EffectList.EffectData>();
					foreach (EffectList.EffectData data in list.m_effectPrefabs)
					{
						if (data != null && data.m_prefab != null)
						{
							kept.Add(data);
						}
					}
					if (kept.Count != list.m_effectPrefabs.Length)
					{
						report.Append(' ').Append(mb.GetType().Name).Append('.').Append(field.Name).Append(' ').Append(list.m_effectPrefabs.Length - kept.Count).Append(" of ").Append(list.m_effectPrefabs.Length);
						list.m_effectPrefabs = kept.ToArray();
					}
				}
			}
			return report.ToString();
		}

		private static string Key(string label)
		{
			StringBuilder sb = new StringBuilder();
			foreach (char c in label)
			{
				if (char.IsLetterOrDigit(c))
				{
					sb.Append(c);
				}
			}
			return sb.ToString();
		}

		// "ThumbsUp" -> "Thumbs Up".
		private static string Spaced(string name)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < name.Length; i++)
			{
				if (i > 0 && char.IsUpper(name[i]))
				{
					sb.Append(' ');
				}
				sb.Append(name[i]);
			}
			return sb.ToString();
		}

		private static string Text(string key, string fallback)
		{
			if (Localization.instance == null)
			{
				return fallback;
			}
			string text = Localization.instance.Localize("$" + key);
			return (string.IsNullOrEmpty(text) || text.Contains(key)) ? fallback : text;
		}

		// A new mannequin piece's ZDO gets its pose, emote and colour before the stand reads its pose (once, by its creator).
		[HarmonyPatch(typeof(ArmorStand), "Awake")]
		[HarmonyPrefix]
		private static void ArmorStand_Awake_Prefix(ArmorStand __instance)
		{
			try
			{
				MannequinPose pose = __instance.GetComponent<MannequinPose>();
				if (pose == null)
				{
					return;
				}
				ZNetView nview = (__instance.m_netViewOverride != null) ? __instance.m_netViewOverride : __instance.GetComponent<ZNetView>();
				ZDO zdo = (nview != null) ? nview.GetZDO() : null;
				if (zdo == null || !nview.IsOwner() || zdo.GetInt(InitKey) != 0)
				{
					return;
				}
				zdo.Set(InitKey, 1);
				zdo.Set(ZDOVars.s_pose, Mathf.Max(pose.m_standPose, 0));
				zdo.Set(EmoteKey, pose.m_emote);
				// The colour chosen in the panel (MannequinColor), when this client is placing a mannequin.
				Player player = Player.m_localPlayer;
				if (player != null && player.m_placementGhost != null && player.m_placementGhost.GetComponent<MannequinTint>() != null)
				{
					if (!WoodTint.IsNatural(MannequinColor.Current))
					{
						zdo.Set(MannequinColor.ColorKey, MannequinColor.ToVector(MannequinColor.Current));
					}
					if (!MannequinColor.ShowBase)
					{
						zdo.Set(MannequinColor.NoBaseKey, value: true);
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning(Plugin.Tag + " Mannequin pose not set: " + ex);
			}
		}

		// "Change pose" (an RPC, so on every client, with effect): an emote mannequin gets the stand's animator back first.
		[HarmonyPatch(typeof(ArmorStand), "SetPose")]
		[HarmonyPrefix]
		private static void ArmorStand_SetPose_Prefix(ArmorStand __instance, bool effect)
		{
			if (!effect)
			{
				return;
			}
			MannequinPose pose = __instance.GetComponent<MannequinPose>();
			if (pose != null)
			{
				pose.EndEmote();
			}
		}
	}

	// On a mannequin piece: its pose (the stand's "Pose" value) or emote (an Emotes value). Poses the placing preview
	// (which has no ZDO, so the stand leaves it alone), and plays and freezes an emote.
	public sealed class MannequinPose : MonoBehaviour
	{
		public int m_standPose = -1;

		public int m_emote = -1;

		// 0 Ask (male), 1 Embla (female).
		public int m_body;

		private const float EmoteTimeout = 6f;

		private static readonly int EmoteTag = Animator.StringToHash("emote");

		private ArmorStand m_stand;

		private Animator m_animator;

		private RuntimeAnimatorController m_standController;

		private bool m_emoteOn;

		private bool m_frozen;

		private float m_start;

		private void Start()
		{
			m_stand = GetComponent<ArmorStand>();
			m_animator = (m_stand != null) ? m_stand.m_poseAnimator : null;
			if (m_animator == null)
			{
				enabled = false;
				return;
			}
			ZNetView nview = m_stand.m_nview;
			bool placed = nview != null && nview.IsValid();
			if (!placed && m_standPose >= 0)
			{
				m_animator.SetInteger("Pose", m_standPose);
			}
			int emote = placed ? nview.GetZDO().GetInt(MannequinPieces.EmoteKey, -1) : m_emote;
			if (emote < 0 || !StartEmote(emote))
			{
				enabled = false;
			}
		}

		// The player's animator controller (the Player prefab's), for emotes.
		private static RuntimeAnimatorController PlayerController()
		{
			GameObject player = (ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab("Player") : null;
			Animator a = (player != null) ? player.GetComponentInChildren<Animator>(true) : null;
			return (a != null) ? a.runtimeAnimatorController : null;
		}

		private bool StartEmote(int emote)
		{
			RuntimeAnimatorController player = PlayerController();
			if (player == null || emote >= (int)Emotes.Count)
			{
				return false;
			}
			m_standController = m_animator.runtimeAnimatorController;
			m_animator.runtimeAnimatorController = player;
			m_animator.fireEvents = false;
			m_animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
			// The player's animator starts in "wakeup ?" and gets up off the ground while "wakeup" is on .
			m_animator.SetBool("wakeup", false);
			m_animator.SetBool("onGround", true);
			string name = "emote_" + ((Emotes)emote).ToString().ToLowerInvariant();
			if (emote == (int)Emotes.Sit)
			{
				m_animator.SetBool(name, true);
			}
			else
			{
				m_animator.SetTrigger(name);
			}
			m_emoteOn = true;
			m_frozen = false;
			m_start = Time.time;
			return true;
		}

		private void Update()
		{
			if (!m_emoteOn || m_frozen || m_animator == null)
			{
				enabled = false;
				return;
			}
			if (Time.time - m_start < EmoteTimeout && !EmoteHalfway())
			{
				return;
			}
			Freeze();
			enabled = false;
		}

		private bool EmoteHalfway()
		{
			for (int i = 0; i < m_animator.layerCount; i++)
			{
				AnimatorStateInfo info = m_animator.GetCurrentAnimatorStateInfo(i);
				if (info.tagHash == EmoteTag && info.normalizedTime >= 0.5f)
				{
					return true;
				}
			}
			return false;
		}

		// The Animator off with the bones where the emote left them.
		private void Freeze()
		{
			Transform[] bones = m_animator.GetComponentsInChildren<Transform>(true);
			Vector3[] pos = new Vector3[bones.Length];
			Quaternion[] rot = new Quaternion[bones.Length];
			Vector3[] scale = new Vector3[bones.Length];
			for (int i = 0; i < bones.Length; i++)
			{
				pos[i] = bones[i].localPosition;
				rot[i] = bones[i].localRotation;
				scale[i] = bones[i].localScale;
			}
			m_animator.enabled = false;
			for (int i = 0; i < bones.Length; i++)
			{
				bones[i].localPosition = pos[i];
				bones[i].localRotation = rot[i];
				bones[i].localScale = scale[i];
			}
			m_frozen = true;
		}

		// Back to the stand's own animator (before it is given a pose), and the emote dropped from the ZDO.
		public void EndEmote()
		{
			if (!m_emoteOn || m_animator == null)
			{
				return;
			}
			m_emoteOn = false;
			m_frozen = false;
			enabled = false;
			m_animator.enabled = true;
			if (m_standController != null)
			{
				m_animator.runtimeAnimatorController = m_standController;
			}
			ZNetView nview = (m_stand != null) ? m_stand.m_nview : null;
			if (nview != null && nview.IsValid() && nview.IsOwner())
			{
				nview.GetZDO().Set(MannequinPieces.EmoteKey, -1);
			}
		}
	}
}
