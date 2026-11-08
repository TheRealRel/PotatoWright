// Build menu icons for the scaffold and the hangings, drawn in the game from their own models, like the ropes and chains
// (SwagIcons): the visible mesh renderers are copied into a bare object (no lights, particles or game components), drawn
// by Jötunn's RenderManager, then cut out and fitted to the icon (MannequinIcons.Fitted).
// RenderManager's camera looks along -z at the object's +z side, lit from the camera's side.
using System;
using Jotunn.Managers;
using UnityEngine;

namespace PotatoWright
{
    internal static class PieceIcons
    {
        // Copies of every mesh renderer under root that would show (each object up to root switched on).
        internal static Sprite FromRenderers(string name, GameObject root, Quaternion rotation)
        {
            if (GUIManager.IsHeadless() || root == null) return null;
            GameObject copy = new GameObject(name + "_icon");
            try
            {
                Matrix4x4 toRoot = root.transform.worldToLocalMatrix;
                int count = 0;
                foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!r.enabled || !Shows(r.transform, root.transform)) continue;
                    MeshFilter f = r.GetComponent<MeshFilter>();
                    if (f == null || f.sharedMesh == null) continue;
                    Matrix4x4 m = toRoot * r.transform.localToWorldMatrix;
                    GameObject part = new GameObject("part");
                    part.transform.SetParent(copy.transform, false);
                    part.transform.localPosition = m.GetColumn(3);
                    part.transform.localRotation = Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
                    part.transform.localScale = new Vector3(m.GetColumn(0).magnitude, m.GetColumn(1).magnitude, m.GetColumn(2).magnitude);
                    part.AddComponent<MeshFilter>().sharedMesh = f.sharedMesh;
                    part.AddComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
                    count++;
                }
                Plugin.Debug("Icon " + name + ": " + count + " meshes");
                return count > 0 ? Draw(name, copy, rotation) : null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        private static bool Shows(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
            {
                if (!t.gameObject.activeSelf) return false;
            }
            return true;
        }

        // Draws the object (destroyed by the caller).
        internal static Sprite Draw(string name, GameObject go, Quaternion rotation)
        {
            try
            {
                go.transform.position = new Vector3(0f, -5000f, 0f);
                Sprite render = RenderManager.Instance.Render(new RenderManager.RenderRequest(go)
                {
                    Rotation = rotation,
                    Width = MannequinIcons.RenderSize,
                    Height = MannequinIcons.RenderSize,
                    UseCache = false,
                });
                if (render == null || render.texture == null)
                {
                    Debug.LogWarning(Plugin.Tag + " Icon " + name + " not drawn; using a borrowed one.");
                    return null;
                }
                int size = MannequinIcons.Size;
                Texture2D icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
                icon.SetPixels(MannequinIcons.Fitted(render.texture));
                icon.Apply();
                Sprite sprite = Sprite.Create(icon, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
                sprite.name = name + "_icon";
                if (Plugin.TestBuild)
                {
                    // TEST ONLY: the icons for the mod page (BepInEx/PotatoWright-test-icons).
                    string dir = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "PotatoWright-test-icons");
                    System.IO.Directory.CreateDirectory(dir);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), icon.EncodeToPNG());
                }
                return sprite;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(Plugin.Tag + " Icon " + name + " not drawn: " + ex.Message);
                return null;
            }
        }
    }
}
