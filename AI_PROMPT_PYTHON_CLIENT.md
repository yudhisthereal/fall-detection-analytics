# AI Prompt for Python Fall Detection Analytics Client

You are a Python developer building a client to interact with a C# Fall Detection Analytics server. The server provides pose estimation and fall detection capabilities through REST API endpoints.

## Server Information

- **Base URL**: `http://103.127.136.213:5000` (configurable via environment variable `ANALYTICS_API_URL`)
- **API Version**: 1.0
- **Content-Type**: `application/json`
- **JSON Naming Convention**: snake_case (e.g., `fallDetectionResult`, not `FallDetectionResult`)

## API Endpoints Overview

### 1. Health Check
**Endpoint**: `GET /api/analytics/health`

Check if the server is running and healthy.

**Response (200 OK)**:
```json
{
    "status": "healthy",
    "timestamp": 1699000000,
    "service": "Fall Detection Analytics Server"
}
```

### 2. Pose Analysis
**Endpoint**: `POST /api/analytics/analyze-pose`

Analyzes pose from 17 keypoints (COCO format) with 2 coordinates each, totaling 34 values.

**Request Body**:
```json
{
    "keypoints": [float, ...],    // Required: Array of 34 floats (17 keypoints × 2)
    "bbox": [float, ...],         // Optional: Bounding box [x, y, width, height]
    "track_id": int,              // Optional: Track ID for multi-object tracking
    "camera_id": "string",        // Optional: Camera identifier
    "use_hme": boolean            // Optional: Use Homomorphic Encryption (default: false)
}
```

**Keypoint Index Mapping (COCO format, indices 0-16)**:
- 5: Left Shoulder (indices 10, 11)
- 6: Right Shoulder (indices 12, 13)
- 11: Left Hip (indices 22, 23)
- 12: Right Hip (indices 24, 25)
- 13: Left Knee (indices 26, 27)
- 14: Right Knee (indices 28, 29)
- 15: Left Ankle (indices 30, 31)
- 16: Right Ankle (indices 32, 33)

**Response (200 OK)**:
```json
{
    "status": "success",
    "pose_data": {
        "label": "standing|sitting|bending_down|lying_down|None",
        "torso_angle": 0.0,           // Angle from vertical (0° = upright)
        "thigh_uprightness": 0.0,     // How upright the thigh is
        "thigh_calf_ratio": 0.0,      // Ratio for sitting detection
        "torso_leg_ratio": 0.0,       // Ratio for bending detection
        "thigh_angle": 0.0,           // Thigh angle from vertical
        "thigh_length": 0.0,
        "calf_length": 0.0,
        "torso_height": 0.0,
        "leg_length": 0.0,
        // HME mode only:
        "encrypted_features": {
            "Tra": [int, int],        // Torso angle encrypted
            "Tha": [int, int],        // Thigh uprightness encrypted
            "Thl": [int, int],        // Thigh length encrypted
            "cl": [int, int],         // Calf length encrypted
            "Trl": [int, int],        // Torso height encrypted
            "ll": [int, int]          // Leg length encrypted
        },
        "raw_int_values": {
            "Tra": int,
            "Tha": int,
            "Thl": int,
            "cl": int,
            "Trl": int,
            "ll": int
        },
        "comparison_flags": {...},    // HME comparison results
        "pose_code": int,             // Encoded pose (0=standing, 1=sitting, 2=bending_down, 3=lying_down)
        "server_analysis": boolean
    },
    "camera_id": "string",
    "track_id": int
}
```

**Pose Classification Logic** (when `use_hme=False`):
- `standing`: torso_angle < 30°, thigh_uprightness < 40°, thigh_calf_ratio ≥ 0.7
`: torso_angle < 30°, thigh_uprightness ≥ 40° OR torso_angle < 30°, thigh_uprightness- `sitting < 40°, thigh_calf_ratio < 0.7
- `bending_down`: 30° ≤ torso_angle < 80°, thigh_uprightness < 60° OR torso_angle < 30°, thigh_uprightness < 40°, torso_leg_ratio < 0.5
- `lying_down`: torso_angle ≥ 80°, thigh_uprightness ≥ 60°

### 3. Fall Detection
**Endpoint**: `POST /api/analytics/detect-fall`

Detects falls using pose data and bounding box motion analysis.

**Request Body**:
```json
{
    "camera_id": "string",          // Required: Camera identifier
    "track_id": int,                // Required: Track ID
    "pose_data": {                  // Optional: Pose data from analyze-pose
        "label": "lying_down",
        "torso_angle": 85.0,
        "thigh_uprightness": 70.0
    },
    "current_bbox": [float, ...],   // Required: Current bounding box [x, y, width, height]
    "previous_bbox": [float, ...],  // Optional: Previous frame's bounding box
    "previous_keypoints": [float, ...], // Optional: Previous frame's keypoints
    "elapsed_ms": float,            // Required: Time since last frame (ms)
    "use_hme": boolean              // Optional: Use HME mode (default: false)
}
```

**Response (200 OK)**:
```json
{
    "status": "success",
    "fall_detection": {
        "fall_detected_method1": boolean,  // Bounding box motion only
        "fall_detected_method2": boolean,  // BBox motion + strict pose (torso_angle > 80°, thigh_uprightness > 60°)
        "fall_detected_method3": boolean,  // Flexible verification (algorithm3Counter ≥ 2)
        "counter_method1": int,            // Consecutive fall counter for method 1 (max: 2)
        "counter_method2": int,            // Consecutive fall counter for method 2 (max: 2)
        "counter_method3": int,            // Algorithm 3 counter (max: 2)
        "algorithm3_counter": int,
        "primary_alert": boolean           // Main alert flag (fall_detected_method3)
    },
    "camera_id": "string",
    "track_id": int
}
```

**Fall Detection Methods**:
1. **Method 1 (BBox Only)**: Detects rapid vertical movement (vTop > 0.43 or vHeight > 0.43)
2. **Method 2 (Strict Pose)**: BBox motion AND strict pose (torso_angle > 80°, thigh_uprightness > 60°)
3. **Method 3 (Flexible)**: BBox motion AND flexible pose (torso_angle > 80° OR 30° < torso_angle < 80° AND thigh_uprightness > 60°)

**Counter Logic**:
- Counters increment when fall conditions are met
- Counters decrement when conditions are not met
- Maximum counter value: 2
- Fall is detected when counter ≥ 2

### 4. HME Comparisons
**Endpoint**: `POST /api/analytics/hme-comparisons`

Performs Homomorphic Encryption comparisons on encrypted features.

**Request Body**:
```json
{
    "encrypted_features": {
        "Tra": [int, int],    // Torso angle encrypted values
        "Tha": [int, int],    // Thigh uprightness encrypted values
        "Thl": [int, int],    // Thigh length encrypted values
        "cl": [int, int],     // Calf length encrypted values
        "Trl": [int, int],    // Torso height encrypted values
        "ll": [int, int]      // Leg length encrypted values
    }
}
```

**Response (200 OK)**:
```json
{
    "comparison_results": {
        "T30": [int, int],    // torso_angle > 30°
        "T40": [int, int],    // thigh_uprightness > 40°
        "T80": [int, int],    // torso_angle > 80°
        "T60": [int, int],    // thigh_uprightness > 60°
        "TC": [int, int],     // thigh_length*10 > calf_length*7
        "TL": [int, int]      // torso_height*10 > leg_length*5
    },
    "pose_label": "standing|sitting|bending_down|lying_down|unknown"
}
```

## Implementation Guidelines

### 1. Error Handling
Handle these HTTP status codes:
- **200**: Success
- **400**: Bad Request (invalid input data)
- **500**: Server Error (exception in processing)

```python
try:
    response = requests.post(url, json=request_body, timeout=10)
    response.raise_for_status()
    return response.json()
except requests.exceptions.RequestException as e:
    print(f"API request failed: {e}")
    return None
```

### 2. Request Retries
Implement exponential backoff for reliability:
```python
from requests.adapters import HTTPAdapter
from urllib3.util.retry import Retry

session = requests.Session()
retry_strategy = Retry(
    total=3,
    backoff_factor=1,
    status_forcelist=[429, 500, 502, 503, 504]
)
adapter = HTTPAdapter(max_retries=retry_strategy)
session.mount("http://", adapter)
```

### 3. Pose Data Flow

**Simple Flow (use_hme=False)**:
```
1. Get keypoints from pose estimation model
2. Call POST /api/analytics/analyze-pose with use_hme=False
3. Get pose_label and angles
4. Call POST /api/analytics/detect-fall with pose data and bbox
5. Check fall_detection.primary_alert
```

**HME Flow (use_hme=True)**:
```
1. Get keypoints from pose estimation model
2. Call POST /api/analytics/analyze-pose with use_hme=True
3. Get encrypted_features from response
4. Call POST /api/analytics/hme-comparisons with encrypted_features
5. Get decrypted pose_label from response
6. Optionally call POST /api/analytics/detect-fall with pose data
```

### 4. Track History Management

The server maintains track history internally. For accurate fall detection:

```python
# For each frame:
current_keypoints = get_keypoints_from_model()
current_bbox = get_bbox_from_model()

# Analyze pose
pose_response = session.post(
    f"{BASE_URL}/api/analytics/analyze-pose",
    json={
        "keypoints": current_keypoints,
        "bbox": current_bbox,
        "track_id": track_id,
        "camera_id": camera_id,
        "use_hme": False
    }
)

# Detect fall (after first few frames for history)
if frame_count >= 5:  # Queue size is 5
    fall_response = session.post(
        f"{BASE_URL}/api/analytics/detect-fall",
        json={
            "camera_id": camera_id,
            "track_id": track_id,
            "pose_data": pose_response["pose_data"],
            "current_bbox": current_bbox,
            "previous_bbox": previous_bbox,
            "elapsed_ms": 1000 / FPS  # ~33ms for 30 FPS
        }
    )
```

### 5. Configuration

```python
import os
from dataclasses import dataclass

@dataclass
class AnalyticsConfig:
    api_url: str = os.getenv("ANALYTICS_API_URL", "http://103.127.136.213:5000")
    timeout: int = 10
    use_hme: bool = False
    fps: int = 30

config = AnalyticsConfig()
```

### 6. Complete Client Example

```python
import requests
import time
from typing import Optional, Dict, Any
from dataclasses import dataclass

@dataclass
class PoseData:
    label: Optional[str]
    torso_angle: float
    thigh_uprightness: float
    thigh_calf_ratio: float
    torso_leg_ratio: float

@dataclass
class FallDetectionResult:
    fall_detected: bool
    method1: bool
    method2: bool
    method3: bool
    primary_alert: bool

class FallDetectionClient:
    def __init__(self, base_url: str = "http://103.127.136.213:5000", timeout: int = 10):
        self.base_url = base_url
        self.timeout = timeout
        self.session = requests.Session()
    
    def health_check(self) -> bool:
        """Check if server is healthy."""
        try:
            response = self.session.get(
                f"{self.base_url}/api/analytics/health",
                timeout=self.timeout
            )
            return response.status_code == 200
        except requests.exceptions.RequestException:
            return False
    
    def analyze_pose(
        self,
        keypoints: list,
        bbox: Optional[list] = None,
        track_id: int = 0,
        camera_id: str = "",
        use_hme: bool = False
    ) -> Optional[Dict[str, Any]]:
        """Analyze pose from keypoints."""
        payload = {
            "keypoints": keypoints,
            "track_id": track_id,
            "camera_id": camera_id,
            "use_hme": use_hme
        }
        if bbox:
            payload["bbox"] = bbox
        
        try:
            response = self.session.post(
                f"{self.base_url}/api/analytics/analyze-pose",
                json=payload,
                timeout=self.timeout
            )
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            print(f"Pose analysis failed: {e}")
            return None
    
    def detect_fall(
        self,
        camera_id: str,
        track_id: int,
        pose_data: Optional[Dict[str, Any]],
        current_bbox: list,
        previous_bbox: Optional[list] = None,
        elapsed_ms: float = 33.33,
        use_hme: bool = False
    ) -> Optional[Dict[str, Any]]:
        """Detect fall from pose and motion data."""
        payload = {
            "camera_id": camera_id,
            "track_id": track_id,
            "pose_data": pose_data,
            "current_bbox": current_bbox,
            "elapsed_ms": elapsed_ms,
            "use_hme": use_hme
        }
        if previous_bbox:
            payload["previous_bbox"] = previous_bbox
        
        try:
            response = self.session.post(
                f"{self.base_url}/api/analytics/detect-fall",
                json=payload,
                timeout=self.timeout
            )
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            print(f"Fall detection failed: {e}")
            return None
    
    def hme_comparisons(
        self,
        encrypted_features: Dict[str, list]
    ) -> Optional[Dict[str, Any]]:
        """Perform HME comparisons on encrypted features."""
        try:
            response = self.session.post(
                f"{self.base_url}/api/analytics/hme-comparisons",
                json={"encrypted_features": encrypted_features},
                timeout=self.timeout
            )
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            print(f"HME comparison failed: {e}")
            return None

# Usage Example
if __name__ == "__main__":
    client = FallDetectionClient()
    
    # Check health
    if client.health_check():
        print("✓ Server is healthy")
    
    # Simulate keypoints (34 floats for 17 COCO keypoints)
    keypoints = [0.5] * 34  # Replace with actual keypoints
    
    # Analyze pose
    result = client.analyze_pose(
        keypoints=keypoints,
        track_id=1,
        camera_id="cam1"
    )
    
    if result and result.get("status") == "success":
        pose_data = result.get("pose_data", {})
        print(f"Detected pose: {pose_data.get('label')}")
        print(f"Torso angle: {pose_data.get('torso_angle'):.2f}°")
```

## Testing Checklist

- [ ] Server health check returns healthy status
- [ ] Pose analysis with valid keypoints returns correct label
- [ ] Fall detection with motion data returns accurate results
- [ ] HME mode encrypts/decrypts values correctly
- [ ] Error handling for invalid keypoints (34 values required)
- [ ] Error handling for missing bbox data
- [ ] Timeout handling for slow requests
- [ ] Retry mechanism for transient failures

## Common Issues and Solutions

1. **Wrong number of keypoints**: Ensure exactly 34 floats are sent (17 keypoints × 2 coordinates)
2. **Missing HME features**: Set `use_hme=True` to get encrypted features
3. **Fall detection not working**: Ensure `elapsed_ms` is calculated correctly (1000/FPS)
4. **Pose label is None**: Check that keypoints are complete (no missing values)
5. **Counters not incrementing**: Verify bbox has at least 4 values [x, y, width, height]

