using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    [Tooltip("The camera to face. If null, will use Camera.main")]
    [SerializeField] private Camera targetCamera;
    
    [Header("Rotation Settings")]
    [Tooltip("Speed of rotation towards camera (0 = instant, higher = smoother)")]
    [SerializeField] private float rotationSpeed = 5f;
    
    [Tooltip("Only rotate on Y axis (recommended for characters)")]
    [SerializeField] private bool lockYAxis = true;
    
    [Header("Conditional Settings")]
    [Tooltip("Only face camera when character is idle")]
    [SerializeField] private bool onlyWhenIdle = true;
    
    [Tooltip("Reference to PlayerController if using conditional facing")]
    [SerializeField] private PlayerController playerController;
    
    private void Start()
    {
        // Auto-find camera if not assigned
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                Debug.LogWarning("No camera found! FaceCamera disabled.");
                enabled = false;
            }
        }
        
        // Auto-find PlayerController if not assigned
        if (onlyWhenIdle && playerController == null)
        {
            playerController = GetComponent<PlayerController>();
            if (playerController == null)
            {
                Debug.LogWarning("PlayerController not found. Will always face camera.");
                onlyWhenIdle = false;
            }
        }
    }
    
    private void LateUpdate()
    {
        if (targetCamera == null) return;
        
        // Check if we should face the camera based on state
        if (onlyWhenIdle && playerController != null)
        {
            if (playerController.GetCurrentState() != CharacterState.Idle) // add all states where marshmallow needs to be facing the camera
            {
                return; // Don't face camera when not idle
            }
        }
        
        // Calculate direction to camera
        Vector3 directionToCamera = targetCamera.transform.position - transform.position;
        
        // Lock Y axis if enabled (keeps character upright)
        if (lockYAxis)
        {
            directionToCamera.y = 0f;
        }
        
        // Don't rotate if direction is too small
        if (directionToCamera.sqrMagnitude < 0.001f)
        {
            return;
        }
        
        // Calculate target rotation
        Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
        
        // Apply rotation (smooth or instant)
        if (rotationSpeed > 0f)
        {
            // Smooth rotation
            transform.rotation = Quaternion.Slerp(
                transform.rotation, 
                targetRotation, 
                rotationSpeed * Time.deltaTime
            );
        }
        else
        {
            // Instant rotation
            transform.rotation = targetRotation;
        }
    }
    
    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
    }

    public void SetFacingEnabled(bool enabled)
    {
        this.enabled = enabled;
    }
}