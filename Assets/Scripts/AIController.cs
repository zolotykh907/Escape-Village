using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class AIController : MonoBehaviour {

    public enum AwarenessState {
        Idle,
        Investigate,
        Chase
    }

    public NavMeshAgent agent;
    public GameObject target;
    public float attackDistance = 2.2f;
    public float attackCooldown = 0.75f;
    public float repathInterval = 0.15f;
    public float manualCommandDuration = 3.0f;
    public bool usePerception = true;
    public float sightRadius = 12.0f;
    public float sightAngle = 110.0f;
    public float eyeHeight = 1.25f;
    public float targetEyeHeight = 1.1f;
    public LayerMask lineOfSightMask = ~0;
    public float targetMemoryDuration = 1.2f;
    public float investigateArriveDistance = 1.25f;
    public float searchDuration = 2.0f;
    [Header("Patrol And Search")]
    public bool patrolWhenIdle = true;
    public float patrolRadius = 9.0f;
    public float patrolWaitTime = 1.3f;
    public float patrolArriveDistance = 1.2f;
    public bool searchAroundLastKnown = true;
    public float searchRadius = 5.0f;
    public float searchStepInterval = 1.2f;
    public float navMeshSampleRadius = 2.0f;
    [Header("In Game Indicator")]
    public bool showAwarenessIndicator = true;
    public bool showIdleIndicator = true;
    public float indicatorHeight = 2.55f;
    public float indicatorSize = 0.28f;
    public float indicatorPulseSpeed = 4.5f;
    public Color idleIndicatorColor = new Color(0.22f, 1.0f, 0.35f, 1.0f);
    public Color investigateIndicatorColor = new Color(1.0f, 0.75f, 0.08f, 1.0f);
    public Color chaseIndicatorColor = new Color(1.0f, 0.12f, 0.05f, 1.0f);
#if UNITY_EDITOR
    [Header("Debug")]
    public bool drawDebugGizmos = true;
    public bool drawDebugOnlyWhenSelected = true;
    public float debugLabelHeight = 2.4f;
    public Color debugSightColor = new Color(1.0f, 0.85f, 0.2f, 0.65f);
    public Color debugWalkNoiseColor = new Color(0.2f, 0.7f, 1.0f, 0.45f);
    public Color debugSprintNoiseColor = new Color(1.0f, 0.35f, 0.1f, 0.5f);
    public Color debugCurrentNoiseColor = new Color(1.0f, 1.0f, 0.15f, 0.8f);
    public Color debugLastKnownColor = new Color(0.9f, 0.2f, 1.0f, 0.8f);
#endif
    public AwarenessState CurrentState {
        get { return awarenessState; }
    }

    Animator anim;
    PlayerController targetPlayer;
    AwarenessState awarenessState = AwarenessState.Idle;
    Vector3 homePosition;
    Vector3 manualDestination;
    Vector3 lastKnownTargetPosition;
    Vector3 patrolDestination;
    Vector3 searchDestination;
    bool hasHomePosition;
    bool hasManualDestination;
    bool hasLastKnownTargetPosition;
    bool hasPatrolDestination;
    bool hasSearchDestination;
    bool targetVisibleThisFrame;
    float manualDestinationUntil;
    float nextAttackTime;
    float nextRepathTime;
    float lastDetectedTime = -100f;
    float searchUntil = -100f;
    float patrolWaitUntil = -100f;
    float nextSearchStepTime = -100f;
    Transform awarenessIndicatorRoot;
    Transform awarenessIndicatorCore;
    Renderer awarenessIndicatorRenderer;
    Material awarenessIndicatorMaterial;
    Color currentIndicatorColor;

    void Start() {
        CacheComponents();
        CaptureHomePosition();
        ResolveTarget();
    }

    void LateUpdate() {
        UpdateAwarenessIndicator();
    }

    void OnDestroy() {
        if (awarenessIndicatorMaterial != null) {
            Destroy(awarenessIndicatorMaterial);
        }
    }

    void Update() {
        CacheComponents();
        CaptureHomePosition();

        if (agent == null || !agent.enabled || !agent.isOnNavMesh) {
            SetMoving(false);
            return;
        }

        if (target == null) {
            ResolveTarget();
        }

        if (targetPlayer != null && targetPlayer.IsDead) {
            SetAwareness(AwarenessState.Idle);
            StopMoving();
            return;
        }

        if (HandleManualDestination()) {
            return;
        }

        if (target == null) {
            SetAwareness(AwarenessState.Idle);
            StopMoving();
            return;
        }

        if (!usePerception) {
            DirectChaseTarget();
            return;
        }

        UpdatePerception();
        UpdateAwarenessMovement();
        UpdateMovementAnimation();
    }

    public void SetManualDestination(Vector3 destination) {
        CacheComponents();

        if (agent == null || !agent.enabled || !agent.isOnNavMesh) {
            return;
        }

        manualDestination = destination;
        hasManualDestination = true;
        manualDestinationUntil = Time.time + manualCommandDuration;
        BeginInvestigating(manualDestination);
        MoveTo(manualDestination);
    }

    public void ResumeChase() {
        hasManualDestination = false;
        if (CanSeeTarget()) {
            NoticeTarget(target.transform.position);
        } else {
            SetAwareness(AwarenessState.Idle);
        }
    }

    void CacheComponents() {
        if (agent == null) {
            agent = GetComponent<NavMeshAgent>();
        }

        if (anim == null) {
            anim = GetComponent<Animator>();
        }

        if (targetPlayer == null && target != null) {
            targetPlayer = target.GetComponent<PlayerController>();
        }
    }

    void CaptureHomePosition() {
        if (hasHomePosition) {
            return;
        }

        homePosition = transform.position;
        hasHomePosition = true;
    }

    void ResolveTarget() {
        if (target != null) {
            targetPlayer = target.GetComponent<PlayerController>();
            return;
        }

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null) {
            targetPlayer = player;
            target = player.gameObject;
        }
    }

    bool HandleManualDestination() {
        if (!hasManualDestination) {
            return false;
        }

        if (Time.time <= manualDestinationUntil) {
            MoveTo(manualDestination);
            UpdateMovementAnimation();
            return true;
        }

        hasManualDestination = false;
        BeginInvestigating(manualDestination);
        return false;
    }

    void UpdatePerception() {
        targetVisibleThisFrame = CanSeeTarget();
        if (targetVisibleThisFrame) {
            NoticeTarget(target.transform.position);
            return;
        }

        Vector3 heardPosition;
        if (CanHearTarget(out heardPosition)) {
            lastKnownTargetPosition = heardPosition;
            hasLastKnownTargetPosition = true;
            lastDetectedTime = Time.time;
            searchUntil = -100f;

            if (awarenessState != AwarenessState.Chase) {
                SetAwareness(AwarenessState.Investigate);
            }

            return;
        }

        if (awarenessState == AwarenessState.Chase && Time.time - lastDetectedTime > targetMemoryDuration) {
            BeginInvestigating(lastKnownTargetPosition);
        }
    }

    void UpdateAwarenessMovement() {
        if (awarenessState == AwarenessState.Idle) {
            UpdatePatrolMovement();
            return;
        }

        if (!hasLastKnownTargetPosition) {
            SetAwareness(AwarenessState.Idle);
            UpdatePatrolMovement();
            return;
        }

        if (awarenessState == AwarenessState.Chase) {
            float effectiveAttackDistance = Mathf.Max(attackDistance, agent.stoppingDistance + 0.1f);
            if (PlanarDistance(transform.position, target.transform.position) <= effectiveAttackDistance && HasLineOfSightToTarget()) {
                StopMoving();
                TryDamageTarget();
                return;
            }

            MoveTo(lastKnownTargetPosition);
            return;
        }

        if (searchUntil >= 0f) {
            UpdateSearchMovement();
            return;
        }

        MoveTo(lastKnownTargetPosition);
        if (!HasReachedInvestigationPoint()) {
            return;
        }

        searchUntil = Time.time + searchDuration;
        hasSearchDestination = false;
        nextSearchStepTime = -100f;
        UpdateSearchMovement();
    }

    void UpdatePatrolMovement() {
        if (!patrolWhenIdle) {
            StopMoving();
            return;
        }

        if (Time.time < patrolWaitUntil) {
            StopMoving();
            return;
        }

        if (!hasPatrolDestination) {
            if (TryFindNavMeshPoint(homePosition, patrolRadius, out patrolDestination)) {
                hasPatrolDestination = true;
            } else {
                StopMoving();
                return;
            }
        }

        MoveTo(patrolDestination);
        if (!HasReachedDestination(patrolDestination, patrolArriveDistance)) {
            return;
        }

        hasPatrolDestination = false;
        patrolWaitUntil = Time.time + patrolWaitTime;
        StopMoving();
    }

    void UpdateSearchMovement() {
        if (Time.time >= searchUntil) {
            hasLastKnownTargetPosition = false;
            hasSearchDestination = false;
            searchUntil = -100f;
            SetAwareness(AwarenessState.Idle);
            UpdatePatrolMovement();
            return;
        }

        if (!searchAroundLastKnown) {
            StopMoving();
            return;
        }

        bool needsSearchDestination = !hasSearchDestination
            || HasReachedDestination(searchDestination, investigateArriveDistance)
            || Time.time >= nextSearchStepTime;

        if (needsSearchDestination) {
            if (!TryFindNavMeshPoint(lastKnownTargetPosition, searchRadius, out searchDestination)) {
                StopMoving();
                return;
            }

            hasSearchDestination = true;
            nextSearchStepTime = Time.time + searchStepInterval;
        }

        MoveTo(searchDestination);
    }

    void DirectChaseTarget() {
        float distance = PlanarDistance(transform.position, target.transform.position);
        float effectiveAttackDistance = Mathf.Max(attackDistance, agent.stoppingDistance + 0.1f);

        if (distance <= effectiveAttackDistance) {
            StopMoving();
            TryDamageTarget();
            return;
        }

        if (Time.time >= nextRepathTime) {
            MoveTo(target.transform.position);
            nextRepathTime = Time.time + repathInterval;
        }

        UpdateMovementAnimation();
    }

    void NoticeTarget(Vector3 position) {
        lastKnownTargetPosition = position;
        hasLastKnownTargetPosition = true;
        lastDetectedTime = Time.time;
        searchUntil = -100f;
        ClearSearch();
        ClearPatrol();
        SetAwareness(AwarenessState.Chase);
    }

    void BeginInvestigating(Vector3 position) {
        lastKnownTargetPosition = position;
        hasLastKnownTargetPosition = true;
        searchUntil = -100f;
        ClearSearch();
        ClearPatrol();
        SetAwareness(AwarenessState.Investigate);
    }

    void SetAwareness(AwarenessState nextState) {
        if (awarenessState == nextState) {
            return;
        }

        awarenessState = nextState;

        if (nextState != AwarenessState.Idle) {
            ClearPatrol();
        }

        if (nextState == AwarenessState.Idle) {
            ClearSearch();
            searchUntil = -100f;
        } else if (nextState == AwarenessState.Chase) {
            ClearSearch();
            searchUntil = -100f;
        }
    }

    bool CanSeeTarget() {
        if (target == null) {
            return false;
        }

        Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPosition = target.transform.position + Vector3.up * targetEyeHeight;
        Vector3 toTarget = targetPosition - eyePosition;
        if (toTarget.magnitude > sightRadius) {
            return false;
        }

        Vector3 planarDirection = target.transform.position - transform.position;
        planarDirection.y = 0f;
        if (planarDirection.sqrMagnitude <= 0.0001f) {
            return HasLineOfSightToTarget();
        }

        float angle = Vector3.Angle(transform.forward, planarDirection.normalized);
        if (angle > sightAngle * 0.5f) {
            return false;
        }

        return HasLineOfSightToTarget();
    }

    bool CanHearTarget(out Vector3 heardPosition) {
        heardPosition = Vector3.zero;
        if (target == null || targetPlayer == null) {
            return false;
        }

        float noiseRadius = targetPlayer.CurrentNoiseRadius;
        if (noiseRadius <= 0.001f) {
            return false;
        }

        if (PlanarDistance(transform.position, target.transform.position) > noiseRadius) {
            return false;
        }

        heardPosition = target.transform.position;
        return true;
    }

    bool HasLineOfSightToTarget() {
        if (target == null) {
            return false;
        }

        Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPosition = target.transform.position + Vector3.up * targetEyeHeight;
        Vector3 toTarget = targetPosition - eyePosition;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f) {
            return true;
        }

        RaycastHit[] hits = Physics.RaycastAll(
            eyePosition,
            toTarget / distance,
            distance,
            lineOfSightMask,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits) {
            if (IsIgnoredSightCollider(hit.collider)) {
                continue;
            }

            return false;
        }

        return true;
    }

    bool IsIgnoredSightCollider(Collider hitCollider) {
        return hitCollider == null
            || hitCollider.transform.IsChildOf(transform)
            || (target != null && hitCollider.transform.IsChildOf(target.transform));
    }

    bool HasReachedInvestigationPoint() {
        return HasReachedDestination(lastKnownTargetPosition, investigateArriveDistance);
    }

    bool HasReachedDestination(Vector3 destination, float arriveDistance) {
        if (agent.pathPending) {
            return false;
        }

        float distance = agent.hasPath
            ? agent.remainingDistance
            : PlanarDistance(transform.position, destination);
        return distance <= Mathf.Max(agent.stoppingDistance + 0.1f, arriveDistance);
    }

    bool TryFindNavMeshPoint(Vector3 center, float radius, out Vector3 point) {
        float effectiveRadius = Mathf.Max(0.1f, radius);
        float effectiveSampleRadius = Mathf.Max(0.1f, navMeshSampleRadius);

        for (int i = 0; i < 12; i++) {
            Vector2 offset = Random.insideUnitCircle * effectiveRadius;
            Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(candidate, out hit, effectiveSampleRadius, NavMesh.AllAreas)) {
                point = hit.position;
                return true;
            }
        }

        NavMeshHit fallbackHit;
        if (NavMesh.SamplePosition(center, out fallbackHit, effectiveRadius + effectiveSampleRadius, NavMesh.AllAreas)) {
            point = fallbackHit.position;
            return true;
        }

        point = center;
        return false;
    }

    void ClearPatrol() {
        hasPatrolDestination = false;
        patrolWaitUntil = -100f;
        nextRepathTime = -100f;
    }

    void ClearSearch() {
        hasSearchDestination = false;
        nextSearchStepTime = -100f;
        nextRepathTime = -100f;
    }

    void MoveTo(Vector3 destination) {
        if (Time.time < nextRepathTime && agent.hasPath) {
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(destination);
        nextRepathTime = Time.time + repathInterval;
    }

    void StopMoving() {
        if (agent != null && agent.enabled && agent.isOnNavMesh) {
            agent.isStopped = true;
        }

        SetMoving(false);
    }

    void UpdateMovementAnimation() {
        bool moving = agent != null
            && agent.enabled
            && agent.isOnNavMesh
            && !agent.isStopped
            && !agent.pathPending
            && agent.remainingDistance > agent.stoppingDistance + 0.1f;

        SetMoving(moving);
    }

    void SetMoving(bool moving) {
        if (anim != null) {
            anim.SetBool("isMoving", moving);
        }
    }

    void UpdateAwarenessIndicator() {
        if (!showAwarenessIndicator || (!showIdleIndicator && awarenessState == AwarenessState.Idle)) {
            if (awarenessIndicatorRoot != null) {
                awarenessIndicatorRoot.gameObject.SetActive(false);
            }

            return;
        }

        EnsureAwarenessIndicator();
        if (awarenessIndicatorRoot == null) {
            return;
        }

        if (!awarenessIndicatorRoot.gameObject.activeSelf) {
            awarenessIndicatorRoot.gameObject.SetActive(true);
        }

        awarenessIndicatorRoot.localPosition = Vector3.up * indicatorHeight;
        FaceIndicatorToCamera();

        Color targetColor = GetIndicatorColor();
        float colorLerp = 1.0f - Mathf.Exp(-14.0f * Time.unscaledDeltaTime);
        currentIndicatorColor = Color.Lerp(currentIndicatorColor, targetColor, colorLerp);
        ApplyIndicatorColor(currentIndicatorColor);

        float pulseStrength = awarenessState == AwarenessState.Chase ? 0.16f : 0.07f;
        float pulse = 1.0f + Mathf.Sin(Time.unscaledTime * indicatorPulseSpeed) * pulseStrength;
        awarenessIndicatorCore.localScale = Vector3.one * Mathf.Max(0.01f, indicatorSize * pulse);
    }

    void EnsureAwarenessIndicator() {
        if (awarenessIndicatorRoot != null) {
            return;
        }

        GameObject root = new GameObject("Awareness Indicator");
        root.transform.SetParent(transform, false);
        awarenessIndicatorRoot = root.transform;

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Indicator Core";
        core.transform.SetParent(awarenessIndicatorRoot, false);
        awarenessIndicatorCore = core.transform;

        Collider coreCollider = core.GetComponent<Collider>();
        if (coreCollider != null) {
            Destroy(coreCollider);
        }

        awarenessIndicatorRenderer = core.GetComponent<Renderer>();
        awarenessIndicatorMaterial = CreateIndicatorMaterial(idleIndicatorColor);
        if (awarenessIndicatorRenderer != null) {
            awarenessIndicatorRenderer.sharedMaterial = awarenessIndicatorMaterial;
        }

        currentIndicatorColor = idleIndicatorColor;
        UpdateAwarenessIndicator();
    }

    Material CreateIndicatorMaterial(Color color) {
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null) {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.name = "Awareness Indicator Material";
        ApplyMaterialColor(material, color);
        return material;
    }

    void FaceIndicatorToCamera() {
        Camera camera = Camera.main;
        if (camera == null) {
            camera = FindObjectOfType<Camera>();
        }

        if (camera != null) {
            awarenessIndicatorRoot.rotation = camera.transform.rotation;
        }
    }

    Color GetIndicatorColor() {
        switch (awarenessState) {
            case AwarenessState.Chase:
                return chaseIndicatorColor;
            case AwarenessState.Investigate:
                return investigateIndicatorColor;
            default:
                return idleIndicatorColor;
        }
    }

    void ApplyIndicatorColor(Color color) {
        if (awarenessIndicatorRenderer != null) {
            ApplyMaterialColor(awarenessIndicatorMaterial, color);
        }
    }

    void ApplyMaterialColor(Material material, Color color) {
        if (material == null) {
            return;
        }

        if (material.HasProperty("_Color")) {
            material.color = color;
        }

        if (material.HasProperty("_BaseColor")) {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_EmissionColor")) {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.4f);
        }
    }

    void TryDamageTarget() {
        if (Time.time < nextAttackTime) {
            return;
        }

        nextAttackTime = Time.time + attackCooldown;

        if (targetPlayer == null && target != null) {
            targetPlayer = target.GetComponent<PlayerController>();
        }

        if (targetPlayer != null) {
            targetPlayer.TakeDamage(1, transform);
        }
    }

    float PlanarDistance(Vector3 a, Vector3 b) {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

#if UNITY_EDITOR
    void OnDrawGizmos() {
        if (!drawDebugOnlyWhenSelected) {
            DrawDebugGizmos();
        }
    }

    void OnDrawGizmosSelected() {
        if (drawDebugOnlyWhenSelected) {
            DrawDebugGizmos();
        }
    }

    void DrawDebugGizmos() {
        if (!drawDebugGizmos) {
            return;
        }

        Vector3 groundPosition = transform.position;
        PlayerController debugPlayer = GetDebugPlayer();

        DrawSightCone(groundPosition);
        DrawNoiseRadii(groundPosition, debugPlayer);
        DrawTargetDebug(debugPlayer);
        DrawLastKnownDebug();
        DrawStateLabel(debugPlayer);
    }

    void DrawSightCone(Vector3 origin) {
        float clampedAngle = Mathf.Clamp(sightAngle, 0f, 360f);
        Vector3 leftDirection = Quaternion.Euler(0f, -clampedAngle * 0.5f, 0f) * transform.forward;
        Vector3 rightDirection = Quaternion.Euler(0f, clampedAngle * 0.5f, 0f) * transform.forward;

        Handles.color = WithAlpha(debugSightColor, 0.08f);
        Handles.DrawSolidArc(origin, Vector3.up, leftDirection, clampedAngle, sightRadius);
        Handles.color = debugSightColor;
        Handles.DrawWireArc(origin, Vector3.up, leftDirection, clampedAngle, sightRadius);
        Handles.DrawLine(origin, origin + leftDirection.normalized * sightRadius);
        Handles.DrawLine(origin, origin + rightDirection.normalized * sightRadius);
    }

    void DrawNoiseRadii(Vector3 origin, PlayerController debugPlayer) {
        if (debugPlayer == null) {
            return;
        }

        DrawGroundDisc(origin, debugPlayer.walkingNoiseRadius, debugWalkNoiseColor);
        DrawGroundDisc(origin, debugPlayer.sprintNoiseRadius, debugSprintNoiseColor);

        if (Application.isPlaying && debugPlayer.CurrentNoiseRadius > 0.001f) {
            DrawGroundDisc(debugPlayer.transform.position, debugPlayer.CurrentNoiseRadius, debugCurrentNoiseColor);
        }
    }

    void DrawTargetDebug(PlayerController debugPlayer) {
        if (target == null && debugPlayer == null) {
            return;
        }

        Transform targetTransform = target != null ? target.transform : debugPlayer.transform;
        Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPosition = targetTransform.position + Vector3.up * targetEyeHeight;
        bool canSeeNow = target != null && CanSeeTarget();
        Color lineColor = canSeeNow ? Color.green : Color.red;
        Handles.color = WithAlpha(lineColor, 0.8f);
        Handles.DrawDottedLine(eyePosition, targetPosition, 4.0f);
    }

    void DrawLastKnownDebug() {
        if (!Application.isPlaying || !hasLastKnownTargetPosition) {
            return;
        }

        float markerSize = 0.45f;
        Vector3 position = lastKnownTargetPosition + Vector3.up * 0.05f;
        Handles.color = debugLastKnownColor;
        Handles.DrawLine(position + Vector3.left * markerSize, position + Vector3.right * markerSize);
        Handles.DrawLine(position + Vector3.forward * markerSize, position + Vector3.back * markerSize);
        Handles.Label(position + Vector3.up * 0.25f, "Last known");
    }

    void DrawStateLabel(PlayerController debugPlayer) {
        Color stateColor = GetDebugStateColor();
        string status = Application.isPlaying ? awarenessState.ToString() : "AI Debug";

        if (Application.isPlaying) {
            if (hasManualDestination) {
                status += " / Manual";
            } else if (targetVisibleThisFrame) {
                status += " / Visible";
            } else if (awarenessState == AwarenessState.Idle && patrolWhenIdle) {
                status += " / Patrol";
            } else if (awarenessState == AwarenessState.Investigate && searchUntil >= 0f) {
                status += " / Searching";
            } else if (hasLastKnownTargetPosition && awarenessState != AwarenessState.Idle) {
                status += " / Last known";
            }

            if (debugPlayer != null && debugPlayer.CurrentNoiseRadius > 0.001f) {
                status += " / Noise " + debugPlayer.CurrentNoiseRadius.ToString("0.0");
            }
        }

        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
        labelStyle.normal.textColor = stateColor;
        Handles.Label(transform.position + Vector3.up * debugLabelHeight, status, labelStyle);
    }

    PlayerController GetDebugPlayer() {
        if (targetPlayer != null) {
            return targetPlayer;
        }

        if (target != null) {
            return target.GetComponent<PlayerController>();
        }

        return FindObjectOfType<PlayerController>();
    }

    Color GetDebugStateColor() {
        switch (awarenessState) {
            case AwarenessState.Chase:
                return Color.red;
            case AwarenessState.Investigate:
                return new Color(1.0f, 0.75f, 0.05f);
            default:
                return Color.white;
        }
    }

    void DrawGroundDisc(Vector3 center, float radius, Color color) {
        if (radius <= 0.001f) {
            return;
        }

        Handles.color = color;
        Handles.DrawWireDisc(center, Vector3.up, radius);
    }

    Color WithAlpha(Color color, float alpha) {
        color.a = alpha;
        return color;
    }
#endif
}
