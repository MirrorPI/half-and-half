using System.Collections;
using HalfAndHalf.Core;
using HalfAndHalf.Gameplay;
using HalfAndHalf.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HalfAndHalf.Tests
{
    public sealed class KnifeInputTests
    {
        AppleGame game;
        [UnitySetUp] public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");
            game = Object.FindFirstObjectByType<AppleGame>();
            yield return Ready();
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(root);
            yield return null;
        }
        IEnumerator Ready()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 3;
            while ((game.Session == null || game.Session.Phase == AttemptPhase.Dropping) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(game.Session.Phase, Is.EqualTo(AttemptPhase.Ready));
        }
        Vector2 Point(float x) => game.GameCamera.WorldToScreenPoint(new Vector3(x, 0, 0));
        void AssertHidden()
        {
            Assert.That(game.Presentation.Knife.gameObject.activeSelf, Is.False, "Idle READY must not suggest a cut position");
            Assert.That(game.Presentation.Guide.gameObject.activeSelf, Is.False);
        }

        [UnityTest] public IEnumerator ReadyAndRetryDoNotSuggestACentralCut()
        {
            AssertHidden();
            yield return null; yield return null; AssertHidden();
            Assert.That(game.BeginPointer(10, Point(.4f)), Is.True);
            Assert.That(game.ReleasePointer(10, Point(.4f)), Is.True);
            game.Retry(); AssertHidden();
            yield return Ready(); AssertHidden();
            var gallery = Object.FindFirstObjectByType<SampleGallery>();
            gallery.Select(1); yield return Ready(); AssertHidden();
        }

        [UnityTest] public IEnumerator FirstTouchAndDragAppearImmediatelyAtTheAcceptedPosition()
        {
            Assert.That(game.BeginPointer(10, Point(.55f)), Is.True);
            Assert.That(game.Presentation.Knife.gameObject.activeSelf, Is.True);
            Assert.That(game.Presentation.Guide.gameObject.activeSelf, Is.True);
            Assert.That(game.Presentation.Knife.localPosition.x, Is.EqualTo(.55f).Within(.0002f));
            Assert.That(game.Presentation.Guide.GetPosition(0).x, Is.EqualTo(.55f).Within(.0002f));
            Assert.That(game.MovePointer(10, Point(-.3f)), Is.True);
            Assert.That(game.Presentation.Knife.localPosition.x, Is.EqualTo(-.3f).Within(.0002f));
            Assert.That(game.BeginPointer(11, Point(.9f)), Is.False);
            Assert.That(game.MovePointer(11, Point(.9f)), Is.False);
            game.CancelPointer(11);
            Assert.That(game.Presentation.Knife.gameObject.activeSelf, Is.True);
            Assert.That(game.Presentation.Knife.localPosition.x, Is.EqualTo(-.3f).Within(.0002f));
            game.CancelPointer(10); AssertHidden();
            yield return null; AssertHidden();
            game.UiOpen = true;
            Assert.That(game.BeginPointer(12, Point(.2f)), Is.False); AssertHidden();
        }

        [UnityTest] public IEnumerator SameFrameTapCutsAtReleasePositionAndInterruptionClearsIt()
        {
            Assert.That(game.BeginPointer(10, Point(-.4f)), Is.True);
            Assert.That(game.ReleasePointer(10, Point(.31f)), Is.True);
            Assert.That(game.Session.Phase, Is.EqualTo(AttemptPhase.Cutting));
            Assert.That(game.Presentation.Knife.gameObject.activeSelf, Is.True);
            Assert.That(game.Presentation.Knife.localPosition.x, Is.EqualTo(.31f).Within(.0002f));
            Assert.That((float)game.Session.Snapshot.KnifeX, Is.EqualTo(.31f).Within(.0002f));
            game.SetInterrupted(); AssertHidden();
            yield return null; AssertHidden();
        }
    }
}
