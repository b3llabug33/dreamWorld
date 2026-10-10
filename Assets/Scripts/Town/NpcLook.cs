using System;
using System.Globalization;
using DreamWorld.CharacterCreation;
using UnityEngine;

namespace DreamWorld.Town
{
    // Dresses a townsperson made from the player's Dreamer model. The look is a short recipe, one slot per
    // entry separated by ';':   Category=Base [colorIndex | #RRGGBB] [axis:Value ...]
    // (Skin is just a color: "Skin=3" or "Skin=#F2F0FA".)
    //   e.g. "Skin=#F2F0FA;Eyes=Doe #D0202A;Hair=Long 0 bangs:Middle length:Long;Top=Hoodie 11;Mouth=Fang;Neck=none"
    // Slots not mentioned keep the model's default. "none" hides an optional slot.
    // elfEars adds two pointy ears on the head.
    public class NpcLook : MonoBehaviour
    {
        [TextArea] public string look = "";
        public bool elfEars;
        public Color earColor = new Color(1f, 0.87f, 0.77f);

        void Start()
        {
            var app = GetComponent<CharacterAppearance>();
            if (app == null) return;
            foreach (var entry in look.Split(';'))
            {
                var kv = entry.Split('=');
                if (kv.Length != 2 || !Enum.TryParse(kv[0].Trim(), out OutfitCategory category)) continue;
                var slot = app.GetSlot(category);
                if (slot == null) continue;
                var parts = kv[1].Trim().Split(' ');
                if (parts[0] == "none" && slot.optional) slot.styleIndex = 0;
                else if (!parts[0].StartsWith("#") && !int.TryParse(parts[0], out _)) slot.SelectBase(parts[0]);
                for (int i = 0; i < parts.Length; i++)
                {
                    var p = parts[i];
                    if (p.StartsWith("#") && ColorUtility.TryParseHtmlString(p, out var c))
                    {
                        slot.customColor = c;
                        slot.useCustom = true;
                    }
                    else if (int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                    {
                        slot.colorIndex = index;
                        slot.useCustom = false;
                    }
                    else if (p.Contains(":"))
                    {
                        var av = p.Split(':');
                        var axis = slot.axes.Find(a => a.label == av[0]);
                        if (axis != null) axis.index = Mathf.Max(0, Array.IndexOf(axis.values, av[1]));
                    }
                }
            }
            app.ApplyAll();
            if (elfEars)
            {
                var skin = app.GetSlot(OutfitCategory.Skin);
                if (skin != null) earColor = skin.CurrentColor;
                AddEars();
            }
        }

        // Two flat pointed ears on the sides of the head, parented to the head bone so they follow its motion.
        void AddEars()
        {
            Transform head = null;
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "Head") { head = t; break; }
            if (head == null) return;
            var mat = new Material(Shader.Find("DreamWorld/PSX Lit")) { color = earColor };
            mat.SetFloat("_Softness", 0.7f);
            Vector3 right = transform.right, up = transform.up, back = -transform.forward;
            // the head bone sits at the bottom of the head; ears go about 6 cm up, on the head's sides
            Vector3 centre = head.position + up * 0.065f;
            foreach (float side in new[] { -1f, 1f })
            {
                var ear = new GameObject(side < 0 ? "EarL" : "EarR", typeof(MeshFilter), typeof(MeshRenderer));
                ear.transform.SetParent(head, true);
                ear.transform.position = centre + right * side * 0.103f + back * 0.015f;
                // point up and back and out, like an elf
                Vector3 tipDir = (up * 0.8f + back * 0.45f + right * side * 0.55f).normalized;
                ear.transform.rotation = Quaternion.LookRotation(tipDir, right * side);
                ear.GetComponent<MeshFilter>().sharedMesh = EarMesh();
                ear.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        static Mesh earMesh;

        static Mesh EarMesh()
        {
            if (earMesh != null) return earMesh;
            // a flat leaf shape along +z (base at 0, tip at 0.11), thin in x, a little curl
            var v = new[]
            {
                new Vector3(0, -0.028f, 0), new Vector3(0, 0.022f, 0.0f), new Vector3(0.004f, 0.02f, 0.05f),
                new Vector3(0.004f, -0.012f, 0.055f), new Vector3(0.008f, 0.004f, 0.115f),
            };
            var tris = new[] { 0, 1, 2, 0, 2, 3, 3, 2, 4, 0, 2, 1, 0, 3, 2, 3, 4, 2 };
            earMesh = new Mesh { vertices = v, triangles = tris, name = "ElfEar" };
            earMesh.RecalculateNormals();
            earMesh.RecalculateBounds();
            return earMesh;
        }
    }
}
