using UnityEngine;
using System.Collections;
using MalbersAnimations.Events;
using System;
using UnityEngine.Profiling;
using MalbersAnimations.Controller;
using MalbersAnimations;
using UnityEngine.Rendering.Universal;
using Project.Common.Runtime;
using Project.Gameplay.Items;
using KWS;


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

    private eBuildingMode _curBuildingMode = eBuildingMode.Snap;

    public eBuildingMode curBuildingMode
    {
        get { return _curBuildingMode; }
        set
        {

            if (_curBuildingMode != value)
            {
                _curBuildingMode = value;


                OnSnapModeChanged?.Invoke(_curBuildingMode);


                if (cubeForCheckCurSnap != null)
                {
                    bool isManual = (_curBuildingMode == eBuildingMode.ManualSnap || _curBuildingMode == eBuildingMode.ManualSnapFree);
                    cubeForCheckCurSnap.SetActive(isManual);
                }
            }
        }
    }

    public Action<eBuildingMode> OnSnapModeChanged;

    [Header("Building States")]
    public IBuildingState IdleState { get; private set; }
    public IBuildingState HoldingState { get; private set; }
    public IBuildingState RemoveState { get; private set; }

    private IBuildingState currentBuildingState;
    public IBuildingState prevBuildingState;
    public IBuildingState CurrentState
    {
        get { return currentBuildingState; }
    }

    public Action<IBuildingState> OnBuildingModeChanged;
    public Action<BuildingDataSO> OnHoldingMaterialChanged;


    [Header("other Components For building")]
    public BuildOrRemove buildOrRemove = null;
    public BuildingMaterialManagement buildingMaterialManagement = null;
    public StructuralIntegritySolver integritySolver = null;
    public SnapController snapController = null;
    public PlacementValidator placementValidator = null;
    public BuildingInputHandler inputHandler = null;
    public PlayerInventoryAdapter inventoryAdapter = null;

    [SerializeField] public UIManager uiManager;

    public MAnimal mAnimal;
    public ModeID buildModeID;
    public int buildModeIndex = 0;

    public Transform playerTransform;


    public bool bIsRemoveMode = false;

    [Header("Input")]
    [SerializeField, Tooltip("Hold to temporarily disable snapping while placing a building.")]
    private KeyCode snapFreeModifierKey = KeyCode.LeftControl;
    [SerializeField, Tooltip("Optional secondary key for temporarily disabling snapping.")]
    private KeyCode secondarySnapFreeModifierKey = KeyCode.RightControl;

    private bool isBuildingModeActive;
    public bool bIsBuildingMode => isBuildingModeActive;

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

    [Header("Variables for Material And Pos")]
    private IMaterial curMaterial = null;
    private Transform curMaterialTr = null;
    private GameObject curPivot = null;
    private GameObject curSnapPoint = null;
    private GameObject curPivotPoint = null; //이동이나 회전에 기준이 되는 축

    private Vector3 pivotPos;


    private Vector3 previousPosition;
    private IMaterial prevMaterial = null;


    [Header("Variables for Rotation")]
    private Vector3 initMatLocalRot = Vector3.zero; //자재의 초기 로컬 회전값 저장용 (회전시 사용)
    private Vector3 prevMatLocalRot = Vector3.zero;
    private float rotationY = 0f; // 회전 상태 저장
    public float maxDistance = 4.0f;
    public float minDistance = 1.0f;

    private Camera mainCamera = null;

    [Header("Variables for Snaps")]
    private Vector3 moveVelocity = Vector3.zero;

    private int curSnapIdx = 0;
    public GameObject curSnapCheckPrefab = null; //현재 스냅 포인트를 확인하기 위한 큐브 프리팹 (스냅 포인트가 활성화되면 해당 큐브가 활성화됨)
    private GameObject cubeForCheckCurSnap = null;
    private bool isInitialized;
    private Coroutine dependencyRoutine;
    private bool hasLoggedMissingSnapPrefab;
    private bool cursorVisibilityRequested;


    public bool IsBuildToolEquipped { get; private set; }

    public Action<BuildingDataSO> OnShowRequirements;
    public Action OnHideRequirements;

    [Header("Visual Indicators")]
    public GameObject rangeIndicatorObj;
    private DecalProjector rangeDecal;
    public Color InRangeColor = new Color(1.0f, 0.4f, 0.4f, 0.45f);
    public Color notInRangeColor = new Color(1.0f, 0.4f, 0.4f, 0.45f);

    [Header("Highlight Settings")]
    private GameObject currentHighlightedVisual = null;
    private int originalLayer = 0;
    private int highlightLayerIndex;

    private GameObject lastSnappedPivot = null;
    private GameObject cachedHighlightRoot = null;



    public void SetRemoveMode(bool isRemoveMode)
    {
        bool changed = bIsRemoveMode != isRemoveMode;
        bIsRemoveMode = isRemoveMode;

        if (bIsRemoveMode == true)
        {
            GetBackToOtherMode();
        }

        buildOrRemove?.ResetRemoveCandidate();

        if (changed)
        {
            OnBuildingModeChanged?.Invoke(bIsRemoveMode ? RemoveState : currentBuildingState);
        }
    }

    public bool IsRemoveMode()
    {
        return bIsRemoveMode || currentBuildingState == RemoveState;
    }

    public bool IsConstructionMode()
    {
        return placementValidator != null && placementValidator.bConstructionMode;
    }

    public void SetBuildToolEquipped(bool isEquipped)
    {
        if (IsBuildToolEquipped == isEquipped)
        {
            return;
        }

        IsBuildToolEquipped = isEquipped;
        if (!IsBuildToolEquipped)
        {
            if (IsHoldingMaterial())
            {
                GetBackToOtherMode();
            }
            else
            {
                NotifyHoldingMaterialChanged();
            }
        }
    }

    void Awake()
    {
        BMInitialize();
        ChangeState(IdleState);

    }

    private void Start()
    {

        if(rangeIndicatorObj != null)
        {
            rangeDecal = rangeIndicatorObj.GetComponent<DecalProjector>();
            if (rangeDecal != null)
            {
                rangeDecal.material.SetColor("_Color", InRangeColor);
            }
        }
    }

    private void OnDestroy()
    {
        ReleaseBuildingCursorVisibility();
    }

    private bool rotateAxis = true; //회전축을 Y로 설정할지 여부 (기본값은 Y축 회전)

    void Update()
    {
        if (!EnsureInitialized())
        {
            return;
        }


        if (uiManager.isInventoryOpen == true)
            return;

        Profiler.BeginSample("BuildingManagement Update");
        currentBuildingState?.Update(this);
        Profiler.EndSample();

    }

    public void ChangeState(IBuildingState newState)
    {
        ChangeState(newState, isBuildingModeActive);
    }

    public void ChangeState(IBuildingState newState, bool buildingModeActive)
    {
        currentBuildingState?.Exit(this);
        if(currentBuildingState != null)
            prevBuildingState = currentBuildingState;

        currentBuildingState = newState;
        isBuildingModeActive = buildingModeActive;
        bIsRemoveMode = newState == RemoveState;
        currentBuildingState?.Enter(this);
        SyncCursorVisibilityForState();
        OnBuildingModeChanged?.Invoke(currentBuildingState);
    }

    private void SyncCursorVisibilityForState()
    {
        bool shouldShowCursor = isBuildingModeActive &&
                                IsBuildToolEquipped &&
                                (currentBuildingState == HoldingState || currentBuildingState == RemoveState);

        if (shouldShowCursor)
        {
            RuntimeCursorController.RequestVisible(this);
            cursorVisibilityRequested = true;
            return;
        }

        ReleaseBuildingCursorVisibility();
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


    public void ProcessRemoveMaterial()
    {
        inputHandler.UpdateInputData();

        GameObject rayCastedObj = inputHandler.RayCastedObject;
        GameObject removeTarget = rayCastedObj;

        if (BuildingColliderUtility.TryResolveMaterialRoot(rayCastedObj, out GameObject materialRoot, out _))
        {
            removeTarget = materialRoot;
        }

        // Validator에게 검사 위임
        if (removeTarget != null && placementValidator.IsRemovableLayer(removeTarget.layer))
        {
            buildOrRemove.RemoveCandidateColorChange(removeTarget, Color.blue);
            if (Input.GetMouseButtonDown(0)) RemoveMaterial(removeTarget);
        }
        else
        {
            buildOrRemove.ResetRemoveCandidate();
        }
    }

    public void ChangeSnapPoint(int val)
    {

        if (curMaterial == null)
            return;

        int anchorCnt = curMaterial.GetAnchors().Count; //현재 스냅 포인트 인덱스 저장
        int maxCnt = anchorCnt * 2;
        int curCnt = 0;
        int idx = curSnapIdx + val;

        if (idx < 0)
        {
            idx = anchorCnt - 1; //마지막 인덱스로 설정
        }
        else if (idx >= anchorCnt)
        {
            idx = 0; //첫번째 인덱스로 설정

        }

        GameObject snapPoint = null;


        while (curCnt < maxCnt)
        {
            snapPoint = curMaterial.GetAnchorByIndx(idx);

            if (snapPoint == null || (snapPoint.CompareTag(LayerAndTagConstants.Tag_Pivot) == false && snapPoint.CompareTag(LayerAndTagConstants.Tag_DoorPivot) == false))
            {
                idx += val; //다음 인덱스로 이동

                if (idx < 0)
                {
                    idx = anchorCnt - 1; //마지막 인덱스로 설정
                }
                else if (idx >= anchorCnt)
                {
                    idx = 0; //첫번째 인덱스로 설정

                }
                curCnt++;
                continue;
            }


            curSnapIdx = idx; //현재 스냅 포인트 인덱스 업데이트
            curSnapPoint = snapPoint; //현재 스냅 포인트 업데이트
            //UnityEngine.Debug.Log($"Snap point changed to index {curSnapIdx} and its name is {curSnapPoint.name}");
            break;

        }


        cubeForCheckCurSnap.transform.SetParent(curSnapPoint.transform, false);
        cubeForCheckCurSnap.transform.localPosition = Vector3.zero;
        cubeForCheckCurSnap.transform.localRotation = Quaternion.identity;

    }

    public void ToggleSnapMode()
    {
        if (curBuildingMode == eBuildingMode.Snap || curBuildingMode == eBuildingMode.SnapFree)
        {
            curBuildingMode = eBuildingMode.ManualSnap;
            ChangeSnapPoint(0); //수동 스냅으로 전환할 때 현재 스냅 포인트로 설정
        }
        else
        {
            curBuildingMode = eBuildingMode.Snap;
        }


    }


    public void PosUpdate()
    {
        if(curMaterial == null)
            return;

        inputHandler.UpdateInputData();

        bool isOutOfRange = NotWithinCoverage();
        GameObject materialObj = curMaterialTr.gameObject;

        if(isOutOfRange)
        {
           // UnityEngine.Debug.Log("Not In Range");
           rangeDecal.material.SetColor("_Color", notInRangeColor);
        }
        else
        {
            rangeDecal.material.SetColor("_Color", InRangeColor);
        }

        if (materialObj.activeSelf == isOutOfRange)
        {
            materialObj.SetActive(!isOutOfRange);
        }

        if (!isOutOfRange)
        {
            MaterialPosUpdate();
        }

    }



    void OnDrawGizmos()
    {
        Gizmos.color = Color.red; // 색상 지정
        Gizmos.DrawSphere(inputHandler.MousePos, 0.25f); // 구체 그리기
    }



    private void BMInitialize()
    {
        if (!EnsureInitialized())
        {
            StartDependencyPolling();
        }

        IdleState = new BuildingIdleState();
        HoldingState = new BuildingHoldingState();
        RemoveState = new BuildingRemoveState();

    } //해당 오브젝트나 객체 받아오는 첫 단계

    public bool EnsureInitialized()
    {
        if (placementValidator == null || inputHandler == null || inventoryAdapter == null || buildingMaterialManagement == null || buildOrRemove == null || snapController == null || inventoryAdapter == null ||
            playerTransform == null || mainCamera == null || (cubeForCheckCurSnap == null && curSnapCheckPrefab != null))
        {
            isInitialized = false;
        }

        if (isInitialized)
        {
            return true;
        }

        if (TryResolveDependencies())
        {
            isInitialized = true;
            return true;
        }

        if (dependencyRoutine == null)
        {
            StartDependencyPolling();
        }

        return false;
    }

    private bool TryResolveDependencies()
    {
        bool resolved = true;

        if (buildingMaterialManagement == null)
        {
            buildingMaterialManagement = GetComponent<BuildingMaterialManagement>();
            resolved &= buildingMaterialManagement != null;
        }

        if (buildOrRemove == null)
        {
            buildOrRemove = GetComponent<BuildOrRemove>();
            resolved &= buildOrRemove != null;
        }

        if (snapController == null)
        {
            snapController = GetComponent<SnapController>();
            resolved &= snapController != null;
        }

        if(inventoryAdapter == null)
        {
            inventoryAdapter = GetComponent<PlayerInventoryAdapter>();
            resolved &= inventoryAdapter != null;
        }

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                resolved = false;
            }
        }

        if (mainCamera == null)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObj = GameObject.FindWithTag("MainCamera");
                if (cameraObj != null)
                {
                    camera = cameraObj.GetComponent<Camera>();
                }
            }

            if (camera != null)
            {
                mainCamera = camera;
            }
            else
            {
                resolved = false;
            }
        }

        if (cubeForCheckCurSnap == null)
        {
            if (curSnapCheckPrefab != null)
            {
                cubeForCheckCurSnap = Instantiate(curSnapCheckPrefab);
            }
            else if (!hasLoggedMissingSnapPrefab)
            {
                UnityEngine.Debug.LogWarning("BuildingManagement: curSnapCheckPrefab is not assigned. Snap point visualization will be disabled.");
                hasLoggedMissingSnapPrefab = true;
            }
        }

        if(inventoryAdapter == null)
        {
            inventoryAdapter = GetComponent<PlayerInventoryAdapter>();
            resolved &= inventoryAdapter != null;
        }

        if(inventoryAdapter == null)
        {
            inventoryAdapter = GetComponentInChildren<PlayerInventoryAdapter>();
            resolved &= inventoryAdapter != null;
        }

        if(inputHandler == null)
        {
            inputHandler = GetComponent<BuildingInputHandler>();
            resolved &= inputHandler != null;
        }

        if(placementValidator == null)
        {
            placementValidator = GetComponent<PlacementValidator>();
            resolved &= placementValidator != null;
        }

        if(placementValidator != null)
        {
            placementValidator.InitializeDependencies(buildingMaterialManagement, integritySolver, snapController, buildOrRemove, inventoryAdapter);
        }

        return resolved;
    }

    private void StartDependencyPolling()
    {
        if (dependencyRoutine != null)
        {
            return;
        }

        dependencyRoutine = StartCoroutine(WaitForDependencies());
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

    // 컨트롤러(인벤토리)에서 마우스를 올렸을 때 호출할 함수
    public void RequestShowRequirements(BuildingDataSO data)
    {
        if (data != null)
        {
            OnShowRequirements?.Invoke(data);
        }
    }

    // 컨트롤러(인벤토리)에서 마우스를 뗐을 때 호출할 함수
    public void RequestHideRequirements()
    {
        if (IsHoldingMaterial() && curMaterial != null)
        {
            var holdData = curMaterial.Data;
            OnShowRequirements?.Invoke(holdData);
        }
        else
        {
            OnHideRequirements?.Invoke();
        }
    }



    private void removeIngredientsByReq()
    {
        if (placementValidator.bConstructionMode == true)
        {
            return;
        }


        if (curMaterial == null) return;

        var requirements = curMaterial.RequirementsForMat; // Dictionary<string,int>

        foreach (var req in requirements)
        {
            string reqName = req.Key;
            int reqCount = req.Value;


           inventoryAdapter.ConsumeItem(reqName, reqCount);
        }

    }
    public bool IsPossibleToPlace()
    {

        GameObject targetRoot = null;

        if (snapController.isSnapped && snapController.bestWorldSnap != null)
        {
            if (lastSnappedPivot == snapController.bestWorldSnap)
            {
                targetRoot = cachedHighlightRoot;
            }
            else
            {
                IMaterial parentMaterial = snapController.bestWorldSnap.GetComponentInParent<IMaterial>();
                targetRoot = parentMaterial != null ? parentMaterial.GetGameObject() : null;

                lastSnappedPivot = snapController.bestWorldSnap;
                cachedHighlightRoot = targetRoot;
            }
        }
        else
        {
            lastSnappedPivot = null;
            cachedHighlightRoot = null;
        }

        //UpdateHighlightTarget(targetRoot);

        GameObject matObject = curMaterialTr != null ? curMaterialTr.gameObject : null;
        return placementValidator.IsPossibleToPlace(matObject,targetRoot, inputHandler.MousePos, pivotPos);
    }

    public void PlaceMaterial()
    {
        if (curMaterial == null)
            return;


        if (!placementValidator.CheckIfMeetRequirement(curMaterial.Data)) return;

        var materialType = curMaterial.GetBuildingMaterialType();
        bool requiresSupport = !(materialType == eBuildingMaterial.Torch) && !(materialType == eBuildingMaterial.Boat);

        if (requiresSupport)
        {
            integritySolver.UpdateParentsAndChildren(curMaterial);

            float finalSupport = placementValidator.GetCachedSupportValue();

            if (finalSupport < 0.25f)
            {
                integritySolver.ClearParentAndChildren(curMaterial);
                return;
            }

            curMaterial.SupportValue = finalSupport;
            integritySolver.HandleMaterialPlacement(curMaterial);

            placementValidator.ResetCache();
            UnityEngine.Debug.Log($"설치 완료! 부여된 지지력: {finalSupport}");
        }



        prevMatLocalRot = curMaterialTr.localEulerAngles;

        OnBuildAction();
        buildingMaterialManagement.ActivateColliderAndLayer(curMaterial.GetGameObject());
        removeIngredientsByReq();
        curMaterial.ResetVisualTransform();
        buildOrRemove.PlaceMaterial(pivotPos);
        ItemDurabilityUtility.TryConsumeEquipped(ItemDurabilityReason.BuildAction, this);

        DoorPlacementProcess();


        if (buildingMaterialManagement.GetCurrentPoolCount() > 0)
        {
            prevMaterial = curMaterial;
            curMaterial = null;
            curPivot = null;

            var data = buildingMaterialManagement.GetCurBuildingDataSO();

            ChangeHoldingMaterial(data);

            if (curMaterial == null)
            {
                prevMaterial = null;
                curMaterial = null;
                NotifyHoldingMaterialChanged();

                return;
            }

            Quaternion originalRot = Quaternion.Euler(prevMatLocalRot);
            curMaterialTr.rotation = Quaternion.identity; // 또는 localRotation = Quaternion.identity
            curMaterialTr.localRotation = originalRot;

            ShowMaterial();

        }
        else
        {
            prevMaterial = null;
            curMaterial = null;
            NotifyHoldingMaterialChanged();

        }



    }


    private void DoorPlacementProcess()
    {

        if (curMaterial.GetBuildingMaterialType() == eBuildingMaterial.Door)
        {
            Door door = curMaterial as Door;

            if (door != null)
            {


                if (snapController.isSnapped && curSnapPoint != null)
                {
                    curMaterialTr.rotation = snapController.bestWorldSnap.transform.rotation * Quaternion.Euler(0, 90f, 0);
                    //// 문틀의 각도에 맞춰서 문의 회전을 먼저 확정 짓습니다.
                    door.SetLocalEulerWhenPlaced(curMaterialTr.localEulerAngles);
                }
                else
                    door.SetLocalEulerWhenPlaced(prevMatLocalRot);
            }
        }
    }

    public void RemoveMaterial(GameObject targetObj)
    {
        //if (cubeForCheckCurSnap != null)
        //{
        //    cubeForCheckCurSnap.transform.SetParent(null, true);
        //    cubeForCheckCurSnap.SetActive(false);
        //}
        buildOrRemove.RemoveMaterial(targetObj);
        if (targetObj != null)
        {
            ItemDurabilityUtility.TryConsumeEquipped(ItemDurabilityReason.BuildAction, this);
        }
    }

    public bool IsHoldingMaterial() //자재를 들고 있는지 체크
    {
        if (curMaterial != null)
            return true;
        else
            return false;
    }

    public IMaterial GetCurMaterial()
    {
        return curMaterial;
    }
    public void ChangeHoldingMaterial(BuildingDataSO data)// 사용자의 입력에 따라 벽, 바닥, 등.. 손에 홀딩하는 것을 교체함
    {
        if (!EnsureInitialized())
        {
            UnityEngine.Debug.LogWarning("BuildingManagement: Dependencies are not ready yet. ChangeHoldingMaterial aborted.");
            return;
        }

        if (curMaterial != null)
        {
            GameObject materialObj = curMaterialTr.gameObject;

            materialObj.SetActive(false); //만약 교체시 현재 들고 있던거 비활성화
            buildingMaterialManagement.HideMaterial(materialObj); //교체시 부모로 다시 종속시켜준다.

            buildingMaterialManagement.ActivateColliderAndLayer(materialObj);
            if (cubeForCheckCurSnap != null)
            {
                cubeForCheckCurSnap.transform.SetParent(null, false);
            }

            prevMaterial = curMaterial;
            curMaterial = null;
            curMaterialTr = null;
            curPivot = null;

            rotationY = 0.0f; //처음 각도를 0으로 설정 매번바꿀때마다 초기화

        }


        curMaterial = buildingMaterialManagement.GetMaterialFromPool(data, true);


        if (curMaterial == null)
        {

            UnityEngine.Debug.Log($"{curMaterial}로 변경하지 못하였습니다.");
            NotifyHoldingMaterialChanged();
        }
        else
        {
            if (prevMaterial == null || curMaterial.Data != prevMaterial.Data)
            {
                curSnapIdx = 0; //스냅 포인트 인덱스 초기화

            }
            curMaterialTr = curMaterial.GetGameObject().transform;

            curSnapPoint = curMaterial.GetAnchorByIndx(curSnapIdx);
            curPivotPoint = curSnapPoint;

            if (cubeForCheckCurSnap == null)
            {
                if (curSnapCheckPrefab != null)
                {
                    cubeForCheckCurSnap = Instantiate(curSnapCheckPrefab);//스냅 포인트를 확인하기 위한 큐브 프리팹이 없을 경우 새로 생성
                }
                else if (!hasLoggedMissingSnapPrefab)
                {
                    UnityEngine.Debug.LogWarning("BuildingManagement: curSnapCheckPrefab is not assigned. Snap point visualization will be disabled.");
                    hasLoggedMissingSnapPrefab = true;
                }
            }

            if (cubeForCheckCurSnap != null)
            {
                cubeForCheckCurSnap.transform.SetParent(curSnapPoint.transform, false);
                cubeForCheckCurSnap.transform.localPosition = Vector3.zero;
                //cubeForCheckCurSnap.transform.localScale = Vector3.one * 0.2f; //스냅 포인트를 확인하기 위한 큐브 크기 설정
                cubeForCheckCurSnap.transform.localRotation = Quaternion.identity;
            }


            initMatLocalRot = curMaterialTr.localEulerAngles;


            ChangeState(HoldingState);
            NotifyHoldingMaterialChanged();

        }
    }


    public void ShowMaterial() //만약 캐릭터가 자재를 홀딩하고 있을 경우 캐릭터 앞쪽에 해당 자재가 보이게 함.
    {
        if(IsHoldingMaterial())
        {
            GameObject materialObj = curMaterialTr.gameObject;

            curMaterialTr.SetParent(null);
            buildingMaterialManagement.DeActiveColliderAndLayer(materialObj);
            snapController.isSnapped = false;
            MaterialPosUpdate(true, false);

            materialObj.SetActive(true);
        }


    }

    public void GetBackToOtherMode() //만약 건축모드 해제시 현재 들고 있던것을 내려놓고 다른 모드로 전환
    {
        if (IsHoldingMaterial())
        {
            GameObject materialObj = curMaterialTr.gameObject;

            buildingMaterialManagement.ActivateColliderAndLayer(materialObj);
            materialObj.SetActive(false);

            buildingMaterialManagement.HideMaterial(materialObj);
            prevMaterial = null;
            curMaterial = null;
            curPivot = null;
        }
        buildOrRemove.ResetRemoveCandidate();

        buildOrRemove.ResetHighlitedObject();
        if (snapController != null)
        {
            snapController.ClearSnapState();
        }

        UpdateHighlightTarget(null);
        lastSnappedPivot = null;
        cachedHighlightRoot = null;

        NotifyHoldingMaterialChanged();
    }

    private void NotifyHoldingMaterialChanged()
    {
        OnHoldingMaterialChanged?.Invoke(curMaterial != null ? curMaterial.Data : null);
    }


    public void MaterialPosUpdate(bool isSyncPosForFirstTime = false, bool debug = false) //자재 위치 업데이트
    {


        if (curMaterial != null)
        {
            Transform materialTr = curMaterialTr;

            if (materialTr != null)
            {
                Vector3 targetPosition = previousPosition;

                SnapFreeCheck();

                if (isSyncPosForFirstTime == true)
                {
                    if (curBuildingMode == eBuildingMode.Snap)
                    {
                        targetPosition = snapController.AdjustMaterialWithClosestSnapPoint(materialTr, inputHandler.MousePos, inputHandler.CurHitData, ref curSnapPoint, ref curPivotPoint, true, false);
                    }
                    else // eBuildingMode.ManualSnap 일 때
                    {
                        //  Transform snapTr = curSnapPoint != null ? curSnapPoint.transform : null;
                        targetPosition = snapController.AdjustMaterialWithCurSnapPoint(curSnapPoint?.transform, curMaterial.GetGameObject(), inputHandler.MousePos, inputHandler.CurHitData, true);
                    }

                }
                else if (curBuildingMode == eBuildingMode.Snap)
                {
                        targetPosition = snapController.AdjustMaterialWithClosestSnapPoint(materialTr, inputHandler.MousePos, inputHandler.CurHitData, ref curSnapPoint, ref curPivotPoint, false, true); //자재 위치를 가장 가까운 스냅 포인트로 조정
                }
                else if(curBuildingMode == eBuildingMode.SnapFree)
                {
                    targetPosition = snapController.AdjustMaterialWithClosestSnapPoint(materialTr, inputHandler.MousePos, inputHandler.CurHitData, ref curSnapPoint, ref curPivotPoint, true, false);
                }
                else if(curBuildingMode == eBuildingMode.ManualSnapFree)
                 {
                    targetPosition = snapController.AdjustMaterialWithCurSnapPoint(curSnapPoint?.transform, curMaterial.GetGameObject(), inputHandler.MousePos, inputHandler.CurHitData, true);
                }
                else
                {
                    targetPosition = snapController.AdjustMaterialWithCurSnapPoint(curSnapPoint?.transform, curMaterial.GetGameObject(), inputHandler.MousePos, inputHandler.CurHitData);
                }

                if(curMaterial.GetBuildingMaterialType() == eBuildingMaterial.Boat)
                {
                    if (WaterSystem.TryGetWaterHeight(targetPosition, out float waterHeight))
                    {
                      //  UnityEngine.Debug.Log($"물입니다");
                        if (curMaterial is Boat boat)
                            targetPosition.y = waterHeight + boat.PreviewHeightOffset;
                    }
                }


                pivotPos = targetPosition;

                if (debug == true)
                    UnityEngine.Debug.Log($"[BuildingSystem] Preview pivot: {pivotPos}");

                if (uiManager.isInventoryOpen == true)
                {
                    if (playerTransform != null)
                    {
                        targetPosition = playerTransform.position + playerTransform.forward * 2.0f;//+ Vector3.up * 1.3f;
                    }

                }

                Transform visual = curMaterial.GetVisualMesh();

                // 부모가 움직이기 전, 현재 자식 메쉬가 눈에 보이고 있는 월드 좌표를 백업
                Vector3 prevVisualWorldPos = visual != null ? visual.position : curMaterialTr.position;
                Quaternion prevVisualWorldRot = visual != null ? visual.rotation : curMaterialTr.rotation;

                //  부모는 스냅 및 입력 처리된 targetPosition으로 즉시 순간이동
                curMaterialTr.position = targetPosition;

                if (visual != null)
                {
                    // 자재 원본의 오프셋을 반영한 '최종 도달해야 할 실제 월드 기준점'을 계산
                    // 부모의 새 위치/회전 기준을 적용하여 자식의 원드 타겟
                    Vector3 targetWorldPos = curMaterialTr.TransformPoint(curMaterial.GetDefaultLocalPos());
                    Quaternion targetWorldRot = curMaterialTr.rotation * curMaterial.GetDefaultLocalRot();

                    visual.position = Vector3.Lerp(prevVisualWorldPos, targetWorldPos, Time.deltaTime * 25f);
                    visual.rotation = Quaternion.Lerp(prevVisualWorldRot, targetWorldRot, Time.deltaTime * 25f);
                }


                previousPosition = materialTr.position; // 위치 갱신
            }
            else
            {
                UnityEngine.Debug.LogError("curMaterial에 Transform 컴포넌트가 없습니다!");
            }
        }
        else
        {
            //   UnityEngine.Debug.LogError("플레이어 또는 자재가 null입니다!");
        }
    }

    public void MakeRotate()
    {
        float angle = inputHandler.GetRotationInput();
        if (angle != 0f)
        {
            RotatePreview(angle);
        }
    }

    private void SnapFreeCheck()
    {
        if (IsSnapFreeModifierHeld())
        {
            if (curBuildingMode == eBuildingMode.Snap)
            {
                curBuildingMode = eBuildingMode.SnapFree;
                snapController.ClearSnapState();
                curSnapPoint = null;
            }
            else if (curBuildingMode == eBuildingMode.ManualSnap)
            {
                curBuildingMode = eBuildingMode.ManualSnapFree;
                snapController.ClearSnapState();
                curSnapPoint = null;
            }
        }
        else
        {
            if (curBuildingMode == eBuildingMode.SnapFree)
            {
                curBuildingMode = eBuildingMode.Snap;
            }
            else if (curBuildingMode == eBuildingMode.ManualSnapFree)
            {
                curBuildingMode = eBuildingMode.ManualSnap;
            }
        }

    }

    private bool IsSnapFreeModifierHeld()
    {
        bool primaryHeld = snapFreeModifierKey != KeyCode.None && Input.GetKey(snapFreeModifierKey);
        bool secondaryHeld = secondarySnapFreeModifierKey != KeyCode.None && Input.GetKey(secondarySnapFreeModifierKey);
        return primaryHeld || secondaryHeld;
    }

    public bool pivotAttached = false;

    public void RotatePreview(float angle)
    {
        if (curMaterial != null)
        {
            Vector3 axis = Vector3.up; // or right

            if (curSnapPoint == null)
            {
                curMaterialTr.RotateAround(curMaterialTr.position, axis, angle);
            }
            else
                curMaterialTr.RotateAround(curSnapPoint.transform.position, axis, angle);


        }
    }

    public void ResetHighlitedObject()
    {
        buildOrRemove?.ResetHighlitedObject();
    }

    public void ResetDecalRange()
    {
        if(rangeDecal != null)
        {
            rangeDecal.material.SetColor("_Color", InRangeColor);
        }
    }

    private bool NotWithinCoverage()
    {
        if (inputHandler.CurHitData.collider == null)
            return true; //만약 raycast가 아무것도 맞추지 못했을 경우 범위 안에 없다고 판단

        if (playerTransform == null || mainCamera == null)
            return true;

        Vector3 flatMousePos = new Vector3(inputHandler.MousePos.x, 0f, inputHandler.MousePos.z);
        Vector3 flatPlayerPos = new Vector3(playerTransform.position.x, 0f, playerTransform.position.z);

        float sqrDist = (flatMousePos - flatPlayerPos).sqrMagnitude;
        float sqrMaxDistance = Mathf.Clamp(maxDistance * maxDistance,0f, 1000f);

        if (sqrDist > sqrMaxDistance)
            return true;//정해진 범위보다 길경우
        else
            return false; //안 길경우
    }

    public void UpdateHighlightTarget(GameObject targetRoot)
    {

        if (currentHighlightedVisual != null && (targetRoot == null || GetVisualFromRoot(targetRoot) != currentHighlightedVisual))
        {
            currentHighlightedVisual.layer = originalLayer;
            currentHighlightedVisual = null;
        }

        if (targetRoot != null)
        {
            GameObject visualChild = GetVisualFromRoot(targetRoot);

            if (visualChild != null && currentHighlightedVisual != visualChild)
            {
                currentHighlightedVisual = visualChild;
                originalLayer = visualChild.layer; // 원래 레이어(보통 Default) 기억
                visualChild.layer = highlightLayerIndex; // Highlight 레이어로 덮어쓰기
            }
        }
    }

    private GameObject GetVisualFromRoot(GameObject root)
    {
        if (root.TryGetComponent(out IMaterial imat) && imat.GetVisualMesh() != null)
        {
            return imat.GetVisualMesh().gameObject;
        }
        return null;
    }

    public void OnBuildAction()
    {
        if (mAnimal)
        {
            mAnimal.Mode_Activate(buildModeID, buildModeIndex);
        }
    }

}
