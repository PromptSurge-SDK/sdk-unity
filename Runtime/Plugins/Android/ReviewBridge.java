package me.promptsurge;

import android.app.Activity;
import com.google.android.play.core.review.ReviewInfo;
import com.google.android.play.core.review.ReviewManager;
import com.google.android.play.core.review.ReviewManagerFactory;
import com.google.android.gms.tasks.Task;

public class ReviewBridge {
    public static void requestReview(Activity activity) {
        ReviewManager manager = ReviewManagerFactory.create(activity);
        Task<ReviewInfo> request = manager.requestReviewFlow();
        request.addOnCompleteListener(task -> {
            if (!task.isSuccessful()) return;
            ReviewInfo info = task.getResult();
            manager.launchReviewFlow(activity, info);
            // Play Store controls whether the sheet actually appears (quota + eligibility).
        });
    }
}
