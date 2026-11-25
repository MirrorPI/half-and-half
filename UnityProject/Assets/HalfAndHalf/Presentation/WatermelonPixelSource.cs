using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HalfAndHalf.Presentation
{
    // B03-only Blender-authored volume. Air stays empty through the existing
    // continuous-X cut path; scoring reads the separately baked 2D mass map.
    public sealed class WatermelonPixelSource : MonoBehaviour
    {
        public const string Version="B03-pixel-watermelon-v1";
        const int Magic=0x57505831;
        public TextAsset Prepared;
        static readonly Dictionary<TextAsset,VoxelVolume> volumes=new Dictionary<TextAsset,VoxelVolume>();
        public VoxelVolume Volume()
        {
            if(Prepared==null)throw new InvalidOperationException("Missing prepared B03 watermelon");
            if(volumes.TryGetValue(Prepared,out var cached))return cached;
            using(var stream=new MemoryStream(Prepared.bytes,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadInt32()!=Magic||reader.ReadString()!=Version)throw new InvalidDataException("Watermelon volume version mismatch");
                var volume=VoxelVolume.ReadPrepared("B03",Color,null,reader,2);
                if(stream.Position!=stream.Length)throw new InvalidDataException("Unexpected watermelon volume data");
                volumes.Add(Prepared,volume);return volume;
            }
        }
        public static Color32 Color(string id,byte material,Vector3 p,int axis,int sign,bool cap)
        {
            UnityEngine.Color c;
            switch(material)
            {
                case 1:
                    var q=new Vector3((p.x-.008f)/1.055f,(p.y+.12f)/.91f,p.z/.603f);
                    var pole=new Vector3(-.18f,.965f,-.19f).normalized;
                    var u=new Vector3(1,.18f,0).normalized;var v=Vector3.Cross(pole,u).normalized;
                    float t=Vector3.Dot(q,pole),theta=Mathf.Atan2(Vector3.Dot(q,v),Vector3.Dot(q,u));
                    float phase=10*theta+.35f*Mathf.Sin(4.2f*t+1.5f*theta)+.16f*Mathf.Sin(9*t-2*theta);
                    bool stripe=Mathf.Cos(phase)>.24f+.09f*Mathf.Sin(3*theta+2*t);
                    c=stripe?new Color32(53,107,64,255):new Color32(121,163,79,255);
                    break;
                case 8:c=new Color32(220,230,181,255);break;
                case 3:c=new Color32(230,101,104,255);break;
                case 10:c=new Color32(222,91,97,255);break;
                case 13:c=new Color32(76,52,45,255);break;
                case 16:c=new Color32(139,121,80,255);break;
                default:throw new InvalidDataException("Unknown B03 material");
            }
            if(cap)return c;
            float light=axis==1?(sign>0?1.025f:.92f):axis==2?(sign<0?1f:.94f):(sign<0?.98f:.94f);
            return c*light;
        }

#if UNITY_EDITOR
        public static void InvalidatePreparedCache()=>volumes.Clear();
        public static VoxelVolume FromGrid(byte[] grid)
        {
            if(grid==null||grid.Length!=160*160*80||Array.Exists(grid,m=>m!=0&&m!=1&&m!=3&&m!=8&&m!=10&&m!=13&&m!=16))throw new InvalidDataException("Invalid watermelon grid");
            return new VoxelVolume("B03",p=>{
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
