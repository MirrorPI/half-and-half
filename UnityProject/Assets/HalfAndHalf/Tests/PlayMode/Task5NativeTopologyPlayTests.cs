#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using HalfAndHalf.Content;
using HalfAndHalf.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HalfAndHalf.Tests
{
    public sealed class Task5NativeTopologyPlayTests
    {
        const BindingFlags Instance=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
        // Overwriting the first source after a later part fails must not orphan its native owner.
        [UnityTest]public IEnumerator PartialWarmRetryReleasesPreviousNativeOwner()
        {
            var preparedType=typeof(MeshSection).GetNestedType("Prepared",BindingFlags.NonPublic);
            var nativeType=typeof(MeshSection).Assembly.GetType("HalfAndHalf.Presentation.NativeCutTopology");
            var release=preparedType.GetMethod("ReleaseNativeTopology",Instance);
            Assert.That(nativeType,Is.Not.Null);Assert.That(release,Is.Not.Null);
            var owners=nativeType.GetProperty("ActiveOwners",Static);var burst=nativeType.GetProperty("BurstEnabled",Static);
            Assert.That(owners,Is.Not.Null);Assert.That(burst,Is.Not.Null);
            if(!(bool)burst.GetValue(null))Assert.Ignore("Burst disabled: partial Warm native ownership requires a Burst-enabled environment");
            int baseline=(int)owners.GetValue(null);
            var firstMesh=Triangle("First valid triangle");var secondMesh=Triangle("Second initially invalid triangle");secondMesh.normals=Array.Empty<Vector3>();
            var model=new GameObject("Partial warm fixture");var host=new GameObject("Native warm retry owner");
            var asset=ScriptableObject.CreateInstance<VisualSampleAsset>();object firstSource=null;SampleVisual visual=null;
            try
            {
                foreach(var mesh in new[]{firstMesh,secondMesh})
                {
                    var child=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));
                    child.transform.SetParent(model.transform,false);child.GetComponent<MeshFilter>().sharedMesh=mesh;
                }
                asset.ObjectId="native-warm-retry";asset.Model=model;
                visual=host.AddComponent<SampleVisual>();visual.Show(asset);
                Assert.Throws<IndexOutOfRangeException>(()=>visual.Warm(),"The second part must fail after the first acquires its owner");
                var parts=(IList)typeof(SampleVisual).GetField("parts",Instance).GetValue(visual);
                Assert.That(parts.Count,Is.EqualTo(2));
                firstSource=parts[0].GetType().GetField("Source",Instance).GetValue(parts[0]);
                Assert.That(firstSource,Is.Not.Null);Assert.That((int)owners.GetValue(null),Is.EqualTo(baseline+1));
                secondMesh.normals=firstMesh.normals;
                visual.Warm();
                Assert.That((int)owners.GetValue(null),Is.EqualTo(baseline+2),"Retry must release the first source before replacing it, leaving only the two current owners");
                UnityEngine.Object.Destroy(host);yield return null;
                Assert.That((int)owners.GetValue(null),Is.EqualTo(baseline),"Owner destruction must release both current sources");
            }
            finally
            {
                // The intentional RED can orphan this saved owner; release it independently.
                if(firstSource!=null)release.Invoke(firstSource,null);
                // A failing assertion cannot yield for deferred destruction. Release live current owners now.
                if(visual!=null)typeof(SampleVisual).GetMethod("ClearAll",Instance).Invoke(visual,null);
                UnityEngine.Object.Destroy(host);UnityEngine.Object.Destroy(model);UnityEngine.Object.Destroy(asset);
                UnityEngine.Object.Destroy(firstMesh);UnityEngine.Object.Destroy(secondMesh);
                Assert.That((int)owners.GetValue(null),Is.EqualTo(baseline),"RED/GREEN cleanup must restore the owner baseline");
            }
            yield return null;
        }
        static Mesh Triangle(string name)
        {
            var mesh=new Mesh{name=name};
            mesh.vertices=new[]{new Vector3(-1,.7f,-.2f),new Vector3(1,-.4f,.8f),new Vector3(1,.1f,-.9f)};
            mesh.normals=new[]{Vector3.forward,Vector3.up,Vector3.right};mesh.triangles=new[]{0,1,2};return mesh;
        }
    }
}
#endif
