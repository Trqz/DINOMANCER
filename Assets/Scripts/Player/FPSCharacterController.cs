using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPSCharacterController : MonoBehaviour
{
    [Header("Basic movement settings")]
    [SerializeField] private float walkingSpeed = 5f;
    [SerializeField] private float runningSpeed = 10f;

    [Header("Run toggling method")]
    [Tooltip("If checked, shift will toggle running. If unchecked, player must hold shift to run.")]
    [SerializeField] public bool runToggle = true;
    [Tooltip("If runToggle is checked, this is the threshold to automatically switch back to walking when the player stops.")]
    [SerializeField] private float stoppedRunningThreshold = 0.01f;
    [Tooltip("This timer is to avoid switching back to walking right after starting to run.")]
    [SerializeField] private float dontStopAfterStartingToRunTimer = 0.5f;
    private float lastStartRunningTime = 0f;

    [Header("Jump and Gravity")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float jumpRequestBuffer = 0.2f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float defaultGravity = -16f;
    [SerializeField] private float maximumFallSpeed = -32f;

    [Header("Ground check")]
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private Vector3 checkOffset;
    [SerializeField] private float checkSphereRadius;
    [Tooltip("Value used to detect if collided with ground or walls.")]
    [SerializeField] private float normalThreshold;

    [Header("Contact damage enemy collision check")]
    [SerializeField] private float contactDamageEnemyCollisionStartCheckingDistance = 3f;
    [SerializeField] private float contactDamageEnemyCollisionCheckMoveDistance = 0.2f;

    [Header("Other")]
    [SerializeField] private Camera _camera;
    [SerializeField] private Health healthScript;   

    private CharacterController _characterController;
    private Vector3 movementInput, treatedInput;
    private float movementSpeed;    
    private bool isRunning = false;
    private bool jumpRequest = false;
    private bool isJumping = false;
    private float currentGravity;
    private float verticalVelocity;
    private float groundAngle;
    private bool isGrounded;
    private bool canJump;
    private float coyoteTimeCounter = 0f;
    private Vector3 checkSpherePosition;
    public List<Enemy> contactDamageEnemies = new List<Enemy>();

    private void Start()
    {
        if(_camera == null)
        {
            Debug.Log("Player camera has not been assigned in Inspector. Do it or performance may be hindered.");
            _camera = Camera.main;
        }

        if (healthScript == null)
        {
            Debug.Log("PlayerHealth Script has not been assigned in Inspector. Do it or performance may be hindered.");
            healthScript = GetComponent<PlayerHealth>();
        }

        _characterController = GetComponent<CharacterController>();

        movementSpeed = walkingSpeed;
        currentGravity = defaultGravity;

        //lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        //DetectCollisions();
        RotateWithCamera();
        GetInput();
        TreatMovementInput();
        ToggleRun();
        GroundCheck();
        CalculateCoyoteTime();
        Jump();
        ApplyGravity();
        HorizontalMovement();
    }

    private void FixedUpdate()
    {
        DetectContactDamageCollisions();
    }

    private void GetInput()
    {
        if (Input.GetButtonDown("Jump"))
            StartCoroutine(RequestJump());

        movementInput.Set(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
    }

    private void ToggleRun()
    {
        if (runToggle)
        {
            if (Input.GetKeyDown(KeyCode.LeftShift))
            {
                if (isRunning)
                    movementSpeed = walkingSpeed;
                else
                {
                    movementSpeed = runningSpeed;

                    lastStartRunningTime = Time.time;
                }     

                isRunning = !isRunning;
            }
            else if((lastStartRunningTime + dontStopAfterStartingToRunTimer) <= Time.time && treatedInput.magnitude < stoppedRunningThreshold)
            {
                movementSpeed = walkingSpeed;

                isRunning = false;
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.LeftShift))
            {
                movementSpeed = runningSpeed;
                isRunning = true;
            }
            if (Input.GetKeyUp(KeyCode.LeftShift))
            {
                movementSpeed = walkingSpeed;
                isRunning = false;
            }
        }
    }

    private void TreatMovementInput()
    {
        //Makes input direction relative to camera and normalized
        treatedInput = Quaternion.Euler(0, _camera.transform.eulerAngles.y, 0) * movementInput;
        treatedInput = new Vector3(treatedInput.x, 0, treatedInput.z);
        if (treatedInput.magnitude > 1)
            treatedInput.Normalize();
    }

    private void HorizontalMovement()
    {
        _characterController.Move(treatedInput * movementSpeed * Time.deltaTime);
    }

    private void Jump()
    {
        if(jumpRequest && canJump)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * currentGravity);

            jumpRequest = false;
            isJumping = true;
        }
    }

    private void RotateWithCamera()
    {
        transform.rotation = Quaternion.Euler(0, _camera.transform.rotation.eulerAngles.y, 0);
    }

    private void GroundCheck()
    {
        Collider[] colliders;

        checkSpherePosition = transform.position + checkOffset;
        
        colliders = Physics.OverlapSphere(checkSpherePosition, checkSphereRadius, groundLayers);

        if (colliders.Length <= 0)
            isGrounded = false;
        else
            foreach(Collider col in colliders)
            {
                Vector3 colliderPosition = col.gameObject.transform.position;

                //check if player collided with ground instead of vertical walls based on angle of collision
                groundAngle = Vector3.Angle(checkSpherePosition, colliderPosition);
                if(groundAngle > 180f + normalThreshold || groundAngle < 180f - normalThreshold)
                {
                    isGrounded = true;

                    //Debug.Log("isGrounded");
                }
            }
    }

    private void CalculateCoyoteTime()
    {
        if (isGrounded)
        {
            isJumping = false;
            canJump = true;
            coyoteTimeCounter = 0f;
            //Debug.Log("_");
        }
        else
        {
            if(!isJumping && coyoteTimeCounter < coyoteTime)
            {
                canJump = true;
                coyoteTimeCounter += Time.deltaTime;
                //Debug.Log("Coyote");
            }
            else
            {
                canJump = false;
                //Debug.Log("NO");
            }
        }
    }

    private void ApplyGravity()
    {
        if (!isGrounded)
            verticalVelocity = Mathf.Clamp(verticalVelocity + defaultGravity * Time.deltaTime, maximumFallSpeed, Mathf.Infinity);
        else
            verticalVelocity = Mathf.Clamp(verticalVelocity + defaultGravity * Time.deltaTime, -2f, Mathf.Infinity);

        _characterController.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    private IEnumerator RequestJump()
    {
        StopCoroutine(RequestJump());

        jumpRequest = true;

        yield return new WaitForSeconds(jumpRequestBuffer);

        jumpRequest = false;

        yield return null;
    }

    //moves player back and forth in the nearest enemy's direction to force OnControllerColliderHit to detect the collision
    private void DetectContactDamageCollisions()
    {
        if (contactDamageEnemies.Count == 0)
            return;
        
        Enemy nearestContactDamageEnemy = contactDamageEnemies[0];

        for (int i = 0; i < contactDamageEnemies.Count; i++)
        {
            if (contactDamageEnemies[i] == null)
            {
                contactDamageEnemies.RemoveAt(i);
                return;
            }

            Enemy enemy = contactDamageEnemies[i];

            if (Vector3.Distance(transform.position, enemy.transform.position) < Vector3.Distance(transform.position, nearestContactDamageEnemy.transform.position))
            {
                nearestContactDamageEnemy = enemy;
            }
        }
        
        if (Vector3.Distance(transform.position, nearestContactDamageEnemy.transform.position) > contactDamageEnemyCollisionStartCheckingDistance)
            return;
        
        Vector3 playerToEnemyDirection = Vector3.Normalize(nearestContactDamageEnemy.transform.position - transform.position);
        playerToEnemyDirection = new Vector3(playerToEnemyDirection.x, 0, playerToEnemyDirection.z);

        _characterController.Move(playerToEnemyDirection * contactDamageEnemyCollisionCheckMoveDistance * Time.fixedDeltaTime);
        _characterController.Move(playerToEnemyDirection * contactDamageEnemyCollisionCheckMoveDistance * -Time.fixedDeltaTime);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Enemy enemyScript = hit.gameObject.GetComponent<Enemy>();

        if(enemyScript != null)
        {
            Debug.Log("hit");
            enemyScript.ContactDamage(healthScript);
        }
    }
}
