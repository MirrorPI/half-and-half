using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HalfAndHalf.Presentation
{
    // A01-only Blender-authored volume. Air stays empty through the existing
    // continuous-X cut path; scoring reads the separately baked 2D mass map.
    public sealed class EggplantPixelSource : MonoBehaviour
    {
        public const string Version="A01-pixel-eggplant-v1";
        const int Magic=0x45505831;
        public TextAsset Prepared;
        static readonly Dictionary<TextAsset,VoxelVolume> volumes=new Dictionary<TextAsset,VoxelVolume>();
        public VoxelVolume Volume()
        {
            if(Prepared==null)throw new InvalidOperationException("Missing prepared A01 eggplant");
            if(volumes.TryGetValue(Prepared,out var cached))return cached;
            using(var stream=new MemoryStream(Prepared.bytes,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadInt32()!=Magic||reader.ReadString()!=Version)throw new InvalidDataException("Eggplant volume version mismatch");
                var volume=VoxelVolume.ReadPrepared("A01",Color,null,reader,2);
                if(stream.Position!=stream.Length)throw new InvalidDataException("Unexpected eggplant volume data");
                volumes.Add(Prepared,volume);return volume;
            }
        }
        public static Color32 Color(string id,byte material,Vector3 p,int axis,int sign,bool cap)
        {
            UnityEngine.Color c;
            switch(material)
            {
                case 1:
                    c=new Color32(121,80,142,255);
                    // Broad quiet purple planes; color never changes density.
                    float shoulder=-.10f+.12f*Mathf.Sin(p.x*1.7f+.7f);
                    if(p.z<-.075f&&p.y>shoulder&&p.x<.40f)c=new Color32(143,101,160,255);
                    else if(p.y<-.40f+.16f*p.x||p.z>.20f)c=new Color32(100,65,121,255);
                    if(cap)c=new Color32(111,70,132,255);
                    break;
                case 3:c=new Color32(233,233,205,255);break;
                case 10:c=new Color32(218,221,185,255);break;
                case 8:c=new Color32(186,153,104,255);break;
                case 16:
                    c=p.y>.43f?new Color32(136,155,92,255):new Color32(116,142,79,255);
                    break;
                default:throw new InvalidDataException("Unknown A01 material");
            }
            if(cap)return c;
            float light=axis==1?(sign>0?1.025f:.94f):axis==2?(sign<0?1f:.94f):(sign<0?.97f:.94f);
            return c*light;
        }
#if UNITY_EDITOR
        public static void InvalidatePreparedCache()=>volumes.Clear();
        public static VoxelVolume FromGrid(byte[] grid)
        {
            if(grid==null||grid.Length!=160*112*80||Array.Exists(grid,m=>m!=0&&m!=1&&m!=3&&m!=8&&m!=10&&m!=16))throw new InvalidDataException("Invalid eggplant grid");
            return new VoxelVolume("A01",p=>{
                int x=Mathf.FloorToInt(p.x*64)+80,y=Mathf.FloorToInt((p.y+.875f)*64),z=Mathf.FloorToInt((p.z+.625f)*64);
                if(x<0||x>=160||y<0||y>=112||z<0||z>=80)return 0;
                return grid[(x*112+y)*80+z];
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
