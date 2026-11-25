using System;
using System.Collections;
using HalfAndHalf.Core;
using HalfAndHalf.Gameplay;
using HalfAndHalf.UI;
using HalfAndHalf.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace HalfAndHalf.Tests
{
    public sealed class RankingUiTests
    {
        Component Panel()
        {
            var t=typeof(MvpCollection).Assembly.GetType("HalfAndHalf.UI.RankingPanel");Assert.That(t,Is.Not.Null,"Task4 needs the in-game ranking panel.");
            var game=Object.FindFirstObjectByType<AppleGame>();var panel=game.GetComponent(t);Assert.That(panel,Is.Not.Null,"Rankings must be wired into Gameplay.");return panel;
        }
        [UnityTest] public IEnumerator OpenBlocksCutsAndOtherModalsCloseResetsHiddenKnife()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");yield return null;var panel=Panel();var game=Object.FindFirstObjectByType<AppleGame>();
            panel.GetType().GetMethod("Open").Invoke(panel,null);Assert.That(game.UiOpen,Is.True);Assert.That(game.BeginPointer(42,Vector2.zero),Is.False);
            var collection=game.GetComponent<MvpCollection>();collection.Open();Assert.That(collection.Panel.activeSelf,Is.False);
            panel.GetType().GetMethod("Close").Invoke(panel,null);Assert.That(game.UiOpen,Is.False);Assert.That(game.Session.Phase,Is.EqualTo(AttemptPhase.Dropping));Assert.That(game.Presentation.Knife.gameObject.activeSelf,Is.False);
        }
        [UnityTest] public IEnumerator SettingsOwnsModalAndRankingsCannotBypassIt()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");yield return null;var panel=Panel();var game=Object.FindFirstObjectByType<AppleGame>();
            var settings=game.GetComponent<GameSettings>();settings.OpenButton.onClick.Invoke();panel.GetType().GetMethod("Open").Invoke(panel,null);
            var rankingObject=(GameObject)panel.GetType().GetField("Panel").GetValue(panel);Assert.That(rankingObject.activeSelf,Is.False);Assert.That(game.UiOpen,Is.True);settings.CloseButton.onClick.Invoke();
        }
        [UnityTest] public IEnumerator RankingShowsOfficialFieldsKoreanAndLiteralPlayerNames()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");yield return null;
            var p=(RankingPanel)Panel();p.Open();yield return null;
            RankingUiCapture.Save(p.Game,"ranking-guest.png",428,926);
            p.Display(new RankingPage{Entries=new[]{new RankingEntry{Rank=1,Score=RankingScore.Encode(10000,999),PlayerName="모의 표시 사과"},new RankingEntry{Rank=2,Score=RankingScore.Encode(9999,1220),PlayerName="모의 표시 수박"},new RankingEntry{Rank=2,Score=RankingScore.Encode(9999,1220),PlayerName="<b>모의 표시</b>"}},LocalPlayer=new RankingEntry{Rank=2,Score=RankingScore.Encode(9999,1220),PlayerName="모의 표시 내 기록"}});
            Assert.That(p.Rows[0].text,Does.Contain("100.00%  /  0.999초"));Assert.That(p.Rows[1].text,Does.Contain("99.99%  /  1.220초"));Assert.That(p.Rows[1].text,Does.StartWith("#2"));Assert.That(p.Rows[2].text,Does.StartWith("#2"));
            foreach(var text in p.Panel.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {Assert.That(text.richText,Is.False);foreach(char ch in text.text)if(!char.IsControl(ch))Assert.That(text.font.HasCharacter(ch,true,true),Is.True,"Missing glyph "+ch);}
            RankingUiCapture.Save(p.Game,"ranking-display-fixture.png",428,926);RankingUiCapture.Save(p.Game,"ranking-display-small-fixture.png",375,667);
            p.Close();yield return null;Assert.That(p.Game.Presentation.Knife.gameObject.activeSelf,Is.False);RankingUiCapture.Save(p.Game,"ranking-close-ready.png",428,926);
        }
        [UnityTest] public IEnumerator FailedLocalSaveDoesNotReachOnlineQueueUntilRecovered()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");yield return null;
            var m=Object.FindFirstObjectByType<MvpCollection>();var c=m.GetComponent<GameCenterController>();var g=m.Game;
            System.IO.Directory.CreateDirectory(m.SavePath);g.Session.Land((long)(Time.realtimeSinceStartupAsDouble*1000),TransformState.Identity);
            var point=g.GameCamera.WorldToScreenPoint(Vector3.zero);g.BeginPointer(42,point);g.ReleasePointer(42,point);yield return new WaitForSecondsRealtime(.6f);
            Assert.That(m.SavePending,Is.True);Assert.That(c.PendingCount,Is.Zero);
            System.IO.Directory.Delete(m.SavePath);g.Retry();g.Session.Land((long)(Time.realtimeSinceStartupAsDouble*1000),TransformState.Identity);g.BeginPointer(42,point);g.ReleasePointer(42,point);yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(m.SavePending,Is.False);Assert.That(c.PendingCount,Is.EqualTo(1));Assert.That(c.Queue.Entries[0].objectId,Is.EqualTo("B01"));Assert.That(c.Queue.Entries[0].playerId,Is.Empty);
        }
        [UnityTest] public IEnumerator RankingBackgroundCoversWorldOutsideNotchedSafeArea()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");yield return null;var p=(RankingPanel)Panel();yield return null;p.Open();var g=p.Game;var safe=g.Hud.SafeRoot;var min=safe.anchorMin;var max=safe.anchorMax;var position=g.Presentation.BodyRoot.localPosition;
            g.enabled=false;
            try
            {
                safe.anchorMin=new Vector2(0,.04f);safe.anchorMax=new Vector2(1,.95f);g.Presentation.BodyRoot.localPosition=new Vector3(0,3.5f,0);
                var pixels=RankingUiCapture.Save(g,"ranking-notched-viewport.png",428,926);
                Assert.That(Vector3.Distance(new Vector3(pixels[0].r,pixels[0].g,pixels[0].b),new Vector3(pixels[1].r,pixels[1].g,pixels[1].b)),Is.LessThan(.035f),"The area above SafeRoot must be covered by the ranking background, not the world model.");
            }
            finally{safe.anchorMin=min;safe.anchorMax=max;g.Presentation.BodyRoot.localPosition=position;g.enabled=true;p.Close();}
        }
    }
    static class RankingUiCapture
    {
        public static Color[] Save(AppleGame g,string name,int width,int height)
        {
            Color[] pixels=null;
            var camera=g.GameCamera;var canvas=g.Hud.GetComponentInParent<Canvas>();var target=new RenderTexture(width,height,24){antiAliasing=4};var prior=camera.targetTexture;var active=RenderTexture.active;var mode=canvas.renderMode;var world=canvas.worldCamera;float plane=canvas.planeDistance,aspect=camera.aspect,size=camera.orthographicSize;
            try
            {
                camera.targetTexture=target;camera.aspect=(float)width/height;camera.orthographicSize=Mathf.Max(3.8f,1.85f/camera.aspect);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.6f;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();pixels=new[]{texture.GetPixel(width/2,height-10),texture.GetPixel(width/2,height/2)};
                // Repeated regressions must not overwrite archived task-4 verification evidence.
                string folder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"half-and-half-test-captures","ranking");System.IO.Directory.CreateDirectory(folder);System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,name),texture.EncodeToPNG());Object.Destroy(texture);
            }
            finally{canvas.renderMode=mode;canvas.worldCamera=world;canvas.planeDistance=plane;camera.targetTexture=prior;camera.aspect=aspect;camera.orthographicSize=size;RenderTexture.active=active;Object.Destroy(target);}
            return pixels;
        }
    }
}
