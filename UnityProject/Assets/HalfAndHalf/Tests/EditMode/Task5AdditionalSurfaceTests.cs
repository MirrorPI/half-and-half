using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using HalfAndHalf.Presentation;
using NUnit.Framework;
using UnityEngine;
namespace HalfAndHalf.Tests { public sealed class Task5AdditionalSurfaceTests {
 [TestCase(-0.773f,true,"b1207e99745a7b1b1053be4055e33a019400e57855ada66c931476643802c75f")]
 [TestCase(-0.773f,false,"52531ac4058f00f42aa90bc9295256b4c5448ba1d53d711715cf34483e14c1f5")]
 [TestCase(-1e-07f,true,"e9a31b0d8ec51ef2fd6ea5a6e752984d695defae3d743c1e185f0583d89b3417")]
 [TestCase(-1e-07f,false,"2d6ac4fb68cbdf8bc1c825f75261829fa716c9038b35e302742307358c7579da")]
 [TestCase(-7.5e-08f,true,"746ff622ccacec04df5a01bdb4e7db22f92f1747896f6b9113cd0a4d11dc0230")]
 [TestCase(-7.5e-08f,false,"ae4a1c8043e8eb9b0da31b19ce09c9f680350ae4bda6baa7b4f43f843bb9073a")]
 [TestCase(0.0f,true,"f15e3246e8cdd22a4620aa11a36eb59b3c7b9838ce64361a408fedf49212e961")]
 [TestCase(0.0f,false,"ae4a1c8043e8eb9b0da31b19ce09c9f680350ae4bda6baa7b4f43f843bb9073a")]
 [TestCase(7.5e-08f,true,"f15e3246e8cdd22a4620aa11a36eb59b3c7b9838ce64361a408fedf49212e961")]
 [TestCase(7.5e-08f,false,"52becf90b3afec1c3a25e6448e0aad543d08b116f342a77d94e779705c5c64b0")]
 [TestCase(1e-07f,true,"c33bf3adc3bf795c5ac003373dd831fa959fbb709963cf96991b31a4e11b4065")]
 [TestCase(1e-07f,false,"6cbb9ed7f808bf34f7978c7a5f9a2e0d9652cade8a555c4625abc43587f4c7ed")]
 [TestCase(0.03125f,true,"7868bbbce9070b1d010af374c84b253429900def74c683b35dea691eab4b211a")]
 [TestCase(0.03125f,false,"7360828c948661b8d36bd295a143496b59842a77c0f2ba4edbe49e4c28ed76df")]
 [TestCase(0.217f,true,"4dcf067d13e578234f43072a6744141d3d19f63203f022db6126552b65609c97")]
 [TestCase(0.217f,false,"0cf4408c83ba1d44a9ead3c6ed2afdf74ed42d38252319c328c90386b11bc563")]
 [TestCase(0.21705f,true,"fefd851d9793b084645bbcf17531fcc64ab043fbd3e9367e43026796d62ca7fa")]
 [TestCase(0.21705f,false,"c54483376da38833b6618274980e7d212185aa44bf04f2f029ea914a33e740fd")]
 [TestCase(0.701f,true,"d2068fe4fba1c2a81a16480d5b9d3e29650f2411087eb9b577966fd02c6ca72d")]
 [TestCase(0.701f,false,"95b9191c49fd7f32564eb576f0278c439824c764a8d20560b60b403d28bf4251")]
 public void CarKeepsEveryLegacyColoredFace(float x,bool left,string expected){var mesh=VoxelCarGeometry.Build(x,left);try{Assert.That(Task5SurfacePreservationTests.Hash(mesh),Is.EqualTo(expected));}finally{UnityEngine.Object.DestroyImmediate(mesh);}}
 [TestCase(PrimitiveType.Cube,-2.0f,true,"e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
 [TestCase(PrimitiveType.Cube,-2.0f,false,"431dd991e66f9217b469558a11c139cec00521bf02e65727ba1604da508ed673")]
 [TestCase(PrimitiveType.Cube,-0.37f,true,"e2f23e3d0a8e027cf397471bc5ebe090bddf1dd3a781c18e8cdc54c0fe94817c")]
 [TestCase(PrimitiveType.Cube,-0.37f,false,"469aa58fcd8856ec0ddc5637174094ff9742ac8fe84e753adf8574d42717416a")]
 [TestCase(PrimitiveType.Cube,0.0f,true,"1c724485dae979d15ceba234942b324aa1108dc69b9973893bb86b5bc74d12a3")]
 [TestCase(PrimitiveType.Cube,0.0f,false,"0e90412157a4908b1436777f2991f98b89681e0a49f30c966c1db0506da11bf8")]
 [TestCase(PrimitiveType.Cube,0.217f,true,"215d1d4b768cd351fed507e9f2f597ec2e7c80c1682731c6260042103e768b56")]
 [TestCase(PrimitiveType.Cube,0.217f,false,"1356ce90a6f3d390c6cb62eb85fa25753ced17a95f2402cccaecbccefbdabcdc")]
 [TestCase(PrimitiveType.Cube,0.21705f,true,"d079c56d02d0fa15b5923af05a7f14f676037743e495d71646f44c4af7a7953b")]
 [TestCase(PrimitiveType.Cube,0.21705f,false,"93871118a88a02aa044f60edd3147b7fb26783a4711f681d66d0782ef343fb6c")]
 [TestCase(PrimitiveType.Cube,2.0f,true,"431dd991e66f9217b469558a11c139cec00521bf02e65727ba1604da508ed673")]
 [TestCase(PrimitiveType.Cube,2.0f,false,"e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
 [TestCase(PrimitiveType.Sphere,-2.0f,true,"e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
 [TestCase(PrimitiveType.Sphere,-2.0f,false,"75c2b1fd2b297e3202d938f8f774ac0fedd7f400748804d7d2ffa44bd73347ec")]
 [TestCase(PrimitiveType.Sphere,-0.37f,true,"4aea81143234684c2b192b657b53186c7f5fab0879778164393a234174e3eddd")]
 [TestCase(PrimitiveType.Sphere,-0.37f,false,"f76d89291c3ecbd1a718a1ad115f40576991cd21eb5b0142ffb8184c31f3bb9d")]
 [TestCase(PrimitiveType.Sphere,0.0f,true,"cd3fa4e0388eef3cb143ccb8c39f49d86fef2d40d31aff22485e0277ac96c0b9")]
 [TestCase(PrimitiveType.Sphere,0.0f,false,"648a812bafa1c0884894d71b08f2000e53ce1a8046eba036a21f6d4d0e9afeee")]
 [TestCase(PrimitiveType.Sphere,0.217f,true,"52e4e364c923308a2029e25d9933c6657a470c7d0fb807c0ff3ef343cb4062b3")]
 [TestCase(PrimitiveType.Sphere,0.217f,false,"544a10e43d396b48ea9e648b991539c36937cb04e3e7b748b2580b600be1cd5b")]
 [TestCase(PrimitiveType.Sphere,0.21705f,true,"d13a12acf6afa6fb0e9fe1d9ee808aba4c0033d09212bb716c90981f10681405")]
 [TestCase(PrimitiveType.Sphere,0.21705f,false,"553efbdb1bed2ac5170fbc8592f246f640abaa759722b6a6b62f2893baf5f031")]
 [TestCase(PrimitiveType.Sphere,2.0f,true,"75c2b1fd2b297e3202d938f8f774ac0fedd7f400748804d7d2ffa44bd73347ec")]
 [TestCase(PrimitiveType.Sphere,2.0f,false,"e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
 [TestCase(PrimitiveType.Capsule,-2.0f,true,"e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
 [TestCase(PrimitiveType.Capsule,-2.0f,false,"65e493d323a8efc67be68407f879daf03f38744a1a3be33f59d39c224ebbec0e")]
 [TestCase(PrimitiveType.Capsule,-0.37f,true,"a9643bb6784475a9a86cac095621b5e1f0c6977d271f240a6f53899fa0a4ea59")]
 [TestCase(PrimitiveType.Capsule,-0.37f,false,"4152076ce03c8c8bd70e29eb6b756eda2a3f3bc16ad5260237cadc0d11257701")]
 [TestCase(PrimitiveType.Capsule,0.0f,true,"0acf5e6d57f351300ffc8a9a616418b4a064ab5e3ed30f6f04f996039a959f20")]
 [TestCase(PrimitiveType.Capsule,0.0f,false,"7f32e78f5b1db5aafdc944000969fb9eeacafa8fdaa928eaf1e1503ab73cf14b")]
 [TestCase(PrimitiveType.Capsule,0.217f,true,"83ab80ec62f8611e668c33cd28788be84a5d906ca9351849f6d75f5841732e1a")]
 [TestCase(PrimitiveType.Capsule,0.217f,false,"a06b290598f6a3c79318f838ad1e264abeebf69ff53ecc95ec7ba2c978213aa4")]
 [TestCase(PrimitiveType.Capsule,0.21705f,true,"5eaa6dc984f9d7cb77672d1d3cc0f425b699ece2964a3f05871aa3be9c4e37a9")]
 [TestCase(PrimitiveType.Capsule,0.21705f,false,"d88161a46ae1a44e53aec3a3fedba47fa73a22f0628ceedb94703bb68efb08ce")]
 [TestCase(PrimitiveType.Capsule,2.0f,true,"65e493d323a8efc67be68407f879daf03f38744a1a3be33f59d39c224ebbec0e")]
 [TestCase(PrimitiveType.Capsule,2.0f,false,"e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
 public void ConvexSliceKeepsEveryTriangleAndNormal(PrimitiveType kind,float x,bool left,string expected){var go=GameObject.CreatePrimitive(kind);var mesh=MeshSection.Slice(go.GetComponent<MeshFilter>().sharedMesh,x,left);try{Assert.That(Triangles(mesh),Is.EqualTo(expected));}finally{UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(go);}}
 public static string Triangles(Mesh m){
  var v=m.vertices;var n=m.normals;var rows=new List<string>();
  for(int sub=0;sub<m.subMeshCount;sub++)foreach(int i in m.GetTriangles(sub)){
   var p=v[i];var normal=n[i];rows.Add(sub+","+p.x.ToString("R",CultureInfo.InvariantCulture)+","+p.y.ToString("R",CultureInfo.InvariantCulture)+","+p.z.ToString("R",CultureInfo.InvariantCulture)+","+normal.x.ToString("R",CultureInfo.InvariantCulture)+","+normal.y.ToString("R",CultureInfo.InvariantCulture)+","+normal.z.ToString("R",CultureInfo.InvariantCulture));
  }
  using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",rows)))).Replace("-","").ToLowerInvariant();
 }
}}
