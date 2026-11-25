using System.Collections;
using HalfAndHalf.Gameplay;
using HalfAndHalf.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HalfAndHalf.Tests
{
    public sealed class UpdateNoticeTests
    {
        [UnityTest] public IEnumerator LaterResumesPlayAndKeepsPrivacyPauseIndependent()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay29");
            yield return null;
            var game = Object.FindFirstObjectByType<AppleGame>();
            var type = typeof(AppleGame).Assembly.GetType("HalfAndHalf.UI.UpdateNotice");
            Assert.That(type, Is.Not.Null, "Update notice must allow the player to continue later.");
            var notice = game.gameObject.AddComponent(type);
            game.Session.Land((long)(Time.realtimeSinceStartupAsDouble * 1000), TransformState.Identity);
            type.GetMethod("Show").Invoke(notice, null);
            yield return null;
            Assert.That(game.BeginPointer(43, Vector2.zero), Is.False);
            RankingUiCapture.Save(game, "update-notice.png", 428, 926);
            var later = GameObject.Find("UpdateNotice/Later");
            // The button belongs to the card, inside the modal backdrop.
            if (later == null) later = GameObject.Find("UpdateNotice/Card/Later");
            Assert.That(later, Is.Not.Null);
            game.SetPrivacyOverlayOpen(true);
            later.GetComponent<Button>().onClick.Invoke();
            Assert.That(game.BeginPointer(43, Vector2.zero), Is.False);
            game.SetPrivacyOverlayOpen(false);
            Assert.That(game.BeginPointer(43, Vector2.zero), Is.True);
            game.CancelPointer(43);
            type.GetMethod("Show").Invoke(notice, null);
            game.SetPrivacyOverlayOpen(true);
            game.SetPrivacyOverlayOpen(false);
            Assert.That(game.BeginPointer(43, Vector2.zero), Is.False);
            type.GetMethod("Dismiss").Invoke(notice, null);
            Assert.That(game.BeginPointer(43, Vector2.zero), Is.True);
            game.CancelPointer(43);
        }
    }
}
