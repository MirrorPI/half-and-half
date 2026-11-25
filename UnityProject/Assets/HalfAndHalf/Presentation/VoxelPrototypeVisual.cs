using System;
using HalfAndHalf.Content;
using UnityEngine;

#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
using HalfAndHalf.Verification;
#endif

namespace HalfAndHalf.Presentation
{
    public sealed class VoxelPrototypeVisual : MonoBehaviour
    {
        public Material Palette;
        public VisualSampleAsset Asset {get;private set;}
        VoxelVolume volume;
        Transform whole,decor,left,right;
        Mesh body,background,leftMesh,rightMesh;
        float leftY,rightY;
        Vector3 authoredPosition;
        bool positionCaptured;
        public int FragmentCount=>(left!=null?1:0)+(right!=null?1:0);
        public float SectionTop {get;private set;}
        public static bool CanRender(VisualSampleAsset a)=>a!=null&&a.Model!=null&&(a.Model.GetComponent<VoxelPrototypeSource>()!=null||a.Model.GetComponent<JejuPixelSource>()!=null||a.Model.GetComponent<FanPixelSource>()!=null||a.Model.GetComponent<ApplePixelSource>()!=null||a.Model.GetComponent<CucumberPixelSource>()!=null||a.Model.GetComponent<WatermelonPixelSource>()!=null||a.Model.GetComponent<PumpkinPixelSource>()!=null||a.Model.GetComponent<EggplantPixelSource>()!=null||a.Model.GetComponent<BananaPixelSource>()!=null||a.Model.GetComponent<BananaRefinedPixelSource>()!=null||a.Model.GetComponent<HammerPixelSource>()!=null||a.Model.GetComponent<AuthoredPixelSource>()!=null);
        public void Show(VisualSampleAsset asset)
        {
            var source=asset.Model.GetComponent<VoxelPrototypeSource>();
            var jeju=asset.Model.GetComponent<JejuPixelSource>();
            var fan=asset.Model.GetComponent<FanPixelSource>();
            var apple=asset.Model.GetComponent<ApplePixelSource>();
            var watermelon=asset.Model.GetComponent<WatermelonPixelSource>();
            var cucumber=asset.Model.GetComponent<CucumberPixelSource>();
            var pumpkin=asset.Model.GetComponent<PumpkinPixelSource>();
            var authored=asset.Model.GetComponent<AuthoredPixelSource>();
            var hammer=asset.Model.GetComponent<HammerPixelSource>();
            var bananaRefined=asset.Model.GetComponent<BananaRefinedPixelSource>();
            var banana=asset.Model.GetComponent<BananaPixelSource>();
            var eggplant=asset.Model.GetComponent<EggplantPixelSource>();
            if(authored!=null?asset.ObjectId!=authored.ObjectId||asset.ContentVersion!=authored.ContentVersion:bananaRefined!=null?asset.ObjectId!="A02"||asset.ContentVersion!=BananaRefinedPixelSource.Version:hammer!=null?asset.ObjectId!="A04"||asset.ContentVersion!=HammerPixelSource.Version:banana!=null?asset.ObjectId!="A02"||asset.ContentVersion!=BananaPixelSource.Version:eggplant!=null?asset.ObjectId!="A01"||asset.ContentVersion!=EggplantPixelSource.Version:pumpkin!=null?asset.ObjectId!="B04"||asset.ContentVersion!=PumpkinPixelSource.Version:watermelon!=null?asset.ObjectId!="B03"||asset.ContentVersion!=WatermelonPixelSource.Version:cucumber!=null?asset.ObjectId!="B02"||asset.ContentVersion!=CucumberPixelSource.Version:apple!=null?asset.ObjectId!="B01"||asset.ContentVersion!=ApplePixelSource.Version:fan!=null?asset.ObjectId!="L01"||asset.ContentVersion!=FanPixelSource.Version:jeju!=null?asset.ObjectId!="N05"||asset.ContentVersion!=jeju.CurrentVersion:source==null||source.ObjectId!=asset.ObjectId||asset.ContentVersion!=VoxelPrototypeGeometry.Version(source.ObjectId))throw new ArgumentException("Voxel model/content version mismatch");
            if(!positionCaptured){authoredPosition=transform.localPosition;positionCaptured=true;}
            transform.localPosition=authoredPosition;
            ClearAll();Asset=asset;
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            double started=SelectionPreparationReview.Start();
#endif
            volume=authored!=null?authored.Volume():bananaRefined!=null?bananaRefined.Volume():hammer!=null?hammer.Volume():banana!=null?banana.Volume():eggplant!=null?eggplant.Volume():pumpkin!=null?pumpkin.Volume():watermelon!=null?watermelon.Volume():cucumber!=null?cucumber.Volume():apple!=null?apple.Volume():fan!=null?fan.Volume():jeju!=null?jeju.Volume():VoxelPrototypeGeometry.For(source.ObjectId);Palette=asset.Section;
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Finish(ref SelectionPreparationReview.VoxelGridMs,started);started=SelectionPreparationReview.Start();
#endif

            body=volume.Build(float.PositiveInfinity,true);
            // B03 rests on the same tray plane used for its cut pieces. Keep
            // the authored position for every existing approved source.
            if(watermelon!=null)transform.localPosition=new Vector3(authoredPosition.x,-1.305f-body.bounds.min.y,authoredPosition.z);
            // B04's distinct squat body rests on that tray by its own bounds.
            if(pumpkin!=null)transform.localPosition=new Vector3(authoredPosition.x,-1.305f-body.bounds.min.y,authoredPosition.z);
            // A01 uses its own authored bulb bottom; reset above restores every other source.
            if(authored!=null||eggplant!=null||banana!=null||hammer!=null||bananaRefined!=null)transform.localPosition=new Vector3(authoredPosition.x,-1.305f-body.bounds.min.y,authoredPosition.z);
            whole=Piece(asset.ObjectId+" voxel whole",body);
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Finish(ref SelectionPreparationReview.VoxelWholeMs,started);started=SelectionPreparationReview.Start();
#endif
            background=volume.Decoration();if(background.vertexCount>0)decor=Piece(asset.ObjectId+" decoration mass0",background);
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Finish(ref SelectionPreparationReview.VoxelDecorMs,started);
#endif

        }
        Transform Piece(string name,Mesh mesh)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=new[]{Palette,Palette};return go.transform;
        }
        public void ResetAttempt(){ClearPieces();if(whole!=null)whole.gameObject.SetActive(true);if(decor!=null)decor.gameObject.SetActive(true);}
        public void Prepare(float x)
        {
            if(volume==null)throw new InvalidOperationException("Select a voxel asset first");ClearPieces();
            leftMesh=volume.Build(x,true);float leftTop=float.IsPositiveInfinity(x)?MeshSection.Top(leftMesh):volume.CutTop;
            leftY=float.IsPositiveInfinity(x)?MeshSection.RestY(leftMesh,Quaternion.Euler(0,30,-6),-1.305f-transform.localPosition.y):volume.CutRestY(Quaternion.Euler(0,30,-6),-1.305f-transform.localPosition.y);
            rightMesh=volume.Build(x,false);rightY=volume.CutRestY(Quaternion.Euler(0,-30,6),-1.305f-transform.localPosition.y);
            SectionTop=Mathf.Max(leftTop,volume.CutTop)+transform.localPosition.y;
            left=Piece(Asset.ObjectId+" voxel left",leftMesh);right=Piece(Asset.ObjectId+" voxel right",rightMesh);
            left.gameObject.SetActive(false);right.gameObject.SetActive(false);
        }
        public void Animate(float split,float imbalance)
        {
            whole.gameObject.SetActive(false);if(decor!=null)decor.gameObject.SetActive(false);left.gameObject.SetActive(true);right.gameObject.SetActive(true);
            left.localPosition=new Vector3(-.42f*split,(leftY+imbalance*.17f)*split,0);right.localPosition=new Vector3(.42f*split,(rightY-imbalance*.17f)*split,0);
            left.localRotation=Quaternion.Euler(0,30*split,-6*split);right.localRotation=Quaternion.Euler(0,-30*split,6*split);
        }
        static void Remove(Transform t){if(t!=null){t.gameObject.SetActive(false);Destroy(t.gameObject);}}
        void ClearPieces(){Remove(left);Remove(right);left=right=null;if(leftMesh!=null)Destroy(leftMesh);if(rightMesh!=null)Destroy(rightMesh);leftMesh=rightMesh=null;}
        void ClearAll(){ClearPieces();Remove(whole);Remove(decor);whole=decor=null;if(body!=null)Destroy(body);if(background!=null)Destroy(background);body=background=null;volume=null;Asset=null;}
        void OnDestroy(){ClearAll();}
    }
}
