using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HalfAndHalf.Presentation
{
    // N05's Blender composition baked into the same pixel grid/cut renderer as
    // the existing game. The phone only reads precomputed faces and cut columns.
    public sealed class JejuPixelSource : MonoBehaviour
    {
        public const string Version="N05-jeju-pixel-v1";
        public const string FineVersion="N05-jeju-pixel-v2";
        public string ContentVersion=Version;
        public string CurrentVersion=>string.IsNullOrEmpty(ContentVersion)?Version:ContentVersion;
        const int Magic=0x4A505831;
        public TextAsset Prepared;
        static readonly Dictionary<TextAsset,VoxelVolume> volumes=new Dictionary<TextAsset,VoxelVolume>();
        public VoxelVolume Volume()
        {
            if(CurrentVersion!=Version&&CurrentVersion!=FineVersion)throw new InvalidOperationException("Unknown pixel island version");
            if(Prepared==null)throw new InvalidOperationException("Missing prepared pixel island");
            if(volumes.TryGetValue(Prepared,out var cached))
            {
                if(cached.Resolution!=(CurrentVersion==FineVersion?2:1))throw new InvalidDataException("Cached pixel island version mismatch");
                return cached;
            }
            using(var stream=new MemoryStream(Prepared.bytes,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadInt32()!=Magic||reader.ReadString()!=CurrentVersion)throw new InvalidDataException("Pixel island version mismatch");
                var volume=VoxelVolume.ReadPrepared("N05",CurrentVersion==FineVersion?FineColor:Color,null,reader,CurrentVersion==FineVersion?2:1);
                if(stream.Position!=stream.Length)throw new InvalidDataException("Unexpected pixel island data");
                volumes.Add(Prepared,volume);return volume;
            }
        }
        static readonly Color32[] Palette={
            new Color32(0,0,0,0),new Color32(244,231,183,255),new Color32(76,141,155,255),
            new Color32(119,82,57,255),new Color32(255,166,51,255),new Color32(139,101,65,255),
            new Color32(101,124,121,255),new Color32(65,155,101,255),new Color32(113,182,103,255),
            new Color32(69,99,105,255),new Color32(123,185,91,255),new Color32(203,242,223,255),
            new Color32(229,125,74,255),new Color32(238,216,151,255),new Color32(139,101,65,255),
            new Color32(87,111,112,255),new Color32(249,241,205,255),new Color32(81,181,190,255)
        };
        public static Color32 Color(string id,byte material,Vector3 p,int axis,int sign,bool cap)
        {
            UnityEngine.Color c=Palette[material];
            int x=Mathf.FloorToInt(p.x*32),y=Mathf.FloorToInt(p.y*32),z=Mathf.FloorToInt(p.z*32);
            if(cap)
            {
                if(material==10)c=new Color32(173,122,73,255);
                else if(material==7||material==8)c=new Color32(148,184,104,255);
                if(material==9)c=(((y+36)/3)&1)==0?new Color32(68,92,98,255):new Color32(91,111,112,255);
                return c;
            }
            // Three-color grass patches, tile courses and basalt bands are
            // authored on the grid. No textures, smooth normals or per-frame noise.
            float detail=1;
            if(material==10)detail=((x/3+z/3)%7==0)?.91f:((x/3-z/4)%6==0?1.06f:1);
            if(material==7||material==8)detail=((x/2+y/2+z/2)%4==0)?1.10f:1;
            if(material==12)detail=((y+36)%2==0)?1.08f:.95f;
            if(material==9)detail=(((y+36)/2)&1)==0?.91f:1.04f;
            float light=axis==1?(sign>0?1.08f:.70f):axis==2?(sign<0?1:.83f):(sign<0?.87f:.94f);
            return c*(light*detail);
        }
        public static Color32 FineColor(string id,byte material,Vector3 p,int axis,int sign,bool cap)
        {
            if(material==10&&!cap)
            {
                // Position-based grass colors formed long vertical bands; the
                // strong top/front contrast also outlined every voxel terrace.
                // Keep the fine pixel silhouette with a quiet meadow palette.
                float meadowLight=axis==1?(sign>0?1.01f:.85f):axis==2?(sign<0?1:.96f):.98f;
                return (UnityEngine.Color)Palette[material]*meadowLight;
            }
            if(material!=3&&material!=14)return Color(id,material,p,axis,sign,cap);
            UnityEngine.Color c=material==3?new Color32(218,186,113,255):new Color32(255,215,74,255);
            if(cap)return c;
            float light=axis==1?(sign>0?1.08f:.70f):axis==2?(sign<0?1:.83f):(sign<0?.87f:.94f);
            return c*light;
        }
#if UNITY_EDITOR
        public static void InvalidatePreparedCache()=>volumes.Clear();
        public static VoxelVolume FromGrid(byte[] grid,int resolution=1)
        {
            if(resolution!=1&&resolution!=2)throw new ArgumentOutOfRangeException(nameof(resolution));
            int nx=80*resolution,ny=80*resolution,nz=40*resolution;float step=VoxelVolume.Step/resolution;
            if(grid==null||grid.Length!=nx*ny*nz||Array.Exists(grid,m=>m>17))throw new InvalidDataException("Invalid Jeju grid");
            return new VoxelVolume("N05",p=>{
                int x=Mathf.FloorToInt(p.x/step)+nx/2;
                int y=Mathf.FloorToInt((p.y+1.125f)/step);
                int z=Mathf.FloorToInt(p.z/step)+nz/2;
                return grid[(x*ny+y)*nz+z];
            },resolution==2?FineColor:Color,null,resolution);
        }
        public static byte[] Bake(VoxelVolume volume)
        {
            using(var stream=new MemoryStream())
            {
                using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
                {writer.Write(Magic);writer.Write(volume.Resolution==2?FineVersion:Version);volume.WritePrepared(writer);writer.Flush();}
                return stream.ToArray();
            }
        }
#endif
    }
}
