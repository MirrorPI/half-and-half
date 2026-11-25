using System;
using System.Collections.Generic;
using System.Globalization;
using HalfAndHalf.Core;
using UnityEngine;

namespace HalfAndHalf.Content
{
    public sealed class AppleDefinitionAsset : ScriptableObject
    {
        public string ContentVersion = "B01-prototype-v1";
        public string Status = "prototype";
        public Mesh Body;
        public Material Skin, Flesh, Stem, Leaf;
        public TextAsset MassData;

        [Serializable] public sealed class CellData { public string minX, maxX, minY, maxY, mass; }
        [Serializable] public sealed class MapData { public string objectId, contentVersion; public CellData[] cells; }
        static decimal D(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
        public ObjectDefinition Definition()
        {
            var data = JsonUtility.FromJson<MapData>(MassData.text);
            if (data.objectId != "B01" || data.contentVersion != ContentVersion)
                throw new ArgumentException("B01 mass data/content version mismatch.");
            var cells = new List<MassCell>();
            foreach (var c in data.cells) cells.Add(new MassCell(D(c.minX), D(c.maxX), D(c.minY), D(c.maxY), D(c.mass)));
            return new ObjectDefinition("B01", ContentVersion, new MassMap(cells), "Content/B01/B01Prototype.asset");
        }
    }
}
