using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Triggers AR image capture on screen tap
/// Uses new Input System
/// </summary>
public class ARTapInput : MonoBehaviour
{
    [Header("AR System")]
    [SerializeField] private ARImageInteractionManager arImageManager;
    
    [Header("Enable/Disable")]
    [SerializeField] private bool captureEnabled = false; // Toggle in inspector or via UI
    [SerializeField] private UnityEngine.UI.Toggle enableToggle; // Optional UI toggle
    
    [Header("Startup Settings")]
    [SerializeField] private bool showStartupMessage = false;
    
    [Header("Input Settings")]
    [SerializeField] private bool requireDoubleTap = false;
    [SerializeField] private float doubleTapTime = 0.3f;
    
    [Header("Screen Exclusion Zones")]
    [SerializeField] private bool enableExclusionZones = true;
    [SerializeField] private Vector2 bottomRightExclusionSize = new Vector2(0.25f, 0.25f); // 25% of screen width/height
    [Tooltip("Exclude taps in bottom-right corner (for UI buttons)")]
    [SerializeField] private bool excludeBottomRight = true;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject tapIndicatorPrefab;
    [SerializeField] private float indicatorDuration = 0.5f;
    
    // Tracking
    private int tapCount = 0;
    private bool startupComplete = false;
    private float lastTapTime = 0f;
    
    // Input System
    private InputAction tapAction;
    
    private void Awake()
    {
        // Enable Enhanced Touch support for new Input System
        EnhancedTouchSupport.Enable();
        
        // Setup tap action for mouse/touch
        tapAction = new InputAction(type: InputActionType.Button, binding: "<Pointer>/press");
        tapAction.performed += ctx => OnTapPerformed(Pointer.current.position.ReadValue());
    }
    
    private void OnEnable()
    {
        tapAction.Enable();
    }
    
    private void OnDisable()
    {
        tapAction.Disable();
    }
    
    private void OnDestroy()
    {
        // Cleanup
        tapAction.Dispose();
        EnhancedTouchSupport.Disable();
    }
    
    private void Start()
    {
        // Auto-find ARImageInteractionManager if not assigned
        if (arImageManager == null)
        {
            arImageManager = FindFirstObjectByType<ARImageInteractionManager>();
        }
        
        // Setup UI toggle listener if assigned
        if (enableToggle != null)
        {
            // Set toggle to match initial state
            enableToggle.isOn = captureEnabled;
            
            // Listen for toggle changes
            enableToggle.onValueChanged.AddListener(OnToggleChanged);
        }
        
        // Set initial enabled state of AR Image Manager
        UpdateARImageManagerState();
    }
    
    private void OnTapPerformed(Vector2 screenPosition)
    {
        HandleTap(screenPosition);
    }
    
    private void HandleTap(Vector2 screenPosition)
    {
        // Check if capture is enabled FIRST
        if (!captureEnabled)
        {
            if (showStartupMessage)
                Debug.Log("Tap ignored - capture is disabled");
            return;
        }
        
        // Check if tap is in exclusion zone
        if (enableExclusionZones && IsInExclusionZone(screenPosition))
        {
            Debug.Log("Tap ignored - in exclusion zone (bottom-right UI area)");
            return;
        }
        
        // Ignore taps on UI elements
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log("Tap ignored - hit UI element");
            return;
        }
        
        // Show visual feedback at tap location
        if (tapIndicatorPrefab != null)
        {
            ShowTapIndicator(screenPosition);
        }
        
        // Trigger capture
        TriggerCapture();
    }
    
    private void TriggerCapture()
    {
        if (arImageManager == null)
        {
            Debug.LogError("ARImageInteractionManager not found!");
            return;
        }
        
        if (!arImageManager.enabled)
        {
            Debug.LogWarning("ARImageInteractionManager is disabled!");
            return;
        }
        
        if (arImageManager.IsProcessing())
        {
            Debug.Log("Already processing, please wait...");
            return;
        }
        
        Debug.Log("Screen tapped - triggering equation check and image capture");
        arImageManager.ProcessImageInteraction();
    }
    
    private void ShowTapIndicator(Vector2 screenPosition)
    {
        GameObject indicator = Instantiate(tapIndicatorPrefab);
        
        // Position in UI space or world space depending on your setup
        // For UI:
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            indicator.transform.SetParent(canvas.transform, false);
            indicator.transform.position = screenPosition;
        }
        
        // Auto-destroy after duration
        Destroy(indicator, indicatorDuration);
    }
    
    /// <summary>
    /// Update the enabled state of ARImageInteractionManager based on captureEnabled
    /// </summary>
    private void UpdateARImageManagerState()
    {
        if (arImageManager != null)
        {
            arImageManager.enabled = captureEnabled;
        }
    }
    
    /// <summary>
    /// Get current tap count
    /// </summary>
    public int GetTapCount()
    {
        return tapCount;
    }
    
    /// <summary>
    /// Enable or disable capture functionality
    /// </summary>
    public void SetCaptureEnabled(bool enabled)
    {
        captureEnabled = enabled;
        
        // Update toggle UI if it exists
        if (enableToggle != null && enableToggle.isOn != enabled)
        {
            enableToggle.isOn = enabled;
        }
        
        // Enable/Disable the AR Image Manager component
        UpdateARImageManagerState();
    }

    public void ToggleCapture()
    {
        SetCaptureEnabled(!captureEnabled);
    }
    
    public bool IsCaptureEnabled()
    {
        return captureEnabled;
    }
    
    private void OnToggleChanged(bool value)
    {
        captureEnabled = value;
        
        // Enable/Disable the AR Image Manager component
        UpdateARImageManagerState();
    }
    
    private bool IsInExclusionZone(Vector2 screenPosition)
    {
        // Get screen dimensions
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;
        
        // Calculate exclusion zone bounds (bottom-right corner)
        if (excludeBottomRight)
        {
            // Bottom-right zone dimensions (as percentage of screen)
            float exclusionWidth = screenWidth * bottomRightExclusionSize.x;
            float exclusionHeight = screenHeight * bottomRightExclusionSize.y;
            
            // Zone starts from bottom-right corner
            // Note: Screen coordinates are (0,0) at BOTTOM-LEFT
            float minX = screenWidth - exclusionWidth;
            float minY = 0; // Bottom of screen
            float maxX = screenWidth; // Right edge
            float maxY = exclusionHeight; // Height from bottom
            
            if (showStartupMessage)
            {
                Debug.Log($"Exclusion zone: X[{minX:F0} to {maxX:F0}], Y[{minY:F0} to {maxY:F0}]");
                Debug.Log($"Exclusion size: {exclusionWidth:F0}px wide × {exclusionHeight:F0}px tall");
            }
            
            // Check if tap is within this zone
            bool inZone = screenPosition.x >= minX && screenPosition.x <= maxX &&
                          screenPosition.y >= minY && screenPosition.y <= maxY;
            
            if (showStartupMessage)
            {
                Debug.Log($"Is in exclusion zone: {inZone}");
            }
            
            return inZone;
        }
        
        return false;
    }
    
    public bool IsTapAllowed(Vector2 screenPosition)
    {
        // Check exclusion zones
        if (enableExclusionZones && IsInExclusionZone(screenPosition))
        {
            return false;
        }
        
        // Check if on UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return false;
        }
        
        return true;
    }
    
    public bool IsPositionInExclusionZone(Vector2 screenPosition)
    {
        return enableExclusionZones && IsInExclusionZone(screenPosition);
    }
}