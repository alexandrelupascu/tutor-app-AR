using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class MoveToPosition : MonoBehaviour
{
    
    [SerializeField] private float moveSpeed = 0.08f;
    [SerializeField] private float rotationSpeed = 10f;
    
    [Header("AR Plane Sticking")]
    [SerializeField] private bool stickToARPlane = true;
    [SerializeField] private float raycastDistance = 2f;
    [SerializeField] private bool lockToSinglePlane = true; // Lock to first plane
    
    private PlayerController playerController;
    private ARRaycastManager arRaycastManager;
    private ARPlane lockedPlane; // The plane we're locked to
    private Vector3 targetPosition;
    private bool isMoving = false;
    private bool hasBeenPlaced = false;
    
    private List<ARRaycastHit> arHits = new List<ARRaycastHit>();

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        arRaycastManager = FindFirstObjectByType<ARRaycastManager>();
        
        if (arRaycastManager == null && stickToARPlane)
        {
            Debug.LogWarning("ARRaycastManager not found! Character won't stick to AR planes.");
        }
    }

    public void SetDestination(Vector3 destination)
    {
        targetPosition = destination;
        
        // First time - teleport and lock to plane
        if (!hasBeenPlaced)
        {
            transform.position = destination;
            hasBeenPlaced = true;
            
            // Lock to the plane we're standing on
            if (lockToSinglePlane && lockedPlane == null)
            {
                LockToCurrentPlane();
            }
            
            playerController.ChangeState(CharacterState.Idle);
            return;
        }
        
        // After first time - move normally
        isMoving = true;
    }

    public bool IsMoving()
    {
        return isMoving;
    }

    private void Update()
    {
        // Always try to stick to plane, even when idle
        if (stickToARPlane)
        {
            StickToARPlane();
        }
        
        if (!isMoving)
            return;

        // Calculate direction to target
        Vector3 direction = (targetPosition - transform.position).normalized;
        
        // Only rotate if there's a significant direction
        if (direction.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Move towards target (horizontal only)
        Vector3 currentPos = transform.position;
        Vector3 targetPosFlat = new Vector3(targetPosition.x, currentPos.y, targetPosition.z);
        transform.position = Vector3.MoveTowards(currentPos, targetPosFlat, moveSpeed * Time.deltaTime);

        // Check if reached destination
        float horizontalDistance = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(targetPosition.x, 0, targetPosition.z)
        );
        
        if (horizontalDistance < 0.0001f)
        {
            isMoving = false;
            
            if (playerController != null)
            {
                playerController.OnMovementComplete();
            }
        }
    }
    
    /// <summary>
    /// Lock to the AR plane we're currently standing on
    /// </summary>
    private void LockToCurrentPlane()
    {
        if (arRaycastManager == null) return;
        
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        arHits.Clear();
        
        if (arRaycastManager.Raycast(
            Camera.main.WorldToScreenPoint(rayOrigin), 
            arHits, 
            TrackableType.PlaneWithinPolygon))
        {
            if (arHits.Count > 0)
            {
                // Get the ARPlane component from the hit
                lockedPlane = arHits[0].trackable as ARPlane;
                
                if (lockedPlane != null)
                {
                    Debug.Log($"Character locked to AR Plane: {lockedPlane.trackableId}");
                }
            }
        }
    }
    
    /// <summary>
    /// Stick to the locked plane, or any plane if not locked
    /// </summary>
    private void StickToARPlane()
    {
        if (arRaycastManager == null)
            return;
        
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        
        // If locked to a specific plane, only use that one
        if (lockToSinglePlane && lockedPlane != null)
        {
            // Check if locked plane still exists and is active
            if (lockedPlane.gameObject.activeInHierarchy)
            {
                // Project character position onto the locked plane
                Vector3 planePoint = lockedPlane.transform.position;
                Vector3 planeNormal = lockedPlane.normal;
                
                // Calculate distance from character to plane along normal
                float distance = Vector3.Dot(planeNormal, transform.position - planePoint);
                
                // Snap to plane
                Vector3 newPos = transform.position - planeNormal * distance;
                transform.position = newPos;
                return;
            }
            else
            {
                // Locked plane was destroyed, find a new one
                Debug.LogWarning("Locked plane lost, searching for new plane...");
                lockedPlane = null;
                LockToCurrentPlane();
            }
        }
        
        // Fallback: Find any plane (if not locked or lock failed)
        arHits.Clear();
        if (arRaycastManager.Raycast(
            Camera.main.WorldToScreenPoint(rayOrigin), 
            arHits, 
            TrackableType.PlaneWithinPolygon))
        {
            if (arHits.Count > 0)
            {
                Vector3 newPos = transform.position;
                newPos.y = arHits[0].pose.position.y;
                transform.position = newPos;
            }
        }
    }

    /// <summary>
    /// Manually unlock from current plane (useful if you want to change planes)
    /// </summary>
    public void UnlockPlane()
    {
        lockedPlane = null;
        Debug.Log("Character unlocked from plane");
    }
    
    /// <summary>
    /// Get the plane the character is locked to
    /// </summary>
    public ARPlane GetLockedPlane()
    {
        return lockedPlane;
    }

    public void ResetPlacement()
    {
        hasBeenPlaced = false;
        isMoving = false;
        lockedPlane = null; // Clear locked plane
    }
}