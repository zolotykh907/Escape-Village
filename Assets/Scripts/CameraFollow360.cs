using UnityEngine;

public class CameraFollow360 : MonoBehaviour {

	public Transform player;
	public float distance = 6.5f;
	public float height = 4;
	public Vector3 lookOffset = new Vector3(0f, 1.35f, 0f);
	public float cameraSpeed = 18f;
	public float rotationSpeed = 18f;
	public bool autoFindPlayer = true;
	public bool snapOnTargetFound = true;
	public float snapDistance = 18;
	public bool allowMouseOrbit = true;
	public int orbitMouseButton = 1;
	public bool requireMouseButtonForOrbit = true;
	public bool lockCursorDuringGameplay = false;
	public float mouseSensitivity = 3.0f;
	public bool invertY = false;
	public float startPitch = 18.0f;
	public float minPitch = -12.0f;
	public float maxPitch = 65.0f;
	public float minDistance = 3.0f;
	public float maxDistance = 14.0f;
	public float zoomSensitivity = 4.0f;
	public bool lockCursorWhileOrbiting = true;
	public bool followPlayerFacingUntilMouseOrbit = false;
	public float followSmoothTime = 0.08f;
	public float positionSmoothTime = 0.09f;
	public float maxFollowSpeed = 120f;
	public bool avoidObstacles = true;
	public LayerMask collisionMask = ~0;
	public float collisionRadius = 0.24f;
	public float collisionSkin = 0.12f;
	public float minCollisionDistance = 1.2f;

	bool snappedToTarget;
	bool userHasOrbited;
	bool orbiting;
	float yaw;
	float pitch;
	Vector3 smoothedLookPosition;
	Vector3 lookVelocity;
	Vector3 cameraVelocity;

	void Start()
	{
		pitch = Mathf.Clamp(startPitch, minPitch, maxPitch);
		FindPlayerIfNeeded();
		if (player != null && !snappedToTarget)
		{
			yaw = player.eulerAngles.y;
			smoothedLookPosition = player.position + lookOffset;
		}

		SnapToTargetIfNeeded(true);
	}

	void OnDisable()
	{
		ReleaseCursor();
	}

	void LateUpdate () 
	{
		FindPlayerIfNeeded();

		if(player == null)
		{
			ReleaseCursor();
			return;
		}

		float deltaTime = GetCameraDeltaTime();
		UpdateOrbitInput();

		if (!userHasOrbited && followPlayerFacingUntilMouseOrbit)
		{
			float yawLerp = 1f - Mathf.Exp(-Mathf.Max(0.01f, rotationSpeed) * deltaTime);
			yaw = Mathf.LerpAngle(yaw, player.eulerAngles.y, yawLerp);
		}

		Vector3 lookPosition = player.position + lookOffset;
		if (!snappedToTarget || Vector3.Distance(transform.position, ResolveCameraPosition(lookPosition)) > snapDistance)
		{
			SnapToTargetIfNeeded(true);
			return;
		}

		smoothedLookPosition = Vector3.SmoothDamp(
			smoothedLookPosition,
			lookPosition,
			ref lookVelocity,
			Mathf.Max(0.001f, followSmoothTime),
			Mathf.Max(1f, maxFollowSpeed),
			deltaTime);

		Vector3 targetPos = ResolveCameraPosition(smoothedLookPosition);
		transform.position = Vector3.SmoothDamp(
			transform.position,
			targetPos,
			ref cameraVelocity,
			Mathf.Max(0.001f, positionSmoothTime),
			float.PositiveInfinity,
			deltaTime);

		LookAt(smoothedLookPosition, deltaTime);
	}

	public void SetTarget(Transform target, bool snapNow)
	{
		player = target;
		snappedToTarget = false;
		userHasOrbited = false;
		lookVelocity = Vector3.zero;
		cameraVelocity = Vector3.zero;

		if (player != null)
		{
			yaw = player.eulerAngles.y;
			pitch = Mathf.Clamp(startPitch, minPitch, maxPitch);
			smoothedLookPosition = player.position + lookOffset;
		}

		if (snapNow)
		{
			SnapToTargetIfNeeded(true);
		}
	}

	void FindPlayerIfNeeded()
	{
		if (player != null || !autoFindPlayer)
		{
			return;
		}

		PlayerController playerController = FindObjectOfType<PlayerController>();
		if (playerController != null)
		{
			SetTarget(playerController.transform, snapOnTargetFound);
			return;
		}

		GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
		if (taggedPlayer != null)
		{
			SetTarget(taggedPlayer.transform, snapOnTargetFound);
		}
	}

	void SnapToTargetIfNeeded(bool force)
	{
		if (player == null || (!force && snappedToTarget))
		{
			return;
		}

		Vector3 lookPosition = player.position + lookOffset;
		smoothedLookPosition = lookPosition;
		lookVelocity = Vector3.zero;
		cameraVelocity = Vector3.zero;
		transform.position = ResolveCameraPosition(lookPosition);
		LookAt(lookPosition, 1f);
		snappedToTarget = true;
	}

	void UpdateOrbitInput()
	{
		if (!allowMouseOrbit || Time.timeScale <= 0f)
		{
			StopOrbiting();
			ReleaseCursor();
			return;
		}

		float scroll = Input.GetAxis("Mouse ScrollWheel");
		if (Mathf.Abs(scroll) > 0.001f)
		{
			distance = Mathf.Clamp(distance - scroll * zoomSensitivity, minDistance, maxDistance);
		}

		bool shouldReadMouse = !PointerIsOverUi();
		if (requireMouseButtonForOrbit)
		{
			if (Input.GetMouseButtonDown(orbitMouseButton) && PointerIsOverUi())
			{
				return;
			}

			if (Input.GetMouseButtonDown(orbitMouseButton))
			{
				orbiting = true;
				if (lockCursorWhileOrbiting)
				{
					LockCursor();
				}
			}

			if (Input.GetMouseButtonUp(orbitMouseButton))
			{
				StopOrbiting();
			}

			shouldReadMouse = orbiting && Input.GetMouseButton(orbitMouseButton);
		}
		else
		{
			StopOrbiting();
			if (lockCursorDuringGameplay)
			{
				LockCursor();
				shouldReadMouse = true;
			}
		}

		if (!shouldReadMouse)
		{
			return;
		}

		float mouseX = Input.GetAxisRaw("Mouse X");
		float mouseY = Input.GetAxisRaw("Mouse Y");

		if (Mathf.Abs(mouseX) > 0.001f || Mathf.Abs(mouseY) > 0.001f)
		{
			userHasOrbited = true;
		}

		yaw += mouseX * mouseSensitivity;
		pitch = Mathf.Clamp(pitch + mouseY * mouseSensitivity * (invertY ? 1f : -1f), minPitch, maxPitch);
	}

	void StopOrbiting()
	{
		if (!orbiting)
		{
			return;
		}

		orbiting = false;
		if (lockCursorWhileOrbiting && requireMouseButtonForOrbit)
		{
			ReleaseCursor();
		}
	}

	void LockCursor()
	{
		if (!Application.isFocused)
		{
			return;
		}

		if (Cursor.lockState != CursorLockMode.Locked)
		{
			Cursor.lockState = CursorLockMode.Locked;
		}

		Cursor.visible = false;
	}

	void ReleaseCursor()
	{
		if (Cursor.lockState != CursorLockMode.None)
		{
			Cursor.lockState = CursorLockMode.None;
		}

		Cursor.visible = true;
	}

	bool PointerIsOverUi()
	{
		return UnityEngine.EventSystems.EventSystem.current != null
			&& UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
	}

	Vector3 ResolveCameraPosition(Vector3 lookPosition)
	{
		Vector3 desiredPosition = GetTargetCameraPosition(lookPosition);
		if (!avoidObstacles)
		{
			return desiredPosition;
		}

		Vector3 toCamera = desiredPosition - lookPosition;
		float targetDistance = toCamera.magnitude;
		if (targetDistance <= minCollisionDistance)
		{
			return desiredPosition;
		}

		Vector3 direction = toCamera / targetDistance;
		RaycastHit closestHit;
		if (!TryGetClosestCameraBlocker(lookPosition, direction, targetDistance, out closestHit))
		{
			return desiredPosition;
		}

		float correctedDistance = Mathf.Max(minCollisionDistance, closestHit.distance - collisionSkin);
		return lookPosition + direction * correctedDistance;
	}

	bool TryGetClosestCameraBlocker(Vector3 origin, Vector3 direction, float distanceToCamera, out RaycastHit closestHit)
	{
		closestHit = new RaycastHit();
		float bestDistance = float.PositiveInfinity;
		bool found = false;
		RaycastHit[] hits = Physics.SphereCastAll(
			origin,
			Mathf.Max(0.01f, collisionRadius),
			direction,
			distanceToCamera,
			collisionMask,
			QueryTriggerInteraction.Ignore);

		foreach (RaycastHit hit in hits)
		{
			if (IsIgnoredCameraCollider(hit.collider) || hit.distance < 0.001f)
			{
				continue;
			}

			if (hit.distance < bestDistance)
			{
				bestDistance = hit.distance;
				closestHit = hit;
				found = true;
			}
		}

		return found;
	}

	bool IsIgnoredCameraCollider(Collider hitCollider)
	{
		return hitCollider == null
			|| (player != null && hitCollider.transform.IsChildOf(player));
	}

	void LookAt(Vector3 lookPosition, float deltaTime)
	{
		Vector3 relativePos = lookPosition - transform.position;
		if (relativePos.sqrMagnitude <= 0.001f)
		{
			return;
		}

		Quaternion targetRotation = Quaternion.LookRotation(relativePos);
		float rotationLerp = 1f - Mathf.Exp(-Mathf.Max(0.01f, rotationSpeed) * Mathf.Max(0.001f, deltaTime));
		transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationLerp);
	}

	float GetCameraDeltaTime()
	{
		return Mathf.Clamp(Time.unscaledDeltaTime, 0.001f, 0.05f);
	}

	Vector3 GetTargetCameraPosition(Vector3 lookPosition)
	{
		Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
		return lookPosition + orbitRotation * Vector3.back * Mathf.Clamp(distance, minDistance, maxDistance);
	}
}
