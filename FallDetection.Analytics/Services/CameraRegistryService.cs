using System.Text.Json;
using FallDetection.Analytics.Models;

namespace FallDetection.Analytics.Services
{
    public class CameraRegistryService
    {
        private readonly string registryFilePath;
        private readonly string pendingRegistryFilePath;
        private Dictionary<string, CameraRegistration> cameraRegistry = new();
        private Dictionary<string, PendingRegistration> pendingRegistrations = new();
        private int cameraCounter = 0;
        private readonly object registryLock = new();

        public CameraRegistryService()
        {
            var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            registryFilePath = Path.Combine(dataDir, "camera_registry.json");
            pendingRegistryFilePath = Path.Combine(dataDir, "pending_cam_registrations.json");
            LoadCameraRegistry();
            LoadPendingRegistrations();
        }

        private void LoadCameraRegistry()
        {
            try
            {
                if (File.Exists(registryFilePath))
                {
                    var json = File.ReadAllText(registryFilePath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    
                    if (data != null)
                    {
                        if (data.ContainsKey("cameras"))
                        {
                            var camerasJson = data["cameras"]?.ToString();
                            cameraRegistry = JsonSerializer.Deserialize<Dictionary<string, CameraRegistration>>(camerasJson!) ?? new();
                        }
                        
                        if (data.ContainsKey("counter"))
                        {
                            cameraCounter = Convert.ToInt32(data["counter"]);
                        }
                    }
                    Console.WriteLine($"Loaded {cameraRegistry.Count} cameras from registry");
                }
                else
                {
                    cameraRegistry = new();
                    cameraCounter = 0;
                    Console.WriteLine("No camera registry found, starting fresh");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading camera registry: {ex.Message}");
                cameraRegistry = new();
                cameraCounter = 0;
            }
        }

        private void LoadPendingRegistrations()
        {
            try
            {
                if (File.Exists(pendingRegistryFilePath))
                {
                    var json = File.ReadAllText(pendingRegistryFilePath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    
                    if (data != null)
                    {
                        if (data.ContainsKey("pending_registrations"))
                        {
                            var pendingJson = data["pending_registrations"]?.ToString();
                            pendingRegistrations = JsonSerializer.Deserialize<Dictionary<string, PendingRegistration>>(pendingJson!) ?? new();
                        }
                        
                        if (data.ContainsKey("counter"))
                        {
                            cameraCounter = Math.Max(cameraCounter, Convert.ToInt32(data["counter"]));
                        }
                    }
                    Console.WriteLine($"Loaded {pendingRegistrations.Count} pending registrations");
                }
                else
                {
                    pendingRegistrations = new();
                    Console.WriteLine("No pending registrations file found, starting fresh");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading pending registrations: {ex.Message}");
                pendingRegistrations = new();
            }
        }

        private void SavePendingRegistrations()
        {
            try
            {
                var data = new Dictionary<string, object>
                {
                    ["pending_registrations"] = pendingRegistrations,
                    ["counter"] = cameraCounter,
                    ["last_updated"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(pendingRegistryFilePath, json);
                Console.WriteLine($"Saved pending registrations with {pendingRegistrations.Count} entries");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving pending registrations: {ex.Message}");
            }
        }

        private void SaveCameraRegistry()
        {
            try
            {
                var data = new Dictionary<string, object>
                {
                    ["cameras"] = cameraRegistry,
                    ["counter"] = cameraCounter,
                    ["last_updated"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(registryFilePath, json);
                Console.WriteLine($"Saved camera registry with {cameraRegistry.Count} cameras");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving camera registry: {ex.Message}");
            }
        }

        private string GetNextCameraId()
        {
            lock (registryLock)
            {
                cameraCounter++;
                return $"camera_{cameraCounter:x4}";
            }
        }

        public object RegisterCamera(string ipAddress, string? cameraId = null)
        {
            lock (registryLock)
            {
                // Check if camera with this ID already exists
                foreach (var (camId, camData) in cameraRegistry)
                {
                    if (camData.IpAddress == ipAddress)
                    {
                        return new
                        {
                            camera_id = camId,
                            camera_name = camData.CameraName,
                            status = "registered"
                        };
                    }
                }

                // If camera_id provided and exists, return it
                if (!string.IsNullOrEmpty(cameraId) && cameraRegistry.ContainsKey(cameraId))
                {
                    var camData = cameraRegistry[cameraId];
                    return new
                    {
                        camera_id = cameraId,
                        camera_name = camData.CameraName,
                        status = "registered"
                    };
                }

                // Generate new camera ID if not provided
                var newCameraId = cameraId ?? GetNextCameraId();

                // Store as pending registration
                pendingRegistrations[ipAddress] = new PendingRegistration
                {
                    CameraId = newCameraId,
                    IpAddress = ipAddress,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Status = "pending"
                };

                // Save pending registrations to file
                SavePendingRegistrations();

                Console.WriteLine($"New camera registration pending from {ipAddress}, camera ID: {newCameraId}");

                return new
                {
                    camera_id = newCameraId,
                    status = "pending",
                    message = "Registration pending user approval"
                };
            }
        }

        public object ApproveCameraRegistration(string ipAddress, string cameraName)
        {
            lock (registryLock)
            {
                if (!pendingRegistrations.ContainsKey(ipAddress))
                {
                    return new { error = "No pending registration for this IP" };
                }

                var pendingData = pendingRegistrations[ipAddress];
                var cameraId = pendingData.CameraId;

                // Add to registry
                cameraRegistry[cameraId] = new CameraRegistration
                {
                    CameraId = cameraId,
                    CameraName = cameraName,
                    IpAddress = ipAddress,
                    FirstSeen = pendingData.Timestamp,
                    LastSeen = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    ApprovedBy = "user",
                    ApprovedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Status = "registered"
                };

                // Remove from pending
                pendingRegistrations.Remove(ipAddress);

                // Save pending registrations and registry
                SavePendingRegistrations();
                SaveCameraRegistry();

                Console.WriteLine($"Camera registered: {cameraId} ({cameraName}) at {ipAddress}");

                return new
                {
                    camera_id = cameraId,
                    camera_name = cameraName,
                    status = "registered"
                };
            }
        }

        public object ForgetCamera(string cameraId)
        {
            lock (registryLock)
            {
                if (cameraRegistry.ContainsKey(cameraId))
                {
                    var cameraData = cameraRegistry[cameraId];
                    cameraRegistry.Remove(cameraId);
                    SaveCameraRegistry();

                    Console.WriteLine($"Camera {cameraId} ({cameraData.CameraName}) forgotten");

                    return new
                    {
                        status = "success",
                        message = $"Camera {cameraId} forgotten"
                    };
                }
                else
                {
                    return new { error = "Camera not found" };
                }
            }
        }

        public object GetPendingRegistrations()
        {
            lock (registryLock)
            {
                var pendingList = pendingRegistrations.Values.Select(reg => new
                {
                    ip_address = reg.IpAddress,
                    camera_id = reg.CameraId,
                    timestamp = reg.Timestamp,
                    age_seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - reg.Timestamp
                }).ToList();

                return new
                {
                    pending = pendingList,
                    count = pendingList.Count
                };
            }
        }

        public object GetRegisteredCameras()
        {
            lock (registryLock)
            {
                return new
                {
                    cameras = cameraRegistry,
                    count = cameraRegistry.Count,
                    counter = cameraCounter
                };
            }
        }
    }
}