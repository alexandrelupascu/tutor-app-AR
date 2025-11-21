using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;

/// <summary>
/// Handles capturing screenshots and camera state for AR image interaction
/// Uses ARCameraManager.TryAcquireLatestCpuImage for proper AR camera capture
/// </summary>
public class ScreenshotManager : MonoBehaviour
{
    [Header("AR References")]
    [SerializeField] private Camera arCamera;
    [SerializeField] private ARCameraManager arCameraManager;
    
    [Header("Screenshot Settings")]
    [SerializeField] private TextureFormat textureFormat = TextureFormat.RGB24;
    
    [Header("AR Plane Tracking")]
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private bool trackVisiblePlanes = true;
    [SerializeField] private float planeVisibilityDistance = 10f;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    
    private void Awake()
    {
        // Auto-find AR camera if not assigned
        if (arCamera == null)
        {
            arCamera = Camera.main;
            if (arCamera == null)
            {
                Debug.LogError("ScreenshotManager: No AR Camera found!");
            }
        }
        
        // Auto-find ARCameraManager if not assigned
        if (arCameraManager == null)
        {
            arCameraManager = Object.FindFirstObjectByType<ARCameraManager>();
            if (arCameraManager == null)
            {
                Debug.LogError("ScreenshotManager: No ARCameraManager found!");
            }
        }
        
        // Auto-find ARPlaneManager if not assigned
        if (planeManager == null)
        {
            planeManager = Object.FindFirstObjectByType<ARPlaneManager>();
        }
    }
    
    /// <summary>
    /// Capture a snapshot of the current camera view and state using AR CPU Image
    /// </summary>
    public CameraSnapshot CaptureSnapshot()
    {
        if (arCamera == null)
        {
            Debug.LogError("Cannot capture snapshot: AR Camera is null!");
            return null;
        }
        
        if (arCameraManager == null)
        {
            Debug.LogError("Cannot capture snapshot: ARCameraManager is null!");
            return null;
        }
        
        if (debugMode)
            Debug.Log("Capturing camera snapshot using CPU image...");
        
        // Try to acquire the latest CPU image from AR camera
        if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
        {
            Debug.LogError("Failed to acquire CPU image from AR camera!");
            return null;
        }
        
        try
        {
            // Convert CPU image to Texture2D
            Texture2D screenshot = ConvertCpuImageToTexture(cpuImage);
            
            if (screenshot == null)
            {
                Debug.LogError("Failed to convert CPU image to texture!");
                return null;
            }
            
            // Create snapshot with camera data
            CameraSnapshot snapshot = new CameraSnapshot(arCamera, screenshot);
            
            // Track visible AR planes if enabled
            if (trackVisiblePlanes && planeManager != null)
            {
                snapshot.VisiblePlanes = GetVisiblePlanes();
            }
            
            if (debugMode)
                Debug.Log($"Snapshot captured: {snapshot}");
            
            return snapshot;
        }
        finally
        {
            // IMPORTANT: Always dispose the CPU image
            cpuImage.Dispose();
        }
    }
    
    /// <summary>
    /// Convert XRCpuImage to Texture2D
    /// </summary>
    private Texture2D ConvertCpuImageToTexture(XRCpuImage cpuImage)
    {
        // Get image dimensions
        var dimensions = cpuImage.dimensions;
        
        if (debugMode)
            Debug.Log($"CPU Image dimensions: {dimensions.x}x{dimensions.y}, format: {cpuImage.format}");
        
        // Create conversion parameters
        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, dimensions.x, dimensions.y),
            outputDimensions = dimensions,
            outputFormat = textureFormat,
            transformation = XRCpuImage.Transformation.MirrorY  // Flip vertically for correct orientation
        };
        
        // Calculate required buffer size
        int size = cpuImage.GetConvertedDataSize(conversionParams);
        
        // Allocate buffer
        var buffer = new NativeArray<byte>(size, Allocator.Temp);
        
        try
        {
            // Convert image
            cpuImage.Convert(conversionParams, buffer);
            
            // Create texture
            Texture2D texture = new Texture2D(
                dimensions.x,
                dimensions.y,
                textureFormat,
                false
            );
            
            // Load raw texture data
            texture.LoadRawTextureData(buffer);
            texture.Apply();
            
            if (debugMode)
                Debug.Log($"Screenshot converted: {texture.width}x{texture.height}");
            
            return texture;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error converting CPU image: {e.Message}");
            return null;
        }
        finally
        {
            // Always dispose the buffer
            buffer.Dispose();
        }
    }
    
    /// <summary>
    /// Async version with callback (useful for ensuring frame timing)
    /// </summary>
    public void CaptureSnapshotAsync(System.Action<CameraSnapshot> callback)
    {
        StartCoroutine(CaptureSnapshotCoroutine(callback));
    }
    
    private IEnumerator CaptureSnapshotCoroutine(System.Action<CameraSnapshot> callback)
    {
        // Wait for end of frame to ensure AR camera has latest image
        yield return new WaitForEndOfFrame();
        
        CameraSnapshot snapshot = CaptureSnapshot();
        callback?.Invoke(snapshot);
    }
    
    /// <summary>
    /// Get list of AR planes that are currently visible to the camera
    /// </summary>
    private List<ARPlane> GetVisiblePlanes()
    {
        List<ARPlane> visiblePlanes = new List<ARPlane>();
        
        if (planeManager == null)
            return visiblePlanes;
        
        foreach (var plane in planeManager.trackables)
        {
            // Check if plane is active and close enough
            if (plane.gameObject.activeInHierarchy)
            {
                float distance = Vector3.Distance(arCamera.transform.position, plane.center);
                
                if (distance <= planeVisibilityDistance)
                {
                    // Check if plane is in camera frustum
                    Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(arCamera);
                    Bounds planeBounds = new Bounds(plane.center, plane.size);
                    
                    if (GeometryUtility.TestPlanesAABB(frustumPlanes, planeBounds))
                    {
                        visiblePlanes.Add(plane);
                    }
                }
            }
        }
        
        if (debugMode && visiblePlanes.Count > 0)
            Debug.Log($"Found {visiblePlanes.Count} visible AR planes");
        
        return visiblePlanes;
    }
    

    // Check if AR camera is ready to capture images
    public bool IsCameraReady()
    {
        return arCameraManager != null && arCameraManager.enabled;
    }
    
    // Set custom camera references
    public void SetARCamera(Camera camera)
    {
        arCamera = camera;
    }
    
    public void SetARCameraManager(ARCameraManager manager)
    {
        arCameraManager = manager;
    }
}