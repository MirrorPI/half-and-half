using System.Collections;
using HalfAndHalf.Core;
using HalfAndHalf.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HalfAndHalf.Tests
{
    public sealed class SceneIntegrationTests
    {
        AppleGame game;
        Mouse mouse;
        Touchscreen touchscreen;
        InputSettings inputSettings;
        InputSettings.BackgroundBehavior priorBackground;
        InputSettings.EditorInputBehaviorInPlayMode priorEditorRouting;
        [UnitySetUp] public IEnumerator Setup()
        {
            // Queued test events must reach the GameView even when CI has no desktop focus.
            // Keep the player's normal focus handling and restore these settings after each test.
            inputSettings = InputSystem.settings;
            priorBackground = inputSettings.backgroundBehavior;
            priorEditorRouting = inputSettings.editorInputBehaviorInPlayMode;
            inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return SceneManager.LoadSceneAsync("Gameplay");
            game = Object.FindFirstObjectByType<AppleGame>();
            mouse = InputSystem.AddDevice<Mouse>();
            yield return Ready();
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (touchscreen != null) InputSystem.RemoveDevice(touchscreen);
            if (inputSettings != null)
            {
                inputSettings.backgroundBehavior = priorBackground;
                inputSettings.editorInputBehaviorInPlayMode = priorEditorRouting;
            }
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(root);
            yield return null;
        }
        IEnumerator Ready()
        {
            double until = Time.realtimeSinceStartupAsDouble + 2;
            while ((game.Session == null || game.Session.Phase == AttemptPhase.Dropping) && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Assert.That(game.Session.Phase, Is.EqualTo(AttemptPhase.Ready));
        }
        IEnumerator MouseEvent(Vector2 point, bool down)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left, down));
            yield return null; yield return null;
        }
        [UnityTest] public IEnumerator RealMouseCutUpdatesMeshHudAndClickableRetry()
        {
            var center = new Vector2(Screen.width * .5f, Screen.height * .35f);
            yield return MouseEvent(center, true); yield return MouseEvent(center, false);
            Assert.That(game.Session.Phase, Is.EqualTo(AttemptPhase.Cutting));
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(game.Session.Result.AccuracyUnits, Is.EqualTo(10000));
            Assert.That(game.Presentation.FragmentCount, Is.EqualTo(2));
            Assert.That(game.Presentation.Body.enabled, Is.False);
            Assert.That(game.Hud.ResultPanel.activeSelf, Is.True);
            Assert.That(game.Hud.Accuracy.text, Is.EqualTo("100.00%"));
            Assert.That(game.Hud.GradeLabel.text, Is.EqualTo("PERFECT"));
            var retryPoint = RectTransformUtility.WorldToScreenPoint(null, game.Hud.RetryButton.transform.position);
            yield return MouseEvent(retryPoint, true); yield return MouseEvent(retryPoint, false);
            Assert.That(game.Session.Phase, Is.EqualTo(AttemptPhase.Dropping));
            Assert.That(game.Presentation.Body.enabled, Is.True);
            Assert.That(game.Presentation.FragmentCount, Is.Zero);
            Assert.That(game.Hud.ResultPanel.activeSelf, Is.False);
        }
        [UnityTest] public IEnumerator TwentyVisibleRetriesLeaveNoPiecesOrMeshGrowth()
        {
            int baseline = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            var point = new Vector2(Screen.width * .56f, Screen.height * .35f);
            for (int i = 0; i < 20; i++)
            {
                Assert.That(game.BeginPointer(3, point), Is.True); Assert.That(game.ReleasePointer(3, point), Is.True);
                if (i % 2 == 0) yield return new WaitForSecondsRealtime(.6f);
                game.Retry(); yield return null;
                Assert.That(game.Presentation.FragmentCount, Is.Zero);
                Assert.That(Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(baseline));
                Assert.That(game.Session.Result, Is.Null); Assert.That(game.Hud.ResultPanel.activeSelf, Is.False);
                yield return Ready();
            }
        }
        [UnityTest] public IEnumerator RealTouchCancellationDoesNotCut()
        {
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            var point = new Vector2(Screen.width * .5f, Screen.height * .35f);
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 17, phase = UnityEngine.InputSystem.TouchPhase.Began, position = point });
            yield return null; yield return null;
            Assert.That(game.Session.ActivePointer, Is.EqualTo(17));
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 17, phase = UnityEngine.InputSystem.TouchPhase.Canceled, position = point });
            yield return null; yield return null;
            Assert.That(game.Session.ActivePointer, Is.Null);
            Assert.That(game.Session.Phase, Is.EqualTo(AttemptPhase.Ready));
            Assert.That(game.Session.Result, Is.Null);
        }
        [UnityTest] public IEnumerator CutSurfacesTurnTowardGameCamera()
        {
            var rect = game.GameCamera.pixelRect;
            var point = new Vector2(rect.center.x, rect.center.y);
            game.BeginPointer(3, point); game.ReleasePointer(3, point);
            yield return new WaitForSecondsRealtime(.6f);
            var left = game.Presentation.BodyRoot.Find("Left"); var right = game.Presentation.BodyRoot.Find("Right");
            Assert.That(Vector3.Dot(left.TransformDirection(Vector3.right), game.GameCamera.transform.position - left.position), Is.GreaterThan(0));
            Assert.That(Vector3.Dot(right.TransformDirection(Vector3.left), game.GameCamera.transform.position - right.position), Is.GreaterThan(0));
        }
        [UnityTest] public IEnumerator TimeoutHidesKnifeAndGuide()
        {
            Assert.That(game.BeginPointer(3, new Vector2(Screen.width * .56f, Screen.height * .35f)), Is.True);
            Assert.That(game.Presentation.Knife.gameObject.activeSelf, Is.True);
            game.Session.Tick((long)(Time.realtimeSinceStartupAsDouble * 1000) + 120001);
            yield return null;
            Assert.That(game.Session.Phase, Is.EqualTo(AttemptPhase.Invalid));
            Assert.That(game.Presentation.Knife.gameObject.activeSelf, Is.False);
            Assert.That(game.Presentation.Guide.gameObject.activeSelf, Is.False);
        }
    }
}
