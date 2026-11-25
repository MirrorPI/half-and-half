using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfAndHalf.Presentation
{
    // Stage art is painted in portrait space and sits behind every world-space model.
    public sealed class StageAtmosphere : MonoBehaviour
    {
        [Serializable]
        public sealed class Theme
        {
            public string ObjectId;
            public Sprite Backdrop;
            public AudioClip Score;
            public bool Night;
        }

        public Theme[] Themes = Array.Empty<Theme>();
        public SpriteRenderer Background;
        Camera target;

        void Awake() => target = GetComponent<Camera>();
        void OnEnable() => RenderPipelineManager.beginCameraRendering += Frame;
        void OnDisable() => RenderPipelineManager.beginCameraRendering -= Frame;

        public bool Select(string id, CutAudio audio)
        {
            // The five archived slots still exist in Gameplay29.
            string themeId = id switch {
                "B05" => "B01", "A03" => "B01", "N03" => "N02",
                "P03" => "P01", "P04" => "P05", _ => id
            };
            var theme = Array.Find(Themes, t => t.ObjectId == themeId);
            Background.sprite = theme?.Backdrop;
            audio.SetStageMusic(theme?.Score);
            return theme != null && theme.Night;
        }

        void Frame(ScriptableRenderContext context, Camera camera)
        {
            if (camera != target || Background.sprite == null) return;
            var bounds = Background.sprite.bounds.size;
            float height = camera.orthographicSize * 2;
            float scale = Mathf.Max(height / bounds.y, height * camera.aspect / bounds.x);
            Background.transform.localScale = new Vector3(scale, scale, 1);
        }
    }
}
