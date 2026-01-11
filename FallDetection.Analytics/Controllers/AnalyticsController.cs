using Microsoft.AspNetCore.Mvc;
using FallDetection.Analytics.Models;
using FallDetection.Analytics.Services;

namespace FallDetection.Analytics.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly PoseEstimationService _poseService;
        private readonly FallDetectionService _fallService;

        public AnalyticsController(PoseEstimationService poseService, FallDetectionService fallService)
        {
            _poseService = poseService;
            _fallService = fallService;
        }

        [HttpPost("analyze-pose")]
        public IActionResult AnalyzePose([FromBody] PoseAnalysisRequest request)
        {
            try
            {
                var poseData = _poseService.AnalyzePose(request.Keypoints, request.UseHme);
                return Ok(new
                {
                    status = "success",
                    pose_data = poseData,
                    camera_id = request.CameraId,
                    track_id = request.TrackId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "error",
                    message = ex.Message
                });
            }
        }

        [HttpPost("detect-fall")]
        public IActionResult DetectFall([FromBody] FallDetectionRequest request)
        {
            try
            {
                var result = _fallService.DetectFall(request);
                return Ok(new
                {
                    status = "success",
                    fall_detection = result,
                    camera_id = request.CameraId,
                    track_id = request.TrackId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "error",
                    message = ex.Message
                });
            }
        }

        [HttpPost("hme-comparisons")]
        public IActionResult PerformHmeComparisons([FromBody] HmeComparisonRequest request)
        {
            try
            {
                var comparisonResults = _poseService.PerformHmeComparisons(request.EncryptedFeatures);
                var poseLabel = _poseService.DecryptComparisonResults(comparisonResults);

                return Ok(new HmeComparisonResult
                {
                    ComparisonResults = comparisonResults,
                    PoseLabel = poseLabel
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "error",
                    message = ex.Message
                });
            }
        }

        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                service = "Fall Detection Analytics Server"
            });
        }
    }
}