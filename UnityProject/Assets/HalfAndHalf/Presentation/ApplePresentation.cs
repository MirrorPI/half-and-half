using HalfAndHalf.Content;
using HalfAndHalf.Core;
using UnityEngine;

namespace HalfAndHalf.Presentation
{
    public sealed class ApplePresentation : MonoBehaviour
    {
        public AppleDefinitionAsset Content;
        public Transform BodyRoot, DecorationRoot, Knife;
        public KnifeAppearance KnifeAppearance;
        public MeshRenderer Body;
        public LineRenderer Guide;
        public CutAudio Audio;
        public SampleVisual Sample;
        public VoxelAppleVisual PixelApple;
        public VoxelCarVisual Car;
        public VoxelPrototypeVisual Voxel;
        public SpaceTrialVisual Space;
        bool UsesSpace => Space != null && Space.gameObject.activeSelf;
        bool UsesPixel => PixelApple != null && PixelApple.gameObject.activeSelf;
        bool UsesCar => Car != null && Car.gameObject.activeSelf;
        bool UsesVoxel => Voxel != null && Voxel.gameObject.activeSelf;
        public Transform ScaleBeam, LeftPan, RightPan;
        Transform left, right;
        Mesh leftMesh, rightMesh;
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
        public double ReviewBodyWarmMs {get;private set;}
        public double ReviewBodySourcePrepareMs {get;private set;}
        public double ReviewBodyNativeWarmMs {get;private set;}
        public double ReviewBodyCacheCheckMs {get;private set;}
        public int ReviewBodyWarmCount {get;private set;}
#endif
        Mesh preparedBody;
        MeshSection.Prepared bodySource;
        decimal cutX;
        float imbalance;
        float leftY,rightY;
        public float ContactProgress { get; private set; }
        bool UsesSample => Sample != null && Sample.Asset != null && Sample.gameObject.activeSelf;
        public int FragmentCount => UsesSpace ? Space.FragmentCount : UsesCar ? Car.FragmentCount : UsesVoxel ? Voxel.FragmentCount : UsesPixel ? PixelApple.FragmentCount : UsesSample ? (Sample.Left != null ? 1 : 0) + (Sample.Right != null ? 1 : 0) : (left != null ? 1 : 0) + (right != null ? 1 : 0);

        public void ResetAttempt()
        {
            if (left != null) {left.gameObject.SetActive(false);Destroy(left.gameObject);} if (right != null) {right.gameObject.SetActive(false);Destroy(right.gameObject);}
            if (leftMesh != null) Destroy(leftMesh); if (rightMesh != null) Destroy(rightMesh);
            left = right = null; leftMesh = rightMesh = null;
            if (Audio != null) Audio.ResetAttempt();
            if (Sample != null) Sample.ResetAttempt();
            if (UsesPixel) PixelApple.ResetAttempt();
            if (UsesCar) Car.ResetAttempt();
            if (UsesVoxel) Voxel.ResetAttempt();
            if (UsesSpace) Space.ResetAttempt();
            Body.enabled = !UsesSample && !UsesPixel && !UsesSpace && !UsesCar && !UsesVoxel; DecorationRoot.gameObject.SetActive(!UsesSample && !UsesPixel && !UsesSpace && !UsesCar && !UsesVoxel);
            if (!UsesSample && !UsesPixel && !UsesSpace && !UsesCar && !UsesVoxel && Content != null)
            {
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
                double warmStart=Time.realtimeSinceStartupAsDouble;
                bool preparedNow=bodySource == null || preparedBody != Content.Body;
                double sourceMs=0;
#endif
                if(bodySource == null || preparedBody != Content.Body)
                {
                    bodySource?.ReleaseNativeTopology();
                    preparedBody = Content.Body; bodySource = new MeshSection.Prepared(preparedBody);
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
                    sourceMs=(Time.realtimeSinceStartupAsDouble-warmStart)*1000;
#endif
                }
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
                double nativeStart=Time.realtimeSinceStartupAsDouble;
#endif
                bodySource.EnableNativeTopology();
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
                double finished=Time.realtimeSinceStartupAsDouble;
                if (preparedNow)
                {
                    // Retain actual preparation; reselection/Retry only check the cached source.
                    ReviewBodyWarmMs=(finished-warmStart)*1000;
                    ReviewBodySourcePrepareMs=sourceMs;
                    ReviewBodyNativeWarmMs=(finished-nativeStart)*1000;
                    ReviewBodyWarmCount++;
                }
                else ReviewBodyCacheCheckMs=(finished-warmStart)*1000;
#endif
            }
            BodyRoot.localPosition = new Vector3(0, 3.5f, 0);
            SetScale(0);
            DecorationRoot.localPosition = Vector3.zero; DecorationRoot.localRotation = Quaternion.identity;
            Knife.gameObject.SetActive(false); Guide.gameObject.SetActive(false);
        }
        public void Drop(float t)
        {
            float y = t < .7f ? 3.5f * (1 - t * t / .49f) : Mathf.Sin((t - .7f) / .3f * Mathf.PI) * .17f;
            BodyRoot.localPosition = new Vector3(0, Mathf.Max(0, y), 0);
            if (t >= 1 && UsesSample) Sample.Warm();
        }
        public float KnifeTravelScale=>UsesSpace?Space.ViewScale:1;
        public void Aim(decimal x)
        {
            BodyRoot.localPosition = Vector3.zero;
            Knife.gameObject.SetActive(true); Guide.gameObject.SetActive(true);
            Knife.localPosition = new Vector3((float)x, 2*KnifeTravelScale, -1.25f*KnifeTravelScale);
            Guide.SetPositions(new[] { new Vector3((float)x, 1.4f*KnifeTravelScale, -1.15f*KnifeTravelScale), new Vector3((float)x, -1.05f*KnifeTravelScale, -1.15f*KnifeTravelScale) });
        }
        public void Cut(AttemptResult result)
        {
            cutX = result.Snapshot.KnifeX;
            Aim(cutX);
            imbalance = (float)((result.RightMass - result.LeftMass) / (result.RightMass + result.LeftMass));
            if (Audio != null) Audio.BeginCut(UsesSpace && Space.IsWhirlpool);
            if (UsesSpace) { Space.Prepare(result); SetContact(Space.SectionTop); return; }
            if (UsesCar) { Car.Prepare((float)cutX); SetContact(Car.SectionTop); return; }
            if (UsesVoxel) { Voxel.Prepare((float)cutX); SetContact(Voxel.SectionTop); return; }
            if (UsesPixel) { PixelApple.Prepare((float)cutX); SetContact(PixelApple.SectionTop); return; }
            if (UsesSample) { Sample.Prepare((float)cutX); SetContact(Sample.SectionTop); return; }
            if (bodySource == null || preparedBody != Content.Body) { bodySource?.ReleaseNativeTopology(); preparedBody = Content.Body; bodySource = new MeshSection.Prepared(preparedBody); }
            leftMesh = MeshSection.Slice(bodySource, (float)cutX, true);
            float leftTop=MeshSection.Top(bodySource);
            leftY=MeshSection.RestY(bodySource,Quaternion.Euler(0,14,-6),-1.305f);
            rightMesh = MeshSection.Slice(bodySource, (float)cutX, false);
            rightY=MeshSection.RestY(bodySource,Quaternion.Euler(0,-14,6),-1.305f);
            SetContact(Mathf.Max(leftTop,MeshSection.Top(bodySource)));
            left = Fragment("Left", leftMesh); right = Fragment("Right", rightMesh);
            left.gameObject.SetActive(false); right.gameObject.SetActive(false);
        }
        void SetContact(float sectionTop)
        {
            float tip;
            if (KnifeAppearance != null) tip = KnifeAppearance.ContactLocalY;
            else
            {
                // Compatibility for the original task-1 scene authoring path.
                var blade=Knife.Find("Blade").GetComponent<MeshFilter>();tip=float.PositiveInfinity;
                foreach(var v in blade.sharedMesh.vertices)tip=Mathf.Min(tip,Knife.InverseTransformPoint(blade.transform.TransformPoint(v)).y);
            }
            ContactProgress=float.IsNegativeInfinity(sectionTop)?float.PositiveInfinity:Mathf.Clamp01((2*KnifeTravelScale+tip-sectionTop)/(3.8f*1.7f*KnifeTravelScale));
        }
        Transform Fragment(string label, Mesh mesh)
        {
            var piece = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            piece.transform.SetParent(BodyRoot, false); piece.GetComponent<MeshFilter>().sharedMesh = mesh;
            piece.GetComponent<MeshRenderer>().sharedMaterials = new[] { Content.Skin, Content.Flesh };
            return piece.transform;
        }
        public void AnimateCut(float t)
        {
            Knife.localPosition = new Vector3((float)cutX, KnifeTravelScale*Mathf.Lerp(2, -1.8f, Mathf.Min(1, t * 1.7f)), -1.25f*KnifeTravelScale);
            if (Audio != null) Audio.Progress(t, ContactProgress);
            if (t < .28f || (!UsesSample && !UsesPixel && !UsesSpace && !UsesCar && !UsesVoxel && left == null)) return;
            float split = Mathf.SmoothStep(0, 1, (t - .28f) / .72f);
            SetScale(imbalance * split);
            if (UsesSpace)
            {
                Space.Animate(split, imbalance); Guide.gameObject.SetActive(false);
                if (t >= 1) Knife.gameObject.SetActive(false);
                return;
            }
            if (UsesPixel)
            {
                PixelApple.Animate(split, imbalance); Guide.gameObject.SetActive(false);
                if (t >= 1) Knife.gameObject.SetActive(false);
                return;
            }
            if (UsesCar)
            {
                Car.Animate(split, imbalance); Guide.gameObject.SetActive(false);
                if (t >= 1) Knife.gameObject.SetActive(false);
                return;
            }
            if (UsesVoxel)
            {
                Voxel.Animate(split, imbalance); Guide.gameObject.SetActive(false);
                if (t >= 1) Knife.gameObject.SetActive(false);
                return;
            }
            if (UsesSample)
            {
                Sample.Animate(split, imbalance); Guide.gameObject.SetActive(false);
                if (t >= 1) Knife.gameObject.SetActive(false);
                return;
            }
            Body.enabled = false; left.gameObject.SetActive(true); right.gameObject.SetActive(true);
            Guide.gameObject.SetActive(false);
            left.localPosition = new Vector3(-.28f * split, (leftY+imbalance*.17f)*split, 0);
            right.localPosition = new Vector3(.28f * split, (rightY-imbalance*.17f)*split, 0);
            left.localRotation = Quaternion.Euler(0, 14 * split, -6 * split);
            right.localRotation = Quaternion.Euler(0, -14 * split, 6 * split);
            bool decorationLeft = cutX > .05m;
            DecorationRoot.localPosition = decorationLeft ? left.localPosition : right.localPosition;
            DecorationRoot.localRotation = decorationLeft ? left.localRotation : right.localRotation;
            if (t >= 1) Knife.gameObject.SetActive(false);
        }
        public void HideInput()
        { Knife.gameObject.SetActive(false); Guide.gameObject.SetActive(false); if (Audio != null) Audio.ResetAttempt(); }
        void SetScale(float value)
        {
            if (ScaleBeam != null) ScaleBeam.localRotation = Quaternion.Euler(0, 0, -value * 12);
            if (LeftPan != null) LeftPan.localPosition = new Vector3(-.75f, value * .17f, 0);
            if (RightPan != null) RightPan.localPosition = new Vector3(.75f, -value * .17f, 0);
        }
        void OnDestroy() { bodySource?.ReleaseNativeTopology(); if (leftMesh != null) Destroy(leftMesh); if (rightMesh != null) Destroy(rightMesh); }
    }
}
