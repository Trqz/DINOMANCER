using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RushingEnemy : Enemy
{
    [SerializeField] private float rushAnticipationDuration = 1f;
    [SerializeField] private float rushAimRotationMultiplier = 1f;
    [SerializeField] private float rushRunDuration = 5f;
    [SerializeField] private float rushAcceleration = 50f;
    [SerializeField] private float rushBrakingAcceleration = 2f;
    [SerializeField] private float rushCanStopTimer = 0.2f;

    private Rigidbody _rigidbody;
    private bool isRushing = false;
    private bool isRunning = false;
    private bool isBraking = false;
    private bool canStop = false;

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.isKinematic = true;
    }

    private void Update()
    {
        if (!isRushing)
        {
            if (MoveUntilPlayerInRangeAndOnSight())
                StartCoroutine(Rush());
        }
    }

    private void FixedUpdate()
    {
        if (isRushing)
        {
            UpdateRush();
        }
    }

    private IEnumerator Rush()
    {
        StartRush();Debug.Log("Set");

        yield return new WaitForSeconds(rushAnticipationDuration);

        isRunning = true; Debug.Log("Go!");

        yield return new WaitForSeconds(rushCanStopTimer);

        canStop = true;

        yield return new WaitForSeconds(rushRunDuration - rushCanStopTimer);

        isRunning = false;
        isBraking = true;

        yield return null;
    }

    private void StartRush()
    {
        Vector3 previousVelocity = navMeshAgent.velocity;

        navMeshAgent.isStopped = true;
        navMeshAgent.enabled = false;

        //transform.LookAt(new Vector3(player.transform.position.x, transform.position.y, player.transform.position.z));
        _rigidbody.isKinematic = false;
        _rigidbody.velocity = previousVelocity;

        isRushing = true;
        isRunning = false;
        isBraking = false;
        canStop = false;

        //show player the enemy is about to rush
    }

    private void UpdateRush()
    {
        if (canStop && _rigidbody.velocity.magnitude <= 0.01f)
            StopRush();


        if (isRunning)
            _rigidbody.AddForce(transform.forward * rushAcceleration, ForceMode.Acceleration);
        else if (isBraking)
            _rigidbody.AddForce(_rigidbody.velocity.normalized * -rushBrakingAcceleration, ForceMode.Acceleration);
        else
            LookAtTarget(_rigidbody, new Vector3(player.transform.position.x, transform.position.y, player.transform.position.z), rushAimRotationMultiplier);
    }

    private void StopRush()
    {
        StopCoroutine(Rush());

        _rigidbody.velocity = Vector3.zero;
        _rigidbody.isKinematic = true;
        isRushing = false;

        navMeshAgent.isStopped = false;
        navMeshAgent.enabled = true;      
    }

    private void LookAtTarget(Rigidbody rb, Vector3 targetPosition, float multiplier = 1)
    {
        // Calculate the desired target orientation Quaternion
        Vector3 direction = (targetPosition - rb.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        // Compute the change in orientation we need to impart
        Quaternion rotationChange = targetRotation * Quaternion.Inverse(rb.rotation);

        // Convert to an angle-axis representation, with angle in range -180...180
        rotationChange.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f)
            angle -= 360f;

        // Convert to radians
        angle *= Mathf.Deg2Rad;

        // Compute an angular velocity that will bring us to the target orientation
        // in a single time step (or more if the multiplier is smaller than 1)
        Vector3 targetAngularVelocity = axis * angle / Time.fixedDeltaTime * multiplier;

        // Apply the torque to reach the target orientation using AddTorque
        rb.AddTorque(targetAngularVelocity - rb.angularVelocity, ForceMode.VelocityChange);
    }
}
