using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace PotatoWright
{
	// Mannequin colours, wood body only. The mannequin body is two layers (MannequinPieces.MakePaintable): the game's
	// wooden material on "PotatoWrightWoodLayer", and the paint body over it showing only painted armor. The colour is
	// the wood layer's planks texture multiplied by the colour (WoodTint); the paint body is never coloured, so armor
	// paint keeps its own colours. (test-p tinted the paint body's _SkinColor, which colours the paint too; test-q
	// multiplied the paint body's main texture, which that shader doesn't show. test-s/t proved the layered body.)
	//
	// - The colour to use: hue, saturation and brightness from the panel beside the build menu (MannequinPanel), kept with the
	//   character. Natural wood is white (the game's planks texture itself).
	// - Each mannequin keeps its colour in its ZDO (ColorKey, an RGB Vector3; missing = natural). A new one gets the
	//   panel's colour from its creator (MannequinPieces' ArmorStand.Awake prefix).
	// - Recolour: with a mannequin piece selected and a placed mannequin under the crosshair (the game's own placement
	//   ray), the preview is hidden, the mannequin shows the panel's colour, and placing recolours it (ward checked) instead
	//   of placing a new one: no cost, no materials needed.
	[HarmonyPatch]
	public static class MannequinColor
	{
		// The wood colour itself (test-v on): missing = natural. test-p..u kept a multiplier under "PotatoWright_color",
		// which is left alone (those mannequins show natural wood).
		public static readonly int ColorKey = "PotatoWright_woodColor".GetStableHashCode();

		// Set (true) on a mannequin placed with the panel's Base switched off: its wooden base and the pose switch's block
		// don't show (the switch still works). Missing = base shown. Chosen when placing only (a recolour leaves it).
		public static readonly int NoBaseKey = "PotatoWright_noBase".GetStableHashCode();

		public const string RpcName = "PotatoWright_Tint";

		private const string SaveKey = "PotatoWright.MannequinWood";

		public static float H;

		public static float S;

		public static float V;

		// The panel's Base switch: new mannequins with or without their wooden base.
		public static bool ShowBase = true;

		// Goes up on every change, so previews follow.
		public static int Version;

		private static Player s_loadedFor;

		private static MannequinTint s_target;

		private static readonly List<Renderer> s_hidden = new List<Renderer>();

		private static readonly List<Renderer> s_renderers = new List<Renderer>();

		public static Color Current
		{
			get
			{
				Color c = Color.HSVToRGB(H, S, V);
				c.a = 1f;
				return c;
			}
		}

		public static Vector3 ToVector(Color c)
		{
			return new Vector3(c.r, c.g, c.b);
		}

		public static Color ToColor(Vector3 v)
		{
			return new Color(Mathf.Clamp01(v.x), Mathf.Clamp01(v.y), Mathf.Clamp01(v.z), 1f);
		}

		// The planks' own colour (their average): the panel's default and "Natural wood".
		public static void Natural()
		{
			Color.RGBToHSV(WoodTint.NaturalColor, out H, out S, out V);
			Version++;
		}

		// Kept with the character as "h=0.08;s=0.4;v=0.9".
		public static void EnsureLoaded(Player player)
		{
			if (player == null || player == s_loadedFor)
			{
				return;
			}
			s_loadedFor = player;
			Natural();
			ShowBase = true;
			if (!player.m_customData.TryGetValue(SaveKey, out string text) || string.IsNullOrEmpty(text))
			{
				return;
			}
			foreach (string entry in text.Split(';'))
			{
				int split = entry.IndexOf('=');
				if (split <= 0 || !float.TryParse(entry.Substring(split + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
				{
					continue;
				}
				switch (entry.Substring(0, split))
				{
				case "h":
					H = Mathf.Clamp01(v);
					break;
				case "s":
					S = Mathf.Clamp01(v);
					break;
				case "v":
					V = Mathf.Clamp01(v);
					break;
				case "base":
					ShowBase = v != 0f;
					break;
				}
			}
			Version++;
		}

		public static void Save(Player player)
		{
			Version++;
			if (player == null)
			{
				return;
			}
			player.m_customData[SaveKey] = "h=" + H.ToString("0.####", CultureInfo.InvariantCulture) + ";s=" + S.ToString("0.####", CultureInfo.InvariantCulture) + ";v=" + V.ToString("0.####", CultureInfo.InvariantCulture) + ";base=" + (ShowBase ? "1" : "0");
		}

		private static bool IsMannequinGhost(Player player)
		{
			GameObject ghost = (player != null) ? player.m_placementGhost : null;
			return ghost != null && ghost.GetComponent<MannequinTint>() != null;
		}

		// The placed mannequin under the crosshair while a mannequin piece is the preview.
		[HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
		[HarmonyPostfix]
		private static void UpdatePlacementGhost_Postfix(Player __instance)
		{
			if (__instance != Player.m_localPlayer)
			{
				return;
			}
			try
			{
				MannequinTint target = null;
				if (IsMannequinGhost(__instance) && __instance.InPlaceMode() && __instance.PieceRayTest(out Vector3 _, out Vector3 _, out Piece piece, out Heightmap _, out Collider _, water: false) && piece != null)
				{
					MannequinTint tint = piece.GetComponentInParent<MannequinTint>();
					if (tint != null && tint.IsPlaced)
					{
						target = tint;
					}
				}
				if (target != s_target)
				{
					ClearTarget();
					if (target != null)
					{
						s_target = target;
						__instance.Message(MessageHud.MessageType.Center, Text("potatowright_recolor", "Recolor"));
					}
				}
				if (s_target != null)
				{
					s_target.SetPreview(Current);
					HideGhost(__instance.m_placementGhost);
				}
			}
			catch (Exception ex)
			{
				ClearTarget();
				Debug.LogWarning(Plugin.Tag + " Mannequin recolor check failed: " + ex.Message);
			}
		}

		private static void HideGhost(GameObject ghost)
		{
			s_renderers.Clear();
			ghost.GetComponentsInChildren(s_renderers);
			foreach (Renderer r in s_renderers)
			{
				if (!r.forceRenderingOff)
				{
					r.forceRenderingOff = true;
					s_hidden.Add(r);
				}
			}
			s_renderers.Clear();
		}

		private static void ClearTarget()
		{
			if (s_target != null)
			{
				s_target.SetPreview(null);
			}
			s_target = null;
			foreach (Renderer r in s_hidden)
			{
				if (r != null)
				{
					r.forceRenderingOff = false;
				}
			}
			s_hidden.Clear();
		}

		// Recolouring needs none of the mannequin's materials: the game checks them before TryPlacePiece.
		[HarmonyPatch(typeof(Player), "HaveRequirements", new Type[2]
		{
			typeof(Piece),
			typeof(Player.RequirementMode)
		})]
		[HarmonyPrefix]
		private static bool HaveRequirements_Prefix(Player __instance, Piece piece, Player.RequirementMode mode, ref bool __result)
		{
			if (s_target == null || mode != Player.RequirementMode.CanBuild || __instance != Player.m_localPlayer || piece == null || piece != __instance.GetSelectedPiece() || piece.GetComponent<MannequinTint>() == null)
			{
				return true;
			}
			__result = true;
			return false;
		}

		[HarmonyPatch(typeof(Player), "TryPlacePiece")]
		[HarmonyPrefix]
		private static bool TryPlacePiece_Prefix(Player __instance, ref bool __result)
		{
			try
			{
				if (s_target == null || __instance != Player.m_localPlayer || !IsMannequinGhost(__instance))
				{
					return true;
				}
				__result = false;
				if (!PrivateArea.CheckAccess(s_target.transform.position, 0f, flash: true))
				{
					__instance.Message(MessageHud.MessageType.Center, "$msg_privatezone");
					return false;
				}
				s_target.Send(Current);
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning(Plugin.Tag + " Mannequin recolor failed: " + ex.Message);
				__result = false;
				return false;
			}
		}

		public static string Text(string key, string fallback)
		{
			string text = Localization.instance.Localize("$" + key);
			return (string.IsNullOrEmpty(text) || text.Contains(key)) ? fallback : text;
		}
	}

	// On every mannequin piece (the placing preview included): colours the wood layer and keeps the paint body's mask in
	// step with its paint. A placed one follows its ZDO colour, checked once a second (only reread when the ZDO
	// changed); the preview follows the panel. The paint mask is rebuilt when the paint changes (VisEquipment's chest or
	// legs, the snap preview), from a cache per pair of paint textures.
	[HarmonyPatch]
	public sealed class MannequinTint : MonoBehaviour
	{
		private static readonly int MainTex = Shader.PropertyToID("_MainTex");

		private static readonly int ChestTex = Shader.PropertyToID("_ChestTex");

		private static readonly int LegsTex = Shader.PropertyToID("_LegsTex");

		private static readonly string[] BaseParts = new string[2] { "Blocks & Switches/block base", "Blocks & Switches/block pose" };

		private void ShowBase(bool show)
		{
			if (m_base == null || m_baseShown == show)
			{
				return;
			}
			m_baseShown = show;
			foreach (Renderer r in m_base)
			{
				if (r != null)
				{
					r.enabled = show;
				}
			}
		}

		private bool m_init;

		private Renderer m_body;

		private Renderer m_layer;

		// The stand's wooden base and the pose switch's block ("Blocks & Switches/block base", ".../block pose").
		private Renderer[] m_base;

		private bool? m_baseShown;

		private ZNetView m_nview;

		private Texture m_wood;

		private Texture m_cutOut;

		private bool m_placed;

		private bool m_applied;

		private Color m_color = Color.white;

		private bool m_live;

		private string m_key;

		private string m_maskKey;

		private Color? m_preview;

		private Color m_held;

		private float m_holdUntil;

		private uint m_revision = uint.MaxValue;

		private int m_version = -1;

		public bool IsPlaced => m_placed && m_nview != null && m_nview.IsValid();

		private bool Init()
		{
			if (m_init)
			{
				return m_layer != null;
			}
			m_init = true;
			ArmorStand stand = GetComponent<ArmorStand>();
			VisEquipment vis = (stand != null) ? stand.m_visEquipment : null;
			m_body = (vis != null) ? vis.m_bodyModel : null;
			Transform layer = (m_body != null) ? m_body.transform.parent.Find(MannequinPieces.WoodLayerName) : null;
			m_layer = (layer != null) ? layer.GetComponent<Renderer>() : null;
			Material wood = (m_layer != null) ? m_layer.sharedMaterial : null;
			m_wood = (wood != null && wood.HasProperty(MainTex)) ? wood.GetTexture(MainTex) : null;
			Material paint = (m_body != null) ? m_body.sharedMaterial : null;
			m_cutOut = (paint != null && paint.HasProperty(MainTex)) ? paint.GetTexture(MainTex) : null;
			if (m_layer == null || m_wood == null)
			{
				m_layer = null;
				return false;
			}
			m_nview = (stand.m_netViewOverride != null) ? stand.m_netViewOverride : GetComponent<ZNetView>();
			List<Renderer> parts = new List<Renderer>();
			foreach (string path in BaseParts)
			{
				Transform part = transform.Find(path);
				Renderer r = (part != null) ? part.GetComponent<Renderer>() : null;
				if (r != null)
				{
					parts.Add(r);
				}
			}
			m_base = parts.ToArray();
			return true;
		}

		private void Start()
		{
			if (!Init())
			{
				return;
			}
			m_placed = m_nview != null && m_nview.IsValid();
			if (m_placed)
			{
				m_nview.Register<Vector3>(MannequinColor.RpcName, RPC_Tint);
			}
			MarkPaintDirty();
			Refresh(force: true);
			InvokeRepeating("Tick", UnityEngine.Random.Range(0.2f, 1f), m_placed ? 1f : 0.1f);
		}

		// Balrond's Weapon Crate (0.1.2) sends this up from a stand's figure when its preview paints the body, or paints the
		// stand's own item back (Unity SendMessageUpwards, no dependency either way).
		private void WeaponCrateStandPaintChanged()
		{
			MarkPaintDirty();
		}

		// The paint changed: the paint body's main texture becomes the mask of the new paint.
		public void MarkPaintDirty()
		{
			if (!Init() || m_body == null)
			{
				return;
			}
			Material paint = m_body.material;
			if (paint == null || !paint.HasProperty(MainTex))
			{
				return;
			}
			Texture chest = paint.HasProperty(ChestTex) ? paint.GetTexture(ChestTex) : null;
			Texture legs = paint.HasProperty(LegsTex) ? paint.GetTexture(LegsTex) : null;
			Texture mask = PaintMask.Acquire(chest, legs, out string key);
			if (m_maskKey != null)
			{
				PaintMask.Release(m_maskKey);
			}
			m_maskKey = key;
			paint.SetTexture(MainTex, (mask != null) ? mask : m_cutOut);
		}

		[HarmonyPatch(typeof(VisEquipment), "SetChestEquipped")]
		[HarmonyPostfix]
		private static void SetChestEquipped_Postfix(VisEquipment __instance, bool __result)
		{
			PaintChanged(__instance, __result);
		}

		[HarmonyPatch(typeof(VisEquipment), "SetLegEquipped")]
		[HarmonyPostfix]
		private static void SetLegEquipped_Postfix(VisEquipment __instance, bool __result)
		{
			PaintChanged(__instance, __result);
		}

		private static void PaintChanged(VisEquipment vis, bool changed)
		{
			// Stands' VisEquipment has m_isPlayer set too (m_isArmorStand tells them apart): only players are skipped.
			if (!changed || vis == null || (vis.m_isPlayer && !vis.m_isArmorStand))
			{
				return;
			}
			try
			{
				MannequinTint tint = vis.GetComponentInParent<MannequinTint>();
				if (tint != null)
				{
					tint.MarkPaintDirty();
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning(Plugin.Tag + " Mannequin paint mask not updated: " + ex.Message);
			}
		}

		private void Tick()
		{
			Refresh(force: false);
		}

		private void Refresh(bool force)
		{
			if (m_preview.HasValue)
			{
				Apply(m_preview.Value, live: true);
				return;
			}
			if (!m_placed)
			{
				if (force || m_version != MannequinColor.Version)
				{
					m_version = MannequinColor.Version;
					Apply(MannequinColor.Current, live: true);
					ShowBase(MannequinColor.ShowBase);
				}
				return;
			}
			if (m_nview == null || !m_nview.IsValid())
			{
				return;
			}
			if (Time.time < m_holdUntil)
			{
				Apply(m_held, live: false);
				return;
			}
			ZDO zdo = m_nview.GetZDO();
			if (!force && zdo.DataRevision == m_revision)
			{
				return;
			}
			m_revision = zdo.DataRevision;
			ShowBase(!zdo.GetBool(MannequinColor.NoBaseKey));
			Apply(MannequinColor.ToColor(zdo.GetVec3(MannequinColor.ColorKey, MannequinColor.ToVector(WoodTint.NaturalColor))), live: false);
		}

		// The panel's colour (preview, recolour preview) uses the shared live copy, rewritten as the sliders move; a
		// mannequin's own colour uses a cached copy per colour, released when no mannequin uses it any more.
		private void Apply(Color c, bool live)
		{
			if (m_layer == null || (m_applied && live == m_live && c == m_color && (live || m_key != null || WoodTint.IsNatural(c))))
			{
				return;
			}
			string key = null;
			Texture texture;
			if (WoodTint.IsNatural(c))
			{
				texture = m_wood;
			}
			else if (live)
			{
				texture = WoodTint.Live(m_wood, c);
			}
			else
			{
				texture = WoodTint.Acquire(m_wood, c, out key);
			}
			if (texture == null)
			{
				texture = m_wood;
				key = null;
			}
			if (m_key != null)
			{
				WoodTint.Release(m_key);
			}
			m_key = key;
			foreach (Material m in m_layer.materials)
			{
				if (m != null && m.HasProperty(MainTex))
				{
					m.SetTexture(MainTex, texture);
				}
			}
			m_color = c;
			m_live = live;
			m_applied = true;
		}

		// The recolour preview (null = back to the mannequin's own colour).
		public void SetPreview(Color? c)
		{
			m_preview = c;
			Refresh(force: true);
		}

		public void Send(Color c)
		{
			if (IsPlaced)
			{
				Plugin.Debug("Mannequin " + name + " recolored to " + ColorUtility.ToHtmlStringRGB(c));
				m_nview.InvokeRPC(ZNetView.Everybody, MannequinColor.RpcName, MannequinColor.ToVector(c));
			}
		}

		// On every client: the owner keeps the colour in the ZDO; everyone shows it at once and holds it a moment, until the
		// ZDO with the new colour has arrived.
		private void RPC_Tint(long sender, Vector3 v)
		{
			if (m_nview == null || !m_nview.IsValid())
			{
				return;
			}
			if (m_nview.IsOwner())
			{
				m_nview.GetZDO().Set(MannequinColor.ColorKey, v);
			}
			m_held = MannequinColor.ToColor(v);
			m_holdUntil = Time.time + 3f;
			m_revision = uint.MaxValue;
			if (!m_preview.HasValue)
			{
				Refresh(force: true);
			}
		}

		// The colour and mask copies are released, and the renderers' own material copies go with the mannequin.
		private void OnDestroy()
		{
			if (m_key != null)
			{
				WoodTint.Release(m_key);
				m_key = null;
			}
			if (m_maskKey != null)
			{
				PaintMask.Release(m_maskKey);
				m_maskKey = null;
			}
			DestroyInstances(m_layer);
			DestroyInstances(m_body);
		}

		private static void DestroyInstances(Renderer r)
		{
			if (r == null)
			{
				return;
			}
			foreach (Material m in r.sharedMaterials)
			{
				if (m != null && m.name.EndsWith(" (Instance)", StringComparison.Ordinal))
				{
					UnityEngine.Object.Destroy(m);
				}
			}
		}
	}

	// The paint body's main texture: white, with the paint's own coverage as alpha, max(_ChestTex.a, _LegsTex.a), read
	// back through the GPU (the game's compressed textures need no read access) at the larger paint texture's size (64 to
	// 512; assumes the paint uses the body's UVs, which test-t showed). One per pair of paint textures, reference counted,
	// destroyed when unused. The shader cuts the body out where the alpha is under _Cutoff (0.45).
	public static class PaintMask
	{
		private sealed class Entry
		{
			public Texture2D Texture;

			public int Refs;
		}

		private static readonly Dictionary<string, Entry> s_masks = new Dictionary<string, Entry>();

		public static Texture2D Acquire(Texture chest, Texture legs, out string key)
		{
			key = ((chest != null) ? chest.GetInstanceID() : 0) + ":" + ((legs != null) ? legs.GetInstanceID() : 0);
			if (s_masks.TryGetValue(key, out Entry entry) && entry.Texture != null)
			{
				entry.Refs++;
				return entry.Texture;
			}
			Texture2D texture = Make(chest, legs);
			if (texture == null)
			{
				key = null;
				return null;
			}
			s_masks[key] = new Entry
			{
				Texture = texture,
				Refs = 1
			};
			return texture;
		}

		public static void Release(string key)
		{
			if (key == null || !s_masks.TryGetValue(key, out Entry entry))
			{
				return;
			}
			entry.Refs--;
			if (entry.Refs <= 0)
			{
				s_masks.Remove(key);
				if (entry.Texture != null)
				{
					UnityEngine.Object.Destroy(entry.Texture);
				}
			}
		}

		private static Texture2D Make(Texture chest, Texture legs)
		{
			int size = 64;
			if (chest != null)
			{
				size = Mathf.Max(size, Mathf.Max(chest.width, chest.height));
			}
			if (legs != null)
			{
				size = Mathf.Max(size, Mathf.Max(legs.width, legs.height));
			}
			size = Mathf.Min(size, 512);
			try
			{
				Color32[] a = ReadAlpha(chest, size);
				Color32[] b = ReadAlpha(legs, size);
				Color32[] mask = new Color32[size * size];
				for (int i = 0; i < mask.Length; i++)
				{
					byte alpha = 0;
					if (a != null && a[i].a > alpha)
					{
						alpha = a[i].a;
					}
					if (b != null && b[i].a > alpha)
					{
						alpha = b[i].a;
					}
					mask[i] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, alpha);
				}
				Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true, linear: false)
				{
					name = "PotatoWright paint mask"
				};
				texture.SetPixels32(mask);
				texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
				return texture;
			}
			catch (Exception ex)
			{
				Debug.LogWarning(Plugin.Tag + " Mannequin paint mask not made: " + ex.Message);
				return null;
			}
		}

		private static Color32[] ReadAlpha(Texture source, int size)
		{
			if (source == null)
			{
				return null;
			}
			RenderTexture previous = RenderTexture.active;
			RenderTexture rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
			Texture2D read = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false, linear: true);
			try
			{
				Graphics.Blit(source, rt);
				RenderTexture.active = rt;
				read.ReadPixels(new Rect(0f, 0f, size, size), 0, 0, recalculateMipMaps: false);
				read.Apply(updateMipmaps: false, makeNoLongerReadable: false);
				return read.GetPixels32();
			}
			finally
			{
				RenderTexture.active = previous;
				RenderTexture.ReleaseTemporary(rt);
				UnityEngine.Object.Destroy(read);
			}
		}
	}

	// Coloured copies of the wood texture: the planks read back once (a GPU copy, so the game's compressed texture needs
	// no read access), each pixel multiplied by the colour on the CPU, uploaded with mipmaps. One copy per colour in use
	// (reference counted, destroyed when unused) plus one live copy per texture for the panel's colour.
	public static class WoodTint
	{
		private sealed class Entry
		{
			public Texture2D Texture;

			public int Refs;
		}

		private static readonly Dictionary<int, Color32[]> s_base = new Dictionary<int, Color32[]>();

		private static readonly Dictionary<string, Entry> s_copies = new Dictionary<string, Entry>();

		private static readonly Dictionary<int, Texture2D> s_live = new Dictionary<int, Texture2D>();

		private static readonly Dictionary<int, Color32> s_liveColor = new Dictionary<int, Color32>();

		private static Color32[] s_work;

		// The planks' average colour (Planks5c_low, read from the game files: 92, 69, 42), replaced by the average of the
		// texture actually read once it has been (MannequinPieces.WoodTexture).
		private static Color32? s_natural;

		public static Color NaturalColor
		{
			get
			{
				if (!s_natural.HasValue && MannequinPieces.WoodTexture != null)
				{
					Base(MannequinPieces.WoodTexture);
				}
				return s_natural.HasValue ? (Color)s_natural.Value : new Color(92f / 255f, 69f / 255f, 42f / 255f, 1f);
			}
		}

		// The natural colour, to within one step per channel (the panel's sliders round-trip through HSV).
		public static bool IsNatural(Color c)
		{
			Color32 c32 = c;
			Color32 n = NaturalColor;
			return Mathf.Abs(c32.r - n.r) <= 1 && Mathf.Abs(c32.g - n.g) <= 1 && Mathf.Abs(c32.b - n.b) <= 1;
		}

		public static Texture2D Acquire(Texture source, Color c, out string key)
		{
			Color32 c32 = c;
			key = source.GetInstanceID() + ":" + c32.r + "," + c32.g + "," + c32.b;
			if (s_copies.TryGetValue(key, out Entry entry) && entry.Texture != null)
			{
				entry.Refs++;
				return entry.Texture;
			}
			Texture2D texture = Make(source, "PotatoWright wood " + ColorUtility.ToHtmlStringRGB(c));
			if (texture == null || !Fill(texture, source, c32, readable: false))
			{
				key = null;
				return null;
			}
			s_copies[key] = new Entry
			{
				Texture = texture,
				Refs = 1
			};
			return texture;
		}

		public static void Release(string key)
		{
			if (key == null || !s_copies.TryGetValue(key, out Entry entry))
			{
				return;
			}
			entry.Refs--;
			if (entry.Refs <= 0)
			{
				s_copies.Remove(key);
				if (entry.Texture != null)
				{
					UnityEngine.Object.Destroy(entry.Texture);
				}
			}
		}

		public static Texture2D Live(Texture source, Color c)
		{
			int id = source.GetInstanceID();
			Color32 c32 = c;
			if (!s_live.TryGetValue(id, out Texture2D texture) || texture == null)
			{
				texture = Make(source, "PotatoWright wood (panel)");
				if (texture == null)
				{
					return null;
				}
				s_live[id] = texture;
				s_liveColor.Remove(id);
			}
			if (!s_liveColor.TryGetValue(id, out Color32 had) || had.r != c32.r || had.g != c32.g || had.b != c32.b)
			{
				if (!Fill(texture, source, c32, readable: true))
				{
					return null;
				}
				s_liveColor[id] = c32;
			}
			return texture;
		}

		private static Texture2D Make(Texture source, string name)
		{
			if (Base(source) == null)
			{
				return null;
			}
			return new Texture2D(source.width, source.height, TextureFormat.RGBA32, mipChain: true, linear: false)
			{
				name = name,
				wrapMode = source.wrapMode,
				filterMode = source.filterMode,
				anisoLevel = source.anisoLevel
			};
		}

		// Each pixel moved from the planks' average colour to the chosen one, keeping the grain: hue shifted by the
		// difference, saturation and brightness scaled by the ratio (clamped). The average itself maps to the chosen colour.
		private static bool Fill(Texture2D texture, Texture source, Color32 c, bool readable)
		{
			Color32[] pixels = Base(source);
			if (pixels == null)
			{
				return false;
			}
			if (s_work == null || s_work.Length != pixels.Length)
			{
				s_work = new Color32[pixels.Length];
			}
			Color.RGBToHSV(NaturalColor, out float nh, out float ns, out float nv);
			Color.RGBToHSV(c, out float th, out float ts, out float tv);
			float dh = th - nh;
			float ks = (ns > 0.001f) ? (ts / ns) : 0f;
			float kv = (nv > 0.001f) ? (tv / nv) : 0f;
			for (int i = 0; i < pixels.Length; i++)
			{
				Color32 p = pixels[i];
				Color.RGBToHSV(p, out float h, out float sat, out float val);
				h += dh;
				h -= Mathf.Floor(h);
				Color o = Color.HSVToRGB(h, Mathf.Clamp01((ns > 0.001f) ? (sat * ks) : ts), Mathf.Clamp01(val * kv));
				Color32 o32 = o;
				o32.a = p.a;
				s_work[i] = o32;
			}
			texture.SetPixels32(s_work);
			texture.Apply(updateMipmaps: true, makeNoLongerReadable: !readable);
			return true;
		}

		// The source's pixels (sRGB, as stored), copied through a temporary render target and read back once.
		private static Color32[] Base(Texture source)
		{
			int id = source.GetInstanceID();
			if (s_base.TryGetValue(id, out Color32[] pixels))
			{
				return pixels;
			}
			RenderTexture previous = RenderTexture.active;
			RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
			Texture2D read = null;
			try
			{
				Graphics.Blit(source, rt);
				RenderTexture.active = rt;
				read = new Texture2D(source.width, source.height, TextureFormat.RGBA32, mipChain: false, linear: false);
				read.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, recalculateMipMaps: false);
				read.Apply(updateMipmaps: false, makeNoLongerReadable: false);
				pixels = read.GetPixels32();
				s_base[id] = pixels;
				if (source == MannequinPieces.WoodTexture && pixels.Length > 0)
				{
					long r = 0;
					long g = 0;
					long b = 0;
					foreach (Color32 p in pixels)
					{
						r += p.r;
						g += p.g;
						b += p.b;
					}
					s_natural = new Color32((byte)(r / pixels.Length), (byte)(g / pixels.Length), (byte)(b / pixels.Length), byte.MaxValue);
					Plugin.Debug("Mannequin wood average #" + ColorUtility.ToHtmlStringRGB((Color)s_natural.Value));
				}
				Plugin.Debug("Mannequin wood " + source.name + " read: " + source.width + "x" + source.height);
				return pixels;
			}
			catch (Exception ex)
			{
				Debug.LogWarning(Plugin.Tag + " Mannequin wood texture not read, colors stay off: " + ex.Message);
				s_base[id] = null;
				return null;
			}
			finally
			{
				RenderTexture.active = previous;
				RenderTexture.ReleaseTemporary(rt);
				if (read != null)
				{
					UnityEngine.Object.Destroy(read);
				}
			}
		}
	}
}
