using HalfAndHalf.Localization;
using System.Collections.Generic;
using HalfAndHalf.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HalfAndHalf.UI
{
    public sealed class PhotoCutoutEditor : MonoBehaviour
    {
        public PhotoBlade Photos;
        public GameObject Panel;
        public RawImage Photo;
        public AspectRatioFitter Fit;
        public PhotoOutlineInput Outline;
        public TMP_Text Instructions, UseCaption;
        public Button CancelButton, RedrawButton, UseButton;
        Texture2D source, cutout;
        public bool CanTrace => source != null && cutout == null;

        void Awake()
        {
            Photos.PhotoSelected += EditPending;
            CancelButton.onClick.AddListener(Close);
            RedrawButton.onClick.AddListener(Redraw);
            UseButton.onClick.AddListener(Confirm);
        }

        public void EditPending()
        {
            Close();
            if (!Photos.HasPendingPhoto) return;
            source = Photos.Preview;
            Panel.SetActive(true);
            Panel.transform.SetAsLastSibling();
            Redraw();
        }

        void Show(Texture2D texture)
        {
            Photo.texture = texture;
            Fit.aspectRatio = (float)texture.width / texture.height;
            Outline.ResetView();
        }

        void Redraw()
        {
            if (cutout != null) Destroy(cutout);
            cutout = null;
            Show(source);
            Instructions.text = L10n.T("한 손가락으로 윤곽을 그리세요. 두 손가락으로 확대하고 이동할 수 있어요.");
            UseCaption.text = L10n.T("원본 그대로");
            RedrawButton.interactable = false;
        }

        public void FinishStroke(IReadOnlyList<Vector2> points)
        {
            if (!CanTrace) return;
            if (!PhotoCutoutMask.TryCrop(source.GetPixels32(), source.width, source.height, points,
                out var pixels, out int width, out int height))
            {
                Instructions.text = L10n.T("남길 부분을 한 바퀴 둘러 그려 주세요.");
                return;
            }
            cutout = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            cutout.SetPixels32(pixels);
            cutout.Apply();
            Show(cutout);
            Instructions.text = L10n.T("칼 모양을 확인하세요. 다시 그리면 원본 사진에서 시작해요.");
            UseCaption.text = L10n.T("이 모양 사용");
            RedrawButton.interactable = true;
        }

        void Confirm()
        {
            if (cutout != null)
            {
                Photos.SetPreview(cutout);
                cutout = null;
            }
            Close();
        }

        public void Close()
        {
            Photo.texture = null;
            Panel.SetActive(false);
            if (cutout != null) Destroy(cutout);
            // The selected original belongs to PhotoBlade.
            source = cutout = null;
        }

        void OnDestroy()
        {
            if (Photos != null) Photos.PhotoSelected -= EditPending;
            if (cutout != null) Destroy(cutout);
        }
    }
}
