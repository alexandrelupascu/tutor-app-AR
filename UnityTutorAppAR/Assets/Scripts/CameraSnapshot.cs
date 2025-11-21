using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Stores a snapshot of the camera state at a specific moment in time.
/// Used to perform raycasts from historical camera positions.
/// </summary>
public class CameraSnapshot
{
    // The captured screenshot image
    public Texture2D Screenshot { get; set; }
    
    // Camera transform at capture time
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    
    // Camera projection matrix (for accurate ray calculation)
    public Matrix4x4 ProjectionMatrix { get; set; }
    public Matrix4x4 ViewMatrix { get; set; }
    
    // Camera properties
    public float FieldOfView { get; set; }
    public float NearClipPlane { get; set; }
    public float FarClipPlane { get; set; }
    public float AspectRatio { get; set; }
    
    // Screen dimensions at time of capture
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
    
    // Timestamp for debugging/tracking
    public float Timestamp { get; set; }
    
    // Optional: Store visible AR planes at capture time
    public List<ARPlane> VisiblePlanes { get; set; }
    
    // Optional: Store the camera reference
    public Camera SourceCamera { get; set; }
    
    /// <summary>
    /// Constructor with essential data
    /// </summary>
    public CameraSnapshot(Camera camera, Texture2D screenshot)
    {
        Screenshot = screenshot;
        Position = camera.transform.position;
        Rotation = camera.transform.rotation;
        ProjectionMatrix = camera.projectionMatrix;
        ViewMatrix = camera.worldToCameraMatrix;
        
        FieldOfView = camera.fieldOfView;
        NearClipPlane = camera.nearClipPlane;
        FarClipPlane = camera.farClipPlane;
        AspectRatio = camera.aspect;
        
        ScreenWidth = Screen.width;
        ScreenHeight = Screen.height;
        
        Timestamp = Time.time;
        SourceCamera = camera;
        
        VisiblePlanes = new List<ARPlane>();
    }
    
    // Empty constructor for manual setup
    public CameraSnapshot()
    {
        VisiblePlanes = new List<ARPlane>();
    }
    
    // Convert screen point to viewport point (0-1 normalized coordinates)
    public Vector3 ScreenToViewportPoint(Vector2 screenPoint)
    {
        return new Vector3(
            screenPoint.x / ScreenWidth,
            screenPoint.y / ScreenHeight,
            0f
        );
    }
    
    // Get a ray from this snapshot's camera position through a screen point
    public Ray GetRayFromScreenPoint(Vector2 screenPoint)
    {
        // Convert screen point to viewport space (0-1)
        Vector3 viewportPoint = ScreenToViewportPoint(screenPoint);
        
        // Convert viewport point to clip space (-1 to 1)
        Vector3 clipPoint = new Vector3(
            viewportPoint.x * 2f - 1f,
            viewportPoint.y * 2f - 1f,
            1f
        );
        
        // Unproject from clip space to world space
        Matrix4x4 inverseVP = (ProjectionMatrix * ViewMatrix).inverse;
        Vector4 worldPoint4 = inverseVP * new Vector4(clipPoint.x, clipPoint.y, clipPoint.z, 1f);
        Vector3 worldPoint = new Vector3(
            worldPoint4.x / worldPoint4.w,
            worldPoint4.y / worldPoint4.w,
            worldPoint4.z / worldPoint4.w
        );
        
        // Create ray from camera position to world point
        Vector3 direction = (worldPoint - Position).normalized;
        
        return new Ray(Position, direction);
    }

    // Debug info
    public override string ToString()
    {
        return $"CameraSnapshot [Time: {Timestamp:F2}s, Pos: {Position}, Rot: {Rotation.eulerAngles}, Screen: {ScreenWidth}x{ScreenHeight}]";
    }
}