using UnityEngine;
using HalfAndHalf.Content;

namespace HalfAndHalf.Presentation
{
    public sealed class VoxelCarVisual : MonoBehaviour
    {
        public Material Palette;
        Transform whole,left,right;
        Mesh body,leftMesh,rightMesh;
        float leftY,rightY;
        CarPixelSource source;
        VoxelVolume volume;
        VisualSampleAsset selected;
        public string ContentVersion => selected==null?VoxelCarGeometry.Version:selected.ContentVersion;
        public void Show(VisualSampleAsset asset)
        {
            if(asset==selected)return;
            var next=asset.Model.GetComponent<CarPixelSource>();
            var detail=asset.Model.GetComponent<CarPixelDetailSource>();
            if(asset.ObjectId!="L02"||(detail!=null?asset.ContentVersion!=CarPixelDetailSource.Version:next!=null?asset.ContentVersion!=CarPixelSource.Version:asset.ContentVersion!=VoxelCarGeometry.Version))throw new System.ArgumentException("Car model/content version mismatch");
            ClearPieces();if(whole!=null){whole.gameObject.SetActive(false);Destroy(whole.gameObject);}if(body!=null)Destroy(body);whole=null;body=null;
            source=next;volume=detail!=null?detail.Volume():source==null?null:source.Volume();selected=asset;Palette=asset.Section;
        }
        Mesh Build(float x,bool isLeft)=>volume==null?VoxelCarGeometry.Build(x,isLeft):volume.Build(x,isLeft);
        float CutTop=>volume==null?VoxelCarGeometry.CutTop:volume.CutTop;
        float Rest(Quaternion rotation,float trayY)=>volume==null?VoxelCarGeometry.CutRestY(rotation,trayY):volume.CutRestY(rotation,trayY);
        public int FragmentCount => (left!=null?1:0)+(right!=null?1:0);
        public float SectionTop {get;private set;}
        void Ensure()
        {
            if(whole!=null)return;
            body=Build(float.PositiveInfinity,true);
            whole=Piece("Car voxel whole",body);
        }
        Transform Piece(string label,Mesh mesh)
        {
            var go=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=new[]{Palette,Palette};return go.transform;
        }
        public void ResetAttempt(){Ensure();ClearPieces();whole.gameObject.SetActive(true);}
        public void Prepare(float x)
        {
            Ensure();ClearPieces();leftMesh=Build(x,true);float leftTop=float.IsPositiveInfinity(x)?MeshSection.Top(leftMesh):CutTop;
            leftY=float.IsPositiveInfinity(x)?MeshSection.RestY(leftMesh,Quaternion.Euler(0,30,-6),-1.305f-transform.localPosition.y):Rest(Quaternion.Euler(0,30,-6),-1.305f-transform.localPosition.y);
            rightMesh=Build(x,false);rightY=Rest(Quaternion.Euler(0,-30,6),-1.305f-transform.localPosition.y);
            SectionTop=Mathf.Max(leftTop,CutTop)+transform.localPosition.y;
            left=Piece("Car voxel left",leftMesh);right=Piece("Car voxel right",rightMesh);
            left.gameObject.SetActive(false);right.gameObject.SetActive(false);
        }
        public void Animate(float split,float imbalance)
        {
            whole.gameObject.SetActive(false);left.gameObject.SetActive(true);right.gameObject.SetActive(true);
            left.localPosition=new Vector3(-.44f*split,(leftY+imbalance*.17f)*split,0);
            right.localPosition=new Vector3(.44f*split,(rightY-imbalance*.17f)*split,0);
            left.localRotation=Quaternion.Euler(0,30*split,-6*split);right.localRotation=Quaternion.Euler(0,-30*split,6*split);
        }
        void ClearPieces()
        {
            if(left!=null){left.gameObject.SetActive(false);Destroy(left.gameObject);}if(right!=null){right.gameObject.SetActive(false);Destroy(right.gameObject);}
            left=right=null;if(leftMesh!=null)Destroy(leftMesh);if(rightMesh!=null)Destroy(rightMesh);leftMesh=rightMesh=null;
        }
        void OnDestroy(){ClearPieces();if(body!=null)Destroy(body);}
    }
}
