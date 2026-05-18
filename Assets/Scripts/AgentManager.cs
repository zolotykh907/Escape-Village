using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class AgentManager : MonoBehaviour {

    public GameObject destination;
    public float maxRaycastDistance = 500f;
    public KeyCode resumeChaseKey = KeyCode.R;
    public bool resumeChaseOnRightClick = false;

    List<AIController> controllers = new List<AIController>();
    List<NavMeshAgent> looseAgents = new List<NavMeshAgent>();
    public int ManualCommandCount { get; private set; }

    void Start() {
        RefreshAgents();
    }

    void Update() {
        if (Input.GetMouseButtonDown(0)) {
            CommandAgentsToMousePosition();
        }

        if ((resumeChaseOnRightClick && Input.GetMouseButtonDown(1)) || Input.GetKeyDown(resumeChaseKey)) {
            ResumeChase();
        }
    }

    void RefreshAgents() {
        controllers.Clear();
        looseAgents.Clear();

        GameObject[] taggedAgents = GameObject.FindGameObjectsWithTag("AI");
        foreach (GameObject go in taggedAgents) {
            AddAgent(go);
        }

        AIController[] allControllers = FindObjectsOfType<AIController>();
        foreach (AIController controller in allControllers) {
            if (!controllers.Contains(controller)) {
                controllers.Add(controller);
            }
        }
    }

    public void ResetRunStats() {
        ManualCommandCount = 0;
    }

    void AddAgent(GameObject go) {
        AIController controller = go.GetComponent<AIController>();
        if (controller != null) {
            controllers.Add(controller);
            return;
        }

        NavMeshAgent navMeshAgent = go.GetComponent<NavMeshAgent>();
        if (navMeshAgent != null) {
            looseAgents.Add(navMeshAgent);
        }
    }

    void CommandAgentsToMousePosition() {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) {
            return;
        }

        RaycastHit hit;
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out hit, maxRaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) {
            return;
        }

        Vector3 targetPosition = hit.point;
        ManualCommandCount++;

        if (destination != null) {
            destination.transform.position = targetPosition;
        }

        foreach (AIController controller in controllers) {
            controller.SetManualDestination(targetPosition);
        }

        foreach (NavMeshAgent navMeshAgent in looseAgents) {
            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh) {
                navMeshAgent.SetDestination(targetPosition);
            }
        }
    }

    void ResumeChase() {
        foreach (AIController controller in controllers) {
            controller.ResumeChase();
        }
    }
}
