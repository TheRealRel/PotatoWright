// Icons for the 15 stance pieces of each mannequin (the game has none; the emote pieces use the game's emote icons).
// Made in the game, once at start, from the mannequin itself: a copy of the piece is put in the stance (its own
// animator, run forward), drawn plain white by Jötunn's RenderManager, and turned into the emote icons' look: an orange
// figure (the emote icons' colour, read from T_emote_wave: RGB 215,153,72) with dark lines where the shading jumps
// (limbs over the body), on a soft dark shadow. Nothing of the game's art is shipped; the icon is drawn on each PC.
using System;
using System.IO;
using Jotunn.Managers;
using UnityEngine;

namespace PotatoWright
{
    internal static class MannequinIcons
    {
        private static readonly Color32 Fill = new Color32(215, 153, 72, 255);
        private static readonly Color32 Line = new Color32(52, 34, 20, 255);
        // Tuned offline on test-d's saved renders (Sobel on 0..1 shading: median 0.3, 90th percentile 0.73): 0.8 keeps the
        // lines where limbs cross the body and drops the soft shading.
        private const float EdgeThreshold = 0.8f;
        private const int Size = 128, RenderSize = 256, Fit = 118;

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
                if (Plugin.TestBuild)
                {
                    // TEST ONLY: the pictures, for checking without a game.
                    string dir = Path.Combine(BepInEx.Paths.BepInExRootPath, "PotatoWright-test-icons");
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir, name + "_render.png"), render.texture.EncodeToPNG());
                    File.WriteAllBytes(Path.Combine(dir, name + ".png"), icon.EncodeToPNG());
                }
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
        private static Color[] Fitted(Texture2D source)
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

        // The white render -> the emote icons' look.
        private static Texture2D Stylize(Texture2D source)
        {
            int w = Size, h = Size;
            Color[] px = Fitted(source);
            float[] lum = new float[w * h];
            bool[] inside = new bool[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                inside[i] = px[i].a > 0.5f;
                lum[i] = px[i].r * 0.3f + px[i].g * 0.59f + px[i].b * 0.11f;
            }
            Color32[] outp = new Color32[w * h];
            // Soft shadow: the figure's outline grown by 3 pixels, fading out.
            float[] shadow = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float best = 0f;
                    for (int dy = -3; dy <= 3; dy++)
                    {
                        for (int dx = -3; dx <= 3; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= w || yy >= h || !inside[yy * w + xx]) continue;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            best = Mathf.Max(best, 1f - d / 4f);
                        }
                    }
                    shadow[y * w + x] = best;
                }
            }
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (inside[i])
                    {
                        // Sobel on the shading, inside the figure only.
                        float gx = 0f, gy = 0f;
                        for (int k = 0; k < 9; k++)
                        {
                            int dx = k % 3 - 1, dy = k / 3 - 1;
                            int xx = Mathf.Clamp(x + dx, 0, w - 1), yy = Mathf.Clamp(y + dy, 0, h - 1);
                            int j = yy * w + xx;
                            float v = inside[j] ? lum[j] : lum[i];
                            int wx = (dx == 0) ? 0 : dx * ((dy == 0) ? 2 : 1);
                            int wy = (dy == 0) ? 0 : dy * ((dx == 0) ? 2 : 1);
                            gx += v * wx;
                            gy += v * wy;
                        }
                        float edge = Mathf.Sqrt(gx * gx + gy * gy);
                        outp[i] = (edge > EdgeThreshold) ? Line : Fill;
                    }
                    else
                    {
                        byte a = (byte)Mathf.RoundToInt(shadow[i] * 150f);
                        outp[i] = new Color32(25, 18, 12, a);
                    }
                }
            }
            Texture2D icon = new Texture2D(w, h, TextureFormat.RGBA32, false);
            icon.SetPixels32(outp);
            icon.Apply();
            return icon;
        }
    }
}
