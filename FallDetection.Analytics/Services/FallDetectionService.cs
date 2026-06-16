using FallDetection.Analytics.Models;

namespace FallDetection.Analytics.Services
{
    public class FallDetectionService
    {
        private const int FALL_COUNT_THRES = 2;
        private const double BBOX_SHRINKAGE_THRESHOLD = 0.43;
        private const double MIN_HEIGHT_DECREMENT_PX = 5.0;

        // Persistent counters for consecutive falls
        private int counterBboxOnly = 0;
        private int counterMotionPoseAnd = 0;

        public FallDetectionResult DetectFall(FallDetectionRequest request)
        {
            try
            {
                // Extract data from request
                var poseData = request.PoseData;
                var currentBbox = request.CurrentBbox;
                var previousBbox = request.PreviousBbox;

                if (currentBbox.Count < 4 || previousBbox.Count < 4)
                {
                    return new FallDetectionResult();
                }

                // 1. Vertical movement of top (y) coordinate — downward movement = positive
                double dyTop = currentBbox[1] - previousBbox[1];

                // 2. Shrinkage of bbox height — only meaningful if the object moved downward
                double heightDecrementPx = 0;
                double shrinkage = 0;
                if (dyTop > 0 && previousBbox[3] > 0)
                {
                    heightDecrementPx = previousBbox[3] - currentBbox[3];
                    shrinkage = heightDecrementPx / previousBbox[3];
                }

                double torsoAngle = poseData?.TorsoAngle ?? 0;
                double thighUprightness = poseData?.ThighUprightness ?? 0;
                bool hasPose = poseData != null && torsoAngle > 0 && thighUprightness > 0;
                bool strictPoseCondition = hasPose && torsoAngle > 80 && thighUprightness > 60;

                // Match judge_fall.py: require downward motion, enough shrinkage, and a minimum pixel decrease.
                bool bboxMotionDetected = dyTop > 0
                                          && Math.Abs(shrinkage) > BBOX_SHRINKAGE_THRESHOLD
                                          && heightDecrementPx > MIN_HEIGHT_DECREMENT_PX;

                // Algorithm 1: BBox Only
                if (bboxMotionDetected)
                {
                    counterBboxOnly = Math.Min(FALL_COUNT_THRES, counterBboxOnly + 1);
                }
                else
                {
                    counterBboxOnly = Math.Max(0, counterBboxOnly - 1);
                }

                // Algorithm 2: BBox Motion AND Strict Pose
                if (bboxMotionDetected && strictPoseCondition)
                {
                    counterMotionPoseAnd = Math.Min(FALL_COUNT_THRES, counterMotionPoseAnd + 2);
                }
                else
                {
                    counterMotionPoseAnd = Math.Max(0, counterMotionPoseAnd - 1);
                }

                // Determine fall status for each algorithm
                bool fallDetectedBboxOnly = counterBboxOnly >= FALL_COUNT_THRES;
                bool fallDetectedMotionPoseAnd = counterMotionPoseAnd >= FALL_COUNT_THRES;
                int algorithm3Counter = Math.Max(counterBboxOnly, counterMotionPoseAnd);

                return new FallDetectionResult
                {
                    FallDetectedMethod1 = fallDetectedBboxOnly,
                    FallDetectedMethod2 = fallDetectedMotionPoseAnd,
                    FallDetectedMethod3 = fallDetectedMotionPoseAnd,
                    CounterMethod1 = counterBboxOnly,
                    CounterMethod2 = counterMotionPoseAnd,
                    CounterMethod3 = algorithm3Counter,
                    Algorithm3Counter = algorithm3Counter,
                    PrimaryAlert = fallDetectedMotionPoseAnd
                };
            }
            catch (Exception)
            {
                // Console.WriteLine($"Fall detection error: {ex.Message}");
                return new FallDetectionResult();
            }
        }

        public void ResetCounters()
        {
            counterBboxOnly = 0;
            counterMotionPoseAnd = 0;
        }
    }
}