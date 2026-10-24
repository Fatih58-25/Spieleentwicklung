using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class RobotAI : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;
    private Transform player;

    [Header("Detection & Ranges")]
    public float detectionRange = 15f;   
    public float stopRange = 3.5f;         

    [Header("Speed Settings")]
    public float approachSpeed = 3.5f;   
    public float flankSpeedValue = 6.0f; 

    [Header("Flanking (Circle) Settings")]
    public float flankRadius = 6.0f; 
    private float currentAngle = 0f; 

    private enum State { Idle, Approach, Flank, HoldPosition }
    private State currentState = State.Idle;
    
    private float stateTimer = 0f;
    private float orbitDirection = 2f; 

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        
        agent.updateRotation = false;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }

    void Update()
    {
        if (agent == null || !agent.isOnNavMesh) return;
        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // --- ANİMASYON HIZI KONTROLÜ (Düzeltilen Kısım) ---
        float targetAnimSpeed = 0f;
        if (currentState == State.Approach) targetAnimSpeed = 0.5f;      // Yürüme
        else if (currentState == State.Flank) targetAnimSpeed = 1.0f;    // Hızlı Koşma
        else targetAnimSpeed = 0f;                                       // Idle

        float currentAnimSpeed = Mathf.Lerp(animator.GetFloat("Speed"), targetAnimSpeed, 10f * Time.deltaTime);
        animator.SetFloat("Speed", currentAnimSpeed);
        // --------------------------------------------------

        switch (currentState)
        {
            case State.Idle:
                agent.isStopped = true;
                FaceTarget(player.position);

                if (distanceToPlayer <= detectionRange)
                {
                    currentState = State.Approach;
                }
                break;

            case State.Approach:
                agent.isStopped = false;
                agent.speed = approachSpeed; 
                agent.SetDestination(player.position);
                
                FaceMovementDirection();

                if (distanceToPlayer <= stopRange + 2f)
                {
                    currentState = State.Flank;
                    stateTimer = 6f; 
                    
                    Vector3 dir = transform.position - player.position;
                    currentAngle = Mathf.Atan2(dir.z, dir.x);
                }
                break;

            case State.Flank:
                agent.isStopped = false;
                agent.speed = flankSpeedValue; 

                currentAngle += orbitDirection * Time.deltaTime;
                float posX = player.position.x + Mathf.Cos(currentAngle) * flankRadius;
                float posZ = player.position.z + Mathf.Sin(currentAngle) * flankRadius;
                Vector3 flankPosition = new Vector3(posX, player.position.y, posZ);

                agent.SetDestination(flankPosition);
                FaceMovementDirection();

                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    orbitDirection *= -1f; 
                    currentState = State.HoldPosition;
                    stateTimer = 2f; 
                }
                break;

            case State.HoldPosition:
                agent.isStopped = true;
                FaceTarget(player.position);

                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    currentState = State.Flank;
                    stateTimer = 6f;
                }
                break;
        }

        if (distanceToPlayer > detectionRange + 5f && currentState != State.Idle)
        {
            currentState = State.Idle;
        }
    }

    void FaceMovementDirection()
    {
        if (agent.velocity.sqrMagnitude > 0.1f)
        {
            Vector3 direction = agent.velocity.normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(-direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
            }
        }
    }

    void FaceTarget(Vector3 targetPos)
    {
        Vector3 direction = (targetPos - transform.position);
        direction.y = 0; 
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(-direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
        }
    }
}