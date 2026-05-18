using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-10000)]
public class GameManager : MonoBehaviour {

    enum GameState {
        StartMenu,
        Playing,
        Paused,
        Won,
        Lost
    }

    enum DifficultyLevel {
        Easy,
        Normal,
        Hard
    }

    public Vector3 artifactSpawnPosition = new Vector3(60.1f, -0.9f, 48.8f);
    public Vector3 portalSpawnPosition = new Vector3(31.8f, -0.8f, 24.8f);
    public bool randomizeQuestSpawns = true;
    public float minArtifactPortalDistance = 24.0f;
    public float minPlayerSpawnDistance = 8.0f;
    public float minSpawnEdgeDistance = 1.1f;
    public float maxSpawnHeightAbovePlayer = 3.0f;
    public float maxSpawnHeightBelowPlayer = 2.5f;
    public float spawnClearanceRadius = 1.0f;
    public float spawnClearanceHeight = 2.4f;
    public LayerMask questSpawnBlockingMask = ~0;
    public int questSpawnAttempts = 120;
    public float navMeshSpawnSampleRadius = 4.0f;
    public bool generateMissingSceneColliders = true;
    public float collectDistance = 2.2f;
    public float portalDistance = 3.0f;
    public string blenderArtifactResourcePath = "TechTrophyBlender";
    public string artifactResourcePath = "EscapeArtifact";
    public string portalResourcePath = "EscapePortal";
    public Color portalLockedGlowColor = new Color(1f, 0.12f, 0.08f, 0.85f);
    public Color portalActiveGlowColor = new Color(0.1f, 1f, 0.45f, 0.85f);
    public float portalGlowColorBlendSpeed = 5.0f;
    public float portalGlowCenterHeight = 1.35f;
    public float portalGlowRadius = 0.86f;
    public float portalGlowWidth = 0.08f;
    public float portalGlowPulseAmount = 0.35f;
    public float portalGlowPulseSpeed = 3.4f;
    public float portalGlowLightIntensity = 1.35f;
    public float portalGlowLightRange = 4.2f;
    public bool showCarriedArtifact = true;
    public Vector3 carriedArtifactLocalPosition = new Vector3(0f, 2.45f, 0f);
    public float carriedArtifactScale = 0.22f;
    public float carriedArtifactBobAmplitude = 0.12f;
    public float carriedArtifactBobFrequency = 3.0f;
    public float carriedArtifactSpinSpeed = 95.0f;
    public string ambientClipPath = "Audio/AmbientLoop";
    public string pickupClipPath = "Audio/ArtifactPickup";
    public string deathClipPath = "Audio/DeathSting";
    public string victoryClipPath = "Audio/VictorySting";

    GameState state = GameState.StartMenu;
    DifficultyLevel difficulty = DifficultyLevel.Normal;
    PlayerController player;
    AIController[] aiControllers;
    AgentManager agentManager;
    GameObject artifactRoot;
    GameObject artifactVisual;
    GameObject carriedArtifactVisual;
    GameObject portalRoot;
    GameObject portalVisual;
    LineRenderer portalGlowRing;
    Light portalGlowLight;
    Material portalGlowMaterial;
    Color currentPortalGlowColor;
    Vector3 currentArtifactSpawnPosition;
    Vector3 currentPortalSpawnPosition;
    Canvas uiCanvas;
    GameObject menuPanel;
    Image menuPanelImage;
    Text titleText;
    Text messageText;
    Text resultStatsText;
    Text objectiveText;
    Text settingsTitleText;
    Text controlsText;
    Text volumeText;
    Text difficultyText;
    Button menuButton;
    Button startButton;
    Button resumeButton;
    Button restartButton;
    Button difficultyButton;
    Button volumeDownButton;
    Button volumeUpButton;
    AudioSource musicSource;
    AudioSource sfxSource;
    AudioClip pickupClip;
    AudioClip deathClip;
    AudioClip victoryClip;
    float volume = 0.7f;
    bool artifactCollected;
    bool deathHandled;
    float lossMenuTime = -1f;
    float runStartedAt = -1f;
    float runFinishedAt = -1f;
    float artifactCollectedAt = -1f;
    static bool sceneBootstrapInProgress;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureGameManagerExists() {
        if (sceneBootstrapInProgress || FindObjectOfType<GameManager>() != null) {
            return;
        }

        sceneBootstrapInProgress = true;
        GameObject managerObject = new GameObject("Runtime GameManager");
        managerObject.AddComponent<GameManager>();
        sceneBootstrapInProgress = false;
    }

    void Awake() {
        Time.timeScale = 0f;
    }

    void Start() {
        FindSceneActors();
        if (generateMissingSceneColliders) {
            SceneCollisionBuilder.EnsureStaticMeshColliders();
        }

        ConfigureGameplayCamera();
        CreateQuestObjects();
        CreateAudio();
        CreateUi();
        ApplySettings();
        ShowStartMenu();
    }

    void Update() {
        if (PausePressed()) {
            TogglePause();
        }

        if (state == GameState.Playing) {
            UpdateQuest();
            UpdateHud();
        }

        if (!deathHandled && player != null && player.IsDead) {
            deathHandled = true;
            FinishRunIfNeeded();
            StopAmbientMusicForResult();
            PlaySfx(deathClip);
            lossMenuTime = Time.unscaledTime + 1.5f;
        }

        if (lossMenuTime > 0f && Time.unscaledTime >= lossMenuTime) {
            lossMenuTime = -1f;
            ShowLoseMenu();
        }

        AnimateQuestObjects();
    }

    void FindSceneActors() {
        player = FindObjectOfType<PlayerController>();
        aiControllers = FindObjectsOfType<AIController>();
        agentManager = FindObjectOfType<AgentManager>();
    }

    void ConfigureGameplayCamera() {
        if (player == null) {
            FindSceneActors();
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null) {
            mainCamera = FindObjectOfType<Camera>();
        }

        if (mainCamera == null) {
            GameObject cameraObject = new GameObject("Main Camera");
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        mainCamera.gameObject.tag = "MainCamera";
        mainCamera.enabled = true;

        if (mainCamera.GetComponent<AudioListener>() == null) {
            mainCamera.gameObject.AddComponent<AudioListener>();
        }

        CameraFollow360 follow = mainCamera.GetComponent<CameraFollow360>();
        if (follow == null) {
            follow = mainCamera.gameObject.AddComponent<CameraFollow360>();
        }

        follow.autoFindPlayer = true;
        follow.distance = 6.5f;
        follow.height = 4f;
        follow.lookOffset = new Vector3(0f, 1.35f, 0f);
        follow.cameraSpeed = 18f;
        follow.rotationSpeed = 18f;
        follow.allowMouseOrbit = true;
        follow.orbitMouseButton = 1;
        follow.requireMouseButtonForOrbit = true;
        follow.lockCursorDuringGameplay = false;
        follow.mouseSensitivity = 3f;
        follow.startPitch = 18f;
        follow.minPitch = -12f;
        follow.maxPitch = 65f;
        follow.minDistance = 3f;
        follow.maxDistance = 14f;
        follow.zoomSensitivity = 4f;
        follow.followPlayerFacingUntilMouseOrbit = false;
        follow.followSmoothTime = 0.08f;
        follow.positionSmoothTime = 0.09f;
        follow.avoidObstacles = true;

        if (player != null) {
            player.cameraTransform = mainCamera.transform;
            player.moveRelativeToCamera = true;
            player.useRawInput = true;
            player.rotationSpeed = Mathf.Max(player.rotationSpeed, 720f);
            player.acceleration = Mathf.Max(player.acceleration, 32f);
            player.deceleration = Mathf.Max(player.deceleration, 42f);
            follow.SetTarget(player.transform, true);
        }
    }

    void CreateQuestObjects() {
        SelectQuestSpawnPositions();

        artifactRoot = new GameObject("Blender Escape Artifact");
        artifactRoot.transform.position = currentArtifactSpawnPosition;
        SphereCollider artifactCollider = artifactRoot.AddComponent<SphereCollider>();
        artifactCollider.isTrigger = true;
        artifactCollider.radius = collectDistance * 0.5f;

        GameObject artifactPrefab = Resources.Load<GameObject>(blenderArtifactResourcePath);
        if (artifactPrefab == null) {
            artifactPrefab = Resources.Load<GameObject>(artifactResourcePath);
        }

        if (artifactPrefab != null) {
            artifactVisual = Instantiate(artifactPrefab, artifactRoot.transform);
            artifactVisual.transform.localPosition = Vector3.zero;
            artifactVisual.transform.localRotation = Quaternion.identity;
            artifactVisual.transform.localScale = Vector3.one * 0.75f;
        } else {
            artifactVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            artifactVisual.name = "Fallback Artifact";
            artifactVisual.transform.SetParent(artifactRoot.transform, false);
            artifactVisual.transform.localScale = Vector3.one * 0.75f;
            Renderer renderer = artifactVisual.GetComponent<Renderer>();
            if (renderer != null) {
                renderer.material = CreateMaterial(new Color(0.1f, 0.9f, 1f, 1f));
            }
        }

        portalRoot = new GameObject("Escape Portal");
        portalRoot.transform.position = currentPortalSpawnPosition;
        CreatePortalVisuals();
    }

    void SelectQuestSpawnPositions() {
        currentArtifactSpawnPosition = artifactSpawnPosition;
        currentPortalSpawnPosition = portalSpawnPosition;

        if (!randomizeQuestSpawns) {
            return;
        }

        Vector3 artifactPosition;
        Vector3 portalPosition;
        if (TryFindSeparatedQuestSpawns(out artifactPosition, out portalPosition)) {
            currentArtifactSpawnPosition = artifactPosition;
            currentPortalSpawnPosition = portalPosition;
        }
    }

    bool TryFindSeparatedQuestSpawns(out Vector3 artifactPosition, out Vector3 portalPosition) {
        artifactPosition = artifactSpawnPosition;
        portalPosition = portalSpawnPosition;

        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        if (triangulation.vertices == null || triangulation.vertices.Length == 0 || triangulation.indices == null || triangulation.indices.Length < 3) {
            return false;
        }

        int attempts = Mathf.Max(12, questSpawnAttempts);
        for (int i = 0; i < attempts; i++) {
            Vector3 candidateArtifact;
            if (!TryRandomNavMeshPoint(triangulation, out candidateArtifact) || !IsQuestSpawnCandidate(candidateArtifact)) {
                continue;
            }

            for (int j = 0; j < attempts; j++) {
                Vector3 candidatePortal;
                if (!TryRandomNavMeshPoint(triangulation, out candidatePortal) || !IsQuestSpawnCandidate(candidatePortal)) {
                    continue;
                }

                if (PlanarDistance(candidateArtifact, candidatePortal) < minArtifactPortalDistance) {
                    continue;
                }

                artifactPosition = candidateArtifact;
                portalPosition = candidatePortal;
                return true;
            }
        }

        return false;
    }

    bool TryRandomNavMeshPoint(NavMeshTriangulation triangulation, out Vector3 position) {
        position = Vector3.zero;
        int triangleCount = triangulation.indices.Length / 3;
        if (triangleCount <= 0) {
            return false;
        }

        int triangleStart = Random.Range(0, triangleCount) * 3;
        Vector3 a = triangulation.vertices[triangulation.indices[triangleStart]];
        Vector3 b = triangulation.vertices[triangulation.indices[triangleStart + 1]];
        Vector3 c = triangulation.vertices[triangulation.indices[triangleStart + 2]];
        Vector3 randomPoint = RandomPointInTriangle(a, b, c);

        NavMeshHit hit;
        if (!NavMesh.SamplePosition(randomPoint, out hit, navMeshSpawnSampleRadius, NavMesh.AllAreas)) {
            return false;
        }

        position = hit.position;
        return true;
    }

    Vector3 RandomPointInTriangle(Vector3 a, Vector3 b, Vector3 c) {
        float r1 = Mathf.Sqrt(Random.value);
        float r2 = Random.value;
        return ((1f - r1) * a) + (r1 * (1f - r2) * b) + (r1 * r2 * c);
    }

    bool IsQuestSpawnCandidate(Vector3 position) {
        if (player != null && PlanarDistance(position, player.transform.position) < minPlayerSpawnDistance) {
            return false;
        }

        if (!IsSpawnHeightReasonable(position)) {
            return false;
        }

        if (!IsReachableFromPlayer(position)) {
            return false;
        }

        if (!HasEnoughNavMeshSpace(position)) {
            return false;
        }

        if (!HasSpawnClearance(position)) {
            return false;
        }

        return true;
    }

    bool IsSpawnHeightReasonable(Vector3 position) {
        if (player == null) {
            return true;
        }

        float heightDifference = position.y - player.transform.position.y;
        return heightDifference <= maxSpawnHeightAbovePlayer && heightDifference >= -maxSpawnHeightBelowPlayer;
    }

    bool IsReachableFromPlayer(Vector3 position) {
        if (player == null) {
            return true;
        }

        NavMeshHit playerHit;
        if (!NavMesh.SamplePosition(player.transform.position, out playerHit, navMeshSpawnSampleRadius, NavMesh.AllAreas)) {
            return false;
        }

        NavMeshHit targetHit;
        if (!NavMesh.SamplePosition(position, out targetHit, navMeshSpawnSampleRadius, NavMesh.AllAreas)) {
            return false;
        }

        NavMeshPath path = new NavMeshPath();
        return NavMesh.CalculatePath(playerHit.position, targetHit.position, NavMesh.AllAreas, path)
            && path.status == NavMeshPathStatus.PathComplete;
    }

    bool HasEnoughNavMeshSpace(Vector3 position) {
        if (minSpawnEdgeDistance <= 0f) {
            return true;
        }

        NavMeshHit edgeHit;
        return NavMesh.FindClosestEdge(position, out edgeHit, NavMesh.AllAreas)
            && edgeHit.distance >= minSpawnEdgeDistance;
    }

    bool HasSpawnClearance(Vector3 position) {
        if (spawnClearanceRadius <= 0f || spawnClearanceHeight <= 0f) {
            return true;
        }

        Vector3 lowerPoint = position + Vector3.up * (spawnClearanceRadius + 0.15f);
        Vector3 upperPoint = position + Vector3.up * spawnClearanceHeight;
        Collider[] colliders = Physics.OverlapCapsule(
            lowerPoint,
            upperPoint,
            spawnClearanceRadius,
            questSpawnBlockingMask,
            QueryTriggerInteraction.Ignore);

        foreach (Collider collider in colliders) {
            if (IsQuestSpawnBlockingCollider(collider)) {
                return false;
            }
        }

        return true;
    }

    bool IsQuestSpawnBlockingCollider(Collider collider) {
        if (collider == null || collider.isTrigger) {
            return false;
        }

        if (player != null && collider.transform.IsChildOf(player.transform)) {
            return false;
        }

        if (artifactRoot != null && collider.transform.IsChildOf(artifactRoot.transform)) {
            return false;
        }

        if (portalRoot != null && collider.transform.IsChildOf(portalRoot.transform)) {
            return false;
        }

        return true;
    }

    void PlaceQuestObjectsForRun() {
        SelectQuestSpawnPositions();
        RemoveCarriedArtifactVisual();

        if (artifactRoot != null) {
            artifactRoot.SetActive(true);
            artifactRoot.transform.position = currentArtifactSpawnPosition;
            artifactRoot.transform.rotation = Quaternion.identity;
        }

        if (portalRoot != null) {
            portalRoot.transform.position = currentPortalSpawnPosition;
            portalRoot.transform.rotation = Quaternion.identity;
            currentPortalGlowColor = GetTargetPortalGlowColor();
            ApplyPortalGlowColor(currentPortalGlowColor);
        }
    }

    void CreatePortalVisuals() {
        GameObject portalPrefab = Resources.Load<GameObject>(portalResourcePath);
        if (portalPrefab != null) {
            portalVisual = Instantiate(portalPrefab, portalRoot.transform);
            portalVisual.transform.localPosition = Vector3.zero;
            portalVisual.transform.localRotation = Quaternion.identity;
            portalVisual.transform.localScale = Vector3.one;
            DisableVisualPhysics(portalVisual);
            CreatePortalGlow();
            return;
        }

        Material portalMaterial = CreateMaterial(new Color(0.1f, 1f, 0.35f, 1f));
        Material pillarMaterial = CreateMaterial(new Color(0.08f, 0.18f, 0.12f, 1f));

        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Portal Disc";
        disc.transform.SetParent(portalRoot.transform, false);
        disc.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        disc.transform.localScale = new Vector3(1.7f, 0.05f, 1.7f);
        disc.GetComponent<Renderer>().material = portalMaterial;
        Destroy(disc.GetComponent<Collider>());

        for (int i = -1; i <= 1; i += 2) {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "Portal Pillar";
            pillar.transform.SetParent(portalRoot.transform, false);
            pillar.transform.localPosition = new Vector3(i * 1.5f, 0.9f, 0f);
            pillar.transform.localScale = new Vector3(0.25f, 1.8f, 0.25f);
            pillar.GetComponent<Renderer>().material = pillarMaterial;
            Destroy(pillar.GetComponent<Collider>());
        }

        CreatePortalGlow();
    }

    void CreatePortalGlow() {
        if (portalRoot == null) {
            return;
        }

        GameObject glowObject = new GameObject("Portal Inner Glow");
        glowObject.transform.SetParent(portalRoot.transform, false);
        glowObject.transform.localPosition = new Vector3(0f, portalGlowCenterHeight, -0.045f);
        glowObject.transform.localRotation = Quaternion.identity;
        currentPortalGlowColor = GetTargetPortalGlowColor();

        portalGlowRing = glowObject.AddComponent<LineRenderer>();
        portalGlowRing.useWorldSpace = false;
        portalGlowRing.loop = true;
        portalGlowRing.positionCount = 96;
        portalGlowRing.widthMultiplier = portalGlowWidth;
        portalGlowRing.numCapVertices = 6;
        portalGlowRing.numCornerVertices = 6;
        portalGlowRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        portalGlowRing.receiveShadows = false;
        portalGlowMaterial = CreateGlowMaterial(currentPortalGlowColor);
        portalGlowRing.material = portalGlowMaterial;
        portalGlowRing.startColor = currentPortalGlowColor;
        portalGlowRing.endColor = currentPortalGlowColor;

        for (int i = 0; i < portalGlowRing.positionCount; i++) {
            float angle = ((float)i / portalGlowRing.positionCount) * Mathf.PI * 2f;
            portalGlowRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * portalGlowRadius, Mathf.Sin(angle) * portalGlowRadius, 0f));
        }

        portalGlowLight = glowObject.AddComponent<Light>();
        portalGlowLight.type = LightType.Point;
        portalGlowLight.color = currentPortalGlowColor;
        portalGlowLight.range = portalGlowLightRange;
        portalGlowLight.intensity = portalGlowLightIntensity;
        portalGlowLight.shadows = LightShadows.None;
    }

    void CreateAudio() {
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        musicSource.clip = Resources.Load<AudioClip>(ambientClipPath);
        pickupClip = Resources.Load<AudioClip>(pickupClipPath);
        deathClip = Resources.Load<AudioClip>(deathClipPath);
        victoryClip = Resources.Load<AudioClip>(victoryClipPath);

        EnsureAmbientMusicPlaying();
    }

    void EnsureAmbientMusicPlaying() {
        if (musicSource == null) {
            return;
        }

        if (musicSource.clip == null) {
            musicSource.clip = Resources.Load<AudioClip>(ambientClipPath);
        }

        if (musicSource.clip != null && !musicSource.isPlaying) {
            musicSource.Play();
        }
    }

    void StopAmbientMusicForResult() {
        if (musicSource != null && musicSource.isPlaying) {
            musicSource.Stop();
        }
    }

    void CreateUi() {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("Runtime Quest UI", typeof(RectTransform));
        uiCanvas = canvasObject.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        objectiveText = CreateText(uiCanvas.transform, "Objective", new Vector2(20f, -20f), new Vector2(620f, 80f), 22, TextAnchor.UpperLeft);
        objectiveText.rectTransform.anchorMin = new Vector2(0f, 1f);
        objectiveText.rectTransform.anchorMax = new Vector2(0f, 1f);
        objectiveText.color = Color.white;

        menuButton = CreateButton(uiCanvas.transform, "Settings", new Vector2(-92f, -42f), ShowPauseMenu);
        RectTransform menuButtonRect = menuButton.GetComponent<RectTransform>();
        menuButtonRect.anchorMin = new Vector2(1f, 1f);
        menuButtonRect.anchorMax = new Vector2(1f, 1f);
        menuButtonRect.sizeDelta = new Vector2(160f, 42f);
        menuButton.gameObject.SetActive(false);

        menuPanel = CreatePanel(uiCanvas.transform, "Menu Panel", Color.black);
        menuPanelImage = menuPanel.GetComponent<Image>();
        RectTransform panelRect = menuPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        titleText = CreateText(menuPanel.transform, "Title", new Vector2(0f, 205f), new Vector2(620f, 64f), 36, TextAnchor.MiddleCenter);
        messageText = CreateText(menuPanel.transform, "Message", new Vector2(0f, 145f), new Vector2(760f, 58f), 20, TextAnchor.MiddleCenter);
        resultStatsText = CreateText(menuPanel.transform, "Result Stats", new Vector2(0f, 55f), new Vector2(760f, 120f), 20, TextAnchor.MiddleCenter);
        resultStatsText.gameObject.SetActive(false);
        startButton = CreateButton(menuPanel.transform, "Start", new Vector2(0f, 78f), StartGame);
        resumeButton = CreateButton(menuPanel.transform, "Resume", new Vector2(0f, 78f), ResumeGame);
        restartButton = CreateButton(menuPanel.transform, "Restart", new Vector2(0f, 25f), RestartScene);

        settingsTitleText = CreateText(menuPanel.transform, "Settings Title", new Vector2(0f, -42f), new Vector2(420f, 34f), 22, TextAnchor.MiddleCenter);
        settingsTitleText.text = "SETTINGS";

        difficultyButton = CreateButton(menuPanel.transform, "Difficulty", new Vector2(0f, -92f), CycleDifficulty);
        difficultyText = difficultyButton.GetComponentInChildren<Text>();

        volumeDownButton = CreateButton(menuPanel.transform, "Volume -", new Vector2(-115f, -148f), DecreaseVolume);
        volumeDownButton.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 42f);
        volumeUpButton = CreateButton(menuPanel.transform, "Volume +", new Vector2(115f, -148f), IncreaseVolume);
        volumeUpButton.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 42f);
        volumeText = CreateText(menuPanel.transform, "Volume", new Vector2(0f, -194f), new Vector2(420f, 34f), 18, TextAnchor.MiddleCenter);
        controlsText = CreateText(menuPanel.transform, "Controls", new Vector2(0f, -235f), new Vector2(720f, 34f), 16, TextAnchor.MiddleCenter);
        controlsText.text = "WASD moves relative to camera. LMB commands monsters. Hold RMB to rotate camera. R resumes chase.";
        UpdateMenuText();
    }

    GameObject CreatePanel(Transform parent, string name, Color color) {
        GameObject panel = new GameObject(name, typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        Image image = panel.AddComponent<Image>();
        image.color = new Color(color.r, color.g, color.b, 0.78f);
        return panel;
    }

    Text CreateText(Transform parent, string name, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment) {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = GetBuiltinUiFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;

        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return text;
    }

    Font GetBuiltinUiFont() {
        Font font = null;

        try {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        } catch {
            font = null;
        }

        if (font != null) {
            return font;
        }

        try {
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        } catch {
            return null;
        }
    }

    Button CreateButton(Transform parent, string label, Vector2 position, UnityAction action) {
        GameObject buttonObject = new GameObject(label + " Button", typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.22f, 0.24f, 0.95f);
        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(action);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(240f, 46f);

        Text text = CreateText(buttonObject.transform, "Text", Vector2.zero, rect.sizeDelta, 20, TextAnchor.MiddleCenter);
        text.text = label;
        return button;
    }

    void EnsureEventSystem() {
        if (FindObjectOfType<EventSystem>() != null) {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    void ShowStartMenu() {
        state = GameState.StartMenu;
        Time.timeScale = 0f;
        SetGameplayEnabled(false);
        menuPanel.SetActive(true);
        SetMenuButtonVisible(false);
        ConfigureMenuPresentation(new Color(0f, 0f, 0f, 0.78f), true, false);
        SetDefaultMenuButtonLayout();
        titleText.text = "Escape Village";
        titleText.color = Color.white;
        messageText.text = "Collect the AI artifact, then reach the green portal.";
        messageText.color = Color.white;
        SetButtonLabel(startButton, "Start");
        SetButtonLabel(restartButton, "Restart");
        startButton.gameObject.SetActive(true);
        resumeButton.gameObject.SetActive(false);
        restartButton.gameObject.SetActive(false);
        UpdateHud();
        UpdateMenuText();
    }

    void StartGame() {
        artifactCollected = false;
        deathHandled = false;
        lossMenuTime = -1f;
        runStartedAt = Time.time;
        runFinishedAt = -1f;
        artifactCollectedAt = -1f;
        if (agentManager == null) {
            FindSceneActors();
        }

        if (agentManager != null) {
            agentManager.ResetRunStats();
        }

        state = GameState.Playing;
        ResetPlayerForNewRun();
        PlaceQuestObjectsForRun();
        ConfigureGameplayCamera();
        SetGameplayEnabled(true);
        menuPanel.SetActive(false);
        SetMenuButtonVisible(true);
        if (resultStatsText != null) {
            resultStatsText.gameObject.SetActive(false);
        }
        Time.timeScale = 1f;
        EnsureAmbientMusicPlaying();
        UpdateHud();
        ApplySettings();
    }

    void ResumeGame() {
        state = GameState.Playing;
        SetGameplayEnabled(true);
        menuPanel.SetActive(false);
        SetMenuButtonVisible(true);
        Time.timeScale = 1f;
        ApplySettings();
    }

    void TogglePause() {
        if (state == GameState.Playing) {
            ShowPauseMenu();
        } else if (state == GameState.Paused) {
            ResumeGame();
        }
    }

    void ShowPauseMenu() {
        state = GameState.Paused;
        Time.timeScale = 0f;
        SetGameplayEnabled(false);
        menuPanel.SetActive(true);
        SetMenuButtonVisible(false);
        ConfigureMenuPresentation(new Color(0f, 0f, 0f, 0.78f), true, false);
        SetDefaultMenuButtonLayout();
        titleText.text = "Settings";
        titleText.color = Color.white;
        messageText.text = "Pause menu";
        messageText.color = Color.white;
        SetButtonLabel(restartButton, "Restart");
        startButton.gameObject.SetActive(false);
        resumeButton.gameObject.SetActive(true);
        restartButton.gameObject.SetActive(true);
        UpdateMenuText();
    }

    void ShowWinMenu() {
        FinishRunIfNeeded();
        state = GameState.Won;
        Time.timeScale = 0f;
        SetGameplayEnabled(false);
        StopAmbientMusicForResult();
        PlaySfx(victoryClip);
        menuPanel.SetActive(true);
        SetMenuButtonVisible(false);
        ConfigureMenuPresentation(new Color(0.01f, 0.12f, 0.07f, 0.9f), false, true);
        SetResultMenuButtonLayout();
        titleText.text = "MISSION COMPLETE";
        titleText.color = new Color(0.55f, 1f, 0.75f, 1f);
        messageText.text = "The artifact is secured. The portal carried you out.";
        messageText.color = Color.white;
        resultStatsText.text = BuildResultStats(true);
        SetButtonLabel(restartButton, "Play Again");
        startButton.gameObject.SetActive(false);
        resumeButton.gameObject.SetActive(false);
        restartButton.gameObject.SetActive(true);
        UpdateHud();
    }

    void ShowLoseMenu() {
        FinishRunIfNeeded();
        state = GameState.Lost;
        Time.timeScale = 0f;
        SetGameplayEnabled(false);
        menuPanel.SetActive(true);
        SetMenuButtonVisible(false);
        ConfigureMenuPresentation(new Color(0.13f, 0.02f, 0.02f, 0.9f), false, true);
        SetResultMenuButtonLayout();
        titleText.text = "ESCAPE FAILED";
        titleText.color = new Color(1f, 0.38f, 0.32f, 1f);
        messageText.text = "A Chomper caught the player before the escape was complete.";
        messageText.color = Color.white;
        resultStatsText.text = BuildResultStats(false);
        SetButtonLabel(restartButton, "Try Again");
        startButton.gameObject.SetActive(false);
        resumeButton.gameObject.SetActive(false);
        restartButton.gameObject.SetActive(true);
        UpdateHud();
    }

    void UpdateQuest() {
        if (player == null) {
            FindSceneActors();
            return;
        }

        if (!artifactCollected && artifactRoot != null && DistanceToPlayer(artifactRoot.transform.position) <= collectDistance) {
            artifactCollected = true;
            artifactCollectedAt = Time.time;
            artifactRoot.SetActive(false);
            CreateCarriedArtifactVisual();
            PlaySfx(pickupClip);
            UpdateHud();
        }

        if (artifactCollected && portalRoot != null && DistanceToPlayer(portalRoot.transform.position) <= portalDistance) {
            ShowWinMenu();
        }
    }

    float DistanceToPlayer(Vector3 position) {
        Vector3 playerPosition = player.transform.position;
        playerPosition.y = 0f;
        position.y = 0f;
        return Vector3.Distance(playerPosition, position);
    }

    float PlanarDistance(Vector3 a, Vector3 b) {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void AnimateQuestObjects() {
        if (artifactRoot != null && artifactRoot.activeSelf) {
            artifactRoot.transform.Rotate(Vector3.up, 75f * Time.unscaledDeltaTime, Space.World);
            Vector3 pos = currentArtifactSpawnPosition;
            pos.y += Mathf.Sin(Time.unscaledTime * 2.2f) * 0.25f;
            artifactRoot.transform.position = pos;
        }

        if (portalRoot != null) {
            portalRoot.transform.Rotate(Vector3.up, 25f * Time.unscaledDeltaTime, Space.World);
            AnimatePortalGlow();
        }

        if (carriedArtifactVisual != null) {
            Vector3 carriedPosition = carriedArtifactLocalPosition;
            carriedPosition.y += Mathf.Sin(Time.unscaledTime * carriedArtifactBobFrequency) * carriedArtifactBobAmplitude;
            carriedArtifactVisual.transform.localPosition = carriedPosition;
            carriedArtifactVisual.transform.localRotation = Quaternion.Euler(0f, Time.unscaledTime * carriedArtifactSpinSpeed, 0f);
        }
    }

    void AnimatePortalGlow() {
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * portalGlowPulseSpeed) * portalGlowPulseAmount;
        pulse = Mathf.Max(0.1f, pulse);
        Color targetColor = GetTargetPortalGlowColor();
        float colorLerp = 1f - Mathf.Exp(-Mathf.Max(0.01f, portalGlowColorBlendSpeed) * Time.unscaledDeltaTime);
        currentPortalGlowColor = Color.Lerp(currentPortalGlowColor, targetColor, colorLerp);

        if (portalGlowRing != null) {
            portalGlowRing.widthMultiplier = portalGlowWidth * pulse;
            Color ringColor = currentPortalGlowColor;
            ringColor.a = Mathf.Clamp01(currentPortalGlowColor.a * (0.75f + pulse * 0.25f));
            portalGlowRing.startColor = ringColor;
            portalGlowRing.endColor = ringColor;

            if (portalGlowMaterial != null) {
                portalGlowMaterial.color = ringColor;
                if (portalGlowMaterial.HasProperty("_EmissionColor")) {
                    portalGlowMaterial.SetColor("_EmissionColor", ringColor * 2.0f);
                }
            }
        }

        if (portalGlowLight != null) {
            portalGlowLight.color = currentPortalGlowColor;
            portalGlowLight.intensity = portalGlowLightIntensity * pulse;
            portalGlowLight.range = portalGlowLightRange * (0.92f + pulse * 0.08f);
        }
    }

    Color GetTargetPortalGlowColor() {
        return artifactCollected ? portalActiveGlowColor : portalLockedGlowColor;
    }

    void ApplyPortalGlowColor(Color color) {
        currentPortalGlowColor = color;

        if (portalGlowRing != null) {
            portalGlowRing.startColor = color;
            portalGlowRing.endColor = color;
        }

        if (portalGlowMaterial != null) {
            portalGlowMaterial.color = color;
            if (portalGlowMaterial.HasProperty("_EmissionColor")) {
                portalGlowMaterial.SetColor("_EmissionColor", color * 2.0f);
            }
        }

        if (portalGlowLight != null) {
            portalGlowLight.color = color;
        }
    }

    void CreateCarriedArtifactVisual() {
        if (!showCarriedArtifact || player == null || artifactVisual == null) {
            return;
        }

        RemoveCarriedArtifactVisual();
        carriedArtifactVisual = Instantiate(artifactVisual, player.transform);
        carriedArtifactVisual.name = "Carried Artifact Visual";
        carriedArtifactVisual.SetActive(true);
        carriedArtifactVisual.transform.localPosition = carriedArtifactLocalPosition;
        carriedArtifactVisual.transform.localRotation = Quaternion.identity;
        carriedArtifactVisual.transform.localScale = Vector3.one * Mathf.Max(0.01f, carriedArtifactScale);
        DisableVisualPhysics(carriedArtifactVisual);
    }

    void RemoveCarriedArtifactVisual() {
        if (carriedArtifactVisual == null) {
            return;
        }

        Destroy(carriedArtifactVisual);
        carriedArtifactVisual = null;
    }

    void DisableVisualPhysics(GameObject visual) {
        Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders) {
            collider.enabled = false;
        }

        Rigidbody[] rigidbodies = visual.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody body in rigidbodies) {
            body.useGravity = false;
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        NavMeshAgent[] agents = visual.GetComponentsInChildren<NavMeshAgent>(true);
        foreach (NavMeshAgent agent in agents) {
            agent.enabled = false;
        }
    }

    void UpdateHud() {
        if (objectiveText == null) {
            return;
        }

        string objective;
        if (state == GameState.Won) {
            objective = "Objective complete";
        } else if (state == GameState.Lost) {
            objective = "Objective failed";
        } else if (artifactCollected) {
            objective = "Artifact collected. Reach the green portal.";
        } else {
            objective = "Find the AI artifact. Press Esc for settings.";
        }

        objectiveText.text = GetHealthHudText() + "\n" + objective;
    }

    string GetHealthHudText() {
        if (player == null) {
            FindSceneActors();
        }

        if (player == null) {
            return "Health: -";
        }

        return "Health: " + player.CurrentHealth + "/" + player.MaxHealth;
    }

    bool PausePressed() {
        if (Input.GetKeyDown(KeyCode.Escape)) {
            return true;
        }

        try {
            return Input.GetButtonDown("Cancel");
        } catch {
            return false;
        }
    }

    void SetMenuButtonVisible(bool visible) {
        if (menuButton != null) {
            menuButton.gameObject.SetActive(visible);
        }
    }

    void ConfigureMenuPresentation(Color panelColor, bool settingsVisible, bool resultStatsVisible) {
        if (menuPanelImage != null) {
            menuPanelImage.color = panelColor;
        }

        SetSettingsControlsVisible(settingsVisible);

        if (resultStatsText != null) {
            resultStatsText.gameObject.SetActive(resultStatsVisible);
        }
    }

    void SetSettingsControlsVisible(bool visible) {
        if (settingsTitleText != null) {
            settingsTitleText.gameObject.SetActive(visible);
        }

        if (difficultyButton != null) {
            difficultyButton.gameObject.SetActive(visible);
        }

        if (volumeDownButton != null) {
            volumeDownButton.gameObject.SetActive(visible);
        }

        if (volumeUpButton != null) {
            volumeUpButton.gameObject.SetActive(visible);
        }

        if (volumeText != null) {
            volumeText.gameObject.SetActive(visible);
        }

        if (controlsText != null) {
            controlsText.gameObject.SetActive(visible);
        }
    }

    void SetDefaultMenuButtonLayout() {
        SetButtonPosition(startButton, new Vector2(0f, 78f));
        SetButtonPosition(resumeButton, new Vector2(0f, 78f));
        SetButtonPosition(restartButton, new Vector2(0f, 25f));
    }

    void SetResultMenuButtonLayout() {
        SetButtonPosition(restartButton, new Vector2(0f, -55f));
    }

    void SetButtonPosition(Button button, Vector2 position) {
        if (button == null) {
            return;
        }

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect != null) {
            rect.anchoredPosition = position;
        }
    }

    void SetButtonLabel(Button button, string label) {
        if (button == null) {
            return;
        }

        Text text = button.GetComponentInChildren<Text>();
        if (text != null) {
            text.text = label;
        }
    }

    void FinishRunIfNeeded() {
        if (runFinishedAt < 0f) {
            runFinishedAt = Time.time;
        }
    }

    string BuildResultStats(bool won) {
        string timeLabel = won ? "Escape time: " : "Survived: ";
        string artifactLine = artifactCollected
            ? "Artifact: recovered at " + FormatTime(Mathf.Max(0f, artifactCollectedAt - runStartedAt))
            : "Artifact: not recovered";

        return timeLabel + FormatTime(GetRunElapsedSeconds()) + "\n"
            + artifactLine + "\n"
            + "Difficulty: " + difficulty + "    Hunters: " + GetHunterCount() + "\n"
            + "Monster commands: " + GetMonsterCommandCount();
    }

    float GetRunElapsedSeconds() {
        if (runStartedAt < 0f) {
            return 0f;
        }

        float endTime = runFinishedAt >= 0f ? runFinishedAt : Time.time;
        return Mathf.Max(0f, endTime - runStartedAt);
    }

    string FormatTime(float seconds) {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return minutes.ToString("00") + ":" + remainingSeconds.ToString("00");
    }

    int GetMonsterCommandCount() {
        if (agentManager == null) {
            FindSceneActors();
        }

        return agentManager != null ? agentManager.ManualCommandCount : 0;
    }

    int GetHunterCount() {
        if (aiControllers == null || aiControllers.Length == 0) {
            aiControllers = FindObjectsOfType<AIController>();
        }

        return aiControllers != null ? aiControllers.Length : 0;
    }

    int GetPlayerMaxHealthForDifficulty() {
        switch (difficulty) {
            case DifficultyLevel.Easy:
                return 3;
            case DifficultyLevel.Hard:
                return 1;
            default:
                return 2;
        }
    }

    void ConfigurePlayerHealthForDifficulty(bool refillHealth) {
        if (player == null) {
            player = FindObjectOfType<PlayerController>();
        }

        if (player != null) {
            player.SetMaxHealth(GetPlayerMaxHealthForDifficulty(), refillHealth);
        }
    }

    void SetGameplayEnabled(bool enabled) {
        if (player == null || aiControllers == null || agentManager == null) {
            FindSceneActors();
        }

        if (player != null && (enabled || !player.IsDead)) {
            player.enabled = enabled;
        }

        if (agentManager != null) {
            agentManager.enabled = enabled;
        }

        if (aiControllers == null) {
            return;
        }

        foreach (AIController controller in aiControllers) {
            if (controller == null) {
                continue;
            }

            controller.enabled = enabled;

            NavMeshAgent agent = controller.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh) {
                agent.isStopped = !enabled;
            }
        }
    }

    void CycleDifficulty() {
        difficulty = (DifficultyLevel)(((int)difficulty + 1) % 3);
        ApplySettings();
        UpdateMenuText();
    }

    void IncreaseVolume() {
        volume = Mathf.Clamp01(volume + 0.1f);
        ApplySettings();
        UpdateMenuText();
    }

    void DecreaseVolume() {
        volume = Mathf.Clamp01(volume - 0.1f);
        ApplySettings();
        UpdateMenuText();
    }

    void PlaySfx(AudioClip clip) {
        if (sfxSource == null || clip == null) {
            return;
        }

        sfxSource.PlayOneShot(clip);
    }

    void ApplySettings() {
        AudioListener.volume = volume;

        if (musicSource != null) {
            musicSource.volume = volume * 0.55f;
        }

        if (sfxSource != null) {
            sfxSource.volume = volume;
        }

        ConfigurePlayerHealthForDifficulty(state != GameState.Playing && state != GameState.Paused);

        if (aiControllers == null || aiControllers.Length == 0) {
            aiControllers = FindObjectsOfType<AIController>();
        }

        float speed = 3.5f;
        float attackDistance = 2.2f;

        if (difficulty == DifficultyLevel.Easy) {
            speed = 2.5f;
            attackDistance = 1.8f;
        } else if (difficulty == DifficultyLevel.Hard) {
            speed = 5.0f;
            attackDistance = 2.6f;
        }

        foreach (AIController controller in aiControllers) {
            if (controller == null) {
                continue;
            }

            controller.attackDistance = attackDistance;
            NavMeshAgent agent = controller.GetComponent<NavMeshAgent>();
            if (agent != null) {
                agent.speed = speed;
            }
        }

        UpdateHud();
    }

    void UpdateMenuText() {
        if (difficultyText != null) {
            difficultyText.text = "Difficulty: " + difficulty;
        }

        if (volumeText != null) {
            volumeText.text = "Volume: " + Mathf.RoundToInt(volume * 100f) + "%";
        }
    }

    void RestartScene() {
        PlayerController.dead = false;
        Time.timeScale = 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.name);
    }

    void ResetPlayerForNewRun() {
        if (player == null) {
            FindSceneActors();
        }

        if (player != null) {
            player.SetMaxHealth(GetPlayerMaxHealthForDifficulty(), true);
            player.ResetLifeState();
        } else {
            PlayerController.dead = false;
        }
    }

    Material CreateMaterial(Color color) {
        Shader shader = Shader.Find("Standard");
        if (shader == null) {
            shader = Shader.Find("Diffuse");
        }

        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    Material CreateGlowMaterial(Color color) {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null) {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.color = color;

        if (material.HasProperty("_EmissionColor")) {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2.0f);
        }

        return material;
    }
}

public static class SceneCollisionBuilder {

    public static int EnsureStaticMeshColliders() {
        MeshFilter[] meshFilters = Object.FindObjectsOfType<MeshFilter>();
        int addedCount = 0;

        foreach (MeshFilter meshFilter in meshFilters) {
            if (meshFilter == null || meshFilter.sharedMesh == null) {
                continue;
            }

            GameObject candidate = meshFilter.gameObject;
            MeshRenderer renderer = candidate.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled || !candidate.activeInHierarchy) {
                continue;
            }

            if (candidate.GetComponent<Collider>() != null || ShouldSkip(candidate)) {
                continue;
            }

            MeshCollider collider = candidate.AddComponent<MeshCollider>();
            collider.sharedMesh = meshFilter.sharedMesh;
            collider.convex = false;
            addedCount++;
        }

        return addedCount;
    }

    static bool ShouldSkip(GameObject candidate) {
        if (candidate.layer == 2 || candidate.layer == 5) {
            return true;
        }

        Transform current = candidate.transform;
        while (current != null) {
            GameObject gameObject = current.gameObject;

            if (gameObject.CompareTag("Player") || gameObject.CompareTag("AI")) {
                return true;
            }

            if (gameObject.GetComponent<Rigidbody>() != null
                || gameObject.GetComponent<PlayerController>() != null
                || gameObject.GetComponent<AIController>() != null
                || gameObject.GetComponent<NavMeshAgent>() != null
                || gameObject.GetComponent<AgentManager>() != null
                || gameObject.GetComponent<GameManager>() != null
                || gameObject.GetComponent<Camera>() != null
                || gameObject.GetComponent<Canvas>() != null) {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
