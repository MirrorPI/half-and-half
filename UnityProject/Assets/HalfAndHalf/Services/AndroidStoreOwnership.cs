#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;

namespace HalfAndHalf.Services
{
    // Android Keystore entries are private to the app installation. The presence of an
    // entry records a previously verified entitlement without trusting a writable save file.
    internal static class AndroidStoreOwnership
    {
        const string Provider = "AndroidKeyStore";
        const string Prefix = "halfandhalf.purchase.v1.";

        static AndroidJavaObject Open()
        {
            using var type = new AndroidJavaClass("java.security.KeyStore");
            var store = type.CallStatic<AndroidJavaObject>("getInstance", Provider);
            try
            {
                // Both arguments must be null. Use the exact overload since JNI cannot infer
                // InputStream and char[] from untyped null arguments.
                var load = AndroidJNI.GetMethodID(store.GetRawClass(), "load", "(Ljava/io/InputStream;[C)V");
                store.Call(load, new object[] { null, null });
                return store;
            }
            catch
            {
                store.Dispose();
                throw;
            }
        }

        public static bool Read(string productId)
        {
            try
            {
                using var store = Open();
                return store.Call<bool>("containsAlias", Prefix + productId);
            }
            catch (AndroidJavaException) { return false; }
        }

        public static bool Write(string productId, bool owned)
        {
            try
            {
                using var store = Open();
                var alias = Prefix + productId;
                if (store.Call<bool>("containsAlias", alias) == owned) return true;
                if (!owned)
                {
                    store.Call("deleteEntry", alias);
                    return true;
                }
                using var properties = new AndroidJavaClass("android.security.keystore.KeyProperties");
                using var builder = new AndroidJavaObject("android.security.keystore.KeyGenParameterSpec$Builder",
                    alias, properties.GetStatic<int>("PURPOSE_SIGN"));
                using var spec = builder.Call<AndroidJavaObject>("build");
                using var generatorClass = new AndroidJavaClass("javax.crypto.KeyGenerator");
                using var generator = generatorClass.CallStatic<AndroidJavaObject>("getInstance", "HmacSHA256", Provider);
                generator.Call("init", spec);
                using var key = generator.Call<AndroidJavaObject>("generateKey");
                return true;
            }
            catch (AndroidJavaException) { return false; }
        }
    }
}
#endif
