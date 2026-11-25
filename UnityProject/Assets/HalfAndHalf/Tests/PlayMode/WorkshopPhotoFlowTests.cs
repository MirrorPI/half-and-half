#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HalfAndHalf.Presentation;
using HalfAndHalf.Services;
using HalfAndHalf.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HalfAndHalf.Tests
{
    public sealed class WorkshopPhotoFlowTests
    {
        [UnityTest]
        public IEnumerator SelectedPhotoCanBeCroppedEquippedRestoredAndRemoved()
        {
            const string scene = "Assets/Scenes/Gameplay29.unity";
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            string directory = Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string saved = Path.Combine(directory, "photo-blade.png");

            // Keep the player's own photos and records out of the test before Start/Update runs.
            void ConfigureScene(Scene loaded, LoadSceneMode mode)
            {
                if (loaded.path != scene) return;
                var workshop = Object.FindFirstObjectByType<BladeWorkshop>();
                typeof(PhotoBlade).GetField("directory", privateInstance).SetValue(workshop.Photos, directory);
                workshop.Collection.SavePath = Path.Combine(directory, "progress.json");
                workshop.Ads.enabled = false;
                typeof(RoundAds).GetProperty("KnifeProgress").SetValue(workshop.Ads, new KnifeAdProgress(5, null));
            }

            SceneManager.sceneLoaded += ConfigureScene;
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(scene, new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var shop = Object.FindFirstObjectByType<BladeWorkshop>();
                shop.OpenButton.onClick.Invoke();
                Assert.IsTrue(shop.PickButton.gameObject.activeInHierarchy);

                var pixels = new Color32[32 * 64];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(60, 150, 240, 255);
                var sample = new Texture2D(32, 64);
                sample.SetPixels32(pixels);
                sample.Apply();
                var png = sample.EncodeToPNG();
                Object.Destroy(sample);
                // Accept is the common return point of the editor, iOS and Android photo pickers.
                typeof(PhotoBlade).GetMethod("Accept", privateInstance).Invoke(shop.Photos, new object[]{ png });
                var editor = shop.GetComponent<PhotoCutoutEditor>();
                Assert.IsTrue(editor.Panel.activeInHierarchy);
                editor.FinishStroke(new[] { new Vector2(.5f, .125f), new Vector2(.125f, .875f), new Vector2(.875f, .875f) });
                Assert.IsTrue(editor.RedrawButton.interactable);
                editor.UseButton.onClick.Invoke();
                Assert.IsFalse(editor.Panel.activeSelf);
                var crop = shop.Photos.Preview;
                Assert.AreEqual(24, crop.width);
                Assert.Less(crop.height, 64);
                Assert.IsTrue(Array.Exists(crop.GetPixels32(), p => p.a == 0));
                yield return null;
                Assert.IsTrue(shop.EquipButton.interactable);
                shop.EquipButton.onClick.Invoke();
                Assert.IsTrue(shop.Photos.HasEquippedPhoto);
                Assert.IsFalse(shop.Photos.Appearance.DefaultVisual.activeSelf);
                var expected = crop.GetPixels32();
                Assert.IsTrue(File.Exists(saved));

                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(scene, new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                shop = Object.FindFirstObjectByType<BladeWorkshop>();
                shop.OpenButton.onClick.Invoke();
                Assert.IsTrue(shop.Photos.HasEquippedPhoto);
                Assert.IsTrue(shop.Photos.Appearance.ImageRenderer.gameObject.activeSelf);
                CollectionAssert.AreEqual(expected, shop.Photos.Preview.GetPixels32());
                Assert.IsTrue(shop.DefaultButton.gameObject.activeInHierarchy);
                shop.DefaultButton.onClick.Invoke();
                Assert.IsFalse(shop.Photos.HasEquippedPhoto);
                Assert.IsTrue(shop.Photos.Appearance.DefaultVisual.activeSelf);
                Assert.IsFalse(shop.Photos.Appearance.ImageRenderer.gameObject.activeSelf);
                Assert.IsFalse(File.Exists(saved));
            }
            finally
            {
                SceneManager.sceneLoaded -= ConfigureScene;
                Directory.Delete(directory, true);
            }
        }
    }
}
#endif
