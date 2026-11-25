using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HalfAndHalf.Presentation
{
    // L01-only Blender-authored volume. Air stays empty through the existing
    // continuous-X cut path; scoring reads the separately baked 2D mass map.
    public sealed class FanPixelSource : MonoBehaviour
    {
        public const string Version="L01-pixel-fan-v1";
        const int Magic=0x46505831;
        public TextAsset Prepared;
        static readonly Dictionary<TextAsset,VoxelVolume> volumes=new Dictionary<TextAsset,VoxelVolume>();
        public VoxelVolume Volume()
        {
            if(Prepared==null)throw new InvalidOperationException("Missing prepared L01 fan");
            if(volumes.TryGetValue(Prepared,out var cached))return cached;
            using(var stream=new MemoryStream(Prepared.bytes,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadInt32()!=Magic||reader.ReadString()!=Version)throw new InvalidDataException("Fan volume version mismatch");
                var volume=VoxelVolume.ReadPrepared("L01",Color,null,reader,2);
                if(stream.Position!=stream.Length)throw new InvalidDataException("Unexpected fan volume data");
                volumes.Add(Prepared,volume);return volume;
            }
        }
        static readonly Color32[] Palette={
            new Color32(0,0,0,0),new Color32(101,172,158,255),new Color32(48,73,77,255),
            new Color32(238,230,201,255),new Color32(184,201,192,255),new Color32(146,167,161,255),
            new Color32(53,71,77,255),new Color32(232,170,103,255),new Color32(207,117,85,255),
            new Color32(61,119,111,255),new Color32(0,0,0,255),new Color32(0,0,0,255),
            new Color32(0,0,0,255),new Color32(0,0,0,255),new Color32(183,121,78,255),new Color32(170,182,177,255)
        };
        public static Color32 Color(string id,byte material,Vector3 p,int axis,int sign,bool cap)
        {
            UnityEngine.Color c=Palette[material];
            // Quiet flat color groups; no checker noise or grass-like banding.
            if(cap)
            {
                if(material==1)c=new Color32(166,208,185,255);
                if(material==7)c=new Color32(255,207,142,255);
                return c;
            }
            float light=axis==1?(sign>0?1.055f:.79f):axis==2?(sign<0?1:.80f):(sign<0?.84f:.93f);
            return c*light;
        }
#if UNITY_EDITOR
        public static void InvalidatePreparedCache()=>volumes.Clear();
        public static VoxelVolume FromGrid(byte[] grid)
        {
            if(grid==null||grid.Length!=160*160*80||Array.Exists(grid,m=>m>15||m==10||m==11||m==12||m==13))throw new InvalidDataException("Invalid fan grid");
            return new VoxelVolume("L01",p=>{
                int x=Mathf.FloorToInt(p.x*64)+80,y=Mathf.FloorToInt((p.y+1.125f)*64),z=Mathf.FloorToInt(p.z*64)+40;
                return grid[(x*160+y)*80+z];
            },Color,null,2);
        }
        public static byte[] Bake(VoxelVolume volume)
        {
            using(var stream=new MemoryStream())
            {
                using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
                {writer.Write(Magic);writer.Write(Version);volume.WritePrepared(writer);writer.Flush();}
                return stream.ToArray();
            }
        }
#endif
    }
}
