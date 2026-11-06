using System;
using System.Globalization;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace PotatoWright
{
	// The mannequin colour panel (the wood colour itself: starts at the planks' own colour, "Natural wood" goes back to it;
	// the swatch and its hex code show it): beside the hammer's build menu (Valheim 1.0's BuildUi, Hud.m_buildUi) while it is open
	// with a mannequin piece selected. Hue, saturation and brightness sliders over strips of the colours they reach, a
	// swatch, and a "Natural wood" button. Built the way the Potato mods' panels are, from the game's own parts (this mod
	// has no Jötunn): the build window's background sprite, a copy of the piece description's text, Unity's default slider
	// and button with the game's UI sprites (UIAtlas: "checkbox_marker" for the handle, "button" for the button).
	[HarmonyPatch]
	public static class MannequinPanel
	{
		private sealed class Row
		{
			public TMP_Text Label;

			public TMP_Text Value;

			public Slider Slider;

			public Texture2D Strip;
		}

		private const int StripWidth = 64;

		private static GameObject s_panel;

		private static Hud s_owner;

		private static readonly Row[] s_rows = new Row[3];

		private static Image s_swatch;

		private static TMP_Text s_hex;

		private static TMP_Text s_baseText;

		private static bool s_syncing;

		private static bool s_failed;

		private static readonly Color ButtonNormal = new Color(0.824f, 0.824f, 0.824f, 1f);

		[HarmonyPatch(typeof(Hud), "Update")]
		[HarmonyPostfix]
		private static void Hud_Update_Postfix(Hud __instance)
		{
			Player player = Player.m_localPlayer;
			if (player == null)
			{
				return;
			}
			MannequinColor.EnsureLoaded(player);
			if (Hud.IsPieceSelectionVisible())
			{
				MannequinPieces.EnsureEmoteIcons();
			}
			bool open = !s_failed && player.InPlaceMode() && Hud.IsPieceSelectionVisible() && SelectedIsMannequin(player);
			if (!open)
			{
				if (s_panel != null && s_panel.activeSelf)
				{
					s_panel.SetActive(false);
				}
				return;
			}
			try
			{
				if (s_panel == null || s_owner != __instance)
				{
					Build(__instance);
				}
				if (s_panel != null && !s_panel.activeSelf)
				{
					s_panel.SetActive(true);
					Show();
					Fit();
				}
			}
			catch (Exception ex)
			{
				s_failed = true;
				Debug.LogWarning(Plugin.Tag + " Mannequin color panel is off this session: " + ex);
				if (s_panel != null)
				{
					UnityEngine.Object.Destroy(s_panel);
				}
				s_panel = null;
			}
		}

		private static bool SelectedIsMannequin(Player player)
		{
			Piece piece = player.GetSelectedPiece();
			return piece != null && piece.GetComponent<MannequinTint>() != null;
		}

		private static void Show()
		{
			s_syncing = true;
			s_rows[0].Slider.value = MannequinColor.H;
			s_rows[1].Slider.value = MannequinColor.S;
			s_rows[2].Slider.value = MannequinColor.V;
			s_syncing = false;
			Refresh();
		}

		private static void Changed()
		{
			if (s_syncing)
			{
				return;
			}
			MannequinColor.H = s_rows[0].Slider.value;
			MannequinColor.S = s_rows[1].Slider.value;
			MannequinColor.V = s_rows[2].Slider.value;
			MannequinColor.Save(Player.m_localPlayer);
			Refresh();
		}

		private static void NaturalWood()
		{
			MannequinColor.Natural();
			MannequinColor.Save(Player.m_localPlayer);
			Show();
		}

		private static void Refresh()
		{
			float h = MannequinColor.H;
			float s = MannequinColor.S;
			float v = MannequinColor.V;
			s_rows[0].Value.text = Mathf.RoundToInt(h * 360f).ToString(CultureInfo.InvariantCulture) + "°";
			s_rows[1].Value.text = Mathf.RoundToInt(s * 100f).ToString(CultureInfo.InvariantCulture) + "%";
			s_rows[2].Value.text = Mathf.RoundToInt(v * 100f).ToString(CultureInfo.InvariantCulture) + "%";
			s_swatch.color = MannequinColor.Current;
			s_hex.text = "#" + ColorUtility.ToHtmlStringRGB(MannequinColor.Current);
			if (s_baseText != null)
			{
				s_baseText.text = MannequinColor.ShowBase ? MannequinColor.Text("potatowright_base_on", "On") : MannequinColor.Text("potatowright_base_off", "Off");
			}
			// Each strip shows what moving its slider does: every hue, then grey to full colour, then dark to light.
			Paint(s_rows[0], (float x) => Color.HSVToRGB(x, 1f, 1f));
			Paint(s_rows[1], (float x) => Color.HSVToRGB(h, x, Mathf.Max(v, 0.35f)));
			Paint(s_rows[2], (float x) => Color.HSVToRGB(h, s, x));
		}

		private static void Paint(Row row, Func<float, Color> at)
		{
			for (int x = 0; x < StripWidth; x++)
			{
				row.Strip.SetPixel(x, 0, at(x / (StripWidth - 1f)));
			}
			row.Strip.Apply(false);
		}

		private static void Build(Hud hud)
		{
			if (s_panel != null)
			{
				UnityEngine.Object.Destroy(s_panel);
			}
			s_panel = null;
			s_owner = hud;
			RectTransform ui = (hud.m_buildUi != null) ? (hud.m_buildUi.transform as RectTransform) : null;
			TMP_Text textSource = hud.m_pieceDescription;
			if (ui == null || textSource == null)
			{
				throw new InvalidOperationException("the build menu or its description text is missing");
			}
			// The window: the build menu's largest picture (its wood background), unless that fills the screen.
			Canvas canvas = ui.GetComponentInParent<Canvas>();
			canvas = (canvas != null) ? canvas.rootCanvas : null;
			Image source = null;
			float best = 0f;
			foreach (Image image in ui.GetComponentsInChildren<Image>(true))
			{
				if (image.sprite == null || image.GetComponentInParent<Selectable>() != null)
				{
					continue;
				}
				Rect r = ((RectTransform)image.transform).rect;
				if (r.width * r.height > best)
				{
					best = r.width * r.height;
					source = image;
				}
			}
			RectTransform window = ui;
			if (source != null && canvas != null && ((RectTransform)source.transform).rect.width < ((RectTransform)canvas.transform).rect.width * 0.8f)
			{
				window = (RectTransform)source.transform;
			}
			const float width = 340f;
			const float pad = 16f;
			const float labelWidth = 104f;
			const float valueWidth = 54f;
			const float rowHeight = 34f;
			float height = pad + 34f + rowHeight * 3f + 50f + 44f + pad;
			s_panel = new GameObject("PotatoWright_MannequinColor", typeof(RectTransform));
			s_panel.layer = ui.gameObject.layer;
			RectTransform rect = (RectTransform)s_panel.transform;
			rect.SetParent(window, false);
			rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
			rect.pivot = new Vector2(0f, 1f);
			rect.anchoredPosition = new Vector2(12f, 0f);
			rect.sizeDelta = new Vector2(width, height);
			Image background = s_panel.AddComponent<Image>();
			if (source != null)
			{
				background.sprite = source.sprite;
				background.type = source.type;
				background.color = source.color;
				background.material = source.material;
				background.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
			}
			else
			{
				background.color = new Color(0f, 0f, 0f, 0.75f);
			}
			// Clicks on the panel don't fall through to what closes the menu.
			background.raycastTarget = true;
			SpriteAtlas atlas = FindAtlas("UIAtlas");
			DefaultControls.Resources resources = default(DefaultControls.Resources);
			resources.knob = (atlas != null) ? atlas.GetSprite("checkbox_marker") : null;
			resources.standard = (atlas != null) ? atlas.GetSprite("button") : null;
			float y = pad;
			TMP_Text title = Text(textSource, "Title", pad, y, width - pad * 2f, 30f);
			title.alignment = TextAlignmentOptions.Center;
			title.fontSize = textSource.fontSize * 1.2f;
			title.text = MannequinColor.Text("potatowright_mannequin_color", "Mannequin color");
			y += 34f;
			string[] names = new string[3]
			{
				MannequinColor.Text("potatowright_hue", "Hue"),
				MannequinColor.Text("potatowright_saturation", "Saturation"),
				MannequinColor.Text("potatowright_brightness", "Brightness")
			};
			for (int i = 0; i < 3; i++)
			{
				Row row = new Row();
				row.Label = Text(textSource, "Label" + i, pad, y, labelWidth, 26f);
				row.Label.alignment = TextAlignmentOptions.Left;
				row.Label.text = names[i];
				GameObject sliderObject = DefaultControls.CreateSlider(resources);
				sliderObject.name = "PotatoWright_Slider" + i;
				foreach (Transform t in sliderObject.GetComponentsInChildren<Transform>(true))
				{
					t.gameObject.layer = s_panel.layer;
				}
				sliderObject.transform.SetParent(rect, false);
				Slider slider = sliderObject.GetComponent<Slider>();
				slider.minValue = 0f;
				slider.maxValue = 1f;
				slider.wholeNumbers = false;
				if (slider.handleRect != null)
				{
					slider.handleRect.sizeDelta = new Vector2(40f, 10f);
					Image knob = slider.handleRect.GetComponent<Image>();
					if (knob != null)
					{
						knob.color = Color.white;
					}
					RectTransform area = slider.handleRect.parent as RectTransform;
					if (area != null)
					{
						area.offsetMin = new Vector2(5f, area.offsetMin.y);
						area.offsetMax = new Vector2(-5f, area.offsetMax.y);
					}
				}
				Place((RectTransform)sliderObject.transform, pad + labelWidth + 4f, y + 4f, width - pad * 2f - labelWidth - 8f - valueWidth, 18f);
				row.Slider = slider;
				Strip(row);
				slider.onValueChanged.AddListener(delegate
				{
					Changed();
				});
				row.Value = Text(textSource, "Value" + i, width - pad - valueWidth, y, valueWidth, 26f);
				row.Value.alignment = TextAlignmentOptions.Right;
				s_rows[i] = row;
				y += rowHeight;
			}
			GameObject swatchObject = new GameObject("PotatoWright_Swatch", typeof(RectTransform));
			swatchObject.layer = s_panel.layer;
			swatchObject.transform.SetParent(rect, false);
			Place((RectTransform)swatchObject.transform, pad, y + 8f, 40f, 30f);
			s_swatch = swatchObject.AddComponent<Image>();
			s_swatch.raycastTarget = false;
			s_hex = Text(textSource, "Hex", pad + 48f, y + 10f, 104f, 26f);
			s_hex.alignment = TextAlignmentOptions.Left;
			MakeButton(ui, rect, resources, textSource, "PotatoWright_NaturalWood", width - pad - 150f, y + 6f, 150f, NaturalWood).text = MannequinColor.Text("potatowright_natural_wood", "Natural wood");
			y += 50f;
			// The Base switch: new mannequins with or without their wooden base.
			TMP_Text baseLabel = Text(textSource, "BaseLabel", pad, y + 8f, labelWidth, 26f);
			baseLabel.alignment = TextAlignmentOptions.Left;
			baseLabel.text = MannequinColor.Text("potatowright_base", "Base");
			s_baseText = MakeButton(ui, rect, resources, textSource, "PotatoWright_Base", width - pad - 150f, y + 4f, 150f, ToggleBase);
			s_panel.SetActive(false);
		}

		// A button the way the game's look: UIAtlas "button", Valheim's colours, the build menu's click sound, a TMP label
		// copied from the piece description (returned).
		private static TMP_Text MakeButton(RectTransform ui, RectTransform parent, DefaultControls.Resources resources, TMP_Text textSource, string name, float x, float y, float w, UnityEngine.Events.UnityAction onClick)
		{
			GameObject button = DefaultControls.CreateButton(resources);
			button.name = name;
			foreach (Transform t in button.GetComponentsInChildren<Transform>(true))
			{
				t.gameObject.layer = s_panel.layer;
			}
			foreach (Text legacy in button.GetComponentsInChildren<Text>(true))
			{
				UnityEngine.Object.Destroy(legacy.gameObject);
			}
			button.transform.SetParent(parent, false);
			Place((RectTransform)button.transform, x, y, w, 36f);
			Button b = button.GetComponent<Button>();
			ColorBlock colors = b.colors;
			colors.normalColor = ButtonNormal;
			colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
			colors.pressedColor = new Color(0.537f, 0.556f, 0.556f, 1f);
			colors.selectedColor = ButtonNormal;
			colors.disabledColor = new Color(0.566f, 0.566f, 0.566f, 0.502f);
			colors.colorMultiplier = 1f;
			colors.fadeDuration = 0.1f;
			b.colors = colors;
			ButtonSfx sfx = ui.GetComponentInChildren<ButtonSfx>(true);
			if (sfx != null && sfx.m_sfxPrefab != null)
			{
				button.AddComponent<ButtonSfx>().m_sfxPrefab = sfx.m_sfxPrefab;
			}
			b.onClick.AddListener(onClick);
			TMP_Text label = Text(textSource, name + "Label", 0f, 0f, w, 36f);
			label.transform.SetParent(button.transform, false);
			RectTransform lr = (RectTransform)label.transform;
			lr.anchorMin = Vector2.zero;
			lr.anchorMax = Vector2.one;
			lr.offsetMin = lr.offsetMax = Vector2.zero;
			label.alignment = TextAlignmentOptions.Center;
			return label;
		}

		private static void ToggleBase()
		{
			MannequinColor.ShowBase = !MannequinColor.ShowBase;
			MannequinColor.Save(Player.m_localPlayer);
			Refresh();
		}

		private static SpriteAtlas FindAtlas(string name)
		{
			foreach (SpriteAtlas atlas in Resources.FindObjectsOfTypeAll<SpriteAtlas>())
			{
				if (atlas != null && atlas.name == name)
				{
					return atlas;
				}
			}
			return null;
		}

		// Colour sliders: the track and fill give way to a strip of the colours they reach; the handle stays.
		private static void Strip(Row row)
		{
			Slider slider = row.Slider;
			RectTransform handle = slider.handleRect;
			foreach (Image part in slider.GetComponentsInChildren<Image>(true))
			{
				if (handle == null || !part.transform.IsChildOf(handle))
				{
					part.enabled = false;
				}
			}
			row.Strip = new Texture2D(StripWidth, 1, TextureFormat.RGBA32, false)
			{
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear,
				name = "PotatoWright strip"
			};
			GameObject holder = new GameObject("PotatoWright_Strip", typeof(RectTransform));
			holder.layer = s_panel.layer;
			RectTransform r = (RectTransform)holder.transform;
			r.SetParent(slider.transform, false);
			r.SetAsFirstSibling();
			r.anchorMin = new Vector2(0f, 0.2f);
			r.anchorMax = new Vector2(1f, 0.8f);
			r.offsetMin = r.offsetMax = Vector2.zero;
			Image strip = holder.AddComponent<Image>();
			strip.sprite = Sprite.Create(row.Strip, new Rect(0f, 0f, StripWidth, 1f), new Vector2(0.5f, 0.5f));
			strip.raycastTarget = true;
		}

		// Keep the whole panel on screen.
		private static void Fit()
		{
			RectTransform rect = (RectTransform)s_panel.transform;
			rect.anchoredPosition = new Vector2(12f, 0f);
			Canvas canvas = rect.GetComponentInParent<Canvas>();
			canvas = (canvas != null) ? canvas.rootCanvas : null;
			if (canvas == null)
			{
				return;
			}
			Canvas.ForceUpdateCanvases();
			RectTransform space = (RectTransform)canvas.transform;
			Vector3[] corners = new Vector3[4];
			rect.GetWorldCorners(corners);
			float minX = float.MaxValue;
			float maxX = float.MinValue;
			float minY = float.MaxValue;
			float maxY = float.MinValue;
			foreach (Vector3 corner in corners)
			{
				Vector3 local = space.InverseTransformPoint(corner);
				minX = Mathf.Min(minX, local.x);
				maxX = Mathf.Max(maxX, local.x);
				minY = Mathf.Min(minY, local.y);
				maxY = Mathf.Max(maxY, local.y);
			}
			Rect bounds = space.rect;
			float dx = 0f;
			float dy = 0f;
			if (maxX > bounds.xMax)
			{
				dx = bounds.xMax - maxX;
			}
			if (minX + dx < bounds.xMin)
			{
				dx = bounds.xMin - minX;
			}
			if (minY < bounds.yMin)
			{
				dy = bounds.yMin - minY;
			}
			if (maxY + dy > bounds.yMax)
			{
				dy = bounds.yMax - maxY;
			}
			if (dx != 0f || dy != 0f)
			{
				Vector3 shift = rect.parent.InverseTransformVector(space.TransformVector(new Vector3(dx, dy, 0f)));
				rect.anchoredPosition += new Vector2(shift.x, shift.y);
			}
		}

		// A copy of the piece description's text (the game's font and look), emptied.
		private static TMP_Text Text(TMP_Text source, string name, float x, float y, float w, float h)
		{
			GameObject copy = UnityEngine.Object.Instantiate(source.gameObject, s_panel.transform, false);
			copy.name = "PotatoWright_" + name;
			foreach (Localize localize in copy.GetComponentsInChildren<Localize>(true))
			{
				UnityEngine.Object.Destroy(localize);
			}
			foreach (LayoutElement layout in copy.GetComponents<LayoutElement>())
			{
				UnityEngine.Object.Destroy(layout);
			}
			foreach (ContentSizeFitter fitter in copy.GetComponents<ContentSizeFitter>())
			{
				UnityEngine.Object.Destroy(fitter);
			}
			foreach (Transform child in copy.transform)
			{
				UnityEngine.Object.Destroy(child.gameObject);
			}
			TMP_Text text = copy.GetComponent<TMP_Text>();
			text.text = "";
			text.textWrappingMode = TextWrappingModes.NoWrap;
			text.overflowMode = TextOverflowModes.Ellipsis;
			text.raycastTarget = false;
			Place((RectTransform)copy.transform, x, y, w, h);
			return text;
		}

		private static void Place(RectTransform rect, float x, float y, float w, float h)
		{
			rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
			rect.pivot = new Vector2(0f, 1f);
			rect.anchoredPosition = new Vector2(x, -y);
			rect.sizeDelta = new Vector2(w, h);
		}
	}
}
