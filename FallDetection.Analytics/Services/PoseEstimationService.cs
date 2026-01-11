using System.Numerics;
using FallDetection.Analytics.Models;
using System.Collections.Concurrent;

namespace FallDetection.Analytics.Services
{
    public class PoseEstimationService
    {
        // === HME PARAMETERS (exact same as Python) ===
        private readonly BigInteger p1 = BigInteger.Parse("234406548094233827948571379965547188853");
        private readonly BigInteger q1 = BigInteger.Parse("583457592311129510314141861330330044443");
        private readonly BigInteger r = BigInteger.Parse("696522972436164062959242838052087531431");
        private readonly BigInteger s = BigInteger.Parse("374670603170509799404699393785831797599");
        private readonly BigInteger t = BigInteger.Parse("443137959904584298054176676987615849169");
        private readonly BigInteger w = BigInteger.Parse("391475886865055383118586393345880578361");
        private readonly BigInteger u = BigInteger.Parse("2355788435550222327802749264573303139783");

        private readonly BigInteger n1;
        private readonly BigInteger n11;

        // Modular inverses
        private readonly BigInteger pinvq = BigInteger.Parse("499967064455987294076532081570894386372");
        private readonly BigInteger qinvp = BigInteger.Parse("33542671637141449679641257954160235148");

        private readonly int gu;
        private readonly BigInteger u1;

        // Partial products
        private readonly BigInteger np1prod;
        private readonly BigInteger nq1prod;
        private readonly BigInteger nrprod;
        private readonly BigInteger nsprod;
        private readonly BigInteger ntprod;
        private readonly BigInteger nwprod;

        // Inverses
        private readonly BigInteger invnp1 = BigInteger.Parse("205139046479782337030801215788009754117");
        private readonly BigInteger invnq1 = BigInteger.Parse("429235397156384978572995593851807405098");
        private readonly BigInteger invnr = BigInteger.Parse("592155359269217457562309991915739180471");
        private readonly BigInteger invns = BigInteger.Parse("115186784058467557094932562011798848762");
        private readonly BigInteger invnt = BigInteger.Parse("51850665316568177665825586294193267244");
        private readonly BigInteger invnw = BigInteger.Parse("44855536902472009823152313099539628632");

        // Thresholds for plain mode
        private readonly double thighCalfRatioThreshold = 0.7;
        private readonly double torsoLegRatioThreshold = 0.5;

        // Track history for fall detection (same as Python analytics.py)
        private readonly ConcurrentDictionary<string, Dictionary<int, TrackHistory>> _cameraTrackHistory = new();
        
        // Queue size for pose averaging
        private const int QUEUE_SIZE = 5;
        private const int FPS = 30;

        private readonly Random random = new();

        public PoseEstimationService()
        {
            // Calculate derived values
            n1 = p1 * q1 * r * s * t * w;
            n11 = p1 * q1;

            np1prod = q1 * r * s * t * w;
            nq1prod = p1 * r * s * t * w;
            nrprod = p1 * q1 * s * t * w;
            nsprod = p1 * q1 * r * t * w;
            ntprod = p1 * q1 * r * s * w;
            nwprod = p1 * q1 * r * s * t;

            gu = (int)Math.Floor(u.GetBitLength() / 2.0);
            u1 = u / 2;
        }

        private class TrackHistory
        {
            public List<int> Id { get; set; } = new();
            public List<Queue<List<float>>> Bbox { get; set; } = new();
            public List<Queue<List<float>>> Points { get; set; } = new();
        }

        // ==================== Original Python Functions Preserved ====================

        private long Truncate(double num)
        {
            long factor = 100;
            return (long)Math.Truncate(num * factor);
        }

        public List<long> EncryptValue(long m)
        {
            long g = random.NextInt64(1, (long)Math.Pow(2, 32) - 1);
            
            return new List<long>
            {
                (long)(((g * u) + m) % p1),
                (long)(((g * u) + m) % q1),
                (long)(((g * u) + m) % r),
                (long)(((g * u) + m) % s),
                (long)(((g * u) + m) % t),
                (long)(((g * u) + m) % w)
            };
        }

        public List<long> EncryptSimple(long m)
        {
            long g = random.NextInt64(1, (long)Math.Pow(2, 32) - 1);
            
            return new List<long>
            {
                (long)(((g * u) + m) % p1),
                (long)(((g * u) + m) % q1)
            };
        }

        public long DecryptValue(List<long> cValues)
        {
            if (cValues.Count != 6) return 0;

            var c1 = new BigInteger(cValues[0]);
            var c2 = new BigInteger(cValues[1]);
            var c3 = new BigInteger(cValues[2]);
            var c4 = new BigInteger(cValues[3]);
            var c5 = new BigInteger(cValues[4]);
            var c6 = new BigInteger(cValues[5]);

            var mout = (((c1 % p1) * invnp1 * np1prod +
                        (c2 % q1) * invnq1 * nq1prod +
                        (c3 % r) * invnr * nrprod +
                        (c4 % s) * invns * nsprod +
                        (c5 % t) * invnt * ntprod +
                        (c6 % w) * invnw * nwprod) % n1);

            if (mout > n1 / 2)
                mout = mout - n1;
            
            mout = mout % u;
            return (long)mout;
        }

        private int DecryptSimpleComparison(List<long> cValues)
        {
            if (cValues.Count != 2) return 0;

            var c11 = new BigInteger(cValues[0]);
            var c12 = new BigInteger(cValues[1]);

            var mout = (((c11 % p1) * qinvp * q1) + ((c12 % q1) * pinvq * p1)) % n11;

            if (mout > n11 / 2)
                mout = mout - n11;

            mout = mout % u;
            var bitLength = (int)mout.GetBitLength();

            if (gu > bitLength) return 0;      // m < threshold
            else if (gu < bitLength) return 1; // m > threshold
            else return -1;                    // m == threshold
        }

        private (double, double, double, double, double, double) CalculateLimbLengths(
            Dictionary<string, (double x, double y)> keypoints)
        {
            try
            {
                // Calculate thigh length (hip to knee)
                var leftThigh = Math.Sqrt(
                    Math.Pow(keypoints["Left Hip"].x - keypoints["Left Knee"].x, 2) +
                    Math.Pow(keypoints["Left Hip"].y - keypoints["Left Knee"].y, 2));
                var rightThigh = Math.Sqrt(
                    Math.Pow(keypoints["Right Hip"].x - keypoints["Right Knee"].x, 2) +
                    Math.Pow(keypoints["Right Hip"].y - keypoints["Right Knee"].y, 2));
                var thighLength = (leftThigh + rightThigh) / 2.0;

                // Calculate calf length (knee to ankle)
                var leftCalf = Math.Sqrt(
                    Math.Pow(keypoints["Left Knee"].x - keypoints["Left Ankle"].x, 2) +
                    Math.Pow(keypoints["Left Knee"].y - keypoints["Left Ankle"].y, 2));
                var rightCalf = Math.Sqrt(
                    Math.Pow(keypoints["Right Knee"].x - keypoints["Right Ankle"].x, 2) +
                    Math.Pow(keypoints["Right Knee"].y - keypoints["Right Ankle"].y, 2));
                var calfLength = (leftCalf + rightCalf) / 2.0;

                // Calculate torso height (shoulder to hip)
                var leftTorso = Math.Sqrt(
                    Math.Pow(keypoints["Left Shoulder"].x - keypoints["Left Hip"].x, 2) +
                    Math.Pow(keypoints["Left Shoulder"].y - keypoints["Left Hip"].y, 2));
                var rightTorso = Math.Sqrt(
                    Math.Pow(keypoints["Right Shoulder"].x - keypoints["Right Hip"].x, 2) +
                    Math.Pow(keypoints["Right Shoulder"].y - keypoints["Right Hip"].y, 2));
                var torsoHeight = (leftTorso + rightTorso) / 2.0;

                // Calculate leg length (hip to ankle)
                var leftLeg = Math.Sqrt(
                    Math.Pow(keypoints["Left Hip"].x - keypoints["Left Ankle"].x, 2) +
                    Math.Pow(keypoints["Left Hip"].y - keypoints["Left Ankle"].y, 2));
                var rightLeg = Math.Sqrt(
                    Math.Pow(keypoints["Right Hip"].x - keypoints["Right Ankle"].x, 2) +
                    Math.Pow(keypoints["Right Hip"].y - keypoints["Right Ankle"].y, 2));
                var legLength = (leftLeg + rightLeg) / 2.0;

                // Calculate ratios
                var thighCalfRatio = calfLength > 0 ? thighLength / calfLength : 1.0;
                var torsoLegRatio = legLength > 0 ? torsoHeight / legLength : 1.0;

                return (thighCalfRatio, torsoLegRatio, thighLength, calfLength, torsoHeight, legLength);
            }
            catch
            {
                return (1.0, 1.0, 0.0, 0.0, 0.0, 0.0);
            }
        }

        private bool IsFrameComplete(Dictionary<string, (double x, double y)> keypoints)
        {
            foreach (var kvp in keypoints)
            {
                if (kvp.Value.x == -1 || kvp.Value.y == -1)
                    return false;
            }
            return true;
        }

        // Main pose analysis function - PRESERVING ALL ORIGINAL LOGIC
        public PoseData AnalyzePose(List<float> keypointsFlat, bool useHme = false)
        {
            try
            {
                // Reshape to 17x2 (same as Python)
                if (keypointsFlat.Count != 34) 
                    return new PoseData { Label = "None" };

                var keypoints = new Dictionary<string, (double x, double y)>
                {
                    ["Left Shoulder"] = (keypointsFlat[10], keypointsFlat[11]),
                    ["Right Shoulder"] = (keypointsFlat[12], keypointsFlat[13]),
                    ["Left Hip"] = (keypointsFlat[22], keypointsFlat[23]),
                    ["Right Hip"] = (keypointsFlat[24], keypointsFlat[25]),
                    ["Left Knee"] = (keypointsFlat[26], keypointsFlat[27]),
                    ["Right Knee"] = (keypointsFlat[28], keypointsFlat[29]),
                    ["Left Ankle"] = (keypointsFlat[30], keypointsFlat[31]),
                    ["Right Ankle"] = (keypointsFlat[32], keypointsFlat[33])
                };

                // Check if frame is complete
                if (!IsFrameComplete(keypoints))
                    return new PoseData { Label = "None" };

                // Compute centers (same as Python)
                var shoulderCenter = (
                    (keypoints["Left Shoulder"].x + keypoints["Right Shoulder"].x) / 2.0,
                    (keypoints["Left Shoulder"].y + keypoints["Right Shoulder"].y) / 2.0);
                var hipCenter = (
                    (keypoints["Left Hip"].x + keypoints["Right Hip"].x) / 2.0,
                    (keypoints["Left Hip"].y + keypoints["Right Hip"].y) / 2.0);
                var kneeCenter = (
                    (keypoints["Left Knee"].x + keypoints["Right Knee"].x) / 2.0,
                    (keypoints["Left Knee"].y + keypoints["Right Knee"].y) / 2.0);

                // Compute vectors
                var torsoVec = (shoulderCenter.Item1 - hipCenter.Item1, shoulderCenter.Item2 - hipCenter.Item2);
                var thighVec = (kneeCenter.Item1 - hipCenter.Item1, kneeCenter.Item2 - hipCenter.Item2);
                var upVector = (0.0, -1.0);

                // Compute angles (same as Python)
                var torsoNorm = Math.Sqrt(torsoVec.Item1 * torsoVec.Item1 + torsoVec.Item2 * torsoVec.Item2);
                var thighNorm = Math.Sqrt(thighVec.Item1 * thighVec.Item1 + thighVec.Item2 * thighVec.Item2);
                
                if (torsoNorm == 0 || thighNorm == 0) 
                    return new PoseData { Label = "None" };

                var torsoAngle = Math.Acos(Math.Clamp(
                    (torsoVec.Item1 * upVector.Item1 + torsoVec.Item2 * upVector.Item2) / 
                    (torsoNorm * Math.Sqrt(upVector.Item1 * upVector.Item1 + upVector.Item2 * upVector.Item2)), -1.0, 1.0)) * 180.0 / Math.PI;

                var thighAngle = Math.Acos(Math.Clamp(
                    (thighVec.Item1 * upVector.Item1 + thighVec.Item2 * upVector.Item2) / 
                    (thighNorm * Math.Sqrt(upVector.Item1 * upVector.Item1 + upVector.Item2 * upVector.Item2)), -1.0, 1.0)) * 180.0 / Math.PI;

                var thighUprightness = Math.Abs(thighAngle - 180.0);

                // Calculate limb lengths
                var (thighCalfRatio, torsoLegRatio, thighLength, calfLength, torsoHeight, legLength) = 
                    CalculateLimbLengths(keypoints);

                if (useHme)
                {
                    // Convert to integers for encryption (same as Python)
                    var Thl = Truncate(thighLength);
                    var cl = Truncate(calfLength);
                    var Trl = Truncate(torsoHeight);
                    var ll = Truncate(legLength);
                    var Tra = Truncate(torsoAngle);
                    var Tha = Truncate(thighUprightness);

                    // Encrypt features (same as Python)
                    var encryptedFeatures = new Dictionary<string, List<long>>
                    {
                        ["Tra"] = EncryptSimple(Tra),
                        ["Tha"] = EncryptSimple(Tha),
                        ["Thl"] = EncryptSimple(Thl),
                        ["cl"] = EncryptSimple(cl),
                        ["Trl"] = EncryptSimple(Trl),
                        ["ll"] = EncryptSimple(ll)
                    };

                    var rawIntValues = new Dictionary<string, int>
                    {
                        ["Tra"] = (int)Tra,
                        ["Tha"] = (int)Tha,
                        ["Thl"] = (int)Thl,
                        ["cl"] = (int)cl,
                        ["Trl"] = (int)Trl,
                        ["ll"] = (int)ll
                    };

                    return new PoseData
                    {
                        Label = null,  // Will be determined after HME processing
                        TorsoAngle = torsoAngle,
                        ThighUprightness = thighUprightness,
                        ThighLength = thighLength,
                        CalfLength = calfLength,
                        TorsoHeight = torsoHeight,
                        LegLength = legLength,
                        EncryptedFeatures = encryptedFeatures,
                        RawIntValues = rawIntValues
                    };
                }
                else
                {
                    // Plain mode classification (SAME AS PYTHON)
                    string label;
                    if (torsoAngle < 30 && thighUprightness < 40)
                    {
                        if (thighCalfRatio < thighCalfRatioThreshold)
                            label = "sitting";
                        else if (torsoLegRatio < torsoLegRatioThreshold)
                            label = "bending_down";
                        else
                            label = "standing";
                    }
                    else if (torsoAngle < 30 && thighUprightness >= 40)
                    {
                        label = "sitting";
                    }
                    else if (30 <= torsoAngle && torsoAngle < 80 && thighUprightness < 60)
                    {
                        label = "bending_down";
                    }
                    else
                    {
                        label = "lying_down";
                    }

                    return new PoseData
                    {
                        Label = label,
                        TorsoAngle = torsoAngle,
                        ThighUprightness = thighUprightness,
                        ThighCalfRatio = thighCalfRatio,
                        TorsoLegRatio = torsoLegRatio,
                        ThighAngle = thighAngle,
                        ThighLength = thighLength,
                        CalfLength = calfLength,
                        TorsoHeight = torsoHeight,
                        LegLength = legLength
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Pose analysis error: {ex.Message}");
                return new PoseData { Label = "None" };
            }
        }

        // ==================== TRACK HISTORY MANAGEMENT ====================
        
        public void InitializeTrackHistory(string cameraId, int trackId)
        {
            var cameraHistory = _cameraTrackHistory.GetOrAdd(cameraId, _ => new Dictionary<int, TrackHistory>());
            
            if (!cameraHistory.ContainsKey(trackId))
            {
                cameraHistory[trackId] = new TrackHistory
                {
                    Id = new List<int> { trackId },
                    Bbox = new List<Queue<List<float>>> { new Queue<List<float>>(QUEUE_SIZE) },
                    Points = new List<Queue<List<float>>> { new Queue<List<float>>(QUEUE_SIZE) }
                };
            }
        }

        public void AddTrackData(string cameraId, int trackId, List<float> bbox, List<float> keypoints)
        {
            if (!_cameraTrackHistory.ContainsKey(cameraId))
                InitializeTrackHistory(cameraId, trackId);

            var cameraHistory = _cameraTrackHistory[cameraId];
            if (!cameraHistory.ContainsKey(trackId))
                InitializeTrackHistory(cameraId, trackId);

            var trackHistory = cameraHistory[trackId];
            var idx = trackHistory.Id.IndexOf(trackId);

            // Add current data to queue
            if (trackHistory.Bbox[idx].Count >= QUEUE_SIZE)
            {
                trackHistory.Bbox[idx].Dequeue();
                trackHistory.Points[idx].Dequeue();
            }

            trackHistory.Bbox[idx].Enqueue(bbox);
            trackHistory.Points[idx].Enqueue(keypoints);
        }

        public bool IsTrackHistoryReady(string cameraId, int trackId)
        {
            if (!_cameraTrackHistory.ContainsKey(cameraId)) return false;
            if (!_cameraTrackHistory[cameraId].ContainsKey(trackId)) return false;
            
            var trackHistory = _cameraTrackHistory[cameraId][trackId];
            var idx = trackHistory.Id.IndexOf(trackId);
            
            return trackHistory.Bbox[idx].Count == QUEUE_SIZE;
        }

        public (List<float> bbox, List<float> keypoints) GetPreviousData(string cameraId, int trackId)
        {
            if (!_cameraTrackHistory.ContainsKey(cameraId) || 
                !_cameraTrackHistory[cameraId].ContainsKey(trackId))
                return (new List<float>(), new List<float>());

            var trackHistory = _cameraTrackHistory[cameraId][trackId];
            var idx = trackHistory.Id.IndexOf(trackId);
            
            if (trackHistory.Bbox[idx].Count == 0)
                return (new List<float>(), new List<float>());

            // Get the oldest data (first in queue)
            var bboxQueue = trackHistory.Bbox[idx];
            var pointsQueue = trackHistory.Points[idx];
            
            return (bboxQueue.Peek(), pointsQueue.Peek());
        }

        // ==================== COMPLETE FALL DETECTION ====================

        public FallDetectionResult DetectFall(string cameraId, int trackId, List<float> currentBbox, 
                                              PoseData poseData, bool useHme)
        {
            try
            {
                // Check if we have enough history
                if (!IsTrackHistoryReady(cameraId, trackId))
                    return new FallDetectionResult();

                var (previousBbox, _) = GetPreviousData(cameraId, trackId);
                if (previousBbox.Count == 0 || currentBbox.Count < 4)
                    return new FallDetectionResult();

                // Calculate elapsed time (same as Python)
                double elapsedMs = QUEUE_SIZE * 1000.0 / FPS;

                // Calculate velocities (same as Python judge_fall.py)
                double dyTop = currentBbox[1] - previousBbox[1];
                double vTop = dyTop / elapsedMs;

                double dh = previousBbox[3] - currentBbox[3];
                double vHeight = dh / elapsedMs;

                Console.WriteLine($"[DEBUG] v_top = {vTop:F6}, v_height = {vHeight:F6}, threshold = 0.43");

                // Get pose data
                double torsoAngle = 0;
                double thighUprightness = 0;
                string? label = null;

                if (useHme)
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
                        
                        Console.WriteLine($"[DEBUG HME] Approx torso_angle={torsoAngle}, thighUprightness={thighUprightness}, label={label}");
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
                        Console.WriteLine($"[DEBUG POSE] torso_angle={torsoAngle}, thighUprightness={thighUprightness}");
                    }
                }

                // Calculate bbox motion evidence
                double vBboxY = 0.43;
                bool bboxMotionDetected = (vTop > vBboxY || vHeight > vBboxY);

                // Calculate pose conditions (SAME AS PYTHON)
                bool strictPoseCondition = false;
                bool flexiblePoseCondition = false;

                if (useHme)
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
                    // Plain mode (SAME AS judge_fall.py)
                    if (torsoAngle > 0 && thighUprightness > 0)
                    {
                        // Strict condition: clearly lying down
                        strictPoseCondition = (torsoAngle > 80 && thighUprightness > 60);
                        
                        // Flexible condition: various falling/lying positions
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

                // ========== ALGORITHM 1: BBox Only ==========
                int counterBboxOnly = GetCounter(cameraId, trackId, "bbox_only");
                
                if (bboxMotionDetected)
                {
                    counterBboxOnly = Math.Min(FALL_COUNT_THRES, counterBboxOnly + 1);
                }
                else
                {
                    counterBboxOnly = Math.Max(0, counterBboxOnly - 1);
                }
                
                UpdateCounter(cameraId, trackId, "bbox_only", counterBboxOnly);

                // ========== ALGORITHM 2: BBox Motion AND Strict Pose ==========
                int counterMotionPoseAnd = GetCounter(cameraId, trackId, "motion_pose_and");
                
                if (bboxMotionDetected && strictPoseCondition)
                {
                    // Strong evidence: both motion AND clearly lying down
                    counterMotionPoseAnd = Math.Min(FALL_COUNT_THRES, counterMotionPoseAnd + 2);
                }
                else if (bboxMotionDetected || strictPoseCondition)
                {
                    // Moderate evidence: one or the other
                    counterMotionPoseAnd = Math.Min(FALL_COUNT_THRES, counterMotionPoseAnd + 1);
                }
                else
                {
                    // No evidence
                    counterMotionPoseAnd = Math.Max(0, counterMotionPoseAnd - 1);
                }
                
                UpdateCounter(cameraId, trackId, "motion_pose_and", counterMotionPoseAnd);

                // ========== ALGORITHM 3: Flexible Verification ==========
                int algorithm3Counter = Math.Max(counterBboxOnly, counterMotionPoseAnd);
                bool fallDetectedFlexible = false;

                if (flexiblePoseCondition)
                {
                    if (algorithm3Counter >= FALL_COUNT_THRES)
                    {
                        fallDetectedFlexible = true;
                        Console.WriteLine($"[FLEXIBLE VERIFICATION] Fall detected using flexible verification");
                    }
                }

                // Determine fall status for each algorithm
                bool fallDetectedBboxOnly = counterBboxOnly >= FALL_COUNT_THRES;
                bool fallDetectedMotionPoseAnd = counterMotionPoseAnd >= FALL_COUNT_THRES;

                if (fallDetectedBboxOnly)
                    Console.WriteLine($"[ALGORITHM 1] Fall detected (BBox only, counter={counterBboxOnly})");
                
                if (fallDetectedMotionPoseAnd)
                    Console.WriteLine($"[ALGORITHM 2] Fall detected (Motion+Pose AND, counter={counterMotionPoseAnd})");

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

        // ==================== COUNTER MANAGEMENT ====================

        private const int FALL_COUNT_THRES = 2;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentDictionary<string, int>>> _counters = new();

        private int GetCounter(string cameraId, int trackId, string counterType)
        {
            var cameraCounters = _counters.GetOrAdd(cameraId, _ => new ConcurrentDictionary<int, ConcurrentDictionary<string, int>>());
            var trackCounters = cameraCounters.GetOrAdd(trackId, _ => new ConcurrentDictionary<string, int>());
            return trackCounters.GetValueOrDefault(counterType, 0);
        }

        private void UpdateCounter(string cameraId, int trackId, string counterType, int value)
        {
            var cameraCounters = _counters.GetOrAdd(cameraId, _ => new ConcurrentDictionary<int, ConcurrentDictionary<string, int>>());
            var trackCounters = cameraCounters.GetOrAdd(trackId, _ => new ConcurrentDictionary<string, int>());
            trackCounters[counterType] = value;
        }

        public void ResetCounters(string cameraId, int trackId)
        {
            if (_counters.ContainsKey(cameraId) && _counters[cameraId].ContainsKey(trackId))
            {
                var trackCounters = _counters[cameraId][trackId];
                trackCounters["bbox_only"] = Math.Max(0, trackCounters.GetValueOrDefault("bbox_only", 0) - 1);
                trackCounters["motion_pose_and"] = Math.Max(0, trackCounters.GetValueOrDefault("motion_pose_and", 0) - 1);
            }
        }

        // ==================== HME COMPARISONS ====================

        public Dictionary<string, List<long>> PerformHmeComparisons(Dictionary<string, List<long>> encryptedFeatures)
        {
            try
            {
                var Tra = encryptedFeatures.GetValueOrDefault("Tra", new List<long> { 0, 0 });
                var Tha = encryptedFeatures.GetValueOrDefault("Tha", new List<long> { 0, 0 });
                var Thl = encryptedFeatures.GetValueOrDefault("Thl", new List<long> { 0, 0 });
                var cl = encryptedFeatures.GetValueOrDefault("cl", new List<long> { 0, 0 });
                var Trl = encryptedFeatures.GetValueOrDefault("Trl", new List<long> { 0, 0 });
                var ll = encryptedFeatures.GetValueOrDefault("ll", new List<long> { 0, 0 });

                // Threshold values (multiplied by 100 for integer comparison)
                long threshold_30 = 3000;
                long threshold_40 = 4000;
                long threshold_60 = 6000;
                long threshold_80 = 8000;

                // Generate random values (same as Python)
                long r1 = random.NextInt64(1, (long)Math.Pow(2, 22) - 1);
                long r2 = random.NextInt64(1, (long)Math.Pow(2, 10) - 1);

                // Perform comparisons (Algorithm 1: compare with plain threshold)
                var T301 = (r2 + (r1 * 2 * (Tra[0] - threshold_30))) % (long)p1;
                var T302 = (r2 + (r1 * 2 * (Tra[1] - threshold_30))) % (long)q1;
                
                var T401 = (r2 + (r1 * 2 * (Tha[0] - threshold_40))) % (long)p1;
                var T402 = (r2 + (r1 * 2 * (Tha[1] - threshold_40))) % (long)q1;
                
                var T801 = (r2 + (r1 * 2 * (Tra[0] - threshold_80))) % (long)p1;
                var T802 = (r2 + (r1 * 2 * (Tra[1] - threshold_80))) % (long)q1;
                
                var T601 = (r2 + (r1 * 2 * (Tha[0] - threshold_60))) % (long)p1;
                var T602 = (r2 + (r1 * 2 * (Tha[1] - threshold_60))) % (long)q1;

                // Algorithm 2: Compare two encrypted values
                // Compare thigh_length * 10 vs calf_length * 7
                var TC1 = (r2 + (r1 * 2 * (Thl[0] * 10 - cl[0] * 7))) % (long)p1;
                var TC2 = (r2 + (r1 * 2 * (Thl[1] * 10 - cl[1] * 7))) % (long)q1;
                
                // Compare torso_height * 10 vs leg_length * 5
                var TL1 = (r2 + (r1 * 2 * (Trl[0] * 10 - ll[0] * 5))) % (long)p1;
                var TL2 = (r2 + (r1 * 2 * (Trl[1] * 10 - ll[1] * 5))) % (long)q1;

                return new Dictionary<string, List<long>>
                {
                    ["T30"] = new List<long> { T301, T302 },
                    ["T40"] = new List<long> { T401, T402 },
                    ["T80"] = new List<long> { T801, T802 },
                    ["T60"] = new List<long> { T601, T602 },
                    ["TC"] = new List<long> { TC1, TC2 },
                    ["TL"] = new List<long> { TL1, TL2 }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HME comparison error: {ex.Message}");
                return new Dictionary<string, List<long>>();
            }
        }

        public string DecryptComparisonResults(Dictionary<string, List<long>> comparisonResults)
        {
            try
            {
                var T30 = DecryptSimpleComparison(comparisonResults.GetValueOrDefault("T30", new List<long> { 0, 0 }));
                var T40 = DecryptSimpleComparison(comparisonResults.GetValueOrDefault("T40", new List<long> { 0, 0 }));
                var T80 = DecryptSimpleComparison(comparisonResults.GetValueOrDefault("T80", new List<long> { 0, 0 }));
                var T60 = DecryptSimpleComparison(comparisonResults.GetValueOrDefault("T60", new List<long> { 0, 0 }));
                var TC = DecryptSimpleComparison(comparisonResults.GetValueOrDefault("TC", new List<long> { 0, 0 }));
                var TL = DecryptSimpleComparison(comparisonResults.GetValueOrDefault("TL", new List<long> { 0, 0 }));

                // Convert comparison results to boolean (0: False, 1: True)
                int a = T30 == 1 ? 1 : 0;  // torso_angle > 30
                int b = T40 == 1 ? 1 : 0;  // thigh_uprightness > 40
                int c = T80 == 1 ? 1 : 0;  // torso_angle > 80
                int d = TC == 1 ? 1 : 0;   // thigh_length*10 > calf_length*7
                int e = TL == 1 ? 1 : 0;   // torso_height*10 > leg_length*5
                int f = T60 == 1 ? 1 : 0;  // thigh_uprightness > 60

                // Determine pose using the polynomial logic from pose_estimation_enc_gsplit.py
                // LSB calculation
                int lsb = (a & b & d) | (a & ~b) | (~a & ~c & ~f);
                
                // MSB calculation
                int msb = (a & b & ~d & e) | ~a;
                
                // Combine MSB and LSB
                int poseCode = (msb << 1) | lsb;
                
                // Map pose code to label
                var poseMap = new Dictionary<int, string>
                {
                    [0] = "standing",
                    [1] = "sitting",
                    [2] = "bending_down",
                    [3] = "lying_down"
                };
                
                return poseMap.GetValueOrDefault(poseCode, "unknown");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HME decryption error: {ex.Message}");
                return "unknown";
            }
        }

        // ==================== COMPLETE POSE ANALYSIS WITH FALL DETECTION ====================

        public CompleteAnalysisResult AnalyzePoseWithFallDetection(string cameraId, int trackId, 
            List<float> keypoints, List<float> bbox, bool useHme)
        {
            try
            {
                // 1. Analyze pose
                var poseData = AnalyzePose(keypoints, useHme);
                
                if (poseData.Label == "None")
                {
                    ResetCounters(cameraId, trackId);
                    return new CompleteAnalysisResult
                    {
                        PoseData = poseData,
                        FallResult = new FallDetectionResult()
                    };
                }

                // 2. Add to track history
                AddTrackData(cameraId, trackId, bbox, keypoints);

                // 3. Detect fall if enough history
                FallDetectionResult fallResult = new();
                
                if (IsTrackHistoryReady(cameraId, trackId))
                {
                    fallResult = DetectFall(cameraId, trackId, bbox, poseData, useHme);
                    
                    // If using HME and we have encrypted features
                    if (useHme && poseData.EncryptedFeatures != null && poseData.EncryptedFeatures.Count > 0)
                    {
                        var comparisonResults = PerformHmeComparisons(poseData.EncryptedFeatures);
                        var hmeLabel = DecryptComparisonResults(comparisonResults);
                        
                        poseData.Label = hmeLabel;
                        poseData.ComparisonFlags = new Dictionary<string, object>
                        {
                            ["hme_processed"] = true,
                            ["comparison_results"] = comparisonResults
                        };
                    }
                }

                return new CompleteAnalysisResult
                {
                    PoseData = poseData,
                    FallResult = fallResult
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Complete analysis error: {ex.Message}");
                return new CompleteAnalysisResult();
            }
        }
    }

    public class CompleteAnalysisResult
    {
        public PoseData PoseData { get; set; } = new();
        public FallDetectionResult FallResult { get; set; } = new();
    }
}