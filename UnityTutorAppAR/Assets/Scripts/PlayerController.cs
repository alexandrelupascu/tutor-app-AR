using UnityEngine;

public enum CharacterState
{
    Idle,
    Moving,
    Thinking,
    Talking,
    Pointing,
    Dancing  // Added
}

public class PlayerController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    private MoveToPosition moveToPosition;
    private CharacterState currentState = CharacterState.Idle;

    [Header("Animation State Names")]
    [Tooltip("Exact name of the Idle state in your Animator")]
    [SerializeField] private string idleAnimationState = "Idle";

    [Tooltip("Exact name of the Running state in your Animator")]
    [SerializeField] private string runningAnimationState = "Running";

    [Tooltip("Exact name of the Thinking state in your Animator")]
    [SerializeField] private string thinkingAnimationState = "Thinking";

    [Tooltip("Exact name of the Talking state in your Animator")]
    [SerializeField] private string talkingAnimationState = "Talking";
    
    [Tooltip("Exact name of the Pointing state in your Animator")]
    [SerializeField] private string pointingAnimationState = "Pointing";
    
    [Tooltip("Exact name of the Dancing state in your Animator")]
    [SerializeField] private string dancingAnimationState = "Dance";

    [Header("Animation Settings")]
    [SerializeField] private float transitionDuration = 0.2f;
    
    [Header("Dance Settings")]
    [SerializeField] private float danceDuration = 3f;  // How long to dance before returning to idle
    private float danceTimer = 0f;

    private void Awake()
    {
        moveToPosition = GetComponent<MoveToPosition>();

        if (moveToPosition == null)
        {
            //Debug.LogError("MoveToPosition component not found on " + gameObject.name);
        }

        // Try to get Animator if not assigned
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                //Debug.LogWarning("Animator component not found on " + gameObject.name);
            }
            else
            {
                //Debug.Log("Animator found on: " + animator.gameObject.name);
            }
        }
    }

    public void MoveTo(Vector3 destination)
    {
        moveToPosition.SetDestination(destination);
        ChangeState(CharacterState.Moving);
    }

    public void OnMovementComplete()
    {
        //Debug.Log("Movement complete!");
        ChangeState(CharacterState.Idle);
    }
    
    /// <summary>
    /// Play the dance animation (e.g., when answer is correct)
    /// </summary>
    public void PlayDance()
    {
        ChangeState(CharacterState.Dancing);
        danceTimer = danceDuration;
    }
    
    /// <summary>
    /// Play dance animation for a custom duration
    /// </summary>
    public void PlayDance(float duration)
    {
        ChangeState(CharacterState.Dancing);
        danceTimer = duration;
    }

    public void ChangeState(CharacterState newState)
    {
        if (currentState == newState) return;

        OnStateExit(currentState);
        currentState = newState;
        OnStateEnter(newState);
    }

    private void OnStateEnter(CharacterState state)
    {
        switch (state)
        {
            case CharacterState.Idle:
                //Debug.Log("Entered Idle");
                PlayAnimation(idleAnimationState);
                break;
            case CharacterState.Moving:
                //Debug.Log("Entered Moving");
                PlayAnimation(runningAnimationState);
                break;
            case CharacterState.Thinking:
                //Debug.Log("Entered Thinking");
                PlayAnimation(thinkingAnimationState);
                break;
            case CharacterState.Talking:
                //Debug.Log("Entered Talking");
                PlayAnimation(talkingAnimationState);
                break;
            case CharacterState.Pointing:
                Debug.Log("Entered Pointing");
                PlayAnimation(pointingAnimationState);
                break;
            case CharacterState.Dancing:
                Debug.Log("Entered Dancing - Celebrating!");
                PlayAnimation(dancingAnimationState);
                break;
        }
    }

    private void OnStateExit(CharacterState state)
    {
        switch (state)
        {
            case CharacterState.Idle:
                break;
            case CharacterState.Moving:
                break;
            case CharacterState.Dancing:
                Debug.Log("Exited Dancing");
                break;
        }
    }

    public void PlayAnimation(string animationStateName)
    {
        if (animator == null)
        {
            return;
        }

        // Method 1: CrossFade (smooth transition)
        animator.CrossFade(animationStateName, transitionDuration);
        //Debug.Log($"Playing animation: {animationStateName}");

        // Method 2: Instant play (no transition)
        // animator.Play(animationStateName);
    }

    public void PlayAnimationAtTime(string animationStateName, float normalizedTime)
    {
        if (animator != null)
        {
            animator.Play(animationStateName, 0, normalizedTime);
        }
    }

    public bool IsPlayingAnimation(string animationStateName)
    {
        if (animator == null) return false;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName(animationStateName);
    }


    public float GetCurrentAnimationTime()
    {
        if (animator == null) return 0f;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.normalizedTime;
    }

    private void Update()
    {
        switch (currentState)
        {
            case CharacterState.Moving:
                if (!moveToPosition.IsMoving())
                {
                    ChangeState(CharacterState.Idle);
                }
                break;
                
            case CharacterState.Dancing:
                // Count down dance timer
                danceTimer -= Time.deltaTime;
                if (danceTimer <= 0f)
                {
                    // Return to idle after dance completes
                    ChangeState(CharacterState.Idle);
                }
                break;
        }
    }

    public CharacterState GetCurrentState() => currentState;
}