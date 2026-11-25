using System;
using System.Collections;
using System.Collections.Generic;
using HalfAndHalf.Core;
using HalfAndHalf.Gameplay;
using HalfAndHalf.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HalfAndHalf.Tests
{
    public sealed class KnifeCustomizationTests
    {
        AppleGame game;
        readonly List<Texture2D> textures = new List<Texture2D>();
        [UnitySetUp] public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");
            game = Object.FindFirstObjectByType<AppleGame>();
            yield return Ready();
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            foreach (var texture in textures) Object.Destroy(texture);
            textures.Clear();
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(root);
            yield return null;
        }
        IEnumerator Ready()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 3;
            while ((game.Session == null || game.Session.Phase == AttemptPhase.Dropping) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(game.Session.Phase, Is.EqualTo(AttemptPhase.Ready));
        }
        KnifeAppearance Appearance()
        {
            var appearance = game.Presentation.KnifeAppearance;
            Assert.That(appearance, Is.Not.Null, "Gameplay must wire the actual appearance component");
            return appearance;
        }
        static void Image(KnifeAppearance appearance, Texture2D texture) => appearance.SetImage(texture);
        static Renderer ImageRenderer(KnifeAppearance appearance) => appearance.ImageRenderer;
        static Texture BoundImage(Renderer renderer)
        { var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block); return block.GetTexture("_BaseMap"); }
        Texture2D Fixture(int width, int height, RectInt visible)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            textures.Add(texture); var pixels = new Color32[width * height];
            for (int y = visible.y; y < visible.yMax; y++)
                for (int x = visible.x; x < visible.xMax; x++) pixels[y * width + x] = new Color32(220, 40, 160, 255);
            texture.SetPixels32(pixels); texture.Apply(); return texture;
        }
        void Cut()
        {
            var point = game.GameCamera.WorldToScreenPoint(Vector3.zero);
            Assert.That(game.BeginPointer(10, point), Is.True);
            Assert.That(game.ReleasePointer(10, point), Is.True);
        }
        Texture2D TipFixture(bool twoTips)
        {
            var texture = Fixture(64, 64, new RectInt(8, 16, 48, 40));
            for (int y = 8; y < 16; y++)
                for (int x = 10; x < 12; x++) texture.SetPixel(x, y, Color.white);
            if (twoTips)
                for (int y = 8; y < 16; y++)
                    for (int x = 50; x < 52; x++) texture.SetPixel(x, y, Color.white);
            texture.Apply(); return texture;
        }
        static float SourcePixelWorldX(Renderer renderer, float sourceU)
        {
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            var uv = block.GetVector("_BaseMap_ST");
            float quadU = (sourceU - uv.z) / uv.x;
            return renderer.bounds.min.x + renderer.bounds.size.x * quadU;
        }

        [UnityTest] public IEnumerator OffCenterTipStaysOnTheCutAxisWithinDisplayBounds()
        {
            var appearance = Appearance(); Image(appearance, TipFixture(false));
            Assert.That(game.BeginPointer(10, game.GameCamera.WorldToScreenPoint(Vector3.zero)), Is.True);
            yield return null;
            var renderer = ImageRenderer(appearance);
            Assert.That(SourcePixelWorldX(renderer, 11f / 64), Is.EqualTo(0).Within(.0002f));
            Assert.That(renderer.bounds.min.x, Is.GreaterThanOrEqualTo(-.5882f));
            Assert.That(renderer.bounds.max.x, Is.LessThanOrEqualTo(.5882f));
            Assert.That(renderer.bounds.min.y, Is.EqualTo(1.32f).Within(.0002f));
        }

        [UnityTest] public IEnumerator TwoBottomTipsChooseAVisibleAnchorInsteadOfTheTransparentGap()
        {
            var appearance = Appearance(); Image(appearance, TipFixture(true));
            Assert.That(game.BeginPointer(10, game.GameCamera.WorldToScreenPoint(Vector3.zero)), Is.True);
            yield return null;
            // The right 50..51 run is nearer the image center than the left 10..11 run.
            Assert.That(SourcePixelWorldX(ImageRenderer(appearance), 51f / 64), Is.EqualTo(0).Within(.0002f));
        }

        [UnityTest] public IEnumerator RepeatSamplerDoesNotBleedTheTopHandleAcrossTheTransparentTip()
        {
            var appearance = Appearance();
            var texture = Fixture(32, 64, new RectInt(0, 32, 32, 32));
            for (int y = 0; y < 32; y++)
                for (int x = 15; x < 17; x++) texture.SetPixel(x, y, Color.white);
            texture.Apply(); texture.wrapMode = TextureWrapMode.Repeat; texture.filterMode = FilterMode.Bilinear;
            Image(appearance, texture);
            Assert.That(game.BeginPointer(10, game.GameCamera.WorldToScreenPoint(Vector3.zero)), Is.True);
            yield return null;
            var renderer = ImageRenderer(appearance); int previousLayer = renderer.gameObject.layer;
            var cameraObject = new GameObject("KnifeImageRenderingProbe", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
            camera.transform.position = renderer.bounds.center + Vector3.back * 3;
            camera.orthographicSize = renderer.bounds.size.y / 2;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.blue;
            camera.cullingMask = 1 << 31; renderer.gameObject.layer = 31;
            var target = new RenderTexture(128, 256, 24); var previous = RenderTexture.active;
            var pixels = new Texture2D(128, 256, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target; camera.aspect = .5f; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 128, 256), 0, 0); pixels.Apply();
                Assert.That(pixels.GetPixel(8, 240).r, Is.GreaterThan(.5f), "Opaque top must actually render");
                Assert.That(pixels.GetPixel(8, 0).r, Is.LessThan(.03f), "Transparent bottom must show the blue background without the opposite edge");
                Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Repeat), "Borrowed texture sampler must remain unchanged");
            }
            finally
            {
                renderer.gameObject.layer = previousLayer; camera.targetTexture = null; RenderTexture.active = previous;
                target.Release(); Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(cameraObject);
            }
        }

        [UnityTest] public IEnumerator OnePixelImageHasFiniteBoundsAndKeepsTheCutValid()
        {
            var appearance = Appearance(); Image(appearance, Fixture(1, 1, new RectInt(0, 0, 1, 1)));
            Cut(); yield return null;
            var bounds = ImageRenderer(appearance).bounds;
            Assert.That(float.IsNaN(bounds.center.x) || float.IsInfinity(bounds.center.x), Is.False);
            Assert.That(bounds.size.x, Is.EqualTo(1.176f).Within(.0002f));
            Assert.That(bounds.size.y, Is.EqualTo(1.176f).Within(.0002f));
            Assert.That(game.Session.Result.AccuracyUnits, Is.EqualTo(10000));
        }

        [UnityTest] public IEnumerator TransparentPaddingDoesNotMoveTipOrChangeScoreAndContact()
        {
            var appearance = Appearance();
            Cut(); float originalContact = game.Presentation.ContactProgress;
            Assert.That(game.Session.Result.AccuracyUnits, Is.EqualTo(10000));
            game.Retry(); yield return Ready();
            var texture = Fixture(32, 64, new RectInt(8, 8, 16, 48));
            Image(appearance, texture);
            Assert.That(game.Presentation.Knife.gameObject.activeSelf, Is.False, "Choosing an image must not reveal an idle knife");
            Assert.That(game.BeginPointer(10, game.GameCamera.WorldToScreenPoint(Vector3.zero)), Is.True);
            yield return null;
            var renderer = ImageRenderer(appearance);
            Assert.That(renderer.gameObject.activeInHierarchy, Is.True);
            Assert.That(BoundImage(renderer), Is.SameAs(texture));
            Assert.That(renderer.bounds.min.y, Is.EqualTo(1.32f).Within(.0002f)); // Root 2 minus the authored .68 tip.
            Assert.That(renderer.bounds.size.y, Is.EqualTo(1.8984f).Within(.0002f));
            Assert.That(renderer.bounds.size.x, Is.EqualTo(1.8984f / 3).Within(.0002f)); // Visible 16:48, transparent border excluded.
            Assert.That(game.ReleasePointer(10, game.GameCamera.WorldToScreenPoint(Vector3.zero)), Is.True);
            Assert.That(game.Session.Result.AccuracyUnits, Is.EqualTo(10000));
            Assert.That(game.Presentation.ContactProgress, Is.EqualTo(originalContact).Within(.00001f));
        }

        [UnityTest] public IEnumerator ReplacingImagesAndRetryKeepSelectionWithoutCreatingRenderers()
        {
            var appearance = Appearance(); var renderer = ImageRenderer(appearance);
            var first = Fixture(64, 16, new RectInt(0, 0, 64, 16));
            var second = Fixture(16, 64, new RectInt(0, 0, 16, 64));
            int count = game.Presentation.Knife.GetComponentsInChildren<Renderer>(true).Length;
            for (int i = 0; i < 12; i++) Image(appearance, i % 2 == 0 ? first : second);
            Assert.That(game.Presentation.Knife.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(count));
            Assert.That(ImageRenderer(appearance), Is.SameAs(renderer));
            Cut(); game.Retry(); yield return Ready();
            Assert.That(game.Presentation.Knife.gameObject.activeSelf, Is.False);
            Assert.That(BoundImage(renderer), Is.SameAs(second));
            Image(appearance, null);
            Assert.That(renderer.gameObject.activeSelf, Is.False);
            Assert.That(BoundImage(renderer), Is.Null);
            Assert.That(first != null && second != null, Is.True, "Caller retains ownership of uploaded textures");
        }

        [UnityTest] public IEnumerator InvisibleAndOversizedImagesPreserveThePreviousSkin()
        {
            var appearance = Appearance(); var renderer = ImageRenderer(appearance);
            var valid = Fixture(16, 32, new RectInt(0, 0, 16, 32)); Image(appearance, valid);
            foreach (var invalid in new[] { Fixture(16, 16, new RectInt()), Fixture(4096, 1, new RectInt(0, 0, 4096, 1)) })
            {
                Assert.Throws<ArgumentException>(() => Image(appearance, invalid));
                Assert.That(BoundImage(renderer), Is.SameAs(valid));
            }
            yield return null;
        }
    }
}
