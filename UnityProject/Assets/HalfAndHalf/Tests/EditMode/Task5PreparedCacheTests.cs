using System;using System.IO;using System.Reflection;using System.Security.Cryptography;using HalfAndHalf.Presentation;using NUnit.Framework;using UnityEngine;
namespace HalfAndHalf.Tests {
 public sealed class Task5PreparedCacheTests {
  [Test]public void PreparedCacheValidatesIdentityAndIntegrityBeforeReturningGeometry(){
   var type=typeof(VoxelVolume).Assembly.GetType("HalfAndHalf.Presentation.VoxelPreparationCache");Assert.That(type,Is.Not.Null,"Frozen Task5 preparation must load validated derived geometry instead of recomputing every cell");
   var asset=Resources.Load<TextAsset>("Task5Prepared/L03");Assert.That(asset,Is.Not.Null);var bytes=asset.bytes;Resources.UnloadAsset(asset);
   var palette=(Func<string,byte,Vector3,int,int,bool,Color32>)Delegate.CreateDelegate(typeof(Func<string,byte,Vector3,int,int,bool,Color32>),typeof(VoxelPrototypeGeometry).GetMethod("Color",BindingFlags.NonPublic|BindingFlags.Static));
   var decode=type.GetMethod("TryDecode",BindingFlags.NonPublic|BindingFlags.Static);
   Func<string,byte[],bool> valid=(id,data)=>{object[] args={id,data,palette,null,null};bool ok=(bool)decode.Invoke(null,args);Assert.That(args[4]!=null,Is.EqualTo(ok));return ok;};
   Assert.That(valid("L03",bytes),Is.True);Assert.That(valid("L04",bytes),Is.False);Assert.That(valid("L03",new byte[0]),Is.False);
   var corrupt=(byte[])bytes.Clone();corrupt[corrupt.Length/2]^=1;Assert.That(valid("L03",corrupt),Is.False);
   var wrongFormat=(byte[])bytes.Clone();wrongFormat[4]=99;Resign(wrongFormat);Assert.That(valid("L03",wrongFormat),Is.False,"A valid checksum does not make an unknown schema compatible");
   var truncated=new byte[bytes.Length-64];Array.Copy(bytes,truncated,truncated.Length);Resign(truncated);Assert.That(valid("L03",truncated),Is.False);
  }
  [Test]public void PreparedCacheRejectsInvalidStringLengthWithoutThrowing(){
   var bytes=ReadAsset("L03");for(int i=8;i<13;i++)bytes[i]=0x80;Resign(bytes);Assert.That(Decode("L03",bytes),Is.False);
  }
  [Test]public void PreparedCacheRejectsNullJoinedFacesInPhysicalColumn(){
   var bytes=ReadAsset("L02");int offset=EmptyJoinedOffset(bytes);Assert.That(offset,Is.GreaterThan(0));for(int i=0;i<4;i++)bytes[offset+i]=0xff;Resign(bytes);Assert.That(Decode("L02",bytes),Is.False,"Physical columns need non-null joined arrays; an empty array is valid");
  }
  static byte[] ReadAsset(string id){var asset=Resources.Load<TextAsset>("Task5Prepared/"+id);Assert.That(asset,Is.Not.Null);var data=asset.bytes;Resources.UnloadAsset(asset);return data;}
  static bool Decode(string id,byte[] data){var type=typeof(VoxelVolume).Assembly.GetType("HalfAndHalf.Presentation.VoxelPreparationCache");Assert.That(type,Is.Not.Null);var palette=(Func<string,byte,Vector3,int,int,bool,Color32>)Delegate.CreateDelegate(typeof(Func<string,byte,Vector3,int,int,bool,Color32>),typeof(VoxelPrototypeGeometry).GetMethod("Color",BindingFlags.NonPublic|BindingFlags.Static));object[] args={id,data,palette,null,null};bool valid=(bool)type.GetMethod("TryDecode",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);Assert.That(args[4]!=null,Is.EqualTo(valid));return valid;}
  static int EmptyJoinedOffset(byte[] data){using(var stream=new MemoryStream(data))using(var r=new BinaryReader(stream)){r.ReadInt32();r.ReadInt32();r.ReadString();r.ReadString();stream.Position+=80*80*40;for(int i=0;i<2;i++){int cells=r.ReadInt32();stream.Position+=cells*3;}int faces=r.ReadInt32();stream.Position+=faces*30;for(int i=0;i<160;i++){int all=r.ReadInt32();stream.Position+=Math.Max(0,all)*30;byte alias=r.ReadByte();if(alias==0){int offset=(int)stream.Position,count=r.ReadInt32();if(all>0&&count==0)return offset;stream.Position+=Math.Max(0,count)*30;}}}return -1;}
  static void Resign(byte[] data){using(var hash=SHA256.Create()){var h=hash.ComputeHash(data,0,data.Length-32);Array.Copy(h,0,data,data.Length-32,32);}}
 }
}
