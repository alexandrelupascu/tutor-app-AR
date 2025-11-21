using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;

/// <summary>
/// Performs raycasts from historical camera positions stored in CameraSnapshots
/// This allows accurate 3D position detection even after the camera has moved
/// </summary>
public class HistoricalRaycastController : MonoBehaviour
{
    [Header("Raycast Settings")]
    [SerializeField] private float maxRaycastDistance = 50f;
    [SerializeField] private LayerMask raycastLayerMask = -1; // All layers by default
    
    [Header("AR Plane Settings")]
    [SerializeField] private bool useARPlanes = true;
    [SerializeField] private float planeRaycastTolerance = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool drawDebugRay = true;
    [SerializeField] private float debugRayDuration = 2f;
    
    // Optional reference to AR plane manager for fallback
    private ARPlaneManager planeManager;
    
    private void Awake()
    {
        // Auto-find ARPlaneManager
        planeManager = Object.FindFirstObjectByType<ARPlaneManager>();
    }
    
    /// <summary>
    /// Raycast from the historical camera position through a screen point
    /// </summary>
    /// <param name="snapshot">The camera snapshot containing position/rotation</param>
    /// <param name="screenPoint">The 2D screen point (from API response)</param>
    /// <param name="worldPosition">Output: The 3D world position where ray hit</param>
    /// <returns>True if raycast hit something</returns>
    public bool RaycastFromSnapshot(
        CameraSnapshot snapshot, 
        Vector2 screenPoint, 
        out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        
        if (snapshot == null)
        {
            Debug.LogError("Cannot raycast: snapshot is null!");
            return false;
        }
        
        if (debugMode)
            Debug.Log($"Raycasting from snapshot at position {snapshot.Position} through screen point {screenPoint}");
        
        // Get ray from historical camera position
        Ray ray = snapshot.GetRayFromScreenPoint(screenPoint);
        
        if (debugMode)
            Debug.Log($"Ray: Origin={ray.origin}, Direction={ray.direction}");
        
        // Draw debug ray
        if (drawDebugRay)
        {
            Debug.DrawRay(ray.origin, ray.direction * maxRaycastDistance, Color.yellow, debugRayDuration);
        }

        // PERFORM THE ACTUAL RAYCAST
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, maxRaycastDistance, raycastLayerMask))
        {
            worldPosition = hit.point;
            
            if (debugMode)
                Debug.Log($"Raycast HIT at {worldPosition}, object: {hit.collider.gameObject.name}");
            
            if (drawDebugRay)
            {
                Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.green, debugRayDuration);
                Debug.DrawLine(hit.point, hit.point + Vector3.up * 0.5f, Color.green, debugRayDuration);
            }
            
            return true;
        }
        
        // If standard raycast failed, try AR plane raycast
        if (useARPlanes && planeManager != null && snapshot.VisiblePlanes != null)
        {
            if (TryRaycastARPlanes(ray, snapshot.VisiblePlanes, out worldPosition))
            {
                if (debugMode)
                    Debug.Log($"AR Plane raycast HIT at {worldPosition}");
                
                if (drawDebugRay)
                {
                    float distance = Vector3.Distance(ray.origin, worldPosition);
                    Debug.DrawRay(ray.origin, ray.direction * distance, Color.cyan, debugRayDuration);
                    Debug.DrawLine(worldPosition, worldPosition + Vector3.up * 0.5f, Color.cyan, debugRayDuration);
                }
                
                return true;
            }
        }
        
        // Nothing hit
        if (debugMode)
            Debug.LogWarning("Raycast missed all targets");
        
        if (drawDebugRay)
        {
            Debug.DrawRay(ray.origin, ray.direction * maxRaycastDistance, Color.red, debugRayDuration);
        }
        
        return false;
    }
    
    /// <summary>
    /// Try to raycast against AR planes manually
    /// </summary>
    private bool TryRaycastARPlanes(Ray ray, List<ARPlane> planes, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        float closestDistance = float.MaxValue;
        bool foundHit = false;
        
        foreach (var plane in planes)
        {
            if (plane == null || !plane.gameObject.activeInHierarchy)
                continue;
            
            // Create a plane at the AR plane's position
            Plane geometryPlane = new Plane(plane.transform.up, plane.center);
            
            // Raycast against the plane
            float enter;
            if (geometryPlane.Raycast(ray, out enter))
            {
                if (enter > 0 && enter < maxRaycastDistance && enter < closestDistance)
                {
                    Vector3 point = ray.GetPoint(enter);
                    
                    // Check if point is within the plane's bounds
                    if (IsPointOnPlane(point, plane))
                    {
                        hitPoint = point;
                        closestDistance = enter;
                        foundHit = true;
                    }
                }
            }
        }
        
        return foundHit;
    }
    
    /// <summary>
    /// Check if a point is within the bounds of an AR plane
    /// </summary>
    private bool IsPointOnPlane(Vector3 point, ARPlane plane)
    {
        // Transform point to plane's local space
        Vector3 localPoint = plane.transform.InverseTransformPoint(point);
        
        // Check if within plane's 2D bounds (ignore Y since it's the plane's normal)
        Vector2 planeSize = plane.size;
        
        return Mathf.Abs(localPoint.x) <= (planeSize.x / 2f + planeRaycastTolerance) &&
               Mathf.Abs(localPoint.z) <= (planeSize.y / 2f + planeRaycastTolerance);
    }
    
    /// <summary>
    /// Raycast with multiple screen points (batch processing)
    /// </summary>
    public List<Vector3> RaycastMultiplePoints(CameraSnapshot snapshot, List<Vector2> screenPoints)
    {
        List<Vector3> worldPositions = new List<Vector3>();
        
        foreach (var screenPoint in screenPoints)
        {
            if (RaycastFromSnapshot(snapshot, screenPoint, out Vector3 worldPos))
            {
                worldPositions.Add(worldPos);
            }
            else if (debugMode)
            {
                Debug.LogWarning($"Failed to raycast screen point: {screenPoint}");
            }
        }
        
        if (debugMode)
            Debug.Log($"Batch raycast: {worldPositions.Count}/{screenPoints.Count} hits");
        
        return worldPositions;
    }
    
    /// <summary>
    /// Get the closest hit point from a screen point
    /// </summary>
    public bool GetClosestHit(CameraSnapshot snapshot, Vector2 screenPoint, out RaycastHit hitInfo)
    {
        hitInfo = default;
        
        if (snapshot == null)
        {
            Debug.LogError("Cannot raycast: snapshot is null!");
            return false;
        }
        
        Ray ray = snapshot.GetRayFromScreenPoint(screenPoint);
        return Physics.Raycast(ray, out hitInfo, maxRaycastDistance, raycastLayerMask);
    }
}