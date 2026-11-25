using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
using HalfAndHalf.Verification;
#endif
namespace HalfAndHalf.Presentation
{
    // Derived rendering preparation only. Official scoring still reads the authored mass JSON.
    internal static class VoxelPreparationCache
    {
        const int Magic=0x35504848,Format=1,MaximumBytes=16*1024*1024;
        internal static string Version(string id)=>id=="L02"?VoxelCarGeometry.Version:Array.IndexOf(VoxelPrototypeGeometry.Ids,id)>=0?VoxelPrototypeGeometry.Version(id):null;
        internal static bool TryLoad(string id,Func<string,byte,Vector3,int,int,bool,Color32> palette,Func<Color32,bool> matchEdges,out VoxelVolume volume)
        {
            volume=null;if(Version(id)==null)return false;
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            double started=SelectionPreparationReview.Start();
#endif
            TextAsset asset=null;
            try
            {
                asset=Resources.Load<TextAsset>("Task5Prepared/"+id);if(asset==null)return false;
                bool loaded=TryDecode(id,asset.bytes,palette,matchEdges,out volume);
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
                if(loaded&&SelectionPreparationReview.Active)SelectionPreparationReview.PreparedCacheLoadedThisSelect=true;
#endif
                return loaded;
            }
            finally
            {
                if(asset!=null)Resources.UnloadAsset(asset);
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
                SelectionPreparationReview.Finish(ref SelectionPreparationReview.CacheLoadMs,started);
#endif
            }
        }
        internal static bool TryDecode(string id,byte[] data,Func<string,byte,Vector3,int,int,bool,Color32> palette,Func<Color32,bool> matchEdges,out VoxelVolume volume)
        {
            volume=null;if(data==null||data.Length<48||data.Length>MaximumBytes||Version(id)==null)return false;
            try
            {
                int length=data.Length-32;
                using(var sha=SHA256.Create()){var expected=sha.ComputeHash(data,0,length);for(int i=0;i<32;i++)if(expected[i]!=data[length+i])return false;}
                using(var stream=new MemoryStream(data,0,length,false))using(var reader=new BinaryReader(stream))
                {
                    if(reader.ReadInt32()!=Magic||reader.ReadInt32()!=Format||reader.ReadString()!=id||reader.ReadString()!=Version(id))return false;
                    var decoded=VoxelVolume.ReadPrepared(id,palette,matchEdges,reader);if(stream.Position!=length)return false;
                    volume=decoded;return true;
                }
            }
            catch(Exception e)when(e is FormatException||e is InvalidDataException||e is IOException||e is ArgumentException||e is OverflowException){return false;}
        }
#if UNITY_EDITOR
        internal static byte[] Encode(string id,VoxelVolume volume)
        {
            using(var stream=new MemoryStream())
            {
                using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true)){writer.Write(Magic);writer.Write(Format);writer.Write(id);writer.Write(Version(id));volume.WritePrepared(writer);writer.Flush();}
                var payload=stream.ToArray();using(var sha=SHA256.Create()){var digest=sha.ComputeHash(payload);var data=new byte[payload.Length+32];Buffer.BlockCopy(payload,0,data,0,payload.Length);Buffer.BlockCopy(digest,0,data,payload.Length,32);return data;}
            }
        }
#endif
    }
#if UNITY_EDITOR
    // A source change requires rebaking derived data before any build can use it.
    public sealed class VoxelPreparationCacheBuildGuard:UnityEditor.Build.IPreprocessBuildWithReport
    {
        public int callbackOrder=>-100;
        public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)=>Validate();
        public static void Validate()
        {
            const string path="Assets/HalfAndHalf/Resources/Task5Prepared/manifest.json";
            if(!File.Exists(path))throw new UnityEditor.Build.BuildFailedException("Bake Task5 derived preparation caches before building");
            var manifest=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
            if((int)manifest["format"]!=1)throw new UnityEditor.Build.BuildFailedException("Unsupported Task5 preparation cache manifest");
            var sources=(Newtonsoft.Json.Linq.JObject)manifest["sources"];var caches=(Newtonsoft.Json.Linq.JObject)manifest["caches"];
            string[] required={"Assets/HalfAndHalf/Presentation/VoxelVolume.cs","Assets/HalfAndHalf/Presentation/VoxelPrototypeGeometry.cs","Assets/HalfAndHalf/Presentation/VoxelCarGeometry.cs","Assets/HalfAndHalf/Presentation/VoxelPreparationCache.cs"};
            if(sources.Count!=required.Length||caches.Count!=8)throw new UnityEditor.Build.BuildFailedException("Incomplete Task5 preparation cache manifest");
            foreach(string source in required)if(!File.Exists(source)||Hash(File.ReadAllBytes(source))!=(string)sources[source])throw new UnityEditor.Build.BuildFailedException("Stale Task5 preparation cache; rebake after source change: "+source);
            foreach(string id in new[]{"L02","L03","L04","L05","N01","N02","N04","N05"})
            {
                string cache="Assets/HalfAndHalf/Resources/Task5Prepared/"+id+".bytes";
                if(!File.Exists(cache)||Hash(File.ReadAllBytes(cache))!=(string)caches[id])throw new UnityEditor.Build.BuildFailedException("Missing or changed Task5 preparation cache: "+id);
            }
        }
        internal static string Hash(byte[] data){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
    }
#endif
}
