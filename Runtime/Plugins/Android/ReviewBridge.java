package me.promptsurge;

import android.app.Activity;
import android.util.Log;
import com.google.android.play.core.review.ReviewInfo;
import com.google.android.play.core.review.ReviewManager;
import com.google.android.play.core.review.ReviewManagerFactory;
import com.google.android.gms.tasks.Task;

public class ReviewBridge {
    private static final String TAG = "PromptSurge";

    public static void requestReview(Activity activity) {
        Log.d(TAG, "requestReview() called");
        ReviewManager manager = ReviewManagerFactory.create(activity);
        Task<ReviewInfo> request = manager.requestReviewFlow();
        Log.d(TAG, "requestReviewFlow() invoked, waiting for completion...");
        request.addOnCompleteListener(task -> {
            Log.d(TAG, "addOnCompleteListener fired, isSuccessful=" + task.isSuccessful());
            if (!task.isSuccessful()) {
                Log.w(TAG, "requestReviewFlow failed", task.getException());
                return;
            }
            ReviewInfo info = task.getResult();
            Log.d(TAG, "ReviewInfo received, launching review flow...");
            manager.launchReviewFlow(activity, info).addOnCompleteListener(launchTask -> {
                // Play Store controls whether the sheet actually appears (quota + eligibility).
                Log.d(TAG, "launchReviewFlow completed, isSuccessful=" + launchTask.isSuccessful());
                if (!launchTask.isSuccessful()) {
                    Log.w(TAG, "launchReviewFlow failed", launchTask.getException());
                }
            });
        });
    }
}
