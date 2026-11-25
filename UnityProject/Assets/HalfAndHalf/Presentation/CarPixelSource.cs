using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using HalfAndHalf.Core;
using UnityEngine;

namespace HalfAndHalf.Presentation
{
    // L02 only. Source and payload hashes guard its own offline preparation;
    // the trial renderer, shared density table and Task5 resource cache stay intact.
    public sealed class CarPixelSource : MonoBehaviour
    {
        public const string Version="L02-pixel-car-v1";
        public const float DisplayY=-.22f;
        public const int NX=160,NY=96,NZ=72;
        const int Magic=0x43415231;
        public TextAsset Prepared;
        public string GridHash,RecipeHash;
        VoxelVolume cached;
        TextAsset cachedAsset;
        string cachedGridHash,cachedRecipeHash;
        // The original car-specific game weights. All ten solid IDs have mass.
        public static decimal Density(byte m)
        {
            switch(m){case 0:return 0m;case 1:return 1.3m;case 2:return .35m;case 3:return 1.1m;case 4:return 2.4m;case 5:return 2.2m;case 6:return 6m;case 7:return .3m;case 8:case 9:return .1m;case 10:return 1.6m;default:throw new InvalidDataException("Unknown car material");}
        }
        public VoxelVolume Volume()
        {
            if(Prepared==null)throw new InvalidDataException("Missing prepared L02 car");
            if(cached!=null&&cachedAsset==Prepared&&cachedGridHash==GridHash&&cachedRecipeHash==RecipeHash)return cached;
            byte[] data=Prepared.bytes;int length=data.Length-32;
            if(length<80)throw new InvalidDataException("Truncated car preparation");
            using(var sha=SHA256.Create()){var hash=sha.ComputeHash(data,0,length);for(int i=0;i<32;i++)if(hash[i]!=data[length+i])throw new InvalidDataException("Car preparation checksum mismatch");}
            using(var stream=new MemoryStream(data,0,length,false))using(var r=new BinaryReader(stream))
            {
                if(r.ReadInt32()!=Magic||r.ReadString()!=Version||r.ReadString()!=GridHash||r.ReadString()!=RecipeHash)throw new InvalidDataException("Car source/version mismatch");
                var v=VoxelVolume.ReadPrepared("L02",Color,null,r,2);
                if(stream.Position!=length)throw new InvalidDataException("Unexpected car preparation data");
                cached=v;cachedAsset=Prepared;cachedGridHash=GridHash;cachedRecipeHash=RecipeHash;return v;
            }
        }
        public static Color32 Color(string id,byte m,Vector3 p,int axis,int sign,bool cap)
        {
            UnityEngine.Color c;
            switch(m)
            {
                case 1:c=cap?new Color32(173,161,121,255):p.y>.12f?new Color32(242,200,109,255):p.y<-.45f?new Color32(208,154,63,255):new Color32(230,179,84,255);break;
                case 2:c=cap?new Color32(125,166,169,255):p.y>.09f?new Color32(102,144,153,255):new Color32(73,112,125,255);break;
                case 3:c=new Color32(57,71,74,255);break;
                case 4:c=new Color32(188,199,188,255);break;
                case 5:c=new Color32(88,107,112,255);break;
                case 6:c=new Color32(115,129,124,255);break;
                case 7:c=new Color32(166,127,96,255);break;
                case 8:c=new Color32(242,228,182,255);break;
                case 9:c=new Color32(188,94,77,255);break;
                case 10:c=new Color32(173,126,89,255);break;
                default:throw new InvalidDataException("Unknown car palette material");
            }
            if(cap)return c;
            float light=axis==1?(sign>0?1.035f:.88f):axis==2?(sign<0?1f:.90f):(sign<0?.94f:.97f);return c*light;
        }
#if UNITY_EDITOR
        public static VoxelVolume FromGrid(byte[] grid)
        {
            if(grid==null||grid.Length!=NX*NY*NZ||Array.Exists(grid,m=>m>10))throw new InvalidDataException("Invalid car grid");
            return new VoxelVolume("L02",p=>{
                int x=Mathf.FloorToInt((p.x+1.25f)*64),y=Mathf.FloorToInt((p.y+1.125f)*64),z=Mathf.FloorToInt((p.z+.5625f)*64);
                if(x<0||x>=NX||y<0||y>=NY||z<0||z>=NZ)return 0;
                return grid[(x*NY+y)*NZ+z];
            },Color,null,2);
        }
        public static MassMap Mass(byte[] grid)
        {
            if(grid.Length!=NX*NY*NZ)throw new InvalidDataException("Invalid car mass grid");
            var p=new decimal[NX,NY];decimal total=0;
            for(int x=0;x<NX;x++)for(int y=0;y<NY;y++)for(int z=0;z<NZ;z++)
            {decimal w=Density(grid[(x*NY+y)*NZ+z]);p[x,y]+=w;total+=w;}
            if(total<=0)throw new InvalidDataException("Empty car mass");
            var cells=new List<MassCell>();decimal sum=0;
            for(int x=0;x<NX;x++)for(int y=0;y<NY;y++)if(p[x,y]>0)
            {decimal xx=-1.25m+x/64m,yy=-1.125m+y/64m-.22m,m=Math.Round(100m*p[x,y]/total,12);cells.Add(new MassCell(xx,xx+1m/64m,yy,yy+1m/64m,m));sum+=m;}
            var last=cells[cells.Count-1];cells[cells.Count-1]=new MassCell(last.MinX,last.MaxX,last.MinY,last.MaxY,last.Mass+100m-sum);return new MassMap(cells);
        }
        public static string Hash(byte[] data){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
        public static string CurrentRecipeHash()=>Hash(System.Text.Encoding.UTF8.GetBytes(File.ReadAllText("Assets/HalfAndHalf/Presentation/CarPixelSource.cs")+File.ReadAllText("Assets/HalfAndHalf/Presentation/VoxelVolume.cs")));
        public static byte[] Bake(VoxelVolume v,string gridHash,string recipeHash)
        {
            using(var stream=new MemoryStream())
            {
                using(var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true)){w.Write(Magic);w.Write(Version);w.Write(gridHash);w.Write(recipeHash);v.WritePrepared(w);w.Flush();}
                var payload=stream.ToArray();using(var sha=SHA256.Create()){var hash=sha.ComputeHash(payload);var bytes=new byte[payload.Length+32];Buffer.BlockCopy(payload,0,bytes,0,payload.Length);Buffer.BlockCopy(hash,0,bytes,payload.Length,32);return bytes;}
            }
        }
#endif
    }
#if UNITY_EDITOR
    public sealed class CarPixelBuildGuard:UnityEditor.Build.IPreprocessBuildWithReport
    {
        public int callbackOrder=>-90;
        public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
        {
            var a=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HalfAndHalf/Content/L02/PixelCar/L02PixelCar.prefab");
            if(a==null)throw new UnityEditor.Build.BuildFailedException("Build L02 pixel car first");
            var source=a.GetComponent<CarPixelSource>();
            if(source==null||source.RecipeHash!=CarPixelSource.CurrentRecipeHash()||source.GridHash!=CarPixelSource.Hash(File.ReadAllBytes("../art/L02-car/pixel-v1/car-grid.bytes")))throw new UnityEditor.Build.BuildFailedException("Stale L02 car preparation; rebake and run BuildCarPixel.Build");
            source.Volume();
        }
    }
#endif
}
