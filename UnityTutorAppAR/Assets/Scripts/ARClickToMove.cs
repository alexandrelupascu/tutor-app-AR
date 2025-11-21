using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;

public class ARClickToMove : MonoBehaviour
{
    [SerializeField] private Camera arCamera;
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private PlayerController character; // reference to your character
    
    private ARTapInput tapInput; // Add reference to tap input manager
    bool isPlacing = false;

    private List<ARRaycastHit> hits = new List<ARRaycastHit>();

    private void Awake()
    {
        // Find the tap input manager
        tapInput = FindFirstObjectByType<ARTapInput>();
    }

    void Update()
    {
        if (!raycastManager || character == null) return;

        // Touch input using new Input System
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame && !isPlacing)
        {
            isPlacing = true;
            Vector2 touchPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            MoveObject(touchPosition);
        }
        // Mouse input (for editor testing)
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !isPlacing)
        {
            isPlacing = true;
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            MoveObject(mousePosition);
        }
    }

    private Vector3 lastHitPosition;

    private void MoveObject(Vector2 screenPosition)
    {
        // CHECK EXCLUSION ZONE FIRST!
        if (tapInput != null && !tapInput.IsTapAllowed(screenPosition))
        {

            isPlacing = false; // Reset placing flag
            return;
        }
        
        hits.Clear();
        if (raycastManager.Raycast(screenPosition, hits, TrackableType.AllTypes))
        {
            Vector3 hitPosePosition = hits[0].pose.position;

            // FIX: Use character.gameObject to get the GameObject, then GetComponent
            character.gameObject.GetComponent<MoveToPosition>().SetDestination(hitPosePosition);
            
            character.ChangeState(CharacterState.Moving);
            
            //Debug.Log("Moving to: " + hitPosePosition);
        }

        StartCoroutine(SetIsPlacingToFalseWithDelay());
    }

    private void OnDrawGizmos()
    {
        // Draw a red sphere at the hit position
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(lastHitPosition, 0.1f);
        
        // Draw a green sphere at character position
        if (character != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(character.transform.position, 0.1f);
        }
    }

    private IEnumerator SetIsPlacingToFalseWithDelay()
    {
        yield return new WaitForSeconds(0.25f);
        isPlacing = false;
    }
}