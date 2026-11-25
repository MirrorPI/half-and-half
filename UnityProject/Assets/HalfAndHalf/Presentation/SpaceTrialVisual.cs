using System.Collections.Generic;
using HalfAndHalf.Core;
using UnityEngine;
namespace HalfAndHalf.Presentation
{
 public sealed class SpaceTrialVisual:MonoBehaviour
 {
  public string Kind="P04";
  public bool ContentMode;
  [System.NonSerialized] public HalfAndHalf.Content.VisualSampleAsset PixelAsset;
  bool Authored=>ContentMode&&PixelAsset!=null;
  bool Orbital=>Authored&&PixelAsset.IsOrbitalSystem;
  bool Galaxy=>Authored&&PixelAsset.IsGalaxy;
  bool SolidGalaxy=>Galaxy&&PixelAsset.IsVoxelGalaxy;
  Mesh galaxyLive,galaxySource,hazeMesh;
  MeshRenderer wholeHaze;
  bool HasHaze=>SolidGalaxy&&PixelAsset.GalaxyHaze!=null;
  public bool IsWhirlpool => HasHaze && Kind=="G01";
  public Transform Landmark(ref Vector3 point)
  {
   if(Orbital)return orbitalWhole.Find(Kind=="S01" ? "Earth" : "Titan");
   // The first star is on a spiral arm, the cartwheel rim, or the left encounter disk.
   if(SolidGalaxy)point=PixelAsset.VoxelGalaxy.CenterAt(0,0);
   return whole;
  }
  static readonly int CutWave = Shader.PropertyToID("_CutWave");
  MaterialPropertyBlock waveProperties;
  MeshRenderer waveLeft,waveRight;
  float waveOrigin,waveStart;
  static readonly int HazeSlice=Shader.PropertyToID("_Slice");
  static readonly int CloudPose=Shader.PropertyToID("_Pose");
  Transform orbitalWhole;
  GameObject orbitalPrefab;
  public float ViewScale=>SolidGalaxy?(Kind=="G03"?10.2f:Kind=="G01"?5.65f:8.1f):Galaxy?(Kind=="G01"?4.5f:6.2f):Orbital?(Kind=="S01"?3f:1.55f):1;
  float StartY=>SolidGalaxy?(Kind=="G03"?2.85f:Kind=="G01"?2.15f:3.40f):Galaxy?(Kind=="G01"?2.12f:3.48f):Orbital?(Kind=="S01"?1.14f:-.32f):Authored?PixelAsset.Planet.RestOffset:0;
  Material SurfaceMaterial=>Authored?PixelAsset.Section:Palette;
  public Material Palette;
  public Shader DustShader;
  public long DisplayedMilliseconds {get;private set;}
  public long FrozenMilliseconds {get;private set;}=-1;
  public int FragmentCount=>(left!=null&&leftMesh.vertexCount>0?1:0)+(right!=null&&rightMesh.vertexCount>0?1:0);
  public float SectionTop {get;private set;}
  readonly List<SpaceVoxel> cells=new List<SpaceVoxel>(30000);
  readonly SpaceVoxelMesh builder=new SpaceVoxelMesh();
  Material dustMaterial;MeshRenderer wholeRenderer;
  MaterialPropertyBlock dustPose;
  static readonly int C0=Shader.PropertyToID("_Center0"),C1=Shader.PropertyToID("_Center1"),M0=Shader.PropertyToID("_Motion0"),M1=Shader.PropertyToID("_Motion1"),S0=Shader.PropertyToID("_Slow0"),S1=Shader.PropertyToID("_Slow1"),F0=Shader.PropertyToID("_Fast0"),F1=Shader.PropertyToID("_Fast1");
  Mesh wholeMesh,leftMesh,rightMesh;Transform whole,left,right;float leftY,rightY,leftX,rightX;
  public ObjectDefinition Definition()=>Authored?PixelAsset.Definition():ContentMode?SpaceContentModel.Definition(Kind):SpaceTrialModel.Definition(Kind);
  void Fill(long time){if(ContentMode)SpaceContentModel.Fill(cells,Kind,time);else SpaceTrialModel.Fill(cells,Kind,time);}
  Transform Piece(string name,Mesh mesh)
  {
   var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
   go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=(Orbital||Galaxy)?new[]{SurfaceMaterial}:new[]{SurfaceMaterial,SurfaceMaterial};return go.transform;
  }
  MeshRenderer AddHaze(Transform parent,float knife=0,float side=0)
  {
   var go=new GameObject("Diffuse starlight",typeof(MeshFilter),typeof(MeshRenderer));
   go.transform.SetParent(parent,false);
   go.GetComponent<MeshFilter>().sharedMesh=hazeMesh;
   var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=PixelAsset.GalaxyHaze;
   if(side!=0){
    var properties=new MaterialPropertyBlock();properties.SetVector(HazeSlice,new Vector4(knife,side,0,0));
    renderer.SetPropertyBlock(properties);
   }
   return renderer;
  }
  public void ResetAttempt()
  {
   ClearPieces();FrozenMilliseconds=-1;if(dustPose==null)dustPose=new MaterialPropertyBlock();
   if(whole==null){wholeMesh=new Mesh{name="Space trial whole"};whole=Piece("Whole",wholeMesh);}
   if(orbitalWhole!=null)orbitalWhole.gameObject.SetActive(false);
   if(wholeHaze!=null)wholeHaze.enabled=false;
   if(Orbital){
    if(orbitalPrefab!=PixelAsset.Model || orbitalWhole==null){
     if(orbitalWhole!=null)Destroy(orbitalWhole.gameObject);
     orbitalPrefab=PixelAsset.Model;orbitalWhole=Instantiate(orbitalPrefab,transform,false).transform;orbitalWhole.name="Orbital bodies";
    }
    whole.gameObject.SetActive(false);orbitalWhole.localPosition=Vector3.up*StartY;orbitalWhole.gameObject.SetActive(true);
    ShowTime(0);return;
   }
   var modelMesh=Authored?PixelAsset.Model.GetComponent<MeshFilter>().sharedMesh:wholeMesh;
   if(SolidGalaxy){
    if(galaxySource!=modelMesh){
     if(galaxyLive!=null)Destroy(galaxyLive);
     if(hazeMesh!=null){Destroy(hazeMesh);hazeMesh=null;}
     if(wholeHaze!=null){Destroy(wholeHaze.gameObject);wholeHaze=null;}
     galaxySource=modelMesh;galaxyLive=Instantiate(modelMesh);galaxyLive.MarkDynamic();
    }
    if(HasHaze){
     if(hazeMesh==null){hazeMesh=PixelAsset.VoxelGalaxy.BuildHaze();wholeHaze=AddHaze(whole);}
     wholeHaze.enabled=true;
    }
    modelMesh=galaxyLive;
   }
   whole.GetComponent<MeshFilter>().sharedMesh=modelMesh;
   whole.localPosition=Vector3.up*StartY;whole.localRotation=Quaternion.identity;
   wholeRenderer=whole.GetComponent<MeshRenderer>();wholeRenderer.SetPropertyBlock(null);
   if(Kind=="G03"&&!SolidGalaxy)
   {
    if(dustMaterial==null)dustMaterial=new Material(DustShader!=null?DustShader:Shader.Find("HalfAndHalf/GalaxyDust"));
    GalaxyDustMesh.Build(wholeMesh);wholeRenderer.sharedMaterials=new[]{dustMaterial};
   }
   else if(Galaxy)wholeRenderer.sharedMaterials=new[]{SurfaceMaterial};
   else wholeRenderer.sharedMaterials=new[]{SurfaceMaterial,SurfaceMaterial};
   whole.gameObject.SetActive(true);ShowTime(0);
  }
  public void ShowTime(long time)
  {
   if(FrozenMilliseconds>=0)return;DisplayedMilliseconds=time;
   if(Orbital){PixelAsset.SystemVolume.Pose(orbitalWhole,time);return;}
   if(SolidGalaxy){
    PixelAsset.VoxelGalaxy.Pose(galaxyLive,time);
    if(HasHaze)PixelAsset.VoxelGalaxy.PoseHaze(hazeMesh);
    return;
   }
   if(Galaxy){dustPose.SetVector(CloudPose,PixelAsset.Galaxy.Motion(time));wholeRenderer.SetPropertyBlock(dustPose);return;}
   if(Authored){whole.localRotation=PixelAsset.Planet.Pose(time);return;}
   if(Kind=="G03"&&!SolidGalaxy)
   {
    SpaceTrialModel.GalaxyPose(0,time,out var c0,out var m0);SpaceTrialModel.GalaxyPose(1,time,out var c1,out var m1);
    SpaceTrialModel.GalaxyPose(0,time,out _,out var s0,.55f);SpaceTrialModel.GalaxyPose(1,time,out _,out var s1,.55f);
    SpaceTrialModel.GalaxyPose(0,time,out _,out var f0,1.65f);SpaceTrialModel.GalaxyPose(1,time,out _,out var f1,1.65f);
    dustPose.SetVector(S0,s0);dustPose.SetVector(S1,s1);dustPose.SetVector(F0,f0);dustPose.SetVector(F1,f1);
    dustPose.SetVector(C0,c0);dustPose.SetVector(C1,c1);dustPose.SetVector(M0,m0);dustPose.SetVector(M1,m1);wholeRenderer.SetPropertyBlock(dustPose);return;
   }
   Fill(time);builder.Fill(wholeMesh,cells,float.PositiveInfinity,true,Kind.StartsWith("G"));
  }
  Quaternion FragmentPose(bool leftSide,float split)
  {
   float side=leftSide?1:-1;
   bool tipForward=SolidGalaxy&&(Kind=="G02"||Kind=="G03");
   return Quaternion.Euler(tipForward?27*split:0,side*(tipForward?19:Kind=="G03"?5:27)*split,-side*(tipForward?3:5)*split);
  }
  Vector2 RestPosition(Mesh mesh,bool leftSide)
  {
   var rotation=FragmentPose(leftSide,1);
   float separation=Galaxy?.26f*ViewScale:Kind=="G03"?.22f:.44f;
   float x=leftSide?-separation:separation;
   if(!SolidGalaxy)return new Vector2(x,MeshSection.RestY(mesh,rotation,-1.305f));
   var vertices=mesh.vertices;
   if(vertices.Length==0)return Vector2.zero;
   float minX=float.PositiveInfinity,maxX=float.NegativeInfinity,bottom=float.PositiveInfinity;
   foreach(var v in vertices){
    var point=rotation*v;
    minX=Mathf.Min(minX,point.x);maxX=Mathf.Max(maxX,point.x);bottom=Mathf.Min(bottom,point.y);
   }
   // The narrowest supported portrait framing spans +/- 1.85 times ViewScale.
   // Leave a margin at the two outer edges after tipping a deep cloud open.
   float limit=1.75f*ViewScale;
   return new Vector2(Mathf.Clamp(x,-limit-minX,limit-maxX),-1.305f-bottom);
  }
  public void Prepare(AttemptResult result)
  {
   ClearPieces();ShowTime(result.ElapsedMilliseconds);FrozenMilliseconds=result.ElapsedMilliseconds;DisplayedMilliseconds=FrozenMilliseconds;
   if(Orbital){
    float x=(float)result.Snapshot.KnifeX;
    leftMesh=PixelAsset.SystemVolume.Slice(orbitalWhole,FrozenMilliseconds,x,true,out float lt);
    rightMesh=PixelAsset.SystemVolume.Slice(orbitalWhole,FrozenMilliseconds,x,false,out float rt);
    SectionTop=Mathf.Max(lt,rt)+StartY;
   }else if(SolidGalaxy){
    float knife=(float)result.Snapshot.KnifeX;
    leftMesh=PixelAsset.VoxelGalaxy.Slice(FrozenMilliseconds,knife,true,out float lt);
    rightMesh=PixelAsset.VoxelGalaxy.Slice(FrozenMilliseconds,knife,false,out float rt);
    SectionTop=Mathf.Max(lt,rt)+StartY;
   }else if(Galaxy){
    float knife=(float)result.Snapshot.KnifeX;
    leftMesh=PixelAsset.Galaxy.Build(FrozenMilliseconds,knife,true,out float lt);
    rightMesh=PixelAsset.Galaxy.Build(FrozenMilliseconds,knife,false,out float rt);
    SectionTop=Mathf.Max(lt,rt)+StartY;
   }else if(Authored){
    float knife=(float)result.Snapshot.KnifeX;
    leftMesh=PixelAsset.Planet.Build(FrozenMilliseconds,knife,true,out float lt);
    rightMesh=PixelAsset.Planet.Build(FrozenMilliseconds,knife,false,out float rt);
    SectionTop=Mathf.Max(lt,rt)+StartY;
   }else{
   Fill(FrozenMilliseconds);
   // Rebuild the whole at the exact CUT time too, so even the knife lead-in is frozen.
   if(Kind!="G03")builder.Fill(wholeMesh,cells,float.PositiveInfinity,true,Kind.StartsWith("G"));
   leftMesh=new Mesh{name="Space trial left"};rightMesh=new Mesh{name="Space trial right"};
   builder.Fill(leftMesh,cells,(float)result.Snapshot.KnifeX,true,Kind.StartsWith("G"),Kind=="P01");SectionTop=builder.SectionTop;
   builder.Fill(rightMesh,cells,(float)result.Snapshot.KnifeX,false,Kind.StartsWith("G"),Kind=="P01");SectionTop=Mathf.Max(SectionTop,builder.SectionTop);
   }
   left=Piece("Left",leftMesh);right=Piece("Right",rightMesh);left.gameObject.SetActive(false);right.gameObject.SetActive(false);
   if(HasHaze){
    float x=(float)result.Snapshot.KnifeX;
    if(leftMesh.vertexCount>0)AddHaze(left,x,-1);
    if(rightMesh.vertexCount>0)AddHaze(right,x,1);
   }
   if(HasHaze){
    waveOrigin=(float)result.Snapshot.KnifeX;
    waveLeft=left.GetComponent<MeshRenderer>();waveRight=right.GetComponent<MeshRenderer>();
    waveProperties ??= new MaterialPropertyBlock();
   }
   var l=RestPosition(leftMesh,true);var r=RestPosition(rightMesh,false);
   leftX=l.x;leftY=l.y;rightX=r.x;rightY=r.y;
  }
  public void Animate(float split,float imbalance)
  {
   if(Orbital)orbitalWhole.gameObject.SetActive(false);
   if(waveLeft!=null && !left.gameObject.activeSelf)waveStart=Time.unscaledTime;
   whole.gameObject.SetActive(false);left.gameObject.SetActive(true);right.gameObject.SetActive(true);
   left.localPosition=new Vector3(leftX*split,Mathf.Lerp(StartY,leftY+imbalance*.17f,split),0);
   right.localPosition=new Vector3(rightX*split,Mathf.Lerp(StartY,rightY-imbalance*.17f,split),0);
   left.localRotation=FragmentPose(true,split);right.localRotation=FragmentPose(false,split);
  }
  void LateUpdate()
  {
   if(waveLeft==null || !waveLeft.gameObject.activeSelf)return;
   float elapsed=Time.unscaledTime-waveStart;
   if(elapsed>1.6f){
    waveLeft.SetPropertyBlock(null);waveRight.SetPropertyBlock(null);
    waveLeft=waveRight=null;
    return;
   }
   waveProperties.SetVector(CutWave,new Vector4(waveOrigin,elapsed,1,.078f*5.65f/ViewScale));
   waveLeft.SetPropertyBlock(waveProperties);waveRight.SetPropertyBlock(waveProperties);
  }
  void ClearPieces(){waveLeft=waveRight=null;if(left!=null){left.gameObject.SetActive(false);Destroy(left.gameObject);}if(right!=null){right.gameObject.SetActive(false);Destroy(right.gameObject);}left=right=null;if(leftMesh!=null)Destroy(leftMesh);if(rightMesh!=null)Destroy(rightMesh);leftMesh=rightMesh=null;}
  void OnDestroy(){ClearPieces();if(hazeMesh!=null)Destroy(hazeMesh);if(galaxyLive!=null)Destroy(galaxyLive);if(wholeMesh!=null)Destroy(wholeMesh);if(dustMaterial!=null)Destroy(dustMaterial);}
 }
}
