// Icons for the 15 stance pieces of each mannequin (the game has none; the emote pieces use the game's emote icons).
// Made in the game, once at start, from the mannequin itself: a copy of the piece is put in the stance (its own
// animator, run forward), drawn plain white by Jötunn's RenderManager, and turned into the emote icons' look: a solid
// orange figure (the emote icons' colour, read from T_emote_wave: RGB 215,153,72) shaded by the render's light, on a
// soft dark shadow. Nothing of the game's art is shipped; the icon is drawn on each PC.
using System;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace PotatoWright
{
    internal static class MannequinIcons
    {
        private static readonly Color32 Fill = new Color32(215, 153, 72, 255);
        internal const int Size = 128, RenderSize = 256, Fit = 118;

        // The piece's icon becomes its stance's picture; false if it couldn't be made (the piece keeps its icon).
        internal static bool Make(GameObject prefab, int pose, string name)
        {
            if (GUIManager.IsHeadless())
            {
                return false;
            }
            GameObject copy = null;
            try
            {
                ZNetView.m_forceDisableInit = true;
                copy = UnityEngine.Object.Instantiate(prefab, new Vector3(0f, -5000f, 0f), Quaternion.identity);
                ZNetView.m_forceDisableInit = false;
                copy.name = name + "_icon";
                ArmorStand stand = copy.GetComponent<ArmorStand>();
                Animator animator = (stand != null) ? stand.m_poseAnimator : null;
                if (animator == null)
                {
                    return false;
                }
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.SetInteger("Pose", pose);
                for (int i = 0; i < 12; i++)
                {
                    animator.Update(0.5f);
                }
                // The figure only: the base and the pose switch's block hidden; every surface plain white.
                Material white = null;
                foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.transform.parent != null && r.transform.parent.name.StartsWith("Blocks"))
                    {
                        r.enabled = false;
                        continue;
                    }
                    if (white == null && r.sharedMaterial != null)
                    {
                        white = new Material(r.sharedMaterial);
                        if (white.HasProperty("_MainTex")) white.SetTexture("_MainTex", Texture2D.whiteTexture);
                        if (white.HasProperty("_Color")) white.SetColor("_Color", Color.white);
                    }
                    Material[] slots = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < slots.Length; i++) slots[i] = white;
                    r.sharedMaterials = slots;
                }
                Sprite render = RenderManager.Instance.Render(new RenderManager.RenderRequest(copy)
                {
                    Rotation = Quaternion.Euler(0f, 160f, 0f),
                    Width = RenderSize,
                    Height = RenderSize,
                    UseCache = false,
                });
                if (render == null || render.texture == null)
                {
                    return false;
                }
                Texture2D icon = Stylize(render.texture);
                Sprite sprite = Sprite.Create(icon, new Rect(0f, 0f, icon.width, icon.height), new Vector2(0.5f, 0.5f));
                sprite.name = name + "_icon";
                prefab.GetComponent<Piece>().m_icon = sprite;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(Plugin.Tag + " Stance icon " + name + " not made: " + ex.Message);
                return false;
            }
            finally
            {
                ZNetView.m_forceDisableInit = false;
                if (copy != null)
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                }
            }
        }

        // The figure cut out of the render and scaled to fill the icon (a sitting figure is small in its render).
        internal static Color[] Fitted(Texture2D source)
        {
            int minX = source.width, minY = source.height, maxX = -1, maxY = -1;
            Color[] src = source.GetPixels();
            for (int y = 0; y < source.height; y++)
            {
                for (int x = 0; x < source.width; x++)
                {
                    if (src[y * source.width + x].a > 0.5f)
                    {
                        minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                        minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    }
                }
            }
            Color[] outp = new Color[Size * Size];
            if (maxX < 0)
            {
                return outp;
            }
            float w = maxX - minX + 1, h = maxY - minY + 1;
            float scale = Fit / Mathf.Max(w, h);
            float ox = (Size - w * scale) / 2f, oy = (Size - h * scale) / 2f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float sx = minX + (x + 0.5f - ox) / scale, sy = minY + (y + 0.5f - oy) / scale;
                    if (sx < minX - 1 || sy < minY - 1 || sx > maxX + 1 || sy > maxY + 1)
                    {
                        continue;
                    }
                    outp[y * Size + x] = source.GetPixelBilinear(sx / source.width, sy / source.height);
                }
            }
            return outp;
        }

        // The white render -> the emote icons' look: a solid orange figure (the emote icons' colour), shaded softly by
        // the render's own light (darkest 60% of the colour, brightest 106%), with smooth edges, on a soft dark shadow.
        // Tuned offline on test-h's saved renders (tools/shade.py): test-d/e's Sobel lines left dark specks inside the
        // figure and along its edge (the user: "still need some filling in"); the emote icons have none, only shading.
        private const float ShadeLow = 0.6f, ShadeHigh = 1.06f;

        private static Texture2D Stylize(Texture2D source)
        {
            int w = Size, h = Size, sw = source.width, sh = source.height;
            Color[] src = source.GetPixels();
            // The figure's box in the render and the scale that fits it to the icon (as Fitted).
            int minX = sw, minY = sh, maxX = -1, maxY = -1;
            float[] alpha = new float[sw * sh], lumA = new float[sw * sh];
            for (int y = 0; y < sh; y++)
            {
                for (int x = 0; x < sw; x++)
                {
                    Color c = src[y * sw + x];
                    alpha[y * sw + x] = c.a;
                    lumA[y * sw + x] = (c.r * 0.3f + c.g * 0.59f + c.b * 0.11f) * c.a;
                    if (c.a > 0.5f)
                    {
                        minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                        minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    }
                }
            }
            Texture2D icon = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color32[] outp = new Color32[w * h];
            if (maxX < 0)
            {
                icon.SetPixels32(outp);
                icon.Apply();
                return icon;
            }
            float bw = maxX - minX + 1, bh = maxY - minY + 1;
            float scale = Fit / Mathf.Max(bw, bh);
            float ox = (w - bw * scale) / 2f, oy = (h - bh * scale) / 2f;
            // Alpha and light sampled bilinearly (light premultiplied by alpha, so the edge isn't darkened).
            float[] a = new float[w * h], light = new float[w * h];
            List<float> inside = new List<float>();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float sx = minX + (x + 0.5f - ox) / scale - 0.5f, sy = minY + (y + 0.5f - oy) / scale - 0.5f;
                    float pa = Sample(alpha, sw, sh, sx, sy), pl = Sample(lumA, sw, sh, sx, sy);
                    int i = y * w + x;
                    a[i] = pa;
                    light[i] = (pa > 0.001f) ? pl / pa : 0f;
                    if (pa > 0.5f) inside.Add(light[i]);
                }
            }
            inside.Sort();
            float bright = (inside.Count > 0) ? Mathf.Max(0.05f, inside[Mathf.Min(inside.Count - 1, (int)(inside.Count * 0.95f))]) : 1f;
            // Shadow: the figure grown by 2 pixels and blurred.
            float[] grown = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float best = 0f;
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx >= 0 && yy >= 0 && xx < w && yy < h) best = Mathf.Max(best, a[yy * w + xx]);
                        }
                    }
                    grown[y * w + x] = Mathf.Clamp01(best);
                }
            }
            float[] shadow = Blur(grown, w, h, 1.6f);
            for (int i = 0; i < outp.Length; i++)
            {
                float k = Mathf.Clamp01(light[i] / bright);
                float m = ShadeLow + (ShadeHigh - ShadeLow) * k;
                float fa = Mathf.Clamp01(a[i]), ba = shadow[i] * (150f / 255f);
                float oa = fa + ba * (1f - fa);
                Color c = (oa > 1e-5f)
                    ? (new Color(Fill.r / 255f * m, Fill.g / 255f * m, Fill.b / 255f * m) * fa + new Color(25f / 255f, 18f / 255f, 12f / 255f) * ba * (1f - fa)) / oa
                    : Color.clear;
                c.a = oa;
                outp[i] = c;
            }
            icon.SetPixels32(outp);
            icon.Apply();
            return icon;
        }

        private static float Sample(float[] map, int w, int h, float x, float y)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            return Mathf.Lerp(Mathf.Lerp(At(map, w, h, x0, y0), At(map, w, h, x0 + 1, y0), fx),
                Mathf.Lerp(At(map, w, h, x0, y0 + 1), At(map, w, h, x0 + 1, y0 + 1), fx), fy);
        }

        private static float At(float[] map, int w, int h, int x, int y)
        {
            return (x < 0 || y < 0 || x >= w || y >= h) ? 0f : map[y * w + x];
        }

        // Separable Gaussian blur.
        private static float[] Blur(float[] map, int w, int h, float sigma)
        {
            int r = Mathf.CeilToInt(sigma * 3f);
            float[] kernel = new float[2 * r + 1];
            float sum = 0f;
            for (int i = -r; i <= r; i++) { kernel[i + r] = Mathf.Exp(-(i * i) / (2f * sigma * sigma)); sum += kernel[i + r]; }
            for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;
            float[] tmp = new float[w * h], outp = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = 0f;
                    for (int i = -r; i <= r; i++) v += kernel[i + r] * At(map, w, h, x + i, y);
                    tmp[y * w + x] = v;
                }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = 0f;
                    for (int i = -r; i <= r; i++) v += kernel[i + r] * At(tmp, w, h, x, y + i);
                    outp[y * w + x] = v;
                }
            return outp;
        }
    }
}
