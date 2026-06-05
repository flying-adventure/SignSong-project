package com.dojang.signsong;

import android.app.Activity;
import android.content.Intent;
import android.util.Log;

public class GoogleAuthBridge {
    private static final String TAG = "GoogleAuthBridge";

    public static void startSignIn(Activity activity, String webClientId, String unityObjectName) {
        if (activity == null) {
            Log.e(TAG, "startSignIn failed: activity is null");
            return;
        }

        if (webClientId == null || webClientId.length() == 0) {
            Log.e(TAG, "startSignIn failed: webClientId is empty");
            return;
        }

        if (unityObjectName == null || unityObjectName.length() == 0) {
            unityObjectName = "AndroidGoogleAuthBridge";
        }

        Log.d(TAG, "Starting GoogleAuthActivity");

        Intent intent = new Intent(activity, GoogleAuthActivity.class);
        intent.putExtra("webClientId", webClientId);
        intent.putExtra("unityObjectName", unityObjectName);
        activity.startActivity(intent);
    }
}