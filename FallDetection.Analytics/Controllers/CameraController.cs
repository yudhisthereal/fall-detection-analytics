using Microsoft.AspNetCore.Mvc;
using FallDetection.Analytics.Services;
using FallDetection.Analytics.Models;

namespace FallDetection.Analytics.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CameraController : ControllerBase
    {
        private readonly CameraRegistryService _registryService;

        public CameraController(CameraRegistryService registryService)
        {
            _registryService = registryService;
        }

        [HttpPost("register")]
        public IActionResult RegisterCamera([FromQuery] string? camera_id)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrEmpty(ipAddress))
            {
                return BadRequest(new { error = "Could not determine IP address" });
            }

            var result = _registryService.RegisterCamera(ipAddress, camera_id);
            return Ok(result);
        }

        [HttpPost("approve")]
        public IActionResult ApproveCamera([FromBody] ApproveCameraRequest request)
        {
            var result = _registryService.ApproveCameraRegistration(request.IpAddress, request.CameraName);
            return Ok(result);
        }

        [HttpPost("forget")]
        public IActionResult ForgetCamera([FromBody] ForgetCameraRequest request)
        {
            var result = _registryService.ForgetCamera(request.CameraId);
            return Ok(result);
        }

        [HttpGet("pending")]
        public IActionResult GetPendingRegistrations()
        {
            var result = _registryService.GetPendingRegistrations();
            return Ok(result);
        }

        [HttpGet("registered")]
        public IActionResult GetRegisteredCameras()
        {
            var result = _registryService.GetRegisteredCameras();
            return Ok(result);
        }
    }
}