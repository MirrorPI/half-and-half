using UnityEngine;
namespace HalfAndHalf.Presentation
{
    public sealed class VoxelAppleVisual : MonoBehaviour
    {
        public Material Palette;
        Transform whole,decoration,left,right;
        Mesh body,stem,leaf,leftMesh,rightMesh,leftDecoration,rightDecoration;
        float leftY,rightY;
        public int FragmentCount=>(left!=null?1:0)+(right!=null?1:0);
        public float SectionTop {get;private set;}
        void Ensure()
        {
            if(whole!=null)return;
            body=VoxelAppleGeometry.Build(float.PositiveInfinity,true);
            whole=Piece("Voxel apple whole",body,transform);
            decoration=new GameObject("Voxel stem and leaf").transform;decoration.SetParent(transform,false);
            stem=VoxelAppleGeometry.Decoration(false);leaf=VoxelAppleGeometry.Decoration(true);
            Piece("Stem",stem,decoration);Piece("Leaf",leaf,decoration);
        }
        Transform Piece(string label,Mesh mesh,Transform parent)
        {
            var go=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=new[]{Palette,Palette};return go.transform;
        }
        public void ResetAttempt(){Ensure();ClearPieces();whole.gameObject.SetActive(true);decoration.gameObject.SetActive(true);decoration.localPosition=Vector3.zero;decoration.localRotation=Quaternion.identity;}
        public void Prepare(float x)
        {
            Ensure();ClearPieces();
            leftMesh=VoxelAppleGeometry.Build(x,true);rightMesh=VoxelAppleGeometry.Build(x,false);
            left=Piece("Voxel left",leftMesh,transform);right=Piece("Voxel right",rightMesh,transform);
            leftDecoration=VoxelAppleGeometry.DecorationSide(x,true);rightDecoration=VoxelAppleGeometry.DecorationSide(x,false);
            Piece("Cut decoration left",leftDecoration,left);Piece("Cut decoration right",rightDecoration,right);
            SectionTop=Mathf.Max(MeshSection.Top(leftMesh),MeshSection.Top(rightMesh))+transform.localPosition.y;
            leftY=MeshSection.RestY(leftMesh,Quaternion.Euler(0,38,-7),-1.305f-transform.localPosition.y);
            rightY=MeshSection.RestY(rightMesh,Quaternion.Euler(0,-38,7),-1.305f-transform.localPosition.y);
            left.gameObject.SetActive(false);right.gameObject.SetActive(false);
        }
        public void Animate(float split,float imbalance)
        {
            whole.gameObject.SetActive(false);decoration.gameObject.SetActive(false);left.gameObject.SetActive(true);right.gameObject.SetActive(true);
            left.localPosition=new Vector3(-.60f*split,(leftY+imbalance*.17f)*split,0);
            right.localPosition=new Vector3(.60f*split,(rightY-imbalance*.17f)*split,0);
            // Open the X-facing cut surfaces toward the camera, which looks along +Z.
            left.localRotation=Quaternion.Euler(0,38*split,-7*split);right.localRotation=Quaternion.Euler(0,-38*split,7*split);

        }
        void ClearPieces(){if(left!=null){left.gameObject.SetActive(false);Destroy(left.gameObject);}if(right!=null){right.gameObject.SetActive(false);Destroy(right.gameObject);}left=right=null;if(leftMesh!=null)Destroy(leftMesh);if(rightMesh!=null)Destroy(rightMesh);if(leftDecoration!=null)Destroy(leftDecoration);if(rightDecoration!=null)Destroy(rightDecoration);leftMesh=rightMesh=leftDecoration=rightDecoration=null;}
        void OnDestroy(){ClearPieces();if(body!=null)Destroy(body);if(stem!=null)Destroy(stem);if(leaf!=null)Destroy(leaf);}
    }
}
