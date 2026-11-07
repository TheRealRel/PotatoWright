// The pieces hung on a rope or chain (the user's choices, 2026-11-06), placed by aiming at a placed rope or chain:
// - Lanterns: five of the game's lanterns, each on a short iron chain with one real light that switches off at a
//   distance (LightLod, like every lamp in the game; "Lanterns light, strings glow"): the Dvergr lantern (the item's
//   body, lit material, piece_dvergr_lantern's light and glow), the hooded lantern (piece_hoodedlantern's model, its
//   beam turned out from the rope and tipped down), the Deep North lantern (Lantern_DN's model and light), the snow
//   lantern (piece_snowlantern's model, candle flame and light) and the lava lantern (piece_Lavalantern's model with its
//   moving lava and light). Test-b: "The lantern was a big win. Multiple types would be great."
// - Hanging brazier: the game's hanging brazier (piece_brazierceiling01) as it is, burning coal, warming and smoking;
//   hung by the top of its chains (the user's choice: "Like the game's").
// - Drying herbs: three thistles and two dandelions tied at the stems, heads down.
// - Pennant bunting: triangle flags along the whole rope, each with a rune of the Elder Futhark in turn (PotatoPainter's
//   rune drawings, painted dark on light cloth and light on dark, into the flag's own picture so it moves with the cloth).
//   Two looks, switched with the mouse wheel while placing: plain cloth (the weave of the game's banner cloth, dyed in
//   the banners' colours) or the banners' own cloth and patterns. Both on the banners' cloth shader (two-sided).
// - Test builds only: the drying fish and the fey lights of test-a and test-b (the user: "Remove the fishes and fairy
//   lights"), kept as prefabs only so the ones placed in the Tester world load and can be removed; they go in the
//   release.
// Saved in worlds: never rename the prefabs.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace PotatoWright
{
    internal static class HangPieces
    {
        internal const int Lantern = 0, Fish = 1, Herbs = 2, Bunting = 3, Fey = 4, LanternHooded = 5, LanternDeepNorth = 6, LanternSnow = 7, LanternLava = 8, Brazier = 9;

        private const float Chain = 0.16f;

        // The game's banner colours, read from each banner's picture (main colour of its cloth): red, yellow, blue,
        // white, green, orange, purple, black (banners 04, 08, 02, 11, 05, 10, 09, 01).
        private static readonly Color32[] Palette =
        {
            new Color32(171, 12, 1, 255), new Color32(186, 146, 0, 255), new Color32(5, 68, 151, 255), new Color32(210, 210, 210, 255),
            new Color32(33, 112, 16, 255), new Color32(209, 110, 8, 255), new Color32(138, 67, 188, 255), new Color32(45, 45, 45, 255),
        };
        // Banner cloth order along the string (piece_bannerNN), and whether its main colour is dark (light rune) or light
        // (dark rune), from each banner's main colour.
        private static readonly int[] ClothOrder = { 4, 8, 2, 11, 5, 10, 9, 1, 3, 6, 7 };
        private static readonly bool[] ClothDark = { true, false, true, false, true, false, true, true, false, true, false };

        // The Elder Futhark in its order (PotatoPainter's drawings, assets/runes/elder_<name>.png).
        private static readonly string[] Futhark =
        {
            "fehu", "uruz", "thurisaz", "ansuz", "raido", "kauna", "gebo", "wunjo",
            "hagalaz", "naudiz", "isaz", "jera", "ihwaz", "pertho", "algiz", "sowilo",
            "tiwaz", "berkanan", "ehwaz", "mannaz", "laguz", "ingwaz", "dagaz", "othala",
        };
        private static readonly Color RuneDarkColor = new Color(0.16f, 0.12f, 0.09f), RuneLightColor = new Color(0.93f, 0.88f, 0.78f);

        // One material per style (the sheet of its flag pictures).
        private static Material[] s_plain, s_cloth;
        internal static GameObject FeyTemplate;

        internal static Material[] BuntingMaterials(int style) => style == 0 ? s_plain : s_cloth;

        internal static void Create()
        {
            if (!GUIManager.IsHeadless())
            {
                Flags();
                if (Plugin.TestBuild)
                {
                    GameObject garland = PrefabManager.Instance.GetPrefab("piece_FairylightGarland");
                    Transform ps = garland ? garland.transform.Find("Particle System") : null;
                    FeyTemplate = ps ? ps.gameObject : null;
                }
            }
            AddSpot("PotatoWright_HangLantern", "piece_potatowright_hang_lantern", Lantern, "forge", new[] { new RequirementConfig("Bronze", 1, 0, true), new RequirementConfig("Resin", 2, 0, true) });
            AddCopied("PotatoWright_HangLanternHooded", "piece_potatowright_hang_lantern_hooded", LanternHooded, "piece_hoodedlantern", null);
            AddCopied("PotatoWright_HangLanternDeepNorth", "piece_potatowright_hang_lantern_deepnorth", LanternDeepNorth, null, "Lantern_DN");
            AddCopied("PotatoWright_HangLanternSnow", "piece_potatowright_hang_lantern_snow", LanternSnow, "piece_snowlantern", null);
            AddCopied("PotatoWright_HangLanternLava", "piece_potatowright_hang_lantern_lava", LanternLava, "piece_Lavalantern", null);
            AddBrazier();
            AddSpot("PotatoWright_HangHerbs", "piece_potatowright_hang_herbs", Herbs, "piece_workbench", new[] { new RequirementConfig("Thistle", 1, 0, true), new RequirementConfig("Dandelion", 2, 0, true) });
            AddSpot("PotatoWright_HangBunting", "piece_potatowright_bunting", Bunting, "piece_workbench", new[] { new RequirementConfig("LeatherScraps", 2, 0, true), new RequirementConfig("Raspberry", 2, 0, true) });
            if (Plugin.TestBuild)
            {
                AddSpot("PotatoWright_HangFish", "piece_potatowright_hang_fish", Fish, "piece_workbench", new[] { new RequirementConfig("FishRaw", 2, 0, true) }, null, false);
                AddSpot("PotatoWright_HangFey", "piece_potatowright_fey", Fey, "piece_workbench", new[] { new RequirementConfig("Resin", 2, 0, true) }, null, false);
            }
        }

        // ------------------------------------------------------------------ costs

        // What the game asks for the same thing: a piece's own requirements and station, or an item's recipe.
        private static RequirementConfig[] GameCost(string piece, string item, out string station)
        {
            station = null;
            if (piece != null)
            {
                Piece p = PrefabManager.Instance.GetPrefab(piece)?.GetComponent<Piece>();
                if (p != null && p.m_resources != null && p.m_resources.Length > 0)
                {
                    station = p.m_craftingStation ? p.m_craftingStation.name : null;
                    return p.m_resources.Where(r => r.m_resItem != null).Select(r => new RequirementConfig(r.m_resItem.name, r.m_amount, 0, true)).ToArray();
                }
            }
            if (item != null)
            {
                foreach (Recipe r in Resources.FindObjectsOfTypeAll<Recipe>())
                {
                    if (r != null && r.m_item != null && r.m_item.name == item && r.m_resources != null && r.m_resources.Length > 0)
                    {
                        station = r.m_craftingStation ? r.m_craftingStation.name : null;
                        return r.m_resources.Where(x => x.m_resItem != null).Select(x => new RequirementConfig(x.m_resItem.name, x.m_amount, 0, true)).ToArray();
                    }
                }
            }
            return null;
        }

        private static string CostText(RequirementConfig[] cost) => string.Join(", ", cost.Select(r => r.Item + " x" + r.Amount));

        // A lantern copied from the game: the game's cost for that lantern (its piece, or its item's recipe).
        private static void AddCopied(string name, string token, int kind, string piece, string item)
        {
            RequirementConfig[] cost = GameCost(piece, item, out string station);
            if (cost == null || cost.Length == 0)
            {
                cost = new[] { new RequirementConfig("Bronze", 1, 0, true), new RequirementConfig("Resin", 2, 0, true) };
                station = "forge";
                Debug.LogWarning(Plugin.Tag + " No game cost found for " + (piece ?? item) + "; " + name + " costs " + CostText(cost) + ".");
            }
            AddSpot(name, token, kind, string.IsNullOrEmpty(station) ? "forge" : station, cost);
        }

        // ------------------------------------------------------------------ the flags

        // The flags' pictures, made once at start: one cell per flag look, the cloth and its rune painted together, so the
        // rune moves with the cloth (the banner shader ripples every vertex by its own position, so a rune drawn on a
        // separate square drifted against the cloth in test-c).
        // Plain: 24 cells (rune i, colour i % 8): the weave of the game's blue-and-yellow banner (its blue cloth only;
        // every banner's knotwork runs down its middle, so no banner area is plain) as light and dark, dyed in each colour.
        // Cloth: 264 cells (banner i % 11, rune i % 24): the banner's picture from just under its top hem (the hanger and
        // hem showed in the offline preview) down to about its middle, edge to middle to edge.
        private static void Flags()
        {
            List<Material> banners = new List<Material>();
            foreach (int n in ClothOrder)
            {
                GameObject banner = PrefabManager.Instance.GetPrefab("piece_banner" + n.ToString("00"));
                Material m = banner ? banner.transform.Find("default")?.GetComponent<MeshRenderer>()?.sharedMaterial : null;
                if (m) banners.Add(m);
            }
            if (banners.Count != ClothOrder.Length)
            {
                Debug.LogWarning(Plugin.Tag + " Banner cloth missing (" + banners.Count + " of " + ClothOrder.Length + "); no bunting.");
                return;
            }
            Texture2D[] runes = LoadRunes();
            if (runes == null) return;
            List<Picture> pictures = new List<Picture>();
            foreach (Material m in banners)
            {
                Texture tex = m.GetTexture("_MainTex");
                Picture pic = tex ? Read(tex) : null;
                if (pic == null)
                {
                    Debug.LogWarning(Plugin.Tag + " Banner picture of " + m.name + " not read; no bunting.");
                    return;
                }
                pictures.Add(pic);
            }
            // banner 02 (blue with yellow knotwork) is third in ClothOrder.
            Picture weave = Weave(pictures[2], out Color clothColour, out int kept);
            Texture2D plain = Sheet("potatowright_flags_plain", PlainColumns, PlainRows, PlainCell, Futhark.Length, (cell, s, t) =>
            {
                Color c = Palette[cell % Palette.Length];
                float w = weave.Sample(s, 0.2f + t * 0.5f).r;
                Color cloth = new Color(c.r * w, c.g * w, c.b * w, 1f);
                return Rune(cloth, runes[cell % Futhark.Length], Luminance(Palette[cell % Palette.Length]) < 0.45f, s, t);
            });
            Texture2D cloth = Sheet("potatowright_flags_cloth", ClothColumns, ClothRows, ClothCell, ClothCells, (cell, s, t) =>
            {
                int b = cell % ClothOrder.Length;
                float u = 0.057f + (1f - Mathf.Abs(2f * s - 1f)) * (0.246f - 0.057f);
                float v = 0.46f + t * (0.92f - 0.46f);
                Color col = pictures[b].Sample(u, v);
                col.a = 1f;
                return Rune(col, runes[cell % Futhark.Length], ClothDark[b], s, t);
            });
            foreach (Texture2D r in runes) Object.Destroy(r);
            s_plain = new[] { FlagMaterial(banners[0], plain) };
            s_cloth = new[] { FlagMaterial(banners[0], cloth) };
            Plugin.Debug("Bunting: plain sheet " + plain.width + "x" + plain.height + " (weave from " + banners[2].name + ", cloth colour #" + ColorUtility.ToHtmlStringRGB(clothColour) + ", " + kept + " cloth pixels of " + weave.Count + "), cloth sheet " + cloth.width + "x" + cloth.height + " (" + banners.Count + " banners), runes " + runes.Length + ", shader " + banners[0].shader.name);
        }

        private const int PlainColumns = 6, PlainRows = 4, PlainCell = 128;
        private const int ClothColumns = 24, ClothRows = 11, ClothCell = 64, ClothCells = 264;

        private static Material FlagMaterial(Material template, Texture2D sheet)
        {
            Material m = new Material(template) { name = sheet.name };
            m.SetTexture("_MainTex", sheet);
            if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", null);
            m.SetColor("_Color", Color.white);
            return m;
        }

        // A picture's pixels, read through a temporary render target (the game's textures can't be read directly).
        private sealed class Picture
        {
            internal int Width, Height;
            internal Color[] Pixels;
            internal int Count => Pixels.Length;

            internal Color Sample(float u, float v)
            {
                float x = Mathf.Repeat(u, 1f) * Width - 0.5f, y = Mathf.Repeat(v, 1f) * Height - 0.5f;
                int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
                float fx = x - x0, fy = y - y0;
                Color a = At(x0, y0), b = At(x0 + 1, y0), c = At(x0, y0 + 1), d = At(x0 + 1, y0 + 1);
                return Color.Lerp(Color.Lerp(a, b, fx), Color.Lerp(c, d, fx), fy);
            }

            private Color At(int x, int y)
            {
                x = Mathf.Clamp(x, 0, Width - 1);
                y = Mathf.Clamp(y, 0, Height - 1);
                return Pixels[y * Width + x];
            }
        }

        private static Picture Read(Texture source)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D read = null;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                read = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
                read.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
                read.Apply(false, false);
                return new Picture { Width = source.width, Height = source.height, Pixels = read.GetPixels() };
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(Plugin.Tag + " Couldn't read " + source.name + ": " + e.Message);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                if (read) Object.Destroy(read);
            }
        }

        // The banner's cloth as light and dark around 1: its own cloth pixels (close to the main colour) by brightness, the
        // knotwork's pixels set to the average.
        private static Picture Weave(Picture banner, out Color main, out int kept)
        {
            const float u0 = 0.06f, u1 = 0.245f, v0 = 0.05f, v1 = 0.95f;
            int x0 = Mathf.RoundToInt(u0 * banner.Width), x1 = Mathf.RoundToInt(u1 * banner.Width);
            int y0 = Mathf.RoundToInt(v0 * banner.Height), y1 = Mathf.RoundToInt(v1 * banner.Height);
            int w = x1 - x0, h = y1 - y0;
            // The main colour: the most common colour, in steps of an eighth.
            Dictionary<int, int> counts = new Dictionary<int, int>();
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    Color c = banner.Pixels[y * banner.Width + x];
                    int key = Mathf.RoundToInt(c.r * 7f) * 64 + Mathf.RoundToInt(c.g * 7f) * 8 + Mathf.RoundToInt(c.b * 7f);
                    counts[key] = counts.TryGetValue(key, out int k) ? k + 1 : 1;
                }
            }
            int top = counts.OrderByDescending(p => p.Value).First().Key;
            main = new Color((top / 64) / 7f, (top / 8 % 8) / 7f, (top % 8) / 7f);
            float[] lum = new float[w * h];
            bool[] cloth = new bool[w * h];
            float sum = 0f;
            kept = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = banner.Pixels[(y0 + y) * banner.Width + x0 + x];
                    float d = Mathf.Abs(c.r - main.r) + Mathf.Abs(c.g - main.g) + Mathf.Abs(c.b - main.b);
                    int i = y * w + x;
                    lum[i] = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                    cloth[i] = c.a > 0.5f && d < 0.45f;
                    if (cloth[i])
                    {
                        sum += lum[i];
                        kept++;
                    }
                }
            }
            float mean = kept > 0 ? sum / kept : 1f;
            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // A knotwork pixel takes the nearest cloth pixel's shade in its row (a flat average left a ghost of
                    // the knotwork in the offline preview).
                    int from = y * w + x;
                    for (int dx = 1; !cloth[from] && dx < w; dx++)
                    {
                        if (x - dx >= 0 && cloth[y * w + x - dx]) from = y * w + x - dx;
                        else if (x + dx < w && cloth[y * w + x + dx]) from = y * w + x + dx;
                    }
                    float f = cloth[from] && mean > 0f ? Mathf.Clamp(lum[from] / mean, 0.7f, 1.3f) : 1f;
                    pixels[y * w + x] = new Color(f, f, f, 1f);
                }
            }
            return new Picture { Width = w, Height = h, Pixels = pixels };
        }

        // One sheet of flag pictures: cell by cell, each pixel from paint(cell, s, t), s across the flag (0 to 1), t up
        // the flag (0 at the tip, 1 at the top edge). Compressed once made.
        private static Texture2D Sheet(string name, int columns, int rows, int cellSize, int cells, System.Func<int, float, float, Color> paint)
        {
            Texture2D sheet = new Texture2D(columns * cellSize, rows * cellSize, TextureFormat.RGBA32, true, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color[] pixels = new Color[sheet.width * sheet.height];
            for (int cell = 0; cell < cells; cell++)
            {
                int x0 = (cell % columns) * cellSize, y0 = (rows - 1 - cell / columns) * cellSize;
                for (int y = 0; y < cellSize; y++)
                {
                    for (int x = 0; x < cellSize; x++)
                    {
                        pixels[(y0 + y) * sheet.width + x0 + x] = paint(cell, (x + 0.5f) / cellSize, (y + 0.5f) / cellSize);
                    }
                }
            }
            sheet.SetPixels(pixels);
            sheet.Apply(true);
            sheet.Compress(true);
            sheet.Apply(true, true);
            return sheet;
        }

        // The rune painted on the cloth: a 0.11 m square (RuneHalf) centred RuneDepth below the top edge.
        private static Color Rune(Color cloth, Texture2D rune, bool darkCloth, float s, float t)
        {
            const float halfS = RuneHalf / (2f * FlagHalf), halfT = RuneHalf / FlagLength, centreT = 1f - RuneDepth / FlagLength;
            float rs = (s - (0.5f - halfS)) / (2f * halfS), rt = (t - (centreT - halfT)) / (2f * halfT);
            if (rs < 0f || rs > 1f || rt < 0f || rt > 1f) return cloth;
            float a = rune.GetPixelBilinear(rs, rt).a;
            return Color.Lerp(cloth, darkCloth ? RuneLightColor : RuneDarkColor, a);
        }

        private static Texture2D[] LoadRunes()
        {
            Assembly asm = typeof(HangPieces).Assembly;
            Texture2D[] runes = new Texture2D[Futhark.Length];
            for (int r = 0; r < Futhark.Length; r++)
            {
                using (Stream s = asm.GetManifestResourceStream("PotatoWright.runes.elder_" + Futhark[r] + ".png"))
                {
                    if (s == null) break;
                    byte[] bytes = new byte[s.Length];
                    s.Read(bytes, 0, bytes.Length);
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(tex, bytes)) runes[r] = tex;
                }
            }
            if (runes.Any(x => x == null))
            {
                Debug.LogWarning(Plugin.Tag + " Rune drawings missing; no bunting.");
                return null;
            }
            return runes;
        }

        internal const float FlagSpacing = 0.34f, FlagHalf = 0.12f, FlagLength = 0.3f, RuneHalf = 0.055f, RuneDepth = 0.095f;

        // Triangle flags along the curve: the top edge on the rope, the tip hanging straight down, each flag's corners on
        // its own cell of the style's sheet (plain: flag f in cell f % 24; cloth: f % 264), so the flags run through the
        // Elder Futhark in turn and through the colours or banners.
        internal static Mesh BuntingMesh(List<Vector3> curve, int style, out int flags)
        {
            List<Vector3> fine = Swag.Resample(curve, 0.01f);
            float total = 0f;
            for (int i = 1; i < fine.Count; i++) total += Vector3.Distance(fine[i - 1], fine[i]);
            float step = total / (fine.Count - 1);
            flags = Mathf.Max(1, Mathf.FloorToInt((total - 2f * FlagHalf) / FlagSpacing) + 1);
            float start = (total - (flags - 1) * FlagSpacing) / 2f;
            int columns = style == 0 ? PlainColumns : ClothColumns, rows = style == 0 ? PlainRows : ClothRows;
            int cellSize = style == 0 ? PlainCell : ClothCell, cells = style == 0 ? Futhark.Length : ClothCells;
            float cu = 1f / columns, cv = 1f / rows, inset = 0.5f / (columns * cellSize);
            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> tris = new List<int>();
            for (int f = 0; f < flags; f++)
            {
                float s = start + f * FlagSpacing;
                Vector3 a = At(fine, step, s - FlagHalf), m = At(fine, step, s), b = At(fine, step, s + FlagHalf);
                Vector3 tip = m + Vector3.down * FlagLength;
                Vector3 normal = Vector3.Cross(b - a, Vector3.down).normalized;
                int cell = f % cells;
                float u0 = (cell % columns) * cu + inset, u1 = (cell % columns + 1) * cu - inset;
                float v1 = 1f - (cell / columns) * cv - inset, v0 = 1f - (cell / columns + 1) * cv + inset;
                int o = v.Count;
                v.Add(a); v.Add(m); v.Add(b); v.Add(tip);
                for (int k = 0; k < 4; k++) n.Add(normal);
                uv.Add(new Vector2(u0, v1)); uv.Add(new Vector2((u0 + u1) / 2f, v1)); uv.Add(new Vector2(u1, v1)); uv.Add(new Vector2((u0 + u1) / 2f, v0));
                tris.Add(o); tris.Add(o + 1); tris.Add(o + 3);
                tris.Add(o + 1); tris.Add(o + 2); tris.Add(o + 3);
            }
            Mesh mesh = new Mesh { name = "potatowright_bunting" };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static float Luminance(Color32 c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;

        private static Vector3 At(List<Vector3> fine, float step, float s)
        {
            float x = Mathf.Clamp(s / step, 0f, fine.Count - 1);
            int i = Mathf.Min(Mathf.FloorToInt(x), fine.Count - 2);
            return Vector3.Lerp(fine[i], fine[i + 1], x - i);
        }

        // ------------------------------------------------------------------ spot hangings and strings

        private static void AddSpot(string name, string token, int kind, string station, RequirementConfig[] cost, Sprite borrowedIcon = null, bool menu = true)
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
                NoWear(wear);
                wear.m_new = null;
                wear.m_worn = null;
                wear.m_broken = null;
                wear.m_fragmentRoots = null;
                wear.m_autoCreateFragments = false;
                wear.m_materialType = (kind == Herbs || kind == Bunting || kind == Fish || kind == Fey) ? WearNTear.MaterialType.Wood : WearNTear.MaterialType.Iron;
            }
            prefab.AddComponent<Steady>();
            Hanging hanging = prefab.AddComponent<Hanging>();
            hanging.m_kind = kind;
            Sprite icon = borrowedIcon;
            if (!GUIManager.IsHeadless())
            {
                if (!hanging.IsString)
                {
                    GameObject visual = SpotVisual(prefab, kind);
                    if (visual && menu) icon = PieceIcons.FromRenderers(name, visual, Quaternion.Euler(10f, kind == Herbs || kind == Fish ? 60f : 30f, 0f)) ?? icon;
                }
                else if (kind == Bunting)
                {
                    icon = BuntingIcon(name) ?? icon;
                }
            }
            if (icon) piece.m_icon = icon;
            if (menu)
            {
                PieceManager.Instance.AddPiece(custom);
            }
            else
            {
                // Not in the hammer at all, only known to the game, so placed ones load and can be removed.
                PrefabManager.Instance.AddPrefab(new CustomPrefab(prefab, true));
            }
            Plugin.Debug("Hanging piece " + name + " (kind " + kind + ", " + CostText(cost) + " at " + station + ", icon " + (bool)icon + (menu ? "" : ", not in the hammer") + ")");
        }

        private static void NoWear(WearNTear wear)
        {
            wear.m_noSupportWear = false;
            wear.m_noRoofWear = false;
            wear.m_supports = false;
        }

        // The model of a spot hanging, under "visual", hanging from the piece's origin (the point on the rope). Local z
        // runs along the rope, y up.
        private static GameObject SpotVisual(GameObject prefab, int kind)
        {
            GameObject visual = new GameObject("visual");
            visual.transform.SetParent(prefab.transform, false);
            List<Renderer> renderers = new List<Renderer>();
            Vector3 colliderCentre, colliderSize;
            if (kind == Herbs)
            {
                HerbsVisual(visual, renderers);
                colliderCentre = new Vector3(0f, -0.28f, 0f);
                colliderSize = new Vector3(0.35f, 0.5f, 0.35f);
            }
            else if (kind == Fish)
            {
                FishVisual(visual, renderers);
                colliderCentre = new Vector3(0f, -0.4f, 0f);
                colliderSize = new Vector3(0.15f, 0.65f, 0.6f);
            }
            else
            {
                float bottom = LanternVisual(visual, kind, renderers);
                colliderCentre = new Vector3(0f, bottom / 2f, 0f);
                colliderSize = new Vector3(0.4f, -bottom, 0.4f);
            }
            // One LODGroup for our own parts; parts copied with a LODGroup of their own keep it.
            renderers.RemoveAll(r => r == null || r.GetComponentInParent<LODGroup>(true) != null);
            if (renderers.Count > 0)
            {
                LODGroup lod = visual.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(0.008f, renderers.ToArray()) });
                lod.RecalculateBounds();
            }
            GameObject box = new GameObject("collider");
            box.layer = LayerMask.NameToLayer("piece_nonsolid");
            box.transform.SetParent(prefab.transform, false);
            box.transform.localPosition = colliderCentre;
            box.AddComponent<BoxCollider>().size = colliderSize;
            return visual;
        }

        // A lantern on a short iron chain; returns the lantern's bottom (below the hook, negative).
        private static float LanternVisual(GameObject visual, int kind, List<Renderer> renderers)
        {
            if (SwagPieces.LinkMetal[1])
            {
                renderers.Add(MeshPart(visual, "chain", Swag.LinkMesh(new List<Vector3> { Vector3.zero, new Vector3(0f, -Chain, 0f) }, false), SwagPieces.LinkMetal[1], Vector3.zero, Quaternion.identity, Vector3.one));
            }
            GameObject holder = new GameObject("lantern");
            holder.transform.SetParent(visual.transform, false);
            float scale = 0.85f;
            Light light = null;
            switch (kind)
            {
                case Lantern:
                {
                    // The Dvergr lantern item's body with its lit material; piece_dvergr_lantern's light and glow at the flame.
                    GameObject item = PrefabManager.Instance.GetPrefab("Lantern");
                    MeshFilter body = item ? item.transform.Find("default")?.GetComponent<MeshFilter>() : null;
                    Material lit = PrefabManager.Cache.GetPrefab<Material>("DvergrTownLantern_mat");
                    if (body && lit) MeshPart(holder, "body", body.sharedMesh, lit, Vector3.zero, Quaternion.identity, Vector3.one);
                    GameObject l = Copy("piece_dvergr_lantern", "New/Point Light", holder.transform, false);
                    if (l) l.transform.localPosition = new Vector3(0f, 0.253f, 0f);
                    GameObject f = Copy("piece_dvergr_lantern", "New/flare", holder.transform, false);
                    if (f) f.transform.localPosition = new Vector3(0f, 0.223f, 0f);
                    light = l ? l.GetComponent<Light>() : null;
                    break;
                }
                case LanternHooded:
                {
                    // piece_hoodedlantern's lantern; its beam (the spot light) turned out from the rope and tipped down,
                    // its small point light left out (one light).
                    GameObject lamp = Copy("piece_hoodedlantern", "default", holder.transform, false);
                    if (lamp)
                    {
                        foreach (Collider c in lamp.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                        lamp.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                        Transform point = lamp.transform.Find("Point Light");
                        if (point) Object.DestroyImmediate(point.gameObject);
                        Transform spot = lamp.transform.Find("Spot Light");
                        if (spot)
                        {
                            spot.localRotation = Quaternion.Euler(30f, 0f, 0f);
                            light = spot.GetComponent<Light>();
                        }
                    }
                    break;
                }
                case LanternDeepNorth:
                {
                    // The Deep North lantern item's lit model with its light and glow.
                    scale = 1f;
                    GameObject lamp = Copy("Lantern_DN", "attach_back/default", holder.transform, false);
                    if (lamp)
                    {
                        Transform sfx = lamp.transform.Find("SFX");
                        if (sfx) Object.DestroyImmediate(sfx.gameObject);
                        light = lamp.GetComponentInChildren<Light>(true);
                        if (light) light.gameObject.SetActive(true);
                        Transform flare = lamp.transform.Find("flare");
                        if (flare) flare.gameObject.SetActive(true);
                    }
                    break;
                }
                case LanternSnow:
                {
                    // piece_snowlantern: the snow globe (its own detail levels), the candle flame and its light.
                    scale = 1f;
                    Copy("piece_snowlantern", "New", holder.transform, true);
                    GameObject fire = Copy("piece_snowlantern", "_enabled", holder.transform, true);
                    light = fire ? fire.GetComponentInChildren<Light>(true) : null;
                    break;
                }
                case LanternLava:
                {
                    // piece_Lavalantern: the lantern with its moving lava and bubbles (its own detail levels) and its light.
                    scale = 0.8f;
                    Copy("piece_Lavalantern", "new", holder.transform, true);
                    GameObject l = Copy("piece_Lavalantern", "Point light", holder.transform, true);
                    light = l ? l.GetComponent<Light>() : null;
                    break;
                }
            }
            foreach (Renderer r in holder.GetComponentsInChildren<MeshRenderer>(true)) renderers.Add(r);
            float top = TopOf(holder.transform);
            holder.transform.localScale = Vector3.one * scale;
            holder.transform.localPosition = new Vector3(0f, -Chain - top * scale, 0f);
            float bottom = -Chain - top * scale;
            int lights = holder.GetComponentsInChildren<Light>(true).Length;
            if (light) EnsureLightLod(light);
            Plugin.Debug("Lantern kind " + kind + ": top " + top.ToString("0.000") + ", scale " + scale + ", lights " + lights + (light ? ", light " + light.type + " range " + light.range + " intensity " + light.intensity + " shadows " + light.shadows + ", components " + string.Join(", ", light.GetComponents<Component>().Select(x => x.GetType().Name)) : ", no light"));
            if (!light) Debug.LogWarning(Plugin.Tag + " Hanging lantern kind " + kind + ": the game's lantern or its light wasn't found.");
            return bottom;
        }

        // A light without the game's LightLod gets one (it switches off at a distance and follows the Point lights setting);
        // added while the prefab is inactive, so its Awake reads these.
        private static void EnsureLightLod(Light light)
        {
            if (light.GetComponent<LightLod>() != null) return;
            LightLod lod = light.gameObject.AddComponent<LightLod>();
            lod.m_lightLod = true;
            lod.m_lightDistance = 40f;
            lod.m_shadowLod = light.shadows != LightShadows.None;
            lod.m_shadowDistance = 20f;
        }

        // The highest point of the meshes under root, in root's space.
        private static float TopOf(Transform root)
        {
            float top = 0f;
            bool any = false;
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            foreach (MeshFilter f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!f.sharedMesh) continue;
                Bounds b = f.sharedMesh.bounds;
                Matrix4x4 m = toRoot * f.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    float y = m.MultiplyPoint3x4(corner).y;
                    if (!any || y > top) top = y;
                    any = true;
                }
            }
            return any ? top : 0.6f;
        }

        // A copy of a part of a game prefab under parent: with its own place in the prefab (keep) or at parent's origin.
        private static GameObject Copy(string prefab, string path, Transform parent, bool keep)
        {
            GameObject source = PrefabManager.Instance.GetPrefab(prefab);
            Transform part = source ? source.transform.Find(path) : null;
            if (!part)
            {
                Debug.LogWarning(Plugin.Tag + " " + prefab + "/" + path + " not found.");
                return null;
            }
            GameObject copy = Object.Instantiate(part.gameObject, parent, false);
            copy.name = part.name;
            if (keep)
            {
                copy.transform.localPosition = part.localPosition;
                copy.transform.localRotation = part.localRotation;
                copy.transform.localScale = part.localScale;
            }
            else
            {
                copy.transform.localPosition = Vector3.zero;
                copy.transform.localRotation = Quaternion.identity;
                copy.transform.localScale = Vector3.one;
            }
            return copy;
        }

        // Three thistles and two dandelions tied at the stems, heads down, fanned a little. The plants' stems start at their
        // origin and grow up +y (read from the meshes).
        private static void HerbsVisual(GameObject visual, List<Renderer> renderers)
        {
            const float cord = 0.05f;
            if (SwagPieces.Rope)
            {
                renderers.Add(MeshPart(visual, "cord", Swag.Tube(new List<Vector3> { new Vector3(0f, 0.01f, 0f), new Vector3(0f, -cord - 0.03f, 0f) }, 6, false, 0.008f), SwagPieces.Rope, Vector3.zero, Quaternion.identity, Vector3.one));
            }
            MeshRenderer thistle = PlantRenderer("Thistle");
            MeshRenderer dandelion = PlantRenderer("Pickable_Dandelion");
            if (thistle)
            {
                Mesh m = thistle.GetComponent<MeshFilter>().sharedMesh;
                for (int i = 0; i < 3; i++)
                {
                    Quaternion q = Quaternion.AngleAxis(i * 120f + 20f, Vector3.up) * Quaternion.AngleAxis(12f, Vector3.right) * Quaternion.Euler(0f, 0f, 180f);
                    renderers.Add(MeshPart(visual, "thistle", m, thistle.sharedMaterial, new Vector3(0f, -cord, 0f), q, Vector3.one * 0.14f));
                }
            }
            if (dandelion)
            {
                Mesh m = dandelion.GetComponent<MeshFilter>().sharedMesh;
                for (int i = 0; i < 2; i++)
                {
                    Quaternion q = Quaternion.AngleAxis(i * 180f + 80f, Vector3.up) * Quaternion.AngleAxis(8f, Vector3.right) * Quaternion.Euler(0f, 0f, 180f);
                    renderers.Add(MeshPart(visual, "dandelion", m, dandelion.sharedMaterial, new Vector3(0f, -cord, 0f), q, new Vector3(0.45f, 0.7f, 0.45f)));
                }
            }
            if (!thistle || !dandelion) Debug.LogWarning(Plugin.Tag + " Herb models missing: thistle " + (bool)thistle + ", dandelion " + (bool)dandelion + ".");
        }

        private static MeshRenderer PlantRenderer(string prefab)
        {
            GameObject go = PrefabManager.Instance.GetPrefab(prefab);
            return go ? go.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.GetComponent<MeshFilter>()) : null;
        }

        // Test builds only (removed from the hammer): perch, pike, perch hung by the tail.
        private static void FishVisual(GameObject visual, List<Renderer> renderers)
        {
            GameObject perch = PrefabManager.Instance.GetPrefab("Fish1"), pike = PrefabManager.Instance.GetPrefab("Fish2");
            MeshRenderer perchR = perch ? perch.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.GetComponent<MeshFilter>()) : null;
            MeshRenderer pikeR = pike ? pike.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.GetComponent<MeshFilter>()) : null;
            const float cord = 0.08f;
            float[] along = { -0.2f, 0f, 0.2f };
            for (int i = 0; i < 3; i++)
            {
                MeshRenderer src = (i == 1) ? pikeR : perchR;
                Vector3 s = (i == 1) ? new Vector3(0.78f, 0.62f, 0.62f) : new Vector3(0.5f, 0.5f, 0.5f);
                if (src)
                {
                    renderers.Add(MeshPart(visual, "fish", src.GetComponent<MeshFilter>().sharedMesh, src.sharedMaterial, new Vector3(-0.047f * s.y, -cord - 0.4f * s.x, along[i]), Quaternion.Euler(0f, 0f, -90f), s));
                }
            }
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

        // Plain flags on a short rope.
        private static Sprite BuntingIcon(string name)
        {
            if (s_plain == null) return null;
            GameObject go = new GameObject(name + "_icon");
            try
            {
                List<Vector3> curve = Swag.Points(new Vector3(0f, 0f, -0.55f), new Vector3(0f, 0f, 0.55f), Vector3.down, 0.12f, 24);
                MeshRenderer r = (MeshRenderer)MeshPart(go, "flags", BuntingMesh(curve, 0, out _), null, Vector3.zero, Quaternion.identity, Vector3.one);
                r.sharedMaterials = s_plain;
                if (SwagPieces.Rope) MeshPart(go, "rope", Swag.Tube(Swag.Resample(curve, 0.02f), 8, false), SwagPieces.Rope, Vector3.zero, Quaternion.identity, Vector3.one);
                return PieceIcons.Draw(name, go, Quaternion.Euler(0f, 90f, 0f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ------------------------------------------------------------------ the brazier

        // The game's hanging brazier as it is (fire, coal, warmth, smoke, its cost and station), hung by the top of its
        // chains: every part moved down so the piece's origin is the chains' top.
        private static void AddBrazier()
        {
            const string name = "PotatoWright_HangBrazier";
            RequirementConfig[] cost = GameCost("piece_brazierceiling01", null, out string station);
            if (cost == null || cost.Length == 0)
            {
                Debug.LogWarning(Plugin.Tag + " piece_brazierceiling01 not found; no hanging brazier.");
                return;
            }
            CustomPiece custom = new CustomPiece(name, "piece_brazierceiling01", new PieceConfig
            {
                Name = "$piece_potatowright_hang_brazier",
                Description = "$piece_potatowright_hang_brazier_description",
                PieceTable = "Hammer",
                Category = "Furniture",
                CraftingStation = string.IsNullOrEmpty(station) ? "forge" : station,
                Requirements = cost,
            });
            GameObject prefab = custom.PiecePrefab;
            if (!prefab) return;
            float top = TopOf(prefab.transform);
            foreach (Transform child in prefab.transform)
            {
                child.localPosition += Vector3.down * top;
            }
            Piece piece = prefab.GetComponent<Piece>();
            piece.m_canRotate = false;
            piece.m_inCeilingOnly = false;
            piece.m_notOnFloor = false;
            piece.m_notOnWood = false;
            piece.m_notOnTiltingSurface = false;
            piece.m_groundPiece = false;
            piece.m_groundOnly = false;
            WearNTear wear = prefab.GetComponent<WearNTear>();
            if (wear) NoWear(wear);
            prefab.AddComponent<Steady>();
            prefab.AddComponent<Hanging>().m_kind = Brazier;
            Sprite icon = GUIManager.IsHeadless() ? null : PieceIcons.FromRenderers(name, prefab, Quaternion.Euler(10f, 30f, 0f));
            if (icon) piece.m_icon = icon;
            PieceManager.Instance.AddPiece(custom);
            Plugin.Debug("Hanging piece " + name + " (piece_brazierceiling01 copy, chains' top " + top.ToString("0.000") + " m above the bowl, " + CostText(cost) + " at " + station + ", components " + string.Join(", ", prefab.GetComponents<Component>().Select(x => x.GetType().Name)) + ", icon " + (bool)icon + ")");
        }
    }
}

namespace PotatoWright
{
    // TEST ONLY: the game's recipes for lanterns, read once the item database is up (test-c found no recipe for
    // Lantern_DN at start, so the Deep North hanging lantern fell back to Bronze 1 + Resin 2). Logs only.
    [HarmonyLib.HarmonyPatch(typeof(ObjectDB), "Awake")]
    internal static class LanternRecipeProbe
    {
        private static bool s_done;

        private static void Postfix(ObjectDB __instance)
        {
            if (!Plugin.TestBuild || s_done || __instance == null || __instance.m_recipes == null || __instance.m_recipes.Count == 0)
            {
                return;
            }
            s_done = true;
            UnityEngine.GameObject item = __instance.GetItemPrefab("Lantern_DN");
            ItemDrop drop = item ? item.GetComponent<ItemDrop>() : null;
            Plugin.Debug("Probe: Lantern_DN item " + (bool)item + (drop ? ", name " + drop.m_itemData.m_shared.m_name + ", type " + drop.m_itemData.m_shared.m_itemType : "") + "; " + __instance.m_recipes.Count + " recipes");
            foreach (Recipe r in __instance.m_recipes)
            {
                if (r == null || r.m_item == null || r.m_item.name.IndexOf("Lantern", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                string res = r.m_resources == null ? "none" : string.Join(", ", System.Linq.Enumerable.Select(r.m_resources, x => (x.m_resItem ? x.m_resItem.name : "?") + " x" + x.m_amount));
                Plugin.Debug("Probe: recipe " + r.name + " makes " + r.m_item.name + ", enabled " + r.m_enabled + ", station " + (r.m_craftingStation ? r.m_craftingStation.name : "none") + ", " + res);
            }
        }
    }
}
