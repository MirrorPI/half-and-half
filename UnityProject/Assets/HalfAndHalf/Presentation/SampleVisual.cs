using System.Collections.Generic;
using HalfAndHalf.Content;
using UnityEngine;

namespace HalfAndHalf.Presentation
{
    public sealed class SampleVisual : MonoBehaviour
    {
        public VisualSampleAsset Asset;
        GameObject whole;
        Transform leftPool,rightPool;
        public Transform Left {get;private set;}
        public Transform Right {get;private set;}
        readonly List<Part> parts=new List<Part>();
        readonly List<Mesh> owned=new List<Mesh>();
        bool warmed;
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
        public double ReviewWarmMs {get;private set;}
#endif
        public float SectionTop {get;private set;}
        float leftY,rightY;
        sealed class Part
        {
            public MeshFilter Original,Left,Right;
            public MeshRenderer LeftRenderer,RightRenderer;
            public float Bias,LeftRest,RightRest;
            public MeshSection.Prepared Source;
            public Mesh FullLeft,FullRight,CutLeft,CutRight;
        }
        public void Show(VisualSampleAsset asset)
        {
            ClearAll();Asset=asset;whole=Instantiate(asset.Model,transform,false);whole.name=asset.ObjectId+" Whole";
            leftPool=new GameObject("Prepared left").transform;rightPool=new GameObject("Prepared right").transform;
            leftPool.SetParent(transform,false);rightPool.SetParent(transform,false);leftPool.gameObject.SetActive(false);rightPool.gameObject.SetActive(false);
            foreach(var filter in whole.GetComponentsInChildren<MeshFilter>())
            {
                var part=filter.GetComponent<SamplePart>();if(part!=null&&part.Density<=0)continue;
                var skin=filter.GetComponent<MeshRenderer>().sharedMaterial;var section=part!=null&&part.Section!=null?part.Section:Asset.Section;
                var p=new Part{Original=filter,Bias=part!=null?part.SectionBias:0};
                p.Left=Holder(filter.sharedMesh.name,leftPool,skin,section,out p.LeftRenderer);
                p.Right=Holder(filter.sharedMesh.name,rightPool,skin,section,out p.RightRenderer);parts.Add(p);
            }
        }
        static MeshFilter Holder(string name,Transform parent,Material skin,Material section,out MeshRenderer renderer)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterials=new[]{skin,section};renderer.enabled=false;return go.GetComponent<MeshFilter>();
        }
        // Called at the landed pose, before input starts. Prepare also supports standalone callers.
        public void Warm()
        {
            if(warmed||whole==null)return;
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
            double warmStart=Time.realtimeSinceStartupAsDouble;
#endif
            foreach(var p in parts)
            {
                var baked=Instantiate(p.Original.sharedMesh);var v=baked.vertices;
                var matrix=transform.worldToLocalMatrix*p.Original.transform.localToWorldMatrix;
                for(int i=0;i<v.Length;i++)v[i]=matrix.MultiplyPoint3x4(v[i]);baked.vertices=v;baked.RecalculateBounds();
                p.Source?.ReleaseNativeTopology();
                p.Source=new MeshSection.Prepared(baked);Destroy(baked);
                p.FullLeft=Own(MeshSection.Slice(p.Source,float.PositiveInfinity,true));Bias(p.FullLeft,p.Bias);
                p.FullRight=Own(MeshSection.Slice(p.Source,float.NegativeInfinity,false));Bias(p.FullRight,-p.Bias);
                p.LeftRest=MeshSection.RestY(p.FullLeft,Quaternion.Euler(0,20,-Asset.Tilt),-1.305f);
                p.RightRest=MeshSection.RestY(p.FullRight,Quaternion.Euler(0,-20,Asset.Tilt),-1.305f);
                p.CutLeft=Own(new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32});p.CutRight=Own(new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32});
                p.Source.EnableNativeTopology();
            }
            warmed=true;
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
            ReviewWarmMs=(Time.realtimeSinceStartupAsDouble-warmStart)*1000;
#endif
        }
        Mesh Own(Mesh mesh){owned.Add(mesh);return mesh;}
        static void Bias(Mesh mesh,float bias)
        {
            if(bias==0)return;var v=mesh.vertices;for(int i=0;i<v.Length;i++)v[i].x+=bias;mesh.vertices=v;mesh.RecalculateBounds();
        }
        public void ResetAttempt(){ClearPieces();if(whole!=null)whole.SetActive(true);}
        public void Prepare(float x)
        {
            Warm();ClearPieces();SectionTop=float.NegativeInfinity;leftY=rightY=float.NegativeInfinity;
            Left=leftPool;Right=rightPool;
            foreach(var p in parts)
            {
                SetPart(p,x,true,ref leftY);SetPart(p,x,false,ref rightY);
            }
            if(float.IsInfinity(leftY))leftY=0;if(float.IsInfinity(rightY))rightY=0;
        }
        void SetPart(Part p,float x,bool left,ref float rest)
        {
            var filter=left?p.Left:p.Right;var renderer=left?p.LeftRenderer:p.RightRenderer;
            if(left?x<p.Source.Min:x>p.Source.Max){renderer.enabled=false;filter.sharedMesh=null;return;}
            bool full=left?x>=p.Source.Max:x<=p.Source.Min;Mesh mesh;
            if(full){mesh=left?p.FullLeft:p.FullRight;rest=Mathf.Max(rest,left?p.LeftRest:p.RightRest);}
            else
            {
                mesh=MeshSection.Slice(p.Source,x,left,left?p.CutLeft:p.CutRight,left?p.Bias:-p.Bias);
                if(mesh.vertexCount>0)rest=Mathf.Max(rest,MeshSection.RestY(p.Source,Quaternion.Euler(0,left?20:-20,left?-Asset.Tilt:Asset.Tilt),-1.305f));
                SectionTop=Mathf.Max(SectionTop,MeshSection.Top(p.Source));
            }
            filter.sharedMesh=mesh;renderer.enabled=mesh.vertexCount>0;
        }
        public void Animate(float split,float imbalance)
        {
            whole.SetActive(false);Left.gameObject.SetActive(true);Right.gameObject.SetActive(true);
            Left.localPosition=new Vector3(-Asset.Separation*split,(leftY+imbalance*.17f)*split,0);
            Right.localPosition=new Vector3(Asset.Separation*split,(rightY-imbalance*.17f)*split,0);
            Left.localRotation=Quaternion.Euler(0,20*split,-Asset.Tilt*split);Right.localRotation=Quaternion.Euler(0,-20*split,Asset.Tilt*split);
        }
        void ClearPieces()
        {
            if(leftPool!=null){leftPool.gameObject.SetActive(false);leftPool.localPosition=Vector3.zero;leftPool.localRotation=Quaternion.identity;}
            if(rightPool!=null){rightPool.gameObject.SetActive(false);rightPool.localPosition=Vector3.zero;rightPool.localRotation=Quaternion.identity;}
            Left=Right=null;
        }
        void ClearAll()
        {
            ClearPieces();if(whole!=null){whole.SetActive(false);Destroy(whole);}whole=null;
            if(leftPool!=null)Destroy(leftPool.gameObject);if(rightPool!=null)Destroy(rightPool.gameObject);leftPool=rightPool=null;
            foreach(var p in parts)p.Source?.ReleaseNativeTopology();
            foreach(var m in owned)if(m!=null)Destroy(m);owned.Clear();parts.Clear();warmed=false;
        }
        void OnDestroy(){ClearAll();}
    }
}
