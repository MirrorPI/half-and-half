using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace HalfAndHalf.Presentation
{
    public sealed class PhotoBlade : MonoBehaviour
    {
        public KnifeAppearance Appearance;
        public Func<bool> CanEquip;
        public event Action PhotoSelected;
        public string Status { get; private set; } = "기본 칼 장착 중";
        public Texture2D Preview => pending != null ? pending : equipped;
        public bool HasPendingPhoto => pending != null;
        public bool HasEquippedPhoto => equipped != null;

        // The native pickers decode at this size before passing pixels to Unity.
        const int MaximumSide = 1024;
        const int MaximumBytes = MaximumSide * MaximumSide * 5;
        Texture2D pending, equipped;
        bool entitlementActive;
        int request;
        float nextPoll;
        string directory;

        void Update()
        {
            bool allowed = Appearance != null && CanEquip?.Invoke() == true;
            if (allowed != entitlementActive)
            {
                entitlementActive = allowed;
                if (allowed) Restore();
                else ReleaseEquipped();
            }
            if (request != 0 && Time.unscaledTime >= nextPoll)
            {
                nextPoll = Time.unscaledTime + .15f;
                try { Poll(); }
                catch (Exception) { EndPick(); Status = "사진을 불러오지 못했어요. 다시 선택해 주세요."; }
            }
        }

        public void Pick()
        {
            if (request != 0 || CanEquip?.Invoke() != true) return;
            try
            {
#if UNITY_EDITOR
                string path = UnityEditor.EditorUtility.OpenFilePanel("칼 사진 선택 (최대 1024px PNG)", "", "png");
                if (string.IsNullOrEmpty(path)) { Status = "선택을 취소했어요."; return; }
                Accept(ReadBounded(path));
#elif UNITY_IOS
                request = HHPhotoBegin(MaximumSide);
                Status = "사진을 선택해 주세요.";
#elif UNITY_ANDROID
                using (var picker = new AndroidJavaClass(AndroidPicker))
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    request = picker.CallStatic<int>("begin", activity, MaximumSide);
                Status = "사진을 선택해 주세요.";
#else
                Status = "기기에서 사진을 선택할 수 있어요.";
#endif
            }
            catch (Exception) { EndPick(); Status = "사진을 불러오지 못했어요. 다른 사진을 선택해 주세요."; }
        }

        void Poll()
        {
            int result = 0;
            byte[] bytes = null;
#if UNITY_IOS && !UNITY_EDITOR
            result = HHPhotoSize(request);
            if (result > 0 && result <= MaximumBytes)
            {
                bytes = new byte[result];
                if (!HHPhotoCopy(request, bytes, result)) bytes = null;
            }
#elif UNITY_ANDROID && !UNITY_EDITOR
            using (var picker = new AndroidJavaClass(AndroidPicker))
            {
                result = picker.CallStatic<int>("size", request);
                if (result > 0 && result <= MaximumBytes)
                {
                    var signed = picker.CallStatic<sbyte[]>("pixels", request);
                    if (signed != null)
                    {
                        bytes = new byte[signed.Length];
                        Buffer.BlockCopy(signed, 0, bytes, 0, bytes.Length);
                    }
                }
            }
#endif
            if (result == 0) return;
            EndPick();
            if (result == -2) Status = "선택을 취소했어요.";
            else Accept(bytes);
        }

        void EndPick()
        {
            if (request == 0) return;
            int finished = request;
            request = 0;
#if UNITY_IOS && !UNITY_EDITOR
            HHPhotoEnd(finished);
#elif UNITY_ANDROID && !UNITY_EDITOR
            using (var picker = new AndroidJavaClass(AndroidPicker)) picker.CallStatic("end", finished);
#endif
        }

        void Accept(byte[] png)
        {
            SetPreview(Decode(png));
            PhotoSelected?.Invoke();
        }

        public void SetPreview(Texture2D next)
        {
            if (next == null) return;
            if (pending != null) Destroy(pending);
            pending = next;
            Status = "사진을 선택했어요.";
        }

        public void Apply()
        {
            if (CanEquip?.Invoke() != true) { Status = "공방에서 커스텀 칼을 먼저 해금해 주세요."; return; }
            if (pending == null || Appearance == null) { Status = "먼저 사진을 선택해 주세요."; return; }
            string temporary = null;
            try
            {
                string path = StoragePath();
                temporary = path + ".new";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                // Re-encoding strips source metadata and never persists the original photo.
                File.WriteAllBytes(temporary, pending.EncodeToPNG());
                Appearance.SetImage(pending);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                if (equipped != null) Destroy(equipped);
                equipped = pending;
                pending = null;
                entitlementActive = true;
                Status = "커스텀 칼 장착 완료";
            }
            catch (Exception)
            {
                Appearance.SetImage(equipped);
                Status = "커스텀 칼을 저장하지 못했어요. 다시 시도해 주세요.";
            }
            finally { if (temporary != null) TryDelete(temporary); }
        }

        public void ResetBlade()
        {
            try
            {
                string path = StoragePath();
                if (Directory.Exists(Path.GetDirectoryName(path)))
                {
                    File.Delete(path + ".new");
                    File.Delete(path);
                }
                EndPick();
                ReleaseEquipped();
                if (pending != null) Destroy(pending);
                pending = null;
                Status = "기본 칼 장착 중";
            }
            catch (Exception) { Status = "사진을 지우지 못했어요. 다시 시도해 주세요."; }
        }

        void Restore()
        {
            Texture2D restored = null;
            try
            {
                string path = StoragePath();
                TryDelete(path + ".new");
                if (!File.Exists(path)) return;
                restored = Decode(ReadBounded(path));
                Appearance.SetImage(restored);
                equipped = restored;
                if (pending == null) Status = "커스텀 칼 장착 중";
            }
            catch (Exception)
            {
                if (restored != null) Destroy(restored);
                Status = "저장된 사진을 열 수 없어요. 다시 선택해 주세요.";
            }
        }

        static void TryDelete(string path)
        {
            try { File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        static byte[] ReadBounded(string path)
        {
            var info = new FileInfo(path);
            if (info.Length < 24 || info.Length > MaximumBytes) throw new InvalidDataException();
            return File.ReadAllBytes(path);
        }

        static Texture2D Decode(byte[] png)
        {
            if (png == null || png.Length < 33 || png.Length > MaximumBytes ||
                png[0] != 137 || png[1] != 80 || png[2] != 78 || png[3] != 71 ||
                png[4] != 13 || png[5] != 10 || png[6] != 26 || png[7] != 10 ||
                png[12] != 'I' || png[13] != 'H' || png[14] != 'D' || png[15] != 'R')
                throw new InvalidDataException();
            int width = Dimension(png, 16), height = Dimension(png, 20);
            if (width < 1 || height < 1 || width > MaximumSide || height > MaximumSide) throw new InvalidDataException();
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            try
            {
                if (!texture.LoadImage(png)) throw new InvalidDataException();
                foreach (var pixel in texture.GetPixels32()) if (pixel.a > 8) return texture;
                throw new InvalidDataException();
            }
            catch { Destroy(texture); throw; }
        }

        static int Dimension(byte[] png, int i) =>
            (png[i] << 24) | (png[i + 1] << 16) | (png[i + 2] << 8) | png[i + 3];

        string StoragePath()
        {
            if (directory == null)
            {
#if UNITY_IOS && !UNITY_EDITOR
                directory = Marshal.PtrToStringAnsi(HHPhotoDirectory());
#elif UNITY_ANDROID && !UNITY_EDITOR
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var folder = activity.Call<AndroidJavaObject>("getNoBackupFilesDir"))
                    directory = folder.Call<string>("getAbsolutePath");
#else
                directory = Application.persistentDataPath;
#endif
            }
            if (string.IsNullOrEmpty(directory)) throw new IOException();
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_PHOTO_KNIFE_PREVIEW
            return Path.Combine(directory, "preview-photo-blade.png");
#else
            return Path.Combine(directory, "photo-blade.png");
#endif
        }

        void ReleaseEquipped()
        {
            if (Appearance != null) Appearance.UseDefault();
            if (equipped != null) Destroy(equipped);
            equipped = null;
        }

        void OnDestroy()
        {
            EndPick();
            if (equipped != null) ReleaseEquipped();
            if (pending != null) Destroy(pending);
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int HHPhotoBegin(int maximumSide);
        [DllImport("__Internal")] static extern int HHPhotoSize(int request);
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.I1)]
        static extern bool HHPhotoCopy(int request, [Out] byte[] bytes, int length);
        [DllImport("__Internal")] static extern void HHPhotoEnd(int request);
        [DllImport("__Internal")] static extern IntPtr HHPhotoDirectory();
#elif UNITY_ANDROID && !UNITY_EDITOR
        const string AndroidPicker = "com.halfandhalf.photos.PhotoBladePicker";
#endif
    }
}
