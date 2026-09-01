using System;
using System.Collections;
using MalbersAnimations;
using MalbersAnimations.Controller;
using Project.Common.Runtime;
using UnityEngine;
using UnityEngine.Rendering.Universal;


public class BuildingSystem : MonoBehaviour
{
    public enum eBuildingMode
    {
        Snap,
        ManualSnap,
        SnapFree,
        ManualSnapFree,
        End,
    }

    [Header("Building States")]
    public IBuildingState IdleState { get; private set; }
    public IBuildingState HoldingState { get; private set; }
    public IBuildingState RemoveState { get; private set; }

    private IBuildingState currentBuildingState;
    public IBuildingState prevBuildingState;
    public IBuildingState CurrentState => currentBuildingState;

    [Header("Building Dependencies")]
    public BuildOrRemove buildOrRemove;
    public BuildingMaterialManagement buildingMaterialManagement;
    public StructuralIntegritySolver integritySolver;
    public SnapController snapController;
    public PlacementValidator placementValidator;
    public BuildingInputHandler inputHandler;
    public PlayerInventoryAdapter inventoryAdapter;
    [SerializeField] public UIManager uiManager;

    [Header("Player / Animation")]
    public Transform playerTransform;
    public MAnimal mAnimal;
    public ModeID buildModeID;
    public int buildModeIndex;

    [Header("Input")]
    [SerializeField, Tooltip("Hold to temporarily disable snapping while placing a building.")]
    private KeyCode snapFreeModifierKey = KeyCode.LeftControl;
    [SerializeField, Tooltip("Optional secondary key for temporarily disabling snapping.")]
    private KeyCode secondarySnapFreeModifierKey = KeyCode.RightControl;

    [Header("Preview Settings")]
    [Min(0f)] public float maxDistance = 4f;
    [Tooltip("Legacy serialized field retained for prefab compatibility.")]
    public float minDistance = 1f;
    [SerializeField, Min(0f)] private float visualInterpolationSpeed = 25f;
    public GameObject curSnapCheckPrefab;

    [Header("Visual Indicators")]
    public GameObject rangeIndicatorObj;
    public Color InRangeColor = new Color(0.2f, 1f, 0.5f, 0.45f);
    public Color notInRangeColor = new Color(1f, 0.4f, 0.4f, 0.45f);

    public Action<eBuildingMode> OnSnapModeChanged;
    public Action<IBuildingState> OnBuildingModeChanged;
    public Action<BuildingDataSO> OnHoldingMaterialChanged;
    public Action<BuildingDataSO> OnShowRequirements;
    public Action OnHideRequirements;

    public bool bIsRemoveMode;
    public bool IsBuildToolEquipped { get; private set; }
    public bool bIsBuildingMode => isBuildingModeActive;
    public BuildingInputHandler InputHandler => inputHandler;

  
    public bool pivotAttached;

    private eBuildingMode currentBuildingMode = eBuildingMode.Snap;
    private bool isBuildingModeActive;
    private bool isInitialized;
    private bool cursorVisibilityRequested;
    private Coroutine dependencyRoutine;

    private BuildingPreviewController previewController;
    private BuildingPlacementService placementService;
    private BuildingRemovalService removalService;

    public eBuildingMode curBuildingMode
    {
        get => currentBuildingMode;
        set
        {
            if (currentBuildingMode == value)
            {
                return;
            }

            currentBuildingMode = value;
            previewController?.OnBuildingModeChanged(value);
            OnSnapModeChanged?.Invoke(value);
        }
    }

    private void Awake()
    {
        CreateStates();

        if (!EnsureInitialized())
        {
            StartDependencyPolling();
        }

        ChangeState(IdleState);
    }

    private void Start()
    {
        EnsureInitialized();
        ResetDecalRange();
    }

    private void Update()
    {
        if (!EnsureInitialized())
        {
            return;
        }

        if (uiManager != null && uiManager.isInventoryOpen)
        {
            return;
        }

        currentBuildingState?.Update(this); //현재 상태 업데이트
    }

    private void OnDisable()
    {
        ReleaseBuildingCursorVisibility();

        if (dependencyRoutine != null)
        {
            StopCoroutine(dependencyRoutine);
            dependencyRoutine = null;
        }

        isInitialized = false;
    }

    private void OnDestroy()
    {
        ReleaseBuildingCursorVisibility();
        previewController?.Dispose();
    }

    public bool EnsureInitialized()
    {
        if (isInitialized && AreRequiredDependenciesReady() && previewController != null)
        {
            return true;
        }

        isInitialized = false;
        if (TryResolveDependencies())
        {
            isInitialized = true;
            return true;
        }

        if (dependencyRoutine == null && isActiveAndEnabled)
        {
            StartDependencyPolling();
        }

        return false;
    }

    public void SetBuildingModeActive(bool active)
    {
        if (isBuildingModeActive == active)
        {
            SyncCursorVisibilityForState();
            return;
        }

        isBuildingModeActive = active;
        SyncCursorVisibilityForState();
    }

    public void SetBuildToolEquipped(bool isEquipped)
    {
        if (IsBuildToolEquipped == isEquipped)
        {
            return;
        }

        IsBuildToolEquipped = isEquipped;
        if (!isEquipped)
        {
            if (currentBuildingState != IdleState)
            {
                ChangeState(IdleState);
                return;
            }

            GetBackToOtherMode();
        }

        SyncCursorVisibilityForState();
    }

    public void SetRemoveMode(bool enabled)
    {
        if (enabled)
        {
            if (currentBuildingState != RemoveState)
            {
                ChangeState(RemoveState);
            }

            return;
        }

        if (currentBuildingState != RemoveState)
        {
            return;
        }

        IBuildingState nextState =
            prevBuildingState == HoldingState && IsHoldingMaterial() ? HoldingState : IdleState;
        ChangeState(nextState);
    }

    public bool IsRemoveMode()
    {
        return bIsRemoveMode || currentBuildingState == RemoveState;
    }

    public bool IsConstructionMode()
    {
        return placementValidator != null && placementValidator.bConstructionMode;
    }

    public void ChangeState(IBuildingState newState)
    {
        ChangeState(newState, isBuildingModeActive);
    }

    public void ChangeState(IBuildingState newState, bool buildingModeActive)//건축 상태 변경
    {
        if (newState == null)
        {
            return;
        }

        currentBuildingState?.Exit(this);
        if (currentBuildingState != null)
        {
            prevBuildingState = currentBuildingState;
        }

        currentBuildingState = newState;
        isBuildingModeActive = buildingModeActive;
        bIsRemoveMode = newState == RemoveState;

        currentBuildingState.Enter(this);
        SyncCursorVisibilityForState();
        OnBuildingModeChanged?.Invoke(currentBuildingState);
    }

    public void ChangeHoldingMaterial(BuildingDataSO data)//자재 변경
    {
        if (!EnsureInitialized())
        {
            Debug.LogWarning("[BuildingSystem] Dependencies are not ready. ChangeHoldingMaterial was ignored.");
            return;
        }

        bool acquired = previewController.Begin(data);
        if (acquired)
        {
            ChangeState(HoldingState);
        }
        else if (currentBuildingState == HoldingState)
        {
            ChangeState(IdleState);
        }

        NotifyHoldingMaterialChanged();
    }

    public bool IsHoldingMaterial()
    {
        return previewController != null && previewController.HasMaterial;
    }

    public IMaterial GetCurMaterial()
    {
        return previewController != null ? previewController.CurrentMaterial : null;
    }

    public void ShowMaterial()//자재 보이게
    {
        previewController?.Show();
    }

    public void HideHeldPreviewForRemoval()
    {
        previewController?.HideTemporarily();
    }

    public void PosUpdate()//매 프레임 자재 위치 업데이트
    {
        previewController?.TickPosition();
    }

    public void MaterialPosUpdate(bool isSyncPosForFirstTime = false, bool debug = false)
    {
        if (previewController == null)
        {
            return;
        }

        inputHandler?.UpdateInputData();
        previewController.UpdatePosition(isSyncPosForFirstTime, debug);
    }

    public void MakeRotate()
    {
        previewController?.RotateFromInput();
    }

    public void RotatePreview(float angle)
    {
        previewController?.Rotate(angle);
    }

    public void ChangeSnapPoint(int direction)
    {
        previewController?.CycleSnapPoint(direction);
    }

    public void ToggleSnapMode()
    {
        previewController?.ToggleSnapMode();
    }

    public void ClearPreviewSnapState()
    {
        snapController?.ClearSnapState();
    }

    public bool IsPossibleToPlace()//배치 가능 여부
    {
        return placementService != null && placementService.CanPlace(previewController);
    }

    public void PlaceMaterial()//배치로직
    {
        if (!EnsureInitialized() || previewController == null || !previewController.HasMaterial)
        {
            return;
        }


        if (!IsPossibleToPlace())//배치여부 재판단
        {
            return;
        }

        BuildingPlacementResult result = placementService.TryCommit(previewController, this);//실제 배치
        if (!result.Succeeded)
        {
            return;
        }

        previewController.DetachCommittedMaterial();//프리뷰 자재관련 초기화

        if (result.HasNextPreview && previewController.Begin(result.NextPreviewData))
        {
            previewController.Show(result.PreservedPreviewRotation);//같은 자재 재료 있으면 이어서 보여줄 수 있게 가져오는 과정 반복
        }

        NotifyHoldingMaterialChanged();
    }

    public void ProcessRemoveMaterial()//삭제 검증 업데이트
    {
        removalService?.Tick(this);
    }

    public void RemoveMaterial(GameObject targetObject)//삭제 커밋
    {
        removalService?.TryRemove(targetObject, this);
    }

    public void ClearRemoveCandidate()//철거용 자재 기록 없앰(색깔 남아있는거 방지)
    {
        removalService?.ClearCandidate();
    }

    public void GetBackToOtherMode()//원상 복구
    {
        previewController?.Cancel();
        removalService?.ClearCandidate();
        buildOrRemove?.ResetHighlitedObject();
        snapController?.ClearSnapState();
        placementValidator?.ResetCache();
        NotifyHoldingMaterialChanged();
    }

    public void RequestShowRequirements(BuildingDataSO data)//인벤토리에서 선택한 자재 불러오는 로직 호출
    {
        if (data != null)
        {
            OnShowRequirements?.Invoke(data);
        }
    }

    public void RequestHideRequirements()//동일 자재 선택시 안 보이게 아닐 경우 보이게
    {
        IMaterial material = GetCurMaterial();
        if (material != null)
        {
            OnShowRequirements?.Invoke(material.Data);
        }
        else
        {
            OnHideRequirements?.Invoke();
        }
    }

    public void ResetHighlitedObject()
    {
        buildOrRemove?.ResetHighlitedObject();
    }

    public void UpdateHighlightTarget(GameObject targetRoot)
    {
        buildOrRemove?.SetHighlightTarget(targetRoot);
    }

    public void ResetDecalRange()
    {
        previewController?.ResetRangeIndicator();
    }

    public void OnBuildAction()
    {
        if (mAnimal != null)
        {
            mAnimal.Mode_Activate(buildModeID, buildModeIndex);
        }
    }

    private void CreateStates()
    {
        IdleState = new BuildingIdleState();
        HoldingState = new BuildingHoldingState();
        RemoveState = new BuildingRemoveState();
    }

    private bool TryResolveDependencies()
    {
        if (buildingMaterialManagement == null)
        {
            buildingMaterialManagement = GetComponent<BuildingMaterialManagement>();
        }

        if (buildOrRemove == null)
        {
            buildOrRemove = GetComponent<BuildOrRemove>();
        }

        if (integritySolver == null)
        {
            integritySolver = GetComponent<StructuralIntegritySolver>();
        }

        if (snapController == null)
        {
            snapController = GetComponent<SnapController>();
        }

        if (placementValidator == null)
        {
            placementValidator = GetComponent<PlacementValidator>();
        }

        if (inputHandler == null)
        {
            inputHandler = GetComponent<BuildingInputHandler>();
        }

        if (inventoryAdapter == null)
        {
            inventoryAdapter = GetComponent<PlayerInventoryAdapter>();
        }

        if (inventoryAdapter == null)
        {
            inventoryAdapter = GetComponentInChildren<PlayerInventoryAdapter>();
        }

        if (playerTransform == null && inputHandler != null && inputHandler.playerTransform != null)
        {
            playerTransform = inputHandler.playerTransform;
        }

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        if (!AreRequiredDependenciesReady())
        {
            return false;
        }

        inputHandler.InitializePlayer(playerTransform);
        snapController.InitializePlayer(playerTransform);
        integritySolver.InitializeDependencies(buildingMaterialManagement);
        buildOrRemove.InitializeDependencies(
            buildingMaterialManagement,
            integritySolver,
            snapController);
        placementValidator.InitializeDependencies(
            buildingMaterialManagement,
            integritySolver,
            snapController,
            buildOrRemove,
            inventoryAdapter);

        InitializeRuntimeServices();
        return previewController != null && placementService != null && removalService != null;
    }

    private bool AreRequiredDependenciesReady()
    {
        return buildingMaterialManagement != null &&
               buildOrRemove != null &&
               integritySolver != null &&
               snapController != null &&
               placementValidator != null &&
               inputHandler != null &&
               inventoryAdapter != null &&
               playerTransform != null;
    }

    private void InitializeRuntimeServices()
    {
        if (previewController != null)
        {
            return;
        }

        DecalProjector rangeDecal =
            rangeIndicatorObj != null ? rangeIndicatorObj.GetComponent<DecalProjector>() : null;

        previewController = new BuildingPreviewController(
            buildingMaterialManagement,
            snapController,
            inputHandler,
            playerTransform,
            () => uiManager != null && uiManager.isInventoryOpen,
            () => currentBuildingMode,
            mode => curBuildingMode = mode,
            snapFreeModifierKey,
            secondarySnapFreeModifierKey,
            maxDistance,
            visualInterpolationSpeed,
            curSnapCheckPrefab,
            rangeDecal,
            InRangeColor,
            notInRangeColor);

        placementService = new BuildingPlacementService(
            placementValidator,
            integritySolver,
            buildingMaterialManagement,
            buildOrRemove,
            inventoryAdapter,
            snapController);

        removalService = new BuildingRemovalService(
            inputHandler,
            placementValidator,
            buildOrRemove);

        previewController.OnBuildingModeChanged(currentBuildingMode);
    }

    private void StartDependencyPolling()
    {
        if (dependencyRoutine == null && isActiveAndEnabled)
        {
            dependencyRoutine = StartCoroutine(WaitForDependencies());
        }
    }

    private IEnumerator WaitForDependencies()
    {
        WaitForSeconds wait = new WaitForSeconds(0.1f);
        while (!TryResolveDependencies())
        {
            yield return wait;
        }

        isInitialized = true;
        dependencyRoutine = null;
    }

    private void SyncCursorVisibilityForState()
    {
        bool shouldShowCursor =
            isBuildingModeActive &&
            IsBuildToolEquipped &&
            (currentBuildingState == HoldingState || currentBuildingState == RemoveState);

        if (shouldShowCursor)
        {
            RuntimeCursorController.RequestVisible(this);
            cursorVisibilityRequested = true;
        }
        else
        {
            ReleaseBuildingCursorVisibility();
        }
    }

    private void ReleaseBuildingCursorVisibility()
    {
        if (!cursorVisibilityRequested)
        {
            return;
        }

        RuntimeCursorController.ReleaseVisible(this);
        cursorVisibilityRequested = false;
    }

    private void NotifyHoldingMaterialChanged()
    {
        IMaterial material = GetCurMaterial();
        OnHoldingMaterialChanged?.Invoke(material != null ? material.Data : null);
    }
}
