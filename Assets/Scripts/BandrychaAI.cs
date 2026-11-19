using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Bandrycha: patrols random reachable points, chases the player when she sees them
/// (within visionRadius and with a clear raycast), and triggers the jumpscare on contact.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class BandrychaAI : MonoBehaviour
{
    enum State { Patrol, Chase, Caught }

    [Header("References")]
    public PlayerController player;
    public Animator animator;
    public GameOverScreen gameOverScreen;

    [Header("Speeds")]
    public float patrolSpeed = 1.6f;
    public float chaseSpeed = 4.6f;

    [Header("Vision")]
    public float visionRadius = 15f;
    public float eyeHeight = 1.38f;
    [Tooltip("How long she keeps hunting the last known position after losing sight.")]
    public float loseSightTime = 4f;
    public LayerMask visionMask = ~0;

    [Header("Patrol")]
    public float patrolPointRadius = 35f;
    public float waypointTolerance = 1f;
    public float pauseAtWaypoint = 1.5f;

    [Header("Catch / Jumpscare")]
    public float catchDistance = 1.5f;
    public float gameOverDelay = 1.5f;
    public Color screamerLightColor = new Color(1f, 0.05f, 0.02f);

    [Header("Animation")]
    [Tooltip("Agent speed (m/s) at which the walk clip plays at normal speed.")]
    public float animReferenceSpeed = 1.4f;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int AnimSpeedHash = Animator.StringToHash("AnimSpeed");

    NavMeshAgent agent;
    State state = State.Patrol;
    float lastSeenTime = -999f;
    Vector3 lastSeenPosition;
    float pauseTimer;
    float repathTimer;
    bool ready;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponent<Animator>();
        // Only the NavMeshAgent may move her; animation root motion must never push her through walls.
        foreach (var a in GetComponentsInChildren<Animator>(true)) a.applyRootMotion = false;
        agent.updatePosition = true;
        agent.updateRotation = true;
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (gameOverScreen == null) gameOverScreen = FindAnyObjectByType<GameOverScreen>();
        agent.enabled = false;
    }

    IEnumerator Start()
    {
        while (!RuntimeNavMeshBaker.IsReady) yield return null;

        if (!NavMesh.SamplePosition(transform.position, out var hit, 10f, NavMesh.AllAreas))
        {
            Debug.LogError("BandrychaAI: no NavMesh near spawn point " + transform.position);
            yield break;
        }
        transform.position = hit.position;
        agent.enabled = true;
        agent.Warp(hit.position);
        agent.speed = patrolSpeed;
        ready = true;
        PickPatrolPoint();
    }

    void Update()
    {
        if (!ready) return;
        UpdateAnimator();
        if (state == State.Caught || player == null) return;

        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.magnitude <= catchDistance)
        {
            StartCoroutine(Jumpscare());
            return;
        }

        if (CanSeePlayer())
        {
            lastSeenTime = Time.time;
            lastSeenPosition = player.transform.position;
            state = State.Chase;
        }

        if (state == State.Chase) UpdateChase();
        else UpdatePatrol();
    }

    bool CanSeePlayer()
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.cameraPivot != null ? player.cameraPivot.position : player.transform.position + Vector3.up * 1.5f;
        Vector3 dir = target - eye;
        float dist = dir.magnitude;
        if (dist > visionRadius) return false;

        if (Physics.Raycast(eye, dir / dist, out var hit, dist, visionMask, QueryTriggerInteraction.Ignore))
            return hit.transform == player.transform || hit.transform.IsChildOf(player.transform);
        return true;
    }

    void UpdateChase()
    {
        agent.speed = chaseSpeed;
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = 0.2f;
            agent.SetDestination(lastSeenPosition);
        }

        if (Time.time - lastSeenTime > loseSightTime &&
            !agent.pathPending && agent.remainingDistance <= waypointTolerance)
        {
            state = State.Patrol;
            PickPatrolPoint();
        }
    }

    void UpdatePatrol()
    {
        agent.speed = patrolSpeed;
        if (agent.pathPending) return;

        if (!agent.hasPath || agent.remainingDistance <= waypointTolerance)
        {
            pauseTimer -= Time.deltaTime;
            if (pauseTimer <= 0f) PickPatrolPoint();
        }
    }

    void PickPatrolPoint()
    {
        pauseTimer = pauseAtWaypoint;
        var bounds = RuntimeNavMeshBaker.WalkBounds;
        var path = new NavMeshPath();

        for (int i = 0; i < 30; i++)
        {
            // Mostly nearby points, sometimes anywhere on the map so she roams the whole level.
            Vector3 candidate;
            if (i % 3 == 2)
                candidate = new Vector3(Random.Range(bounds.min.x, bounds.max.x), transform.position.y, Random.Range(bounds.min.z, bounds.max.z));
            else
            {
                Vector2 r = Random.insideUnitCircle * patrolPointRadius;
                candidate = transform.position + new Vector3(r.x, 0f, r.y);
            }

            if (!NavMesh.SamplePosition(candidate, out var hit, 3f, NavMesh.AllAreas)) continue;
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;

            agent.SetPath(path);
            return;
        }
    }

    void UpdateAnimator()
    {
        if (animator == null) return;
        float speed = agent.enabled ? agent.velocity.magnitude : 0f;
        animator.SetFloat(SpeedHash, speed);
        animator.SetFloat(AnimSpeedHash, Mathf.Max(0.3f, speed / animReferenceSpeed));
    }

    IEnumerator Jumpscare()
    {
        state = State.Caught;
        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;

        Vector3 look = player.transform.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(look);

        Transform head = null;
        if (animator != null && animator.isHuman) head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null)
        {
            head = new GameObject("FaceTarget").transform;
            head.SetParent(transform, false);
            head.localPosition = Vector3.up * eyeHeight;
        }

        var lightGO = new GameObject("ScreamerLight");
        lightGO.transform.SetParent(transform, true);
        lightGO.transform.position = head.position + transform.forward * 0.5f;
        var red = lightGO.AddComponent<Light>();
        red.type = LightType.Point;
        red.color = screamerLightColor;
        red.range = 5f;
        red.intensity = 6f;
        red.shadows = LightShadows.None;
        StartCoroutine(Blink(red));

        player.Caught(head);

        yield return new WaitForSeconds(gameOverDelay);
        if (gameOverScreen != null) gameOverScreen.Show();
    }

    IEnumerator Blink(Light l)
    {
        while (l != null)
        {
            l.enabled = !l.enabled;
            l.intensity = Random.Range(4f, 9f);
            yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, visionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);
    }
}
