using UnityEngine;

public class BuildingInputHandler : MonoBehaviour
{
    [Header("References")]
    public Transform playerTransform;
    [SerializeField] private PlayerBuildingController playerBuildingController;

    [Header("Action Input")]
    [SerializeField] private int placeMouseButton = 0;
    [SerializeField] private int secondaryActionMouseButton = 1;
    [SerializeField] private KeyCode toggleSnapModeKey = KeyCode.Q;
    [SerializeField] private KeyCode cycleSnapPointKey = KeyCode.E;
    [SerializeField, Min(1f)] private float rotationStep = 15f;

    public Vector3 MousePos { get; private set; }
    public RaycastHit CurHitData { get; private set; }
    public GameObject RayCastedObject { get; private set; }

    public bool WasPlacePressed => Input.GetMouseButtonDown(placeMouseButton);
    public bool WasSecondaryActionPressed => Input.GetMouseButtonDown(secondaryActionMouseButton);
    public bool WasToggleSnapModePressed => Input.GetKeyDown(toggleSnapModeKey);
    public bool WasCycleSnapPointPressed => Input.GetKeyDown(cycleSnapPointKey);

    private void Awake()
    {
        ResolveDependencies();
    }

    public void InitializePlayer(Transform player)
    {
        if (player != null)
        {
            playerTransform = player;
        }

        ResolveDependencies();
    }

    public void UpdateInputData()//지정한 레이어에 닿은 레이캐스트 정보 가져오기
    {
        MousePos = GetMousePos();

        if (Mouse3D.Instance == null)
        {
            CurHitData = default(RaycastHit);
            RayCastedObject = null;
            return;
        }

        CurHitData = Mouse3D.GetHiDataFromRaycast();
        RayCastedObject = CurHitData.collider != null ? CurHitData.collider.gameObject : null;
    }

    public bool IsAnyKeyHeld(KeyCode primary, KeyCode secondary)
    {
        bool primaryHeld = primary != KeyCode.None && Input.GetKey(primary);
        bool secondaryHeld = secondary != KeyCode.None && Input.GetKey(secondary);
        return primaryHeld || secondaryHeld;
    }

    public float GetRotationInput()
    {
        if (playerBuildingController != null && playerBuildingController.ShouldRouteBuildingScrollToCameraZoom)
        {
            return 0f;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Approximately(scroll, 0f))
        {
            return 0f;
        }

        return (scroll > 0f ? 1f : -1f) * rotationStep;
    }

    private Vector3 GetMousePos()
    {
        if (Mouse3D.Instance != null)
        {
            return Mouse3D.GetMouseWorldPosition();
        }

        return playerTransform != null ? playerTransform.position : Vector3.zero;
    }

    private void ResolveDependencies()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        if (playerBuildingController == null)
        {
            playerBuildingController = FindFirstObjectByType<PlayerBuildingController>();
        }
    }
}
