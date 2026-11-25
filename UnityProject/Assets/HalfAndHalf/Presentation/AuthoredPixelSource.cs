using System;
using System.IO;
using UnityEngine;

namespace HalfAndHalf.Presentation
{
    // The prefab and its baked volume carry the same object/version identity.
    public sealed class AuthoredPixelSource : MonoBehaviour
    {
        public string ObjectId, ContentVersion;
        public TextAsset Prepared;
        public Color32[] DecorationPalette;
        [NonSerialized] VoxelVolume cached;
        const int Magic = 0x48485031;

        static float FaceLight(int axis, int sign) =>
            axis == 1 ? (sign > 0 ? 1.025f : .93f) : (axis == 2 && sign < 0 ? 1f : .95f);

        Color32 DecorationShade(string id, byte material, Vector3 p, int axis, int sign, bool cap) =>
            (Color)DecorationPalette[material] * FaceLight(axis, sign);

        public VoxelVolume Volume()
        {
            if (cached != null) return cached;
            using (var stream = new MemoryStream(Prepared.bytes, false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != Magic || reader.ReadString() != ContentVersion)
                    throw new InvalidDataException("Prepared volume version mismatch");
                // Prepared mesh faces and caps already include the authored palette.
                var volume = VoxelVolume.ReadPrepared(ObjectId, DecorationShade, null, reader, 2);
                if (stream.Position != stream.Length) throw new InvalidDataException("Trailing volume data");
                return cached = volume;
            }
        }
#if UNITY_EDITOR
        [Serializable] public sealed class Swatch { public int id; public string surface, section; }
        [Serializable] public sealed class Palette
        {
            public Swatch[] materials;
            public Color32[] ReadColors(bool crossSection)
            {
                var colors = new Color32[18];
                foreach (var m in materials)
                {
                    if (!ColorUtility.TryParseHtmlString("#"+(crossSection ? m.section : m.surface), out var c))
                        throw new InvalidDataException("Invalid palette");
                    colors[m.id] = c;
                }
                return colors;
            }
        }
        public static VoxelVolume FromGrid(string id, byte[] grid, Palette palette, byte[] tintIndices=null, Color32[] tints=null)
        {
            if (grid.Length != 160 * 112 * 80) throw new InvalidDataException("Unexpected compact grid size");
            if (tintIndices != null && (tintIndices.Length != grid.Length || tints == null || Array.Exists(tintIndices, i => i >= tints.Length)))
                throw new InvalidDataException("Invalid authored exterior tint grid");
            int Index(Vector3 p)
            {
                int x = Mathf.FloorToInt(p.x*64)+80, y = Mathf.FloorToInt((p.y+.875f)*64), z = Mathf.FloorToInt((p.z+.625f)*64);
                return x < 0 || x >= 160 || y < 0 || y >= 112 || z < 0 || z >= 80 ? -1 : (x*112+y)*80+z;
            }
            var surface = palette.ReadColors(false);
            var section = palette.ReadColors(true);
            Color32 Shade(string objectId, byte material, Vector3 p, int axis, int sign, bool cap)
            {
                if (surface[material].a == 0) throw new InvalidDataException("Unmapped material");
                Color color = cap ? section[material] : surface[material];
                if (!cap && tintIndices != null)
                {
                    byte index = tintIndices[Index(p)];
                    if (index != 0) color = tints[index];
                }
                float light = FaceLight(axis, sign);
                if (objectId == "A05" && material == 16 && Mathf.Sin(39*p.y-7*p.x+1.8f*Mathf.Sin(4*p.x)) > .78f)
                    light *= .95f;
                return color * light;
            }
            return new VoxelVolume(id, p => {
                int index = Index(p);
                return index < 0 ? (byte)0 : grid[index];
            }, Shade, null, 2);
        }
        public static byte[] Bake(VoxelVolume volume, string version)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(version);
                volume.WritePrepared(writer);
                writer.Flush();
                return stream.ToArray();
            }
        }
#endif
    }
}
