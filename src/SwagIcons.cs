// Build menu icons for the ropes and chains, drawn in the game from their own models (the user, 2026-11-06: the rope's
// icon must be a rope, the black metal chain's must look black metal). Rendered once at start by Jötunn's RenderManager
// in colour, like the game's own piece icons, then cut out and fitted to the icon (MannequinIcons.Fitted).
// RenderManager's camera looks along -z at the object's +z side, lit from the camera's side.
using System;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace PotatoWright
{
    internal static class SwagIcons
    {
        // A coil of the rope lying flat (three and a half loops stacked a rope's width apart, the end trailing out),
        // tipped towards the camera so the loops show.
        internal static Sprite Rope()
        {
            if (GUIManager.IsHeadless() || !SwagPieces.Rope) return null;
            List<Vector3> path = new List<Vector3>();
            const float radius = 0.14f, turns = 3.5f, rise = 0.05f;
            int steps = 140;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float a = t * turns * Mathf.PI * 2f;
                path.Add(new Vector3(Mathf.Cos(a) * radius, t * turns * rise, Mathf.Sin(a) * radius));
            }
            // The loose end: from the top loop out past the coil and down to the ground.
            Vector3 last = path[path.Count - 1];
            Vector3 tangent = (last - path[path.Count - 2]).normalized;
            for (int i = 1; i <= 12; i++)
            {
                float t = i / 12f;
                path.Add(last + tangent * (0.16f * t) + Vector3.down * (last.y * t * t));
            }
            Mesh mesh = Swag.Tube(Swag.Resample(path, 0.02f), 12, true);
            return Draw("PotatoWright_SwagRope", mesh, SwagPieces.Rope, Quaternion.Euler(50f, 0f, 0f));
        }

        // A short hanging length of chain in the chain's own metal, slanted like the game's Chain item icon and turned so
        // both link directions show.
        internal static Sprite Chain(int kind)
        {
            if (GUIManager.IsHeadless() || !SwagPieces.LinkMetal[kind]) return null;
            List<Vector3> path = new List<Vector3> { Vector3.zero, new Vector3(0f, 0.5f, 0f) };
            Mesh mesh = Swag.LinkMesh(path, kind == 3);
            return Draw("PotatoWright_SwagChain" + kind, mesh, SwagPieces.LinkMetal[kind], Quaternion.Euler(0f, 0f, -40f) * Quaternion.Euler(0f, 45f, 0f));
        }

        private static Sprite Draw(string name, Mesh mesh, Material material, Quaternion rotation)
        {
            GameObject go = null;
            try
            {
                go = new GameObject(name + "_icon");
                go.transform.position = new Vector3(0f, -5000f, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
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
                return sprite;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(Plugin.Tag + " Icon " + name + " not drawn: " + ex.Message);
                return null;
            }
            finally
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
