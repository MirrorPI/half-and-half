using HalfAndHalf.Core;
using HalfAndHalf.Content;
using HalfAndHalf.Presentation;
using HalfAndHalf.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace HalfAndHalf.Gameplay
{
    public sealed class AppleGame : MonoBehaviour
    {
        public AppleDefinitionAsset Content;
        public ApplePresentation Presentation;
        public GameHud Hud;
        public Camera GameCamera;
        public bool UiOpen;
        public BladeWorkshop Workshop;
        public event System.Action<AttemptResult> AttemptCompleted;
        public event System.Action<CutSnapshot> CutConfirmed;
        public AttemptSession Session { get; private set; }
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
        public double ReviewSessionMs {get;private set;}
        public double ReviewSubscribersMs {get;private set;}
        public double ReviewPresentationMs {get;private set;}
#endif
        float previousView=1, currentView=1, targetView=1;
        public float Framing(float aspect)=>currentView*Mathf.Max(3.8f,1.85f/aspect);
        readonly System.Collections.Generic.HashSet<string> revealed = new System.Collections.Generic.HashSet<string>();
        Vector3 restingCameraPosition, detailOffset;
        float detailRatio;
        double phaseStart;
        const double DropSeconds = .9, CutSeconds = .60;
        const int MousePointer = -1;
        bool privacyOverlayOpen, updateNoticeOpen;
        public bool OverlayOpen => privacyOverlayOpen || updateNoticeOpen;
        double privacyOpenedAt, privacyPausedSeconds;
        double GameSeconds => (OverlayOpen ? privacyOpenedAt : Time.realtimeSinceStartupAsDouble) - privacyPausedSeconds;
        long Now => (long)(GameSeconds * 1000);
        public void SetPrivacyOverlayOpen(bool open) => SetOverlayOpen(ref privacyOverlayOpen, open);
        public void SetUpdateNoticeOpen(bool open) => SetOverlayOpen(ref updateNoticeOpen, open);
        void SetOverlayOpen(ref bool owner, bool open)
        {
            bool wasOpen = OverlayOpen;
            owner = open;
            if (wasOpen == OverlayOpen) return;
            if (OverlayOpen)
            {
                privacyOpenedAt = Time.realtimeSinceStartupAsDouble;
                if (Session?.ActivePointer is int pointer) CancelPointer(pointer);
            }
            else privacyPausedSeconds += Time.realtimeSinceStartupAsDouble - privacyOpenedAt;
        }
        void OnEnable() { EnhancedTouchSupport.Enable(); }
        void OnDisable() { FinishReveal(); EnhancedTouchSupport.Disable(); }
        void Start()
        {
            Application.targetFrameRate = 60;
            if (Session == null && Content != null) Initialize(Content.Definition());
            if (Hud != null) Hud.RetryButton.onClick.AddListener(()=>AfterResult(Retry));
#if UNITY_IOS && !UNITY_EDITOR
            if (GetComponent<UpdateNotice>() == null) gameObject.AddComponent<UpdateNotice>();
#endif
        }
        public void Initialize(ObjectDefinition definition)
        {
            FinishReveal();
            previousView=currentView;
            var space=Presentation!=null?Presentation.Space:null;
            targetView=space!=null&&space.gameObject.activeSelf?space.ViewScale:1;
            decimal limit=targetView==1?1.35m:1.80m*(decimal)targetView;
            Session = new AttemptSession(definition,-limit,limit);
            phaseStart = GameSeconds;
            if (Presentation != null) Presentation.ResetAttempt();
            if (definition.Id == "B01") previousView = currentView = targetView;
            if (GameCamera == null || Presentation == null || revealed.Contains(definition.Id)) return;

            var anchor = StageScaleReveal.Landmark(Presentation, definition.ContentVersion, out var point, out float zoom);
            if (anchor == null) return;
            Presentation.Drop(1);
            restingCameraPosition = GameCamera.transform.position;
            detailOffset = Vector3.ProjectOnPlane(anchor.TransformPoint(point) - restingCameraPosition, GameCamera.transform.forward);
            detailRatio = 1 / zoom;
            revealed.Add(definition.Id);
            FrameReveal(0);
            phaseStart = GameSeconds;
        }
        void FrameReveal(float t)
        {
            float progress = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.16f, 1, t));
            float ratio = Mathf.Pow(detailRatio, 1 - progress);
            currentView = targetView * ratio;
            GameCamera.transform.position = restingCameraPosition + detailOffset * (1 - progress * ratio);
            GameCamera.orthographicSize = Framing(GameCamera.aspect);
        }
        void FinishReveal()
        {
            if (detailRatio <= 0) return;
            if (GameCamera != null) {
                GameCamera.transform.position = restingCameraPosition;
                currentView = targetView;
                GameCamera.orthographicSize = Framing(GameCamera.aspect);
            }
            detailRatio = 0;
        }
        void Update()
        {
            if (Session == null || OverlayOpen) return;
            double elapsed = GameSeconds - phaseStart;
            bool revealing = detailRatio > 0 && Session.Phase == AttemptPhase.Dropping;
            if (revealing) FrameReveal((float)(elapsed / DropSeconds));
            else currentView=Session.Phase==AttemptPhase.Dropping?Mathf.SmoothStep(previousView,targetView,Mathf.Clamp01((float)(elapsed/DropSeconds))):targetView;
            if (GameCamera != null) GameCamera.orthographicSize=Framing(GameCamera.aspect);
            if (Session.Phase == AttemptPhase.Dropping)
            {
                if (!revealing && Presentation != null) Presentation.Drop(Mathf.Min(1, (float)(elapsed / DropSeconds)));
                if (elapsed >= DropSeconds) { FinishReveal(); Session.Land(Now, TransformState.Identity); }
            }
            if (Session.Phase == AttemptPhase.Ready)
            {
                Session.Tick(Now);
                if (Presentation != null && Presentation.Space != null && Presentation.Space.gameObject.activeSelf && Session.Phase == AttemptPhase.Ready) Presentation.Space.ShowTime(Session.Elapsed(Now));
                PollInput();
                if (Presentation != null && Session.Phase == AttemptPhase.Ready)
                {
                    if (Session.ActivePointer.HasValue) Presentation.Aim(Session.KnifeX);
                    else if (Presentation.Knife.gameObject.activeSelf || Presentation.Guide.gameObject.activeSelf) Presentation.HideInput();
                }
            }
            if (Session.Phase == AttemptPhase.Invalid && Presentation != null && Presentation.Knife.gameObject.activeSelf) Presentation.HideInput();
            if (Session.Phase == AttemptPhase.Cutting)
            {
                float t = Mathf.Min(1, (float)((GameSeconds - phaseStart) / CutSeconds));
                if (Presentation != null) Presentation.AnimateCut(t);
                if (t >= 1)
                {
                    Session.FinishCut();
                    if (Presentation != null && Presentation.Audio != null) Presentation.Audio.Result();
                    AttemptCompleted?.Invoke(Session.Result);
                }
            }
            if (Hud != null) { Hud.Refresh(Session, Now); Hud.RetryButton.interactable = !UiOpen; }
        }
        decimal WorldX(Vector2 screen)
        {
            if (GameCamera != null) return (decimal)GameCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 12)).x;
            return (decimal)targetView*(decimal)((screen.x / Mathf.Max(1, Screen.width) - .5f) * 2.7f);
        }
        public bool BeginPointer(int id, Vector2 screen)
        {
            if (OverlayOpen || UiOpen || Session == null || !Session.BeginDrag(id, WorldX(screen), Now)) return false;
            if (Presentation != null) Presentation.Aim(Session.KnifeX);
            return true;
        }
        public bool MovePointer(int id, Vector2 screen)
        {
            if (OverlayOpen || Session == null || !Session.MoveDrag(id, WorldX(screen), Now)) return false;
            if (Presentation != null) Presentation.Aim(Session.KnifeX);
            return true;
        }
        public bool ReleasePointer(int id, Vector2 screen)
        {
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
            double reviewStart=Time.realtimeSinceStartupAsDouble;
#endif
            if (OverlayOpen || Session == null || !Session.ReleaseDrag(id, WorldX(screen), Now)) return false;
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
            double reviewSessionEnd=Time.realtimeSinceStartupAsDouble;
            ReviewSessionMs=(reviewSessionEnd-reviewStart)*1000;
#endif
            CutConfirmed?.Invoke(Session.Result.Snapshot);
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
            double presentationStart = Time.realtimeSinceStartupAsDouble;
            ReviewSubscribersMs=(presentationStart-reviewSessionEnd)*1000;
#endif
            if (Presentation != null) Presentation.Cut(Session.Result);
            phaseStart = GameSeconds;
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
            ReviewPresentationMs=(Time.realtimeSinceStartupAsDouble-presentationStart)*1000;
#endif
            return true;
        }
        public void CancelPointer(int id)
        {
            if (Session == null || Session.ActivePointer != id) return;
            Session.CancelDrag(id);
            if (Presentation != null) Presentation.HideInput();
        }
        void PollInput()
        {
            if (UiOpen || OverlayOpen) return;
            foreach (var touch in Touch.activeTouches)
            {
                switch (touch.phase)
                {
                    case UnityEngine.InputSystem.TouchPhase.Began: if (!OverUi(touch.screenPosition)) BeginPointer(touch.touchId, touch.screenPosition); break;
                    case UnityEngine.InputSystem.TouchPhase.Moved: MovePointer(touch.touchId, touch.screenPosition); break;
                    case UnityEngine.InputSystem.TouchPhase.Ended: ReleasePointer(touch.touchId, touch.screenPosition); break;
                    case UnityEngine.InputSystem.TouchPhase.Canceled: CancelPointer(touch.touchId); break;
                }
            }
            if (Touch.activeTouches.Count == 0 && Mouse.current != null)
            {
                var mouse = Mouse.current; var position = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame && !OverUi(position)) BeginPointer(MousePointer, position);
                if (mouse.leftButton.isPressed) MovePointer(MousePointer, position);
                if (mouse.leftButton.wasReleasedThisFrame) ReleasePointer(MousePointer, position);
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && Session.ActivePointer.HasValue) CancelPointer(Session.ActivePointer.Value);
        }
        static bool OverUi(Vector2 point)
        {
            var system = UnityEngine.EventSystems.EventSystem.current; if (system == null) return false;
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            system.RaycastAll(new UnityEngine.EventSystems.PointerEventData(system) { position = point }, hits);
            return hits.Count > 0;
        }
        public void AfterResult(System.Action action)
        {
            if (UiOpen || OverlayOpen) return;
            if (Workshop != null && Session?.Phase == AttemptPhase.Result) Workshop.ContinueFromResult(action);
            else action();
        }
        public void Retry()
        {
            if (Session == null || UiOpen || OverlayOpen) return;
            FinishReveal();
            previousView=targetView;Session.Retry(); phaseStart = GameSeconds;
            if (Presentation != null) Presentation.ResetAttempt();
            if (Hud != null) Hud.Refresh(Session, Now);
        }
        public void SetInterrupted()
        {
            FinishReveal();
            Session?.Interrupt();
            if (Presentation != null) Presentation.HideInput();
            if (Hud != null && Session != null) Hud.Refresh(Session, Now);
        }
        void OnApplicationPause(bool paused) { if (paused) SetInterrupted(); }
        void OnApplicationFocus(bool focused)
        {
            // Changing editor panes is not suspending the mobile application.
            if (!focused && !Application.isEditor && !OverlayOpen) SetInterrupted();
        }
    }
}
