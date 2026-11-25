using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HalfAndHalf.Presentation
{
    // B04-only saved Blender volume. Actual air has neither mass nor cap faces.
    // Gameplay reads the separately baked 2D projection; X remains continuous.
    public sealed class PumpkinPixelSource : MonoBehaviour
    {
        public const string Version="B04-pixel-pumpkin-v1";
        const int Magic=0x50505831;
        public TextAsset Prepared;
        static readonly Dictionary<TextAsset,VoxelVolume> volumes=new Dictionary<TextAsset,VoxelVolume>();
        public VoxelVolume Volume()
        {
            if(Prepared==null)throw new InvalidOperationException("Missing prepared B04 pumpkin");
            if(volumes.TryGetValue(Prepared,out var cached))return cached;
            using(var stream=new MemoryStream(Prepared.bytes,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadInt32()!=Magic||reader.ReadString()!=Version)throw new InvalidDataException("Pumpkin volume version mismatch");
                var volume=VoxelVolume.ReadPrepared("B04",Color,null,reader,2);
                if(stream.Position!=stream.Length)throw new InvalidDataException("Unexpected pumpkin volume data");
                volumes.Add(Prepared,volume);return volume;
            }
        }
        public static Color32 Color(string id,byte material,Vector3 p,int axis,int sign,bool cap)
        {
            UnityEngine.Color c;
            switch(material)
            {
                case 1:
                    // Quiet warm steps follow the geometric ribs. Color never
                    // changes density; no noise, checker pattern or black grooves.
                    float a=Mathf.Atan2(p.z/.548f,(p.x-.008f)/1.015f);
                    float rib=Mathf.Cos(9*a+.26f+.08f*Mathf.Sqrt(Mathf.Max(0,1-Mathf.Pow((p.y+.14f)/.76f,2))));
                    c=rib<-.45f?new Color32(188,107,46,255):rib<.20f?new Color32(211,123,49,255):new Color32(226,140,59,255);
                    if(p.y>.10f&&p.x<.17f&&p.z<-.12f&&rib>.2f)c=new Color32(235,154,72,255);
                    if(p.y<-.65f)c*=.94f;
                    if(cap)c=new Color32(205,119,48,255);
                    break;
                case 3:c=new Color32(246,183,83,255);break;
                case 10:
                    c=new Color32(232,196,122,255);
                    if(!cap)
                    {
                        // Baked interior shadow distinguishes the recessed wall
                        // from the cut lip and the coarse pale attached tissue.
                        // Geometry is still real air; this does not draw a cap.
                        float radius=new Vector3((p.x-.018f)/.672f,(p.y+.17f)/.476f,(p.z-.006f)/.335f).magnitude;
                        if(radius>.975f)c=new Color32(188,140,71,255);
                    }
                    break;
                case 8:c=new Color32(247,233,192,255);break;
                case 16:
                    c=new Color32(121,126,77,255);
                    if(axis==1&&sign>0)c=new Color32(155,149,96,255);
                    break;
                default:throw new InvalidDataException("Unknown B04 material");
            }
            if(cap)return c;
            float light=axis==1?(sign>0?1.025f:.89f):axis==2?(sign<0?1f:.92f):(sign<0?.97f:.93f);
            return c*light;
        }
#if UNITY_EDITOR
        public static void InvalidatePreparedCache()=>volumes.Clear();
        public static VoxelVolume FromGrid(byte[] grid)
        {
            if(grid==null||grid.Length!=160*160*80||Array.Exists(grid,m=>m!=0&&m!=1&&m!=3&&m!=8&&m!=10&&m!=16))throw new InvalidDataException("Invalid pumpkin grid");
            return new VoxelVolume("B04",p=>{
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
