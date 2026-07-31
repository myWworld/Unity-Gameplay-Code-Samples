using UnityEngine;

public class BuildingInputHandler : MonoBehaviour
{
    [Header("References")]
    public Transform playerTransform;
    [SerializeField] private PlayerBuildingController playerBuildingController;

    public Vector3 MousePos { get; private set; }
    public RaycastHit CurHitData { get; private set; }
    public GameObject RayCastedObject { get; private set; }

    private Vector3 lastMousePos = Vector3.zero;

    void Awake()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        if (playerBuildingController == null)
        {
            playerBuildingController = FindFirstObjectByType<PlayerBuildingController>();
        }
    }

    public void UpdateInputData()
    {
        MousePos = GetMousePos();
        CurHitData = Mouse3D.GetHiDataFromRaycast();
        RayCastedObject = CurHitData.collider != null ? CurHitData.collider.gameObject : null;
    }

    private Vector3 GetMousePos()
    {
        if (Mouse3D.Instance != null)
        {
            return Mouse3D.GetMouseWorldPosition();
        }

        return playerTransform != null ? playerTransform.position : Vector3.zero;
    }


    public float GetRotationInput()
    {
        if (playerBuildingController != null && playerBuildingController.ShouldRouteBuildingScrollToCameraZoom)
        {
            return 0f;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            return (scroll > 0 ? 1 : -1) * 15f; // Rotate in 15-degree increments.
        }
        return 0f;
    }
}
