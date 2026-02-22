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

        [HttpPost("compute-intermediate")]
        public IActionResult ComputeIntermediate([FromBody] EncryptedPoseFeatures request)
        {
            try
            {
                var result = _poseService.ComputeIntermediateResults(request);
                return Ok(new
                {
                    status = "success",
                    intermediate_results = result
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

        [HttpPost("evaluate-polynomial")]
        public IActionResult EvaluatePolynomial([FromBody] EncryptedComparisonResults request)
        {
            try
            {
                var result = _poseService.EvaluatePolynomial(request);
                return Ok(new
                {
                    status = "success",
                    evaluation_result = result
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