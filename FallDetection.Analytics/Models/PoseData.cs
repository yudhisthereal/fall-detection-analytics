namespace FallDetection.Analytics.Models
{
    public class PoseAnalysisRequest
    {
        public List<float> Keypoints { get; set; } = new();
        public List<float> Bbox { get; set; } = new();
        public int TrackId { get; set; }
        public string CameraId { get; set; } = string.Empty;
        public bool UseHme { get; set; }
    }

    public class PoseData
    {
        public string? Label { get; set; }
        public double TorsoAngle { get; set; }
        public double ThighUprightness { get; set; }
        public double ThighCalfRatio { get; set; }
        public double TorsoLegRatio { get; set; }
        public double ThighAngle { get; set; }
        public double ThighLength { get; set; }
        public double CalfLength { get; set; }
        public double TorsoHeight { get; set; }
        public double LegLength { get; set; }
        public Dictionary<string, List<long>>? EncryptedFeatures { get; set; }
        public Dictionary<string, int>? RawIntValues { get; set; }
        public Dictionary<string, object>? ComparisonFlags { get; set; }
        public int? PoseCode { get; set; }
        public bool ServerAnalysis { get; set; }
    }

    public class FallDetectionRequest
    {
        public string CameraId { get; set; } = string.Empty;
        public int TrackId { get; set; }
        public PoseData? PoseData { get; set; }
        public List<float> CurrentBbox { get; set; } = new();
        public List<float> PreviousBbox { get; set; } = new();
        public List<float> PreviousKeypoints { get; set; } = new();
        public double ElapsedMs { get; set; }
        public bool UseHme { get; set; }
    }

    public class FallDetectionResult
    {
        public bool FallDetectedMethod1 { get; set; }
        public bool FallDetectedMethod2 { get; set; }
        public bool FallDetectedMethod3 { get; set; }
        public int CounterMethod1 { get; set; }
        public int CounterMethod2 { get; set; }
        public int CounterMethod3 { get; set; }
        public int Algorithm3Counter { get; set; }
        public bool PrimaryAlert { get; set; }
    }

    public class HmeComparisonRequest
    {
        public Dictionary<string, List<long>> EncryptedFeatures { get; set; } = new();
    }

    public class HmeComparisonResult
    {
        public Dictionary<string, List<long>> ComparisonResults { get; set; } = new();
        public string? PoseLabel { get; set; }
    }
}