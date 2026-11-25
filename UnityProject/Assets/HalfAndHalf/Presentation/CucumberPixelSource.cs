using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HalfAndHalf.Presentation
{
    // B02-only Blender-authored volume. Air stays empty through the existing
    // continuous-X cut path; scoring reads the separately baked 2D mass map.
    public sealed class CucumberPixelSource : MonoBehaviour
    {
        public const string Version="B02-pixel-cucumber-v1";
        const int Magic=0x43505831;
        public TextAsset Prepared;
        static readonly Dictionary<TextAsset,VoxelVolume> volumes=new Dictionary<TextAsset,VoxelVolume>();
        public VoxelVolume Volume()
        {
            if(Prepared==null)throw new InvalidOperationException("Missing prepared B02 cucumber");
            if(volumes.TryGetValue(Prepared,out var cached))return cached;
            using(var stream=new MemoryStream(Prepared.bytes,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadInt32()!=Magic||reader.ReadString()!=Version)throw new InvalidDataException("Cucumber volume version mismatch");
                var volume=VoxelVolume.ReadPrepared("B02",Color,null,reader,2);
                if(stream.Position!=stream.Length)throw new InvalidDataException("Unexpected cucumber volume data");
                volumes.Add(Prepared,volume);return volume;
            }
        }
        public static Color32 Color(string id,byte material,Vector3 p,int axis,int sign,bool cap)
        {
            UnityEngine.Color c;
            switch(material)
            {
                case 1:
                    c=new Color32(92,145,72,255);
                    // Broad, nonperiodic green areas: no repeated dark stripes or dots.
                    float cy=-.024f+.052f*(p.x/1.12f)*(p.x/1.12f)+.012f*p.x/1.12f;
                    if(p.y>cy+.075f+.065f*Mathf.Sin(p.x*1.9f+.5f)&&p.z<-.035f&&p.x<.65f-.8f*p.z)c=new Color32(111,159,81,255);
                    else if(p.y<cy-.23f+.075f*p.x||p.z>.19f)c=new Color32(75,127,65,255);
                    if(cap)c=new Color32(83,135,66,255);
                    break;
                case 13:c=p.y>.10f?new Color32(104,153,77,255):new Color32(82,134,66,255);break;
                case 3:c=new Color32(220,232,185,255);break;
                case 10:c=new Color32(201,221,164,255);break;
                case 8:c=new Color32(242,239,207,255);break;
                case 16:c=new Color32(113,132,75,255);break;
                default:throw new InvalidDataException("Unknown B02 material");
            }
            if(cap)return c;
            float light=axis==1?(sign>0?1.025f:.94f):axis==2?(sign<0?1f:.94f):(sign<0?.97f:.94f);
            return c*light;
        }
#if UNITY_EDITOR
        public static void InvalidatePreparedCache()=>volumes.Clear();
        public static VoxelVolume FromGrid(byte[] grid)
        {
            if(grid==null||grid.Length!=160*64*48||Array.Exists(grid,m=>m!=0&&m!=1&&m!=3&&m!=8&&m!=10&&m!=13&&m!=16))throw new InvalidDataException("Invalid cucumber grid");
            return new VoxelVolume("B02",p=>{
                int x=Mathf.FloorToInt(p.x*64)+80,y=Mathf.FloorToInt((p.y+.5f)*64),z=Mathf.FloorToInt((p.z+.375f)*64);
                if(x<0||x>=160||y<0||y>=64||z<0||z>=48)return 0;
                return grid[(x*64+y)*48+z];
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
