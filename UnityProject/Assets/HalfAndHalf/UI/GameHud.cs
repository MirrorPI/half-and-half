using HalfAndHalf.Localization;
using System.Globalization;
using HalfAndHalf.Core;
using TMPro;
using UnityEngine;

namespace HalfAndHalf.UI
{
    public sealed class GameHud : MonoBehaviour
    {
        public ArcadeHud Arcade;
        public RectTransform SafeRoot;
        public TMP_Text State, Accuracy, GradeLabel, Left, Right, TimeLabel, Hint;
        public UnityEngine.UI.Button RetryButton;
        public GameObject ResultPanel;
        Rect previousSafe;
        int width, height;
        long shownHundredths = -1;
        AttemptPhase shownPhase = (AttemptPhase)(-1);
        public void RefreshSafeArea()
        {
            var safe = Screen.safeArea;
            if (safe == previousSafe && width == Screen.width && height == Screen.height) return;
            previousSafe = safe; width = Screen.width; height = Screen.height;
            if (width <= 0 || height <= 0) return;
            SafeRoot.anchorMin = new Vector2(safe.xMin / width, safe.yMin / height);
            SafeRoot.anchorMax = new Vector2(safe.xMax / width, safe.yMax / height);
            SafeRoot.offsetMin = SafeRoot.offsetMax = Vector2.zero;
        }
        public void Refresh(AttemptSession session, long nowMs)
        {
            RefreshSafeArea();
            if (Arcade != null) { Arcade.Render(session, nowMs); return; }
            if (shownPhase != session.Phase)
            {
                shownPhase = session.Phase; shownHundredths = -1;
                ResultPanel.SetActive(session.Phase == AttemptPhase.Result);
                RetryButton.gameObject.SetActive(session.Phase == AttemptPhase.Result || session.Phase == AttemptPhase.Invalid);
                State.text = session.Phase == AttemptPhase.Ready ? "READY" : session.Phase == AttemptPhase.Cutting ? "CUT" : session.Phase == AttemptPhase.Dropping ? "GET READY" : session.Phase == AttemptPhase.Invalid ? "TRY AGAIN" : "RESULT";
                Hint.text = session.Phase == AttemptPhase.Invalid ? (session.InvalidReason == InvalidReason.TimedOut ? L10n.T("120초 시간 초과 · Retry로 다시 시작하세요.") : L10n.T("시도가 중단됐습니다. Retry로 다시 시작하세요.")) : session.Phase == AttemptPhase.Result ? L10n.T("질량 결과를 보고 다음 절단 위치를 조절하세요.") : L10n.T("화면에서 좌우로 이동 · 손을 놓으면 절단");
                if (session.Phase == AttemptPhase.Result)
                {
                    var result = session.Result;
                    Accuracy.text = Scoring.Percent(result.AccuracyUnits / 100m) + "%";
                    GradeLabel.text = result.Grade == Grade.Perfect ? "PERFECT" : result.Grade == Grade.SPlus ? "S+" : result.Grade == Grade.None ? "KEEP TRYING" : result.Grade.ToString();
                    Left.text = "LEFT  " + Scoring.Percent(result.LeftPercent) + "%";
                    Right.text = "RIGHT  " + Scoring.Percent(result.RightPercent) + "%";
                    TimeLabel.text = (result.ElapsedMilliseconds / 1000m).ToString("F3", CultureInfo.InvariantCulture) + " sec";
                }
            }
            long hundredths = session.Phase == AttemptPhase.Ready ? session.Elapsed(nowMs) / 10 : 0;
            if (session.Phase != AttemptPhase.Result && hundredths != shownHundredths)
            { shownHundredths = hundredths; TimeLabel.text = session.Phase == AttemptPhase.Ready ? (hundredths / 100m).ToString("F2", CultureInfo.InvariantCulture) + " sec" : ""; }
        }
    }
}
