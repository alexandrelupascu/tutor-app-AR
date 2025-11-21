using UnityEngine;
using System.Threading.Tasks;

public class ARImageInteractionManager : MonoBehaviour
{
    [Header("Component References")]
    private ScreenshotManager screenshotManager;
    private ImageAPIClient apiClient;
    private HistoricalRaycastController raycastController;
    
    [Header("Character Interaction")]
    [SerializeField] private PlayerController character;
    [SerializeField] private float approachRadius = 0.5f; // How close to get to error position
    [SerializeField] private string pointingAnimationState = "Pointing"; // Name of pointing animation
    [SerializeField] private float pointingDuration = 3f; // How long to point
    
    [Header("Settings")]
    [SerializeField] private bool debugMode = true;
    
    // Current snapshot data
    private CameraSnapshot currentSnapshot;
    
    // Processing state
    private bool isProcessing = false;
    
    private void Awake()
    {
        // Validate component references
        screenshotManager = GetComponent<ScreenshotManager>();
        apiClient = GetComponent<ImageAPIClient>();
        raycastController = GetComponent<HistoricalRaycastController>();
        
        // Error checking
        if (screenshotManager == null)
            Debug.LogError("ARImageInteractionManager: ScreenshotManager not found!");
        
        if (apiClient == null)
            Debug.LogError("ARImageInteractionManager: ImageAPIClient not found!");
        
        if (raycastController == null)
            Debug.LogError("ARImageInteractionManager: HistoricalRaycastController not found!");
    }
    
    /// <summary>
    /// Main entry point - call this when user initiates image capture
    /// </summary>
    public async void ProcessImageInteraction()
    {
        if (isProcessing)
        {
            if (debugMode)
                Debug.Log("Already processing an image interaction. Please wait.");
            return;
        }
        
        await ProcessImageInteractionAsync();
    }
    
    /// <summary>
    /// Async version for more control over the flow
    /// </summary>
    public async Task ProcessImageInteractionAsync()
    {
        isProcessing = true;
        
        try
        {
            // Step 1: Capture screenshot and camera state
            if (debugMode)
                Debug.Log("Step 1: Capturing screenshot and camera state...");
            
            currentSnapshot = screenshotManager.CaptureSnapshot();
            
            if (currentSnapshot == null || currentSnapshot.Screenshot == null)
            {
                Debug.LogError("Failed to capture snapshot!");
                return;
            }
            
            if (debugMode)
                Debug.Log($"Snapshot captured successfully: {currentSnapshot}");
            
            // Step 2: Send image to API for analysis
            if (debugMode)
                Debug.Log("Step 2: Sending image to API for equation analysis...");
            
            Vector2 detectedPoint = await apiClient.SendImageForAnalysis(currentSnapshot.Screenshot);
            
            if (detectedPoint == Vector2.zero)
            {
                Debug.LogWarning("API returned zero position - either no error found or API failed");
                OnNoErrorDetected();
                return;
            }
            
            if (debugMode)
                Debug.Log($"API returned error location at screen point: {detectedPoint}");
            
            // Step 3: Raycast from historical camera position
            if (debugMode)
                Debug.Log("Step 3: Performing historical raycast...");
            
            if (raycastController.RaycastFromSnapshot(
                currentSnapshot, 
                detectedPoint, 
                out Vector3 worldPosition))
            {
                // Step 4: Successfully found world position
                if (debugMode)
                    Debug.Log($"Hit detected at world position: {worldPosition}");
                
                OnWorldPositionDetected(worldPosition);
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("Raycast did not hit any AR plane.");
                
                OnRaycastMiss();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error during image interaction processing: {e.Message}\n{e.StackTrace}");
            OnProcessingError(e);
        }
        finally
        {
            // Cleanup
            if (currentSnapshot?.Screenshot != null)
            {
                Destroy(currentSnapshot.Screenshot);
            }
            
            isProcessing = false;
            
            if (debugMode)
                Debug.Log("Processing complete. Ready for next interaction.");
        }
    }
    
    /// <summary>
    /// Called when a valid world position is detected
    /// Override this or subscribe to events for custom behavior
    /// </summary>
    protected virtual void OnWorldPositionDetected(Vector3 worldPosition)
    {
        Debug.Log($"✓ World position detected: {worldPosition}");
        
        if (character == null)
        {
            Debug.LogError("Character reference not assigned! Cannot move character to error position.");
            return;
        }
        
        // Calculate position near the error (within approach radius)
        Vector3 characterPosition = character.transform.position;
        Vector3 directionToError = (worldPosition - characterPosition).normalized;
        
        // Target position: Stop at approachRadius distance from error
        Vector3 targetPosition = worldPosition - (directionToError * approachRadius);
        
        // Make sure target is on the same Y level as character (stay on ground plane)
        targetPosition.y = characterPosition.y;
        
        if (debugMode)
        {
            Debug.Log($"Moving character from {characterPosition} to {targetPosition}");
            Debug.Log($"Error is at: {worldPosition}");
            Debug.Log($"Distance to error: {Vector3.Distance(characterPosition, worldPosition):F2}m");
        }
        
        // Move character to position near error
        character.MoveTo(targetPosition);
        
        // Wait for movement to complete, then point at error
        StartCoroutine(WaitForMovementThenPoint(worldPosition));
    }
    
    /// <summary>
    /// Wait for character to finish moving, then play pointing animation
    /// </summary>
    private System.Collections.IEnumerator WaitForMovementThenPoint(Vector3 errorPosition)
    {
        // Wait until character finishes moving
        while (character.GetCurrentState() == CharacterState.Moving)
        {
            yield return null;
        }
        
        if (debugMode)
            Debug.Log("Character reached position, now pointing at error");
        
        // Make character face the error position
        Vector3 lookDirection = errorPosition - character.transform.position;
        lookDirection.y = 0; // Keep rotation on horizontal plane only
        
        if (lookDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            character.transform.rotation = targetRotation;
        }
        
        // Play pointing animation
        character.ChangeState(CharacterState.Pointing); // Using Talking state for pointing
        
        if (debugMode)
            Debug.Log($"Playing pointing animation for {pointingDuration} seconds");
        
        // Wait for pointing duration
        yield return new UnityEngine.WaitForSeconds(pointingDuration);
        
        // Return to idle
        character.ChangeState(CharacterState.Idle);
        
        if (debugMode)
            Debug.Log("Pointing complete, character returned to idle");
    }
    
    /// <summary>
    /// Called when raycast doesn't hit any AR plane
    /// </summary>
    protected virtual void OnRaycastMiss()
    {
        Debug.LogWarning("× No AR plane was hit by the raycast.");
        
        // TODO: Handle miss scenario
        // Examples:
        // - Show user feedback "Please scan more surfaces"
        // - Retry logic
        // - Fallback behavior
    }
    
    /// <summary>
    /// Called when no error is detected in the equation (or API fails)
    /// </summary>
    protected virtual void OnNoErrorDetected()
    {
        Debug.Log("○ No error detected in equation (or API returned zero position).");
        
        // TODO: Handle no error scenario
        // Examples:
        // - Show "Equation is correct!" message
        // - Play success animation
        // - Return to main menu
    }
    
    /// <summary>
    /// Called when an error occurs during processing
    /// </summary>
    protected virtual void OnProcessingError(System.Exception exception)
    {
        Debug.LogError($"× Processing error: {exception.Message}");
        
        // TODO: Handle errors
        // Examples:
        // - Show error UI to user
        // - Retry mechanism
        // - Fallback behavior
        // - Send error telemetry
    }
    
    /// <summary>
    /// Check if currently processing
    /// </summary>
    public bool IsProcessing()
    {
        return isProcessing;
    }
    
    /// <summary>
    /// Get the current snapshot (may be null)
    /// </summary>
    public CameraSnapshot GetCurrentSnapshot()
    {
        return currentSnapshot;
    }
    
    /// <summary>
    /// Manual trigger for testing - can be called from inspector or button
    /// </summary>
    [ContextMenu("Test Image Interaction")]
    public void TestInteraction()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Can only test in Play mode!");
            return;
        }
        
        ProcessImageInteraction();
    }
}