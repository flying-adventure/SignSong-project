using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AndroidGoogleAuthBridge : MonoBehaviour
{
    private const string UnityCallbackObjectName = "AndroidGoogleAuthBridge";

    // Java package와 반드시 일치해야 함
    private const string BridgeClassName = "com.dojang.signsong.GoogleAuthBridge";

    [Header("Google OAuth")]
    [SerializeField]
    private string webClientId = "310234877349-hegevlvvic7ht4frlevmvd20ivtcqiok.apps.googleusercontent.com";

    [Header("After Login")]
    [SerializeField]
    private string nextSceneName = "Sign_list";

    private FirebaseAuth auth;
    private bool firebaseReady;

    private static AndroidGoogleAuthBridge instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        gameObject.name = UnityCallbackObjectName;
        DontDestroyOnLoad(gameObject);

        InitializeFirebase();
    }

    private void InitializeFirebase()
    {
        Debug.Log("[Auth] Checking Firebase dependencies...");

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            DependencyStatus dependencyStatus = task.Result;

            if (dependencyStatus == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                firebaseReady = true;
                Debug.Log("[Auth] Firebase ready");
            }
            else
            {
                firebaseReady = false;
                Debug.LogError("[Auth] Firebase dependency error: " + dependencyStatus);
            }
        });
    }

    public void StartGoogleLogin()
    {
        if (!firebaseReady)
        {
            Debug.LogWarning("[Auth] Firebase is not ready yet");
            return;
        }

        if (string.IsNullOrEmpty(webClientId) ||
            webClientId == "YOUR_WEB_CLIENT_ID.apps.googleusercontent.com")
        {
            Debug.LogError("[Auth] Web Client ID is not set");
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            Debug.Log("[Auth] Starting Android Google login");

            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject currentActivity =
                    unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                using (AndroidJavaClass bridge = new AndroidJavaClass(BridgeClassName))
                {
                    bridge.CallStatic(
                        "startSignIn",
                        currentActivity,
                        webClientId,
                        UnityCallbackObjectName
                    );
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Auth] Android bridge error: " + e.Message);
        }
#else
        Debug.LogWarning("[Auth] Google login bridge runs only on Android device/emulator");
#endif
    }

    // Java에서 UnityPlayer.UnitySendMessage로 호출됨
    public void OnGoogleIdToken(string idToken)
    {
        if (string.IsNullOrEmpty(idToken))
        {
            Debug.LogError("[Auth] Received empty Google ID token");
            return;
        }

        Debug.Log("[Auth] Google ID token received. Signing in to Firebase...");

        Credential credential = GoogleAuthProvider.GetCredential(idToken, null);

        auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                Debug.LogWarning("[Auth] Firebase sign-in canceled");
                return;
            }

            if (task.IsFaulted)
            {
                Debug.LogError("[Auth] Firebase sign-in failed: " + task.Exception);
                return;
            }

            FirebaseUser user = task.Result;

            string uid = user.UserId ?? "";
            string email = user.Email ?? "";
            string displayName = user.DisplayName ?? "";
            string photoUrl = user.PhotoUrl != null ? user.PhotoUrl.ToString() : "";

            // FirebaseUser.DisplayName이 비어 있으면 이메일 앞부분을 이름으로 사용
            if (string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(email))
            {
                int atIndex = email.IndexOf("@");
                displayName = atIndex > 0 ? email.Substring(0, atIndex) : email;
            }

            // 그래도 비어 있으면 기본값
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = "사용자";
            }

            Debug.Log("[Auth] Firebase login success");
            Debug.Log("[Auth] UID: " + uid);
            Debug.Log("[Auth] Name: " + displayName);
            Debug.Log("[Auth] Email: " + email);
            Debug.Log("[Auth] PhotoUrl: " + photoUrl);

            SaveUserProfile(uid, displayName, email, photoUrl);
            MoveToNextScene();
        });
    }

    // Java에서 UnityPlayer.UnitySendMessage로 호출됨
    public void OnGoogleAuthError(string error)
    {
        Debug.LogError("[Auth] Google auth error: " + error);
    }

    private void SaveUserProfile(string uid, string displayName, string email, string photoUrl)
    {
        PlayerPrefs.SetString("USER_UID", uid);
        PlayerPrefs.SetString("USER_NAME", displayName);
        PlayerPrefs.SetString("USER_EMAIL", email);
        PlayerPrefs.SetString("USER_PHOTO_URL", photoUrl);
        PlayerPrefs.Save();

        Debug.Log("[Auth] User profile saved to PlayerPrefs");
    }

    private void MoveToNextScene()
    {
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning("[Auth] Next scene name is empty");
            return;
        }

        Debug.Log("[Auth] Loading next scene: " + nextSceneName);
        SceneManager.LoadScene(nextSceneName);
    }

    public void SignOut()
    {
        if (auth != null)
        {
            auth.SignOut();
        }

        PlayerPrefs.DeleteKey("USER_UID");
        PlayerPrefs.DeleteKey("USER_NAME");
        PlayerPrefs.DeleteKey("USER_EMAIL");
        PlayerPrefs.DeleteKey("USER_PHOTO_URL");
        PlayerPrefs.Save();

        Debug.Log("[Auth] Signed out");
    }
}