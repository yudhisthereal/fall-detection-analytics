using FallDetection.Analytics.Models;

namespace FallDetection.Analytics.Services
{
    public class FallDetectionService
    {
        private const int FALL_COUNT_THRES = 2;

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
                var elapsedMs = request.ElapsedMs;

                if (currentBbox.Count < 4 || previousBbox.Count < 4)
                {
                    return new FallDetectionResult();
                }

                // 1. Vertical speed of top (y) coordinate
                double dyTop = currentBbox[1] - previousBbox[1];
                double vTop = dyTop / elapsedMs;

                // 2. Vertical change of height (shrinking = falling)
                double dh = previousBbox[3] - currentBbox[3];
                double vHeight = dh / elapsedMs;

                double torsoAngle = 0;
                double thighUprightness = 0;
                string? label = null;

                if (request.UseHme)
                {
                    // HME mode handling
                    if (poseData != null)
                    {
                        label = poseData.Label;
                        var rawVals = poseData.RawIntValues;
                        
                        if (label == "lying_down")
                        {
                            torsoAngle = rawVals?.GetValueOrDefault("Tra", 0) / 100.0 ?? 85.0;
                            thighUprightness = rawVals?.GetValueOrDefault("Tha", 0) / 100.0 ?? 70.0;
                        }
                        else
                        {
                            torsoAngle = 30.0;
                            thighUprightness = 30.0;
                        }
                    }
                }
                else
                {
                    // Plain mode
                    if (poseData != null)
                    {
                        torsoAngle = poseData.TorsoAngle;
                        thighUprightness = poseData.ThighUprightness;
                        label = poseData.Label;
                    }
                }

                // Calculate bbox motion evidence
                double vBboxY = 0.43; // fallParam["v_bbox_y"]
                bool bboxMotionDetected = (vTop > vBboxY || vHeight > vBboxY);

                // Calculate pose conditions
                bool strictPoseCondition = false;
                bool flexiblePoseCondition = false;

                if (request.UseHme)
                {
                    // In HME mode
                    if (label == "lying_down")
                    {
                        flexiblePoseCondition = true;
                        if (torsoAngle > 80 && thighUprightness > 60)
                        {
                            strictPoseCondition = true;
                        }
                    }
                }
                else
                {
                    // Plain mode
                    if (torsoAngle > 0 && thighUprightness > 0)
                    {
                        strictPoseCondition = (torsoAngle > 80 && thighUprightness > 60);
                        
                        if (torsoAngle > 80)
                        {
                            flexiblePoseCondition = true;
                        }
                        else if (30 < torsoAngle && torsoAngle < 80 && thighUprightness > 60)
                        {
                            flexiblePoseCondition = true;
                        }
                    }
                }

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
                else if (bboxMotionDetected || strictPoseCondition)
                {
                    counterMotionPoseAnd = Math.Min(FALL_COUNT_THRES, counterMotionPoseAnd + 1);
                }
                else
                {
                    counterMotionPoseAnd = Math.Max(0, counterMotionPoseAnd - 1);
                }

                // Algorithm 3: Flexible Verification
                int algorithm3Counter = Math.Max(counterBboxOnly, counterMotionPoseAnd);
                bool fallDetectedFlexible = false;

                if (flexiblePoseCondition)
                {
                    if (algorithm3Counter >= FALL_COUNT_THRES)
                    {
                        fallDetectedFlexible = true;
                    }
                }

                // Determine fall status for each algorithm
                bool fallDetectedBboxOnly = counterBboxOnly >= FALL_COUNT_THRES;
                bool fallDetectedMotionPoseAnd = counterMotionPoseAnd >= FALL_COUNT_THRES;

                return new FallDetectionResult
                {
                    FallDetectedMethod1 = fallDetectedBboxOnly,
                    FallDetectedMethod2 = fallDetectedMotionPoseAnd,
                    FallDetectedMethod3 = fallDetectedFlexible,
                    CounterMethod1 = counterBboxOnly,
                    CounterMethod2 = counterMotionPoseAnd,
                    CounterMethod3 = algorithm3Counter,
                    Algorithm3Counter = algorithm3Counter,
                    PrimaryAlert = fallDetectedFlexible
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fall detection error: {ex.Message}");
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