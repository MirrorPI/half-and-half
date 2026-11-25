using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HalfAndHalf.Presentation
{
    // B01-only Blender-authored volume. Air stays empty through the existing
    // continuous-X cut path; scoring reads the separately baked 2D mass map.
    public sealed class ApplePixelSource : MonoBehaviour
    {
        public const string Version="B01-pixel-apple-v1";
        const int Magic=0x41505831;
        public TextAsset Prepared;
        static readonly Dictionary<TextAsset,VoxelVolume> volumes=new Dictionary<TextAsset,VoxelVolume>();
        public VoxelVolume Volume()
        {
            if(Prepared==null)throw new InvalidOperationException("Missing prepared B01 apple");
            if(volumes.TryGetValue(Prepared,out var cached))return cached;
            using(var stream=new MemoryStream(Prepared.bytes,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadInt32()!=Magic||reader.ReadString()!=Version)throw new InvalidDataException("Apple volume version mismatch");
                var volume=VoxelVolume.ReadPrepared("B01",Color,null,reader,2);
                if(stream.Position!=stream.Length)throw new InvalidDataException("Unexpected apple volume data");
                volumes.Add(Prepared,volume);return volume;
            }
        }
        public static Color32 Color(string id,byte material,Vector3 p,int axis,int sign,bool cap)
        {
            UnityEngine.Color c;
            switch(material)
            {
                case 1:
                    c=new Color32(217,86,80,255);
                    // Broad irregularly curved cheek groups. No periodic noise,
                    // horizontal latitude bands, white specular dot or texture overlay.
                    float shoulder=.06f+.17f*Mathf.Sin(p.x*3.5f+.8f);
                    if(p.z<-.10f&&p.x<-.12f+.16f*p.y&&p.y>shoulder)c=new Color32(230,106,90,255);
                    else if(p.x>.47f-.20f*p.y*p.y||p.y<-.67f+.12f*p.x)c=new Color32(195,75,75,255);
                    if(cap)c=new Color32(199,72,69,255);
                    break;
                case 8:c=new Color32(245,230,194,255);break;
                case 10:c=new Color32(236,222,183,255);break;
                case 3:c=new Color32(217,194,148,255);break;
                case 13:c=new Color32(101,67,47,255);break;
                case 16:c=new Color32(137,96,68,255);break;
                case 7:
                    c=p.y>1.105f?new Color32(145,181,107,255):new Color32(113,155,91,255);
                    if(axis==2&&sign>0)c=new Color32(80,120,75,255);
                    break;
                default:throw new InvalidDataException("Unknown B01 material");
            }
            if(cap)return c;
            float light=axis==1?(sign>0?1.025f:.90f):axis==2?(sign<0?1f:.91f):(sign<0?.97f:.94f);
            return c*light;
        }
#if UNITY_EDITOR
        public static void InvalidatePreparedCache()=>volumes.Clear();
        public static VoxelVolume FromGrid(byte[] grid)
        {
            if(grid==null||grid.Length!=160*160*80||Array.Exists(grid,m=>m!=0&&m!=1&&m!=3&&m!=7&&m!=8&&m!=10&&m!=13&&m!=16))throw new InvalidDataException("Invalid apple grid");
            return new VoxelVolume("B01",p=>{
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
