using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HalfAndHalf.Presentation
{
    // A04-only Blender-authored volume. Air stays empty through the existing
    // continuous-X cut path; scoring reads the separately baked 2D mass map.
    public sealed class HammerPixelSource : MonoBehaviour
    {
        public const string Version="A04-pixel-hammer-v1";
        const int Magic=0x45505831;
        public TextAsset Prepared;
        static readonly Dictionary<TextAsset,VoxelVolume> volumes=new Dictionary<TextAsset,VoxelVolume>();
        public VoxelVolume Volume()
        {
            if(Prepared==null)throw new InvalidOperationException("Missing prepared A04 hammer");
            if(volumes.TryGetValue(Prepared,out var cached))return cached;
            using(var stream=new MemoryStream(Prepared.bytes,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadInt32()!=Magic||reader.ReadString()!=Version)throw new InvalidDataException("Hammer volume version mismatch");
                var volume=VoxelVolume.ReadPrepared("A04",Color,null,reader,2);
                if(stream.Position!=stream.Length)throw new InvalidDataException("Unexpected hammer volume data");
                volumes.Add(Prepared,volume);return volume;
            }
        }
        public static Color32 Color(string id,byte material,Vector3 p,int axis,int sign,bool cap)
        {
            UnityEngine.Color c;
            switch(material)
            {
                case 6:c=p.y>.35f?new Color32(175,193,197,255):new Color32(116,140,147,255);if(cap)c=new Color32(152,169,174,255);break;
                case 16:c=new Color32(183,127,66,255);if(Mathf.Sin(p.x*13+p.z*10)>.75f)c=new Color32(167,111,57,255);if(cap)c=new Color32(211,165,100,255);break;
                default:throw new InvalidDataException("Unknown A04 material");
            }
            if(cap)return c;
            float light=axis==1?(sign>0?1.025f:.94f):axis==2?(sign<0?1f:.94f):(sign<0?.97f:.94f);
            return c*light;
        }
#if UNITY_EDITOR
        public static void InvalidatePreparedCache()=>volumes.Clear();
        public static VoxelVolume FromGrid(byte[] grid)
        {
            if(grid==null||grid.Length!=160*112*80||Array.Exists(grid,m=>m!=0&&m!=6&&m!=16))throw new InvalidDataException("Invalid hammer grid");
            return new VoxelVolume("A04",p=>{
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
