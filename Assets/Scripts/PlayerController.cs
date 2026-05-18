using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour {

    Rigidbody rb;
    Animator animator;
    CapsuleCollider capsuleCollider;
    Camera gameplayCamera;
    bool deathTriggered;
    float controlledYaw;
    Vector3 smoothedPlanarVelocity;

    public float speed = 10.0F;
    public float sprintMultiplier = 1.7f;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode jumpKey = KeyCode.Space;
    public float jumpVelocity = 6.2f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.16f;
    public float jumpGroundCheckDistance = 0.22f;
    public float airborneControlMultiplier = 0.85f;
    public float jumpAnimationDuration = 0.42f;
    public float jumpPoseStrength = 1.0f;
    public float rotationSpeed = 720.0F;
    public bool moveRelativeToCamera = true;
    public Transform cameraTransform;
    public float acceleration = 32.0f;
    public float deceleration = 42.0f;
    public float inputDeadZone = 0.08f;
    public float walkingNoiseRadius = 4.0f;
    public float sprintNoiseRadius = 10.0f;
    public float airborneNoiseRadius = 6.0f;
    public bool useRawInput = false;
    public float maxStepHeight = 0.85f;
    public float groundSnapDistance = 1.15f;
    public float maxSlopeAngle = 60.0f;
    public float groundProbeRadius = 0.22f;
    public float skinWidth = 0.04f;
    public LayerMask collisionMask = ~0;
    public bool constrainToNavMesh = true;
    public bool allowOffNavMeshJumpLandings = true;
    public float navMeshCheckRadius = 1.2f;
    public float maxNavMeshSnapDistance = 0.35f;
    public float navMeshVerticalTolerance = 2.2f;
    public float offNavMeshSurfaceHeightTolerance = 0.12f;
    public float offNavMeshGroundCheckDistance = 0.45f;
    public float jumpLedgeAssistHeight = 1.15f;
    public float jumpLedgeOverstep = 0.38f;
    public int maxHealth = 2;
    public int aiContactDamage = 1;
    public float damageInvulnerabilityDuration = 0.45f;
    public float damageReactionDuration = 0.32f;
    public float damagePoseStrength = 1.0f;
    public float damageKnockbackImpulse = 2.4f;
    public bool showBloodOnDamage = true;
    public Color bloodColor = new Color(0.55f, 0.0f, 0.0f, 1.0f);
    public int bloodParticleCount = 24;
    public float bloodSpawnHeight = 1.15f;
    public float bloodSpawnRadius = 0.08f;
    public float bloodParticleLifetime = 0.48f;
    public float bloodParticleSpeed = 3.2f;
    public float bloodParticleSize = 0.07f;
    public float bloodConeAngle = 28.0f;
    public float bloodEffectDuration = 1.2f;
    public bool renderBloodAsSmoothMeshes = true;
    bool jumpQueued;
    float jumpQueuedAt = -100f;
    bool isGrounded;
    float lastGroundedTime = -100f;
    float jumpPoseTime;
    int currentHealth;
    float nextDamageTime = -100f;
    float damagePoseTime;
    Vector3 damageReactionDirection = Vector3.back;
    Transform hipsBone;
    Transform spineBone;
    Transform leftUpLegBone;
    Transform rightUpLegBone;
    Transform leftLegBone;
    Transform rightLegBone;
    Transform leftFootBone;
    Transform rightFootBone;
    Material bloodParticleMaterial;
    Mesh bloodParticleMesh;
    static public bool dead = false;
    public bool IsSprintingNow { get; private set; }

    public bool IsDead {
        get { return dead; }
    }

    public bool IsGrounded {
        get { return isGrounded; }
    }

    public int CurrentHealth {
        get { return currentHealth; }
    }

    public int MaxHealth {
        get { return Mathf.Max(1, maxHealth); }
    }

    public float CurrentPlanarSpeed {
        get { return smoothedPlanarVelocity.magnitude; }
    }

    public bool IsMoving {
        get { return CurrentPlanarSpeed > 0.15f; }
    }

    public float CurrentNoiseRadius {
        get {
            if (dead) {
                return 0f;
            }

            if (!isGrounded && IsMoving) {
                return airborneNoiseRadius;
            }

            if (IsSprintingNow) {
                return sprintNoiseRadius;
            }

            return IsMoving ? walkingNoiseRadius : 0f;
        }
    }

    void Awake() {
        CacheComponents();
        ResetLifeState();
    }

    void Start(){
        CacheComponents();
        CacheJumpPoseBones();
        ResetLifeState();
    }

    void OnDestroy() {
        if (bloodParticleMaterial != null) {
            Destroy(bloodParticleMaterial);
        }

        if (bloodParticleMesh != null) {
            Destroy(bloodParticleMesh);
        }
    }

    void Update() {
        if (!dead && Input.GetKeyDown(jumpKey)) {
            jumpQueued = true;
            jumpQueuedAt = Time.time;
        }
    }

    void LateUpdate() {
        if (!dead) {
            ApplyJumpPose();
            ApplyDamagePose();
        }
    }

    public void ResetLifeState() {
        dead = false;
        deathTriggered = false;
        jumpQueued = false;
        jumpQueuedAt = -100f;
        isGrounded = false;
        lastGroundedTime = -100f;
        jumpPoseTime = 0f;
        currentHealth = MaxHealth;
        nextDamageTime = -100f;
        damagePoseTime = 0f;
        damageReactionDirection = Vector3.back;
        controlledYaw = transform.eulerAngles.y;
        smoothedPlanarVelocity = Vector3.zero;
        IsSprintingNow = false;

        if (animator != null) {
            animator.ResetTrigger("isDead");
            animator.SetBool("Idling", true);
        }

        StopBody();
    }

    public void SetMaxHealth(int value, bool refillHealth) {
        maxHealth = Mathf.Max(1, value);

        if (refillHealth || currentHealth <= 0) {
            currentHealth = maxHealth;
        } else {
            currentHealth = Mathf.Min(currentHealth, maxHealth);
        }
    }

    void CacheComponents() {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        if (rb != null) {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.maxDepenetrationVelocity = speed * Mathf.Max(1f, sprintMultiplier);
        }
    }

    void FixedUpdate () {
        if (dead) {
            StopBody();
            PlayDeathAnimation();
            enabled = false;
            return;
        }

        Vector2 moveInput = GetMoveInput();
        Vector3 desiredMoveDirection = GetDesiredMoveDirection(moveInput);
        float inputAmount = Mathf.Clamp01(moveInput.magnitude);
        bool hasMoveInput = desiredMoveDirection.sqrMagnitude > 0.0001f && inputAmount > 0.001f;
        IsSprintingNow = hasMoveInput && IsSprinting(moveInput);
        float currentSpeed = IsSprintingNow ? speed * sprintMultiplier : speed;

        RefreshGroundedState();
        if (jumpQueued) {
            if (Time.time - jumpQueuedAt > jumpBufferTime) {
                jumpQueued = false;
            } else if (CanJump()) {
                Jump();
            }
        }

        ClearPlanarVelocity();
        rb.angularVelocity = Vector3.zero;
        UpdatePlanarVelocity(hasMoveInput, desiredMoveDirection, inputAmount, currentSpeed);
        RotateTowardsMovement(hasMoveInput, desiredMoveDirection);

        Vector3 movement = smoothedPlanarVelocity * Time.fixedDeltaTime;
        if (isGrounded) {
            MoveWithCollision(movement);
        } else {
            MoveAirborne(movement);
        }

        ClearPlanarVelocity();

        if (animator != null) {
            animator.SetBool("Idling", smoothedPlanarVelocity.sqrMagnitude <= 0.01f && isGrounded);
        }
    }

    void MoveAirborne(Vector3 movement) {
        if (rb == null || movement.sqrMagnitude <= 0.000001f) {
            return;
        }

        Vector3 direction = movement.normalized;
        float distance = movement.magnitude;
        float allowedDistance = GetAllowedMoveDistance(direction, distance);

        if (allowedDistance >= distance - 0.001f) {
            MoveBody(rb.position + direction * allowedDistance, false);
            return;
        }

        Vector3 ledgeTargetPosition;
        if (TryGetJumpLedgeTargetPosition(direction, allowedDistance, out ledgeTargetPosition)) {
            MoveBody(ledgeTargetPosition, false);
            return;
        }

        if (allowedDistance > 0f) {
            MoveBody(rb.position + direction * allowedDistance, false);
        }
    }

    void RefreshGroundedState() {
        RaycastHit groundHit;
        bool groundedNow = rb != null
            && rb.velocity.y <= 0.15f
            && TryGetGroundBelow(jumpGroundCheckDistance, out groundHit);

        isGrounded = groundedNow;
        if (isGrounded) {
            lastGroundedTime = Time.time;
            if (rb.velocity.y < 0f) {
                rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            }
        }
    }

    bool TryGetGroundBelow(float maxDistance, out RaycastHit groundHit) {
        float bottomOffset = GetCapsuleBottomOffset();
        Vector3 rayStart = rb.position + Vector3.up * (bottomOffset + maxDistance + 0.08f);
        return TryFindGround(rayStart, maxDistance + 0.12f, out groundHit);
    }

    bool CanJump() {
        return Time.time - lastGroundedTime <= coyoteTime;
    }

    void Jump() {
        jumpQueued = false;
        isGrounded = false;
        lastGroundedTime = -100f;
        jumpPoseTime = jumpAnimationDuration;

        Vector3 velocity = rb.velocity;
        velocity.y = jumpVelocity;
        rb.velocity = velocity;

        if (animator != null) {
            animator.SetBool("Idling", false);
        }
    }

    void ClearPlanarVelocity() {
        if (rb == null) {
            return;
        }

        rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
    }

    void LockRotationToControlledYaw() {
        Quaternion targetRotation = Quaternion.Euler(0f, controlledYaw, 0f);

        if (rb != null) {
            rb.rotation = targetRotation;
            rb.angularVelocity = Vector3.zero;
        }

        transform.rotation = targetRotation;
    }

    void MoveWithCollision(Vector3 movement) {
        if (rb == null || movement.sqrMagnitude <= 0.000001f) {
            return;
        }

        Vector3 direction = movement.normalized;
        float distance = movement.magnitude;
        float allowedDistance = GetAllowedMoveDistance(direction, distance);

        if (allowedDistance >= distance - 0.001f) {
            MoveBody(GetGroundedMoveTarget(rb.position + direction * allowedDistance, maxStepHeight, groundSnapDistance), true);
            return;
        }

        Vector3 stepTargetPosition;
        if (TryGetStepTargetPosition(direction, distance, allowedDistance, out stepTargetPosition)) {
            MoveBody(stepTargetPosition, true);
            return;
        }

        if (allowedDistance > 0f) {
            MoveBody(GetGroundedMoveTarget(rb.position + direction * allowedDistance, maxStepHeight, groundSnapDistance), true);
        }
    }

    void MoveBody(Vector3 targetPosition, bool respectNavMesh) {
        Vector3 constrainedPosition = targetPosition;
        if (!respectNavMesh || TryGetNavMeshConstrainedPosition(targetPosition, out constrainedPosition)) {
            rb.MovePosition(constrainedPosition);
        }
    }

    float GetAllowedMoveDistance(Vector3 direction, float distance) {
        return GetAllowedMoveDistance(direction, distance, Vector3.zero);
    }

    float GetAllowedMoveDistance(Vector3 direction, float distance, Vector3 capsuleOffset) {
        if (capsuleCollider == null || distance <= 0f) {
            return distance;
        }

        Vector3 point1;
        Vector3 point2;
        float radius;
        GetCapsuleWorldPoints(capsuleOffset, out point1, out point2, out radius);

        RaycastHit[] hits = Physics.CapsuleCastAll(
            point1,
            point2,
            radius,
            direction,
            distance + skinWidth,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        RaycastHit hit;
        if (!TryFindBlockingHit(hits, out hit)) {
            return distance;
        }

        return Mathf.Max(0f, hit.distance - skinWidth);
    }

    bool TryFindBlockingHit(RaycastHit[] hits, out RaycastHit blockingHit) {
        blockingHit = new RaycastHit();
        float bestDistance = float.PositiveInfinity;
        bool found = false;

        foreach (RaycastHit hit in hits) {
            if (IsOwnCollider(hit.collider) || IsWalkableSurface(hit.normal)) {
                continue;
            }

            if (hit.distance < bestDistance) {
                bestDistance = hit.distance;
                blockingHit = hit;
                found = true;
            }
        }

        return found;
    }

    void GetCapsuleWorldPoints(out Vector3 point1, out Vector3 point2, out float radius) {
        GetCapsuleWorldPoints(Vector3.zero, out point1, out point2, out radius);
    }

    void GetCapsuleWorldPoints(Vector3 capsuleOffset, out Vector3 point1, out Vector3 point2, out float radius) {
        Vector3 center = transform.TransformPoint(capsuleCollider.center);
        float scaleX = Mathf.Abs(transform.lossyScale.x);
        float scaleY = Mathf.Abs(transform.lossyScale.y);
        float scaleZ = Mathf.Abs(transform.lossyScale.z);
        radius = capsuleCollider.radius * Mathf.Max(scaleX, scaleZ);
        float height = Mathf.Max(capsuleCollider.height * scaleY, radius * 2f);
        float halfSegment = Mathf.Max(0f, (height * 0.5f) - radius);
        Vector3 up = transform.up * halfSegment;

        point1 = center + up + capsuleOffset;
        point2 = center - up + capsuleOffset;
    }

    float GetMoveAxis(string axisName) {
        return useRawInput ? Input.GetAxisRaw(axisName) : Input.GetAxis(axisName);
    }

    Vector2 GetMoveInput() {
        Vector2 input = new Vector2(GetMoveAxis("Horizontal"), GetMoveAxis("Vertical"));
        if (input.sqrMagnitude < inputDeadZone * inputDeadZone) {
            return Vector2.zero;
        }

        return Vector2.ClampMagnitude(input, 1f);
    }

    Vector3 GetDesiredMoveDirection(Vector2 moveInput) {
        if (moveInput.sqrMagnitude <= 0.0001f) {
            return Vector3.zero;
        }

        if (!moveRelativeToCamera) {
            return new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        }

        Transform referenceTransform = GetCameraTransform();
        if (referenceTransform == null) {
            return transform.TransformDirection(new Vector3(moveInput.x, 0f, moveInput.y)).normalized;
        }

        Vector3 forward = referenceTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f) {
            forward = transform.forward;
        }

        Vector3 right = referenceTransform.right;
        right.y = 0f;
        if (right.sqrMagnitude <= 0.0001f) {
            right = transform.right;
        }

        forward.Normalize();
        right.Normalize();
        return (forward * moveInput.y + right * moveInput.x).normalized;
    }

    Transform GetCameraTransform() {
        if (cameraTransform != null) {
            return cameraTransform;
        }

        if (gameplayCamera == null) {
            gameplayCamera = Camera.main;
        }

        if (gameplayCamera == null) {
            return null;
        }

        cameraTransform = gameplayCamera.transform;
        return cameraTransform;
    }

    void UpdatePlanarVelocity(bool hasMoveInput, Vector3 desiredMoveDirection, float inputAmount, float currentSpeed) {
        float controlMultiplier = isGrounded ? 1f : Mathf.Clamp01(airborneControlMultiplier);
        Vector3 targetVelocity = hasMoveInput
            ? desiredMoveDirection * (currentSpeed * inputAmount * controlMultiplier)
            : Vector3.zero;
        float changeRate = targetVelocity.sqrMagnitude > smoothedPlanarVelocity.sqrMagnitude
            ? acceleration
            : deceleration;

        if (!isGrounded) {
            changeRate *= Mathf.Clamp01(airborneControlMultiplier);
        }

        smoothedPlanarVelocity = Vector3.MoveTowards(
            smoothedPlanarVelocity,
            targetVelocity,
            Mathf.Max(0f, changeRate) * Time.fixedDeltaTime);
    }

    void RotateTowardsMovement(bool hasMoveInput, Vector3 desiredMoveDirection) {
        if (!hasMoveInput || desiredMoveDirection.sqrMagnitude <= 0.0001f) {
            rb.MoveRotation(Quaternion.Euler(0f, controlledYaw, 0f));
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(desiredMoveDirection, Vector3.up);
        Quaternion nextRotation = Quaternion.RotateTowards(
            rb.rotation,
            targetRotation,
            Mathf.Max(1f, rotationSpeed) * Time.fixedDeltaTime);
        controlledYaw = nextRotation.eulerAngles.y;
        rb.MoveRotation(nextRotation);
    }

    bool IsSprinting(Vector2 moveInput) {
        if (moveInput.sqrMagnitude <= 0.01f) {
            return false;
        }

        return Input.GetKey(sprintKey) || Input.GetKey(KeyCode.RightShift);
    }

    bool TryGetStepTargetPosition(Vector3 direction, float distance, float blockedDistance, out Vector3 targetPosition) {
        targetPosition = rb.position;

        if (capsuleCollider == null || maxStepHeight <= 0f || distance <= 0f) {
            return false;
        }

        Vector3 liftOffset = Vector3.up * maxStepHeight;
        float raisedAllowedDistance = GetAllowedMoveDistance(direction, distance, liftOffset);
        float usefulStepDistance = Mathf.Min(distance, Mathf.Max(blockedDistance + 0.02f, distance * 0.35f));
        if (raisedAllowedDistance < usefulStepDistance) {
            return false;
        }

        Vector3 raisedTargetPosition = rb.position + direction * Mathf.Min(distance, raisedAllowedDistance) + liftOffset;
        Vector3 groundedTargetPosition;
        if (!TryGetGroundedMoveTarget(raisedTargetPosition, maxStepHeight, groundSnapDistance, out groundedTargetPosition)) {
            return false;
        }

        float stepHeight = groundedTargetPosition.y - rb.position.y;
        if (stepHeight < -groundSnapDistance || stepHeight > maxStepHeight + 0.05f) {
            return false;
        }

        targetPosition = groundedTargetPosition;
        return true;
    }

    bool TryGetJumpLedgeTargetPosition(Vector3 direction, float blockedDistance, out Vector3 targetPosition) {
        targetPosition = rb.position;

        if (capsuleCollider == null || jumpLedgeAssistHeight <= 0f || rb.velocity.y < -1.5f) {
            return false;
        }

        float overstep = Mathf.Max(jumpLedgeOverstep, capsuleCollider.radius + skinWidth);
        Vector3 probePosition = rb.position + direction * (blockedDistance + overstep);
        Vector3 groundedTargetPosition;
        if (!TryGetGroundedMoveTarget(probePosition, jumpLedgeAssistHeight, jumpLedgeAssistHeight, out groundedTargetPosition)) {
            return false;
        }

        float heightDelta = groundedTargetPosition.y - rb.position.y;
        if (heightDelta <= 0.05f || heightDelta > jumpLedgeAssistHeight) {
            return false;
        }

        Vector3 liftOffset = Vector3.up * Mathf.Max(heightDelta + 0.08f, maxStepHeight);
        float horizontalDistance = Vector3.Distance(
            new Vector3(rb.position.x, 0f, rb.position.z),
            new Vector3(groundedTargetPosition.x, 0f, groundedTargetPosition.z));

        if (GetAllowedMoveDistance(direction, horizontalDistance, liftOffset) < horizontalDistance - 0.02f) {
            return false;
        }

        targetPosition = groundedTargetPosition;
        return true;
    }

    Vector3 GetGroundedMoveTarget(Vector3 targetPosition, float maxRise, float maxDrop) {
        Vector3 groundedTargetPosition;
        if (TryGetGroundedMoveTarget(targetPosition, maxRise, maxDrop, out groundedTargetPosition)) {
            return groundedTargetPosition;
        }

        return targetPosition;
    }

    bool TryGetGroundedMoveTarget(Vector3 targetPosition, float maxRise, float maxDrop, out Vector3 groundedTargetPosition) {
        groundedTargetPosition = targetPosition;

        RaycastHit groundHit;
        Vector3 rayStart = targetPosition + Vector3.up * (maxRise + 0.25f);
        float rayDistance = maxRise + maxDrop + 0.6f;
        if (!TryFindGround(rayStart, rayDistance, out groundHit)) {
            return false;
        }

        float groundedY = groundHit.point.y - GetCapsuleBottomOffset();
        float heightDelta = groundedY - rb.position.y;
        if (heightDelta > maxRise + 0.05f || heightDelta < -maxDrop) {
            return false;
        }

        groundedTargetPosition = new Vector3(targetPosition.x, groundedY, targetPosition.z);
        return true;
    }

    bool TryFindGround(Vector3 rayStart, float rayDistance, out RaycastHit groundHit) {
        groundHit = new RaycastHit();
        bool found = false;
        float bestDistance = float.PositiveInfinity;

        float probeRadius = GetGroundProbeRadius();
        if (probeRadius > 0f) {
            RaycastHit[] sphereHits = Physics.SphereCastAll(rayStart, probeRadius, Vector3.down, rayDistance, collisionMask, QueryTriggerInteraction.Ignore);
            found = TryChooseGroundHit(sphereHits, ref groundHit, ref bestDistance) || found;
        }

        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, rayDistance, collisionMask, QueryTriggerInteraction.Ignore);
        found = TryChooseGroundHit(hits, ref groundHit, ref bestDistance) || found;
        return found;
    }

    bool TryChooseGroundHit(RaycastHit[] hits, ref RaycastHit groundHit, ref float bestDistance) {
        bool found = false;

        foreach (RaycastHit hit in hits) {
            if (IsOwnCollider(hit.collider)) {
                continue;
            }

            if (!IsWalkableSurface(hit.normal)) {
                continue;
            }

            if (hit.distance < bestDistance) {
                bestDistance = hit.distance;
                groundHit = hit;
                found = true;
            }
        }

        return found;
    }

    float GetGroundProbeRadius() {
        if (capsuleCollider == null) {
            return groundProbeRadius;
        }

        float scaleX = Mathf.Abs(transform.lossyScale.x);
        float scaleZ = Mathf.Abs(transform.lossyScale.z);
        float capsuleRadius = capsuleCollider.radius * Mathf.Max(scaleX, scaleZ);
        return Mathf.Clamp(groundProbeRadius, 0.02f, Mathf.Max(0.02f, capsuleRadius - skinWidth));
    }

    bool IsWalkableSurface(Vector3 normal) {
        return Vector3.Angle(normal, Vector3.up) <= maxSlopeAngle;
    }

    void CacheJumpPoseBones() {
        if (hipsBone != null) {
            return;
        }

        hipsBone = FindChildByName(transform, "mixamorig:Hips");
        spineBone = FindChildByName(transform, "mixamorig:Spine");
        leftUpLegBone = FindChildByName(transform, "mixamorig:LeftUpLeg");
        rightUpLegBone = FindChildByName(transform, "mixamorig:RightUpLeg");
        leftLegBone = FindChildByName(transform, "mixamorig:LeftLeg");
        rightLegBone = FindChildByName(transform, "mixamorig:RightLeg");
        leftFootBone = FindChildByName(transform, "mixamorig:LeftFoot");
        rightFootBone = FindChildByName(transform, "mixamorig:RightFoot");
    }

    void ApplyJumpPose() {
        if (jumpPoseStrength <= 0f || jumpAnimationDuration <= 0f) {
            return;
        }

        bool airborne = !isGrounded && rb != null && rb.velocity.y > -3.0f;
        if (!airborne && jumpPoseTime <= 0f) {
            return;
        }

        CacheJumpPoseBones();

        float timedPose = 0f;
        if (jumpPoseTime > 0f) {
            jumpPoseTime = Mathf.Max(0f, jumpPoseTime - Time.deltaTime);
            float t = 1f - (jumpPoseTime / jumpAnimationDuration);
            timedPose = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
        }

        float airbornePose = airborne ? 1f : 0f;
        float amount = Mathf.Clamp01(Mathf.Max(timedPose, airbornePose * 0.65f)) * jumpPoseStrength;
        if (amount <= 0.001f) {
            return;
        }

        if (hipsBone != null) {
            hipsBone.localPosition += Vector3.down * (0.035f * amount);
        }

        RotateBone(spineBone, new Vector3(-8f * amount, 0f, 0f));
        RotateBone(leftUpLegBone, new Vector3(18f * amount, 0f, -3f * amount));
        RotateBone(rightUpLegBone, new Vector3(18f * amount, 0f, 3f * amount));
        RotateBone(leftLegBone, new Vector3(-22f * amount, 0f, 0f));
        RotateBone(rightLegBone, new Vector3(-22f * amount, 0f, 0f));
        RotateBone(leftFootBone, new Vector3(10f * amount, 0f, 0f));
        RotateBone(rightFootBone, new Vector3(10f * amount, 0f, 0f));
    }

    void ApplyDamagePose() {
        if (damagePoseTime <= 0f || damageReactionDuration <= 0f || damagePoseStrength <= 0f) {
            return;
        }

        CacheJumpPoseBones();

        damagePoseTime = Mathf.Max(0f, damagePoseTime - Time.deltaTime);
        float t = 1f - (damagePoseTime / damageReactionDuration);
        float amount = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * damagePoseStrength;
        if (amount <= 0.001f) {
            return;
        }

        Vector3 localHitDirection = transform.InverseTransformDirection(damageReactionDirection.normalized);
        if (hipsBone != null) {
            hipsBone.localPosition += new Vector3(localHitDirection.x * 0.025f * amount, -0.018f * amount, -0.03f * amount);
        }

        RotateBone(spineBone, new Vector3(-13f * amount, localHitDirection.x * 4f * amount, -localHitDirection.x * 12f * amount));
        RotateBone(leftUpLegBone, new Vector3(-6f * amount, 0f, -3f * amount));
        RotateBone(rightUpLegBone, new Vector3(-6f * amount, 0f, 3f * amount));
    }

    void RotateBone(Transform bone, Vector3 eulerOffset) {
        if (bone != null) {
            bone.localRotation *= Quaternion.Euler(eulerOffset);
        }
    }

    Transform FindChildByName(Transform root, string childName) {
        if (root == null) {
            return null;
        }

        if (root.name == childName) {
            return root;
        }

        foreach (Transform child in root) {
            Transform result = FindChildByName(child, childName);
            if (result != null) {
                return result;
            }
        }

        return null;
    }

    float GetCapsuleBottomOffset() {
        if (capsuleCollider == null) {
            return 0f;
        }

        float scaleY = Mathf.Abs(transform.lossyScale.y);
        return (capsuleCollider.center.y - capsuleCollider.height * 0.5f) * scaleY;
    }

    bool IsOwnCollider(Collider hitCollider) {
        return hitCollider != null && hitCollider.transform.IsChildOf(transform);
    }

    bool TryGetNavMeshConstrainedPosition(Vector3 targetPosition, out Vector3 constrainedPosition) {
        constrainedPosition = targetPosition;

        if (!constrainToNavMesh) {
            return true;
        }

        NavMeshHit targetHit;
        float targetSampleRadius = Mathf.Max(maxNavMeshSnapDistance, navMeshVerticalTolerance);
        bool targetOnNavMesh = NavMesh.SamplePosition(targetPosition, out targetHit, targetSampleRadius, NavMesh.AllAreas);

        if (IsAllowedOffNavMeshWalkableTarget(targetPosition, targetOnNavMesh, targetHit)) {
            return true;
        }

        if (!targetOnNavMesh) {
            return false;
        }

        if (PlanarDistance(targetPosition, targetHit.position) > maxNavMeshSnapDistance) {
            return false;
        }

        NavMeshHit startHit;
        if (!NavMesh.SamplePosition(rb.position, out startHit, navMeshCheckRadius, NavMesh.AllAreas)) {
            return true;
        }

        NavMeshHit blockedHit;
        if (NavMesh.Raycast(startHit.position, targetHit.position, out blockedHit, NavMesh.AllAreas)) {
            return false;
        }

        constrainedPosition = targetPosition;
        return true;
    }

    bool IsAllowedOffNavMeshWalkableTarget(Vector3 targetPosition, bool targetOnNavMesh, NavMeshHit targetHit) {
        if (!allowOffNavMeshJumpLandings) {
            return false;
        }

        if (targetOnNavMesh && Mathf.Abs(targetPosition.y - targetHit.position.y) <= offNavMeshSurfaceHeightTolerance) {
            return false;
        }

        RaycastHit groundHit;
        return TryGetWalkableGroundAt(targetPosition, offNavMeshGroundCheckDistance, out groundHit);
    }

    bool TryGetWalkableGroundAt(Vector3 targetPosition, float maxDistance, out RaycastHit groundHit) {
        float bottomOffset = GetCapsuleBottomOffset();
        Vector3 rayStart = targetPosition + Vector3.up * (bottomOffset + maxDistance + 0.12f);
        return TryFindGround(rayStart, maxDistance + 0.16f, out groundHit);
    }

    float PlanarDistance(Vector3 a, Vector3 b) {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    public void Die() {
        Die(null);
    }

    public void Die(Transform killer) {
        if (dead) {
            return;
        }

        dead = true;

        if (killer != null) {
            FacePosition(killer.position);
        }

        StopBody();
        PlayDeathAnimation();
        enabled = false;
    }

    public bool TakeDamage(int damage, Transform damageSource) {
        if (dead || Time.time < nextDamageTime) {
            return false;
        }

        currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(1, damage));
        nextDamageTime = Time.time + damageInvulnerabilityDuration;
        SpawnBloodEffect(damageSource);

        if (currentHealth <= 0) {
            Die(damageSource);
            return true;
        }

        PlayDamageReaction(damageSource);
        return true;
    }

    void OnCollisionEnter(Collision collision) {
        if (!dead && collision.gameObject.CompareTag("AI")) {
            TakeDamage(aiContactDamage, collision.transform);
        }
    }

    void OnTriggerEnter(Collider other) {
        if (!dead && other.CompareTag("AI")) {
            TakeDamage(aiContactDamage, other.transform);
        }
    }

    void StopBody() {
        if (rb == null) {
            return;
        }

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        IsSprintingNow = false;
    }

    void PlayDeathAnimation() {
        if (deathTriggered || animator == null) {
            return;
        }

        deathTriggered = true;
        animator.SetBool("Idling", true);
        animator.SetTrigger("isDead");
    }

    void PlayDamageReaction(Transform damageSource) {
        damageReactionDirection = GetDamageReactionDirection(damageSource);
        damagePoseTime = damageReactionDuration;

        if (animator != null) {
            animator.SetBool("Idling", false);
        }

        if (rb != null && damageKnockbackImpulse > 0f) {
            rb.AddForce(damageReactionDirection * damageKnockbackImpulse, ForceMode.VelocityChange);
        }
    }

    Vector3 GetDamageReactionDirection(Transform damageSource) {
        Vector3 reactionDirection = -transform.forward;
        if (damageSource != null) {
            reactionDirection = transform.position - damageSource.position;
            reactionDirection.y = 0f;
        }

        if (reactionDirection.sqrMagnitude <= 0.001f) {
            reactionDirection = -transform.forward;
        }

        return reactionDirection.normalized;
    }

    void SpawnBloodEffect(Transform damageSource) {
        if (!showBloodOnDamage || bloodParticleCount <= 0) {
            return;
        }

        Vector3 sprayDirection = GetDamageReactionDirection(damageSource);
        Vector3 spawnPosition = transform.position + Vector3.up * Mathf.Max(0f, bloodSpawnHeight) + sprayDirection * 0.16f;
        GameObject bloodObject = new GameObject("Damage Blood Effect");
        bloodObject.transform.position = spawnPosition;
        bloodObject.transform.rotation = Quaternion.LookRotation(sprayDirection, Vector3.up);

        ParticleSystem particles = bloodObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.08f;
        main.loop = false;
        main.startLifetime = Mathf.Max(0.05f, bloodParticleLifetime);
        main.startSpeed = Mathf.Max(0.1f, bloodParticleSpeed);
        float particleSize = Mathf.Max(0.01f, bloodParticleSize);
        main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.65f, particleSize * 1.25f);
        main.startColor = bloodColor;
        main.gravityModifier = 1.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(1, bloodParticleCount);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = Mathf.Clamp(bloodConeAngle, 1.0f, 85.0f);
        shape.radius = Mathf.Max(0.0f, bloodSpawnRadius);

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null) {
            particleRenderer.material = GetBloodParticleMaterial();
            if (renderBloodAsSmoothMeshes) {
                particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
                particleRenderer.mesh = GetBloodParticleMesh();
            } else {
                particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            }
        }

        particles.Emit(Mathf.Max(1, bloodParticleCount));
        Destroy(bloodObject, Mathf.Max(bloodEffectDuration, bloodParticleLifetime + 0.25f));
    }

    Material GetBloodParticleMaterial() {
        if (bloodParticleMaterial == null) {
            Shader shader = Shader.Find("Standard");
            if (shader == null) {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            if (shader == null) {
                shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            }

            if (shader == null) {
                shader = Shader.Find("Sprites/Default");
            }

            bloodParticleMaterial = new Material(shader);
            bloodParticleMaterial.name = "Runtime Blood Particle Material";
        }

        ApplyBloodMaterialColor(bloodParticleMaterial);
        return bloodParticleMaterial;
    }

    Mesh GetBloodParticleMesh() {
        if (bloodParticleMesh != null) {
            return bloodParticleMesh;
        }

        const int latitudeSegments = 6;
        const int longitudeSegments = 10;
        const float radius = 0.5f;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        vertices.Add(Vector3.up * radius);
        for (int lat = 1; lat < latitudeSegments; lat++) {
            float polar = Mathf.PI * lat / latitudeSegments;
            float y = Mathf.Cos(polar) * radius;
            float ringRadius = Mathf.Sin(polar) * radius;

            for (int lon = 0; lon < longitudeSegments; lon++) {
                float azimuth = Mathf.PI * 2f * lon / longitudeSegments;
                vertices.Add(new Vector3(Mathf.Cos(azimuth) * ringRadius, y, Mathf.Sin(azimuth) * ringRadius));
            }
        }
        int bottomIndex = vertices.Count;
        vertices.Add(Vector3.down * radius);

        for (int lon = 0; lon < longitudeSegments; lon++) {
            int current = 1 + lon;
            int next = 1 + ((lon + 1) % longitudeSegments);
            triangles.Add(0);
            triangles.Add(next);
            triangles.Add(current);
        }

        for (int lat = 0; lat < latitudeSegments - 2; lat++) {
            int ringStart = 1 + lat * longitudeSegments;
            int nextRingStart = ringStart + longitudeSegments;

            for (int lon = 0; lon < longitudeSegments; lon++) {
                int current = ringStart + lon;
                int next = ringStart + ((lon + 1) % longitudeSegments);
                int lower = nextRingStart + lon;
                int lowerNext = nextRingStart + ((lon + 1) % longitudeSegments);

                triangles.Add(current);
                triangles.Add(next);
                triangles.Add(lowerNext);

                triangles.Add(current);
                triangles.Add(lowerNext);
                triangles.Add(lower);
            }
        }

        int lastRingStart = 1 + (latitudeSegments - 2) * longitudeSegments;
        for (int lon = 0; lon < longitudeSegments; lon++) {
            int current = lastRingStart + lon;
            int next = lastRingStart + ((lon + 1) % longitudeSegments);
            triangles.Add(bottomIndex);
            triangles.Add(current);
            triangles.Add(next);
        }

        bloodParticleMesh = new Mesh();
        bloodParticleMesh.name = "Runtime Blood Droplet Mesh";
        bloodParticleMesh.SetVertices(vertices);
        bloodParticleMesh.SetTriangles(triangles, 0);
        bloodParticleMesh.RecalculateNormals();
        bloodParticleMesh.RecalculateBounds();
        return bloodParticleMesh;
    }

    void ApplyBloodMaterialColor(Material material) {
        if (material == null) {
            return;
        }

        if (material.HasProperty("_Color")) {
            material.color = bloodColor;
        }

        if (material.HasProperty("_BaseColor")) {
            material.SetColor("_BaseColor", bloodColor);
        }

        if (material.HasProperty("_TintColor")) {
            material.SetColor("_TintColor", bloodColor);
        }
    }

    void FacePosition(Vector3 position) {
        Vector3 direction = position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f) {
            Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            controlledYaw = lookRotation.eulerAngles.y;
            transform.rotation = lookRotation;
        }
    }
}
