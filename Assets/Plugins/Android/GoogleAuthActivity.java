package com.dojang.signsong;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.util.Log;

import com.google.android.gms.auth.api.signin.GoogleSignIn;
import com.google.android.gms.auth.api.signin.GoogleSignInAccount;
import com.google.android.gms.auth.api.signin.GoogleSignInClient;
import com.google.android.gms.auth.api.signin.GoogleSignInOptions;
import com.google.android.gms.common.api.ApiException;
import com.google.android.gms.tasks.OnCompleteListener;
import com.google.android.gms.tasks.Task;
import com.unity3d.player.UnityPlayer;

public class GoogleAuthActivity extends Activity {
    private static final String TAG = "GoogleAuthActivity";
    private static final int RC_SIGN_IN = 9001;

    private String unityObjectName = "AndroidGoogleAuthBridge";

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        String webClientId = getIntent().getStringExtra("webClientId");
        String objectName = getIntent().getStringExtra("unityObjectName");

        if (objectName != null && objectName.length() > 0) {
            unityObjectName = objectName;
        }

        if (webClientId == null || webClientId.length() == 0) {
            sendError("WEB_CLIENT_ID_EMPTY");
            finish();
            return;
        }

        Log.d(TAG, "GoogleAuthActivity created");

        GoogleSignInOptions gso =
                new GoogleSignInOptions.Builder(GoogleSignInOptions.DEFAULT_SIGN_IN)
                        .requestIdToken(webClientId)
                        .requestEmail()
                        .build();

        final GoogleSignInClient googleSignInClient = GoogleSignIn.getClient(this, gso);

        googleSignInClient.signOut().addOnCompleteListener(new OnCompleteListener<Void>() {
            @Override
            public void onComplete(Task<Void> task) {
                Log.d(TAG, "Launching Google sign-in intent");
                Intent signInIntent = googleSignInClient.getSignInIntent();
                startActivityForResult(signInIntent, RC_SIGN_IN);
            }
        });
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);

        if (requestCode != RC_SIGN_IN) {
            return;
        }

        Task<GoogleSignInAccount> task = GoogleSignIn.getSignedInAccountFromIntent(data);

        try {
            GoogleSignInAccount account = task.getResult(ApiException.class);

            if (account == null) {
                sendError("GOOGLE_ACCOUNT_NULL");
                finish();
                return;
            }

            String idToken = account.getIdToken();

            if (idToken == null || idToken.length() == 0) {
                sendError("GOOGLE_ID_TOKEN_NULL");
                finish();
                return;
            }

            Log.d(TAG, "Google ID token received. Sending to Unity.");
            UnityPlayer.UnitySendMessage(unityObjectName, "OnGoogleIdToken", idToken);

        } catch (ApiException e) {
            sendError("GOOGLE_SIGN_IN_FAILED:" + e.getStatusCode() + ":" + e.getMessage());
        } catch (Exception e) {
            sendError("GOOGLE_SIGN_IN_EXCEPTION:" + e.getMessage());
        } finally {
            finish();
        }
    }

    private void sendError(String message) {
        Log.e(TAG, message);

        try {
            UnityPlayer.UnitySendMessage(unityObjectName, "OnGoogleAuthError", message);
        } catch (Exception e) {
            Log.e(TAG, "Failed to send error to Unity: " + e.getMessage());
        }
    }
}