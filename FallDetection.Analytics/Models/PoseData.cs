namespace FallDetection.Analytics.Models
{
    // ------------------------------------------------------------
    // Step 2 & 3: Encrypted Features (Caregiver → Analytics)
    // ------------------------------------------------------------
    public class EncryptedPoseFeatures
    {
        public required string[] Tra { get; set; }
        public required string[] Tha { get; set; }
        public required string[] Thl { get; set; }
        public required string[] Cl { get; set; }
        public required string[] Trl { get; set; }
        public required string[] Ll { get; set; }
    }

    public class EncryptedIntermediateResults
    {
        public required string[] T30 { get; set; }
        public required string[] T40 { get; set; }
        public required string[] T80 { get; set; }
        public required string[] T60 { get; set; }
        public required string[] TC { get; set; }
        public required string[] TL { get; set; }
    }

    // ------------------------------------------------------------
    // Step 4 & 5: Encrypted Comparisons (Caregiver → Analytics)
    // ------------------------------------------------------------
    public class EncryptedComparisonResults
    {
        public required string[] CompA { get; set; } // Length 6: p1 residues for c11..c61
        public required string[] CompB { get; set; } // Length 6: q1 residues for c21..c62
        public required string[] CompC { get; set; } // Length 6: r residues for c31..c63
        public required string[] CompD { get; set; } // Length 6: s residues (TC)
        public required string[] CompE { get; set; } // Length 6: t residues (TL)
        public required string[] CompF { get; set; } // Length 6: w residues (T60)
    }

    public class EvaluationResult
    {
        public required string[] PolynomialResults { get; set; }
    }

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
}
