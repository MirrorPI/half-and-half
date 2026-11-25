using System;
using System.Collections.Generic;
using System.Globalization;
using HalfAndHalf.Core;
using UnityEngine;

namespace HalfAndHalf.Content
{
    public sealed class VisualSampleAsset : ScriptableObject
    {
        public string ObjectId, DisplayName, ContentVersion, MassHint;
        public string Status = "prototype";
        public GameObject Model;
        public Material Section;
        public Material GalaxyHaze;
        public AudioClip[] CutVariants;
        public TextAsset MassData;
        public float Separation = .32f, Tilt = 8f;
        public bool IsPixelPlanet => (ObjectId=="P01" && ContentVersion=="P01-pixel-moon-v1") ||
                                     (ObjectId=="P02" && ContentVersion=="P02-pixel-earth-v1");
        public bool IsOrbitalSystem => ContentVersion=="P05-pixel-saturn-v1" && ObjectId=="P05" ||
                                       ContentVersion=="S01-pixel-solarsystem-v1" && ObjectId=="S01";
        public bool IsVoxelGalaxy => (ObjectId=="G03" && ContentVersion=="G03-pixel-antennae-v1") ||
                                     (ObjectId=="G01" && ContentVersion=="G01-pixel-whirlpool-v2") ||
                                     (ObjectId=="G02" && ContentVersion=="G02-pixel-cartwheel-v2");
        Presentation.VoxelGalaxyVolume voxelGalaxy;
        public Presentation.VoxelGalaxyVolume VoxelGalaxy => voxelGalaxy ??= new Presentation.VoxelGalaxyVolume(MassData.bytes,ObjectId);
        public bool IsGalaxy => IsVoxelGalaxy || ObjectId=="G01" && ContentVersion=="G01-pixel-whirlpool-v1" ||
                                ObjectId=="G02" && ContentVersion=="G02-pixel-cartwheel-v1";
        Presentation.GalaxyVolume galaxy;
        public Presentation.GalaxyVolume Galaxy => galaxy ??= new Presentation.GalaxyVolume(MassData.bytes,ObjectId=="G02");
        public bool SolarLimbDarkening;
        Presentation.OrbitalSystemVolume system;
        public Presentation.OrbitalSystemVolume SystemVolume => system ??= new Presentation.OrbitalSystemVolume(MassData.bytes,SolarLimbDarkening);
        Presentation.PixelPlanetVolume planet;
        ObjectDefinition planetDefinition;
        public Presentation.PixelPlanetVolume Planet => planet ??= new Presentation.PixelPlanetVolume(MassData.bytes);
        public ObjectDefinition Definition()
        {
            if(IsVoxelGalaxy)return planetDefinition ??= new ObjectDefinition(ObjectId,ContentVersion,VoxelGalaxy.MassAt(0),"Galaxy/"+ObjectId,VoxelGalaxy.MassAt);
            if(IsGalaxy)return planetDefinition ??= new ObjectDefinition(ObjectId,ContentVersion,Galaxy.MassAt(0),"Galaxy/"+ObjectId,Galaxy.MassAt);
            if(IsOrbitalSystem)return planetDefinition ??= new ObjectDefinition(ObjectId,ContentVersion,SystemVolume.MassAt(0),"OrbitalSystem/"+ObjectId,SystemVolume.MassAt);
            if(IsPixelPlanet)return planetDefinition ??= new ObjectDefinition(ObjectId,ContentVersion,Planet.MassAt(0),"PixelPlanet/"+ObjectId,Planet.MassAt);
            if(ContentVersion==Presentation.SpaceContentModel.Version&&Presentation.SpaceContentModel.Supports(ObjectId))return Presentation.SpaceContentModel.Definition(ObjectId);
            var map = JsonUtility.FromJson<AppleDefinitionAsset.MapData>(MassData.text);
            if (map.objectId != ObjectId || map.contentVersion != ContentVersion)
                throw new ArgumentException("Sample mass data/version mismatch: " + ObjectId);
            var cells = new List<MassCell>();
            foreach (var c in map.cells) cells.Add(new MassCell(D(c.minX), D(c.maxX), D(c.minY), D(c.maxY), D(c.mass)));
            return new ObjectDefinition(ObjectId, ContentVersion, new MassMap(cells), "Content/" + ObjectId + "/" + ObjectId + "Sample.asset");
        }
        static decimal D(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);
    }
}
