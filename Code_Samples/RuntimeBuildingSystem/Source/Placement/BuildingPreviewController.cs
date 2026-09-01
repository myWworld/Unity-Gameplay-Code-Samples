using System;
using System.Collections.Generic;
using KWS;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 현재 플레이어가 홀딩하고 있는 프리뷰 자재에 대한 처리를 담당
/// </summary>
public sealed class BuildingPreviewController
{
    private readonly BuildingMaterialManagement materialManagement;
    private readonly SnapController snapController;
    private readonly BuildingInputHandler inputHandler;

    private readonly Transform playerTransform;

    private readonly Func<bool> isInventoryOpen;
    private readonly Func<BuildingSystem.eBuildingMode> getBuildingMode;
    private readonly Action<BuildingSystem.eBuildingMode> setBuildingMode;

    private readonly KeyCode primarySnapFreeKey;
    private readonly KeyCode secondarySnapFreeKey;

    private readonly float maxPlacementDistance;
    private readonly float visualInterpolationSpeed;

    private readonly GameObject snapIndicatorPrefab;

    private readonly DecalProjector rangeDecal;
    private readonly Color inRangeColor;
    private readonly Color outOfRangeColor;

    private IMaterial currentMaterial;//현재 자재
    private Transform currentTransform;
    private BuildingDataSO previousMaterialData;

    private GameObject currentSnapPoint;//현재 스냅 포인트
    private GameObject currentPivotPoint;
    private int currentSnapIndex;

    private Vector3 pivotPosition;
    private Vector3 previousPosition;

    private GameObject snapIndicator;
    private Material rangeIndicatorMaterial;
    private bool hasLoggedMissingSnapIndicator;

    private GameObject lastSnappedPivot;
    private GameObject cachedSnappedTargetRoot;

    public BuildingPreviewController(
        BuildingMaterialManagement materialManagement,
        SnapController snapController,
        BuildingInputHandler inputHandler,
        Transform playerTransform,
        Func<bool> isInventoryOpen,
        Func<BuildingSystem.eBuildingMode> getBuildingMode,
        Action<BuildingSystem.eBuildingMode> setBuildingMode,
        KeyCode primarySnapFreeKey,
        KeyCode secondarySnapFreeKey,
        float maxPlacementDistance,
        float visualInterpolationSpeed,
        GameObject snapIndicatorPrefab,
        DecalProjector rangeDecal,
        Color inRangeColor,
        Color outOfRangeColor)
    {
        this.materialManagement = materialManagement;
        this.snapController = snapController;
        this.inputHandler = inputHandler;
        this.playerTransform = playerTransform;
        this.isInventoryOpen = isInventoryOpen;
        this.getBuildingMode = getBuildingMode;
        this.setBuildingMode = setBuildingMode;
        this.primarySnapFreeKey = primarySnapFreeKey;
        this.secondarySnapFreeKey = secondarySnapFreeKey;
        this.maxPlacementDistance = Mathf.Max(0f, maxPlacementDistance);
        this.visualInterpolationSpeed = Mathf.Max(0f, visualInterpolationSpeed);
        this.snapIndicatorPrefab = snapIndicatorPrefab;
        this.rangeDecal = rangeDecal;
        this.inRangeColor = inRangeColor;
        this.outOfRangeColor = outOfRangeColor;

        if (rangeDecal != null)
        {
            rangeIndicatorMaterial = rangeDecal.material;
        }

        ResetRangeIndicator();
    }

    public bool HasMaterial => currentMaterial != null && CurrentGameObject != null;
    public IMaterial CurrentMaterial => currentMaterial;
    public Transform CurrentTransform => currentTransform;
    public GameObject CurrentGameObject => currentMaterial != null ? currentMaterial.GetGameObject() : null;
    public GameObject CurrentSnapPoint => currentSnapPoint;
    public Vector3 PivotPosition => pivotPosition;
    public Vector3 MousePosition => inputHandler != null ? inputHandler.MousePos : pivotPosition;

    public bool Begin(BuildingDataSO data)//data에 해당하는 자재로 프리뷰 변경
    {
        if (data == null || materialManagement == null)
        {
            return false;
        }

        if (HasMaterial)//다른 거 홀딩 중이였다면 그 자재 풀에 반환
        {
            previousMaterialData = currentMaterial.Data;
            ReturnCurrentPreviewToPool();
        }

        currentMaterial = materialManagement.GetMaterialFromPool(data, true);//풀에서 바꿀 자재 꺼내옴
        if (currentMaterial == null || currentMaterial.GetGameObject() == null)
        {
            ClearCurrentReferences(clearPreviousData: false);//자재 오류 처리
            return false;
        }

        if (previousMaterialData == null || previousMaterialData != currentMaterial.Data)
        {
            currentSnapIndex = 0;//전이랑 다른 자재일 경우 수동 스냅포인트 초기화
        }

        //새 자재 관련 등록
        currentTransform = currentMaterial.GetGameObject().transform;
        previousPosition = currentTransform.position;
        SelectAnchorAtCurrentIndex();//첫 수동 스냅 포인트 설정
        EnsureSnapIndicator();//스냅 위치 표시 구 없으면 새로 할당
        AttachSnapIndicator();//표시 구 포인트에 붙여줌
        ClearSnappedTargetCache();//이전 대상 스냅 포인트 초기화
        snapController.ClearSnapState();
        OnBuildingModeChanged(getBuildingMode());
        return true;
    }

    public void Show(Quaternion? desiredWorldRotation = null)
    {
        if (!HasMaterial || currentTransform == null)
        {
            return;
        }

        GameObject materialObject = CurrentGameObject;
        currentTransform.SetParent(null);//풀에 있는 관리자 부모로부터 해제

        if (desiredWorldRotation.HasValue)
        {
            currentTransform.rotation = desiredWorldRotation.Value;
        }

        materialManagement.DeActiveColliderAndLayer(materialObject);//콜라이더 비활성화로 잘못된 raycast 작동 및 플레이어와 충돌 방지
        snapController.ClearSnapState();
        ClearSnappedTargetCache();//스냅 상태 초기화
        materialObject.SetActive(true);//자재 렌더 활성화

        inputHandler.UpdateInputData();//마우스 위치 업데이트
        UpdatePosition(isFirstSync: true, debug: false);//처음 자재 보일때 마우스쪽으로 보일 수 있도록 첫 동기화
    }

    public void HideTemporarily()//잠시 렌더 비활성화
    {
        GameObject materialObject = CurrentGameObject;
        if (materialObject != null)
        {
            materialObject.SetActive(false);
        }
    }

    public void TickPosition()
    {
        if (!HasMaterial || currentTransform == null)
        {
            return;
        }

        inputHandler.UpdateInputData();//raycast 정보 업데이트(마우스 위치, raycast된 오브젝트)

        bool isOutOfRange = IsOutsidePlacementRange();
        SetRangeIndicator(isOutOfRange);

        GameObject materialObject = CurrentGameObject;
        if (materialObject != null && materialObject.activeSelf == isOutOfRange)//플레이어 기준 정해진 범위 이상으로 있을 시 렌더 안함
        {
            materialObject.SetActive(!isOutOfRange);
        }

        if (!isOutOfRange)
        {
            UpdatePosition(isFirstSync: false, debug: false);//위치 업데이트
        }
    }

    public void UpdatePosition(bool isFirstSync = false, bool debug = false)
    {
        if (!HasMaterial || currentTransform == null)
        {
            return;
        }

        ApplySnapFreeModifier();//스냅 모드 업데이트

        Vector3 targetPosition = CalculateTargetPosition(isFirstSync);//스냅 모드에 따라 타겟 위치 계산
        targetPosition = AdjustSpecialPreviewPosition(targetPosition);//보트처럼 특이 자재일 경우 추가 연산
        pivotPosition = targetPosition;

        if (debug)
        {
            Debug.Log($"[BuildingPreviewController] Preview pivot: {pivotPosition}");
        }

        if (isInventoryOpen != null && isInventoryOpen() && playerTransform != null)
        {
            targetPosition = playerTransform.position + playerTransform.forward * 2f;//인벤토리열린 상태에선 자재 선택할때 플레이어 앞 쪽에 보이도록
        }

        InterpolateVisualTo(targetPosition);//Root와 Visual 분리 이동로직
        previousPosition = currentTransform.position;
    }

    public void RotateFromInput()
    {
        if (inputHandler == null)
        {
            return;
        }

        float angle = inputHandler.GetRotationInput();
        if (!Mathf.Approximately(angle, 0f))
        {
            Rotate(angle);
        }
    }

    public void Rotate(float angle)
    {
        if (!HasMaterial || currentTransform == null)
        {
            return;
        }

        Vector3 pivot = currentSnapPoint != null ? currentSnapPoint.transform.position : currentTransform.position;

        currentTransform.RotateAround(pivot, Vector3.up, angle);
    }

    public void ToggleSnapMode()//스냅 모드 변경
    {
        BuildingSystem.eBuildingMode mode = getBuildingMode();

        if (mode == BuildingSystem.eBuildingMode.Snap ||
            mode == BuildingSystem.eBuildingMode.SnapFree)
        {
            setBuildingMode(BuildingSystem.eBuildingMode.ManualSnap);// 수동 스냅
            CycleSnapPoint(0);
        }
        else
        {
            setBuildingMode(BuildingSystem.eBuildingMode.Snap);//자동 스냅
            snapController.ClearSnapState();
            ClearSnappedTargetCache();
        }
    }

    public void CycleSnapPoint(int direction)//수동 스냅에서 사용할 스냅 포인트 변경하는 로직
    {
        if (!HasMaterial)
        {
            return;
        }

        List<GameObject> anchors = currentMaterial.GetAnchors();
        if (anchors == null || anchors.Count == 0)
        {
            currentSnapPoint = null;
            currentPivotPoint = null;
            AttachSnapIndicator();
            return;
        }

        int count = anchors.Count;
        int step = direction == 0 ? 1 : Math.Sign(direction);
        int index = WrapIndex(currentSnapIndex + (direction == 0 ? 0 : step), count);

        for (int checkedCount = 0; checkedCount < count; checkedCount++)
        {
            GameObject candidate = currentMaterial.GetAnchorByIndx(index);

            if (IsManualSnapAnchor(candidate))//면 계산에 사용되는 포인트말고 스냅에 사용되는 포인트인지 체크
            {
                currentSnapIndex = index;
                currentSnapPoint = candidate;
                currentPivotPoint = candidate;
                AttachSnapIndicator();
                return;
            }

            index = WrapIndex(index + step, count);
        }

        currentSnapPoint = null;
        currentPivotPoint = null;
        AttachSnapIndicator();
    }

    public GameObject ResolveSnappedTargetRoot()//스냅된 자재 캐시 업데이트(캐시에 있는 거랑 같을 시 그대로 반환)
    {
        if (snapController == null || !snapController.isSnapped || snapController.bestWorldSnap == null)
        {
            ClearSnappedTargetCache();//스냅된 거 없을 시 캐시 초기화
            return null;
        }

        if (lastSnappedPivot == snapController.bestWorldSnap)
        {
            return cachedSnappedTargetRoot;//새 스냅포인트가 이전 캐시된 거랑 같으면 그대로 반환
        }

        IMaterial parentMaterial = snapController.bestWorldSnap.GetComponentInParent<IMaterial>();
        cachedSnappedTargetRoot = parentMaterial != null ? parentMaterial.GetGameObject() : null;
        lastSnappedPivot = snapController.bestWorldSnap;//캐시 업데이트
        return cachedSnappedTargetRoot;
    }

    public void DetachCommittedMaterial()
    {
        if (currentMaterial != null)
        {
            previousMaterialData = currentMaterial.Data;
        }

        DetachSnapIndicator();
        ClearCurrentReferences(clearPreviousData: false);
        ClearSnappedTargetCache();
    }

    public void Cancel()//상태 초기화 풀 반환 , 스냅 상태 초기화 등..
    {
        if (HasMaterial)
        {
            ReturnCurrentPreviewToPool();
        }
        else
        {
            ClearCurrentReferences(clearPreviousData: true);
        }

        previousMaterialData = null;
        snapController?.ClearSnapState();
        ClearSnappedTargetCache();
        ResetRangeIndicator();
    }

    public void OnBuildingModeChanged(BuildingSystem.eBuildingMode mode)
    {
        if (snapIndicator == null)
        {
            return;
        }

        bool isManual = mode == BuildingSystem.eBuildingMode.ManualSnap ||
                        mode == BuildingSystem.eBuildingMode.ManualSnapFree;
        snapIndicator.SetActive(isManual && currentSnapPoint != null && HasMaterial);
    }

    public void ResetRangeIndicator()
    {
        if (rangeIndicatorMaterial != null)
        {
            rangeIndicatorMaterial.SetColor("_Color", inRangeColor);
        }
    }

    public void Dispose()
    {
        if (snapIndicator != null)
        {
            UnityEngine.Object.Destroy(snapIndicator);//실제로 스냅 포인트 표시 객체 파괴
            snapIndicator = null;
        }
    }

    private Vector3 CalculateTargetPosition(bool isFirstSync)//스냅 모드에따른 위치 계산
    {
        BuildingSystem.eBuildingMode mode = getBuildingMode();

        if (isFirstSync)//처음 동기화용일 경우 마우스가 있는 위치에 가져다 두고 스냅은 하지 않음
        {
            if (mode == BuildingSystem.eBuildingMode.Snap ||
                mode == BuildingSystem.eBuildingMode.SnapFree)
            {
                return snapController.AdjustMaterialWithClosestSnapPoint(
                    currentTransform,
                    inputHandler.MousePos,
                    inputHandler.CurHitData,
                    ref currentSnapPoint,
                    ref currentPivotPoint,
                    bIsFree: true,
                    bIsSnaptime: false);
            }

            EnsureManualSnapPoint();
            return snapController.AdjustMaterialWithCurSnapPoint(
                currentSnapPoint != null ? currentSnapPoint.transform : null,
                CurrentGameObject,
                inputHandler.MousePos,
                inputHandler.CurHitData,
                bIsFree: true);
        }

        switch (mode)//스냅 모드에 따라 가장 가까운 자재의 스냅 포인트 가져옴
        {
            case BuildingSystem.eBuildingMode.Snap:
                return snapController.AdjustMaterialWithClosestSnapPoint(
                    currentTransform,
                    inputHandler.MousePos,
                    inputHandler.CurHitData,
                    ref currentSnapPoint,
                    ref currentPivotPoint,
                    bIsFree: false,
                    bIsSnaptime: true);

            case BuildingSystem.eBuildingMode.SnapFree:
                return snapController.AdjustMaterialWithClosestSnapPoint(
                    currentTransform,
                    inputHandler.MousePos,
                    inputHandler.CurHitData,
                    ref currentSnapPoint,
                    ref currentPivotPoint,
                    bIsFree: true,
                    bIsSnaptime: false);

            case BuildingSystem.eBuildingMode.ManualSnapFree:
                return snapController.AdjustMaterialWithCurSnapPoint(
                    currentSnapPoint != null ? currentSnapPoint.transform : null,
                    CurrentGameObject,
                    inputHandler.MousePos,
                    inputHandler.CurHitData,
                    bIsFree: true);

            case BuildingSystem.eBuildingMode.ManualSnap:
            default:
                EnsureManualSnapPoint();
                return snapController.AdjustMaterialWithCurSnapPoint(
                    currentSnapPoint != null ? currentSnapPoint.transform : null,
                    CurrentGameObject,
                    inputHandler.MousePos,
                    inputHandler.CurHitData,
                    bIsFree: false);
        }
    }

    private Vector3 AdjustSpecialPreviewPosition(Vector3 targetPosition)
    {
        if (currentMaterial == null ||
            currentMaterial.GetBuildingMaterialType() != eBuildingMaterial.Boat)
        {
            return targetPosition;
        }

        if (WaterSystem.TryGetWaterHeight(targetPosition, out float waterHeight) &&
            currentMaterial is Boat boat)
        {
            targetPosition.y = waterHeight + boat.PreviewHeightOffset;//보트는 오프셋 적용
        }

        return targetPosition;
    }

    private void InterpolateVisualTo(Vector3 targetPosition)//논리좌표는 바로 이동, 비쥬얼 좌표는 보간으로 자연스럽게 이동하도록
    {
        Transform visual = currentMaterial.GetVisualMesh();
        Vector3 previousVisualWorldPosition = visual != null ? visual.position : currentTransform.position;
        Quaternion previousVisualWorldRotation = visual != null ? visual.rotation : currentTransform.rotation;

        currentTransform.position = targetPosition;//실제 논리 좌표는 바로 이동

        if (visual == null)
        {
            return;
        }

        Vector3 targetWorldPosition = currentTransform.TransformPoint(currentMaterial.GetDefaultLocalPos());//비쥬얼 지역 위치활용해서 현재 타겟 기준 월드 좌표로 변환
        Quaternion targetWorldRotation = currentTransform.rotation * currentMaterial.GetDefaultLocalRot();
        float interpolation = visualInterpolationSpeed <= 0f ? 1f : Mathf.Clamp01(Time.deltaTime * visualInterpolationSpeed);

        visual.position = Vector3.Lerp(previousVisualWorldPosition, targetWorldPosition, interpolation);//비쥬얼 메시는 보간으로 자연스럽게 이동
        visual.rotation = Quaternion.Lerp(previousVisualWorldRotation, targetWorldRotation, interpolation);
    }

    private void ApplySnapFreeModifier()//스냅 모드 설정
    {
        bool modifierHeld = inputHandler.IsAnyKeyHeld(primarySnapFreeKey, secondarySnapFreeKey);//이 키가 눌린경우
        BuildingSystem.eBuildingMode mode = getBuildingMode();

        if (modifierHeld)//자유 배치 상태로 전환
        {
            if (mode == BuildingSystem.eBuildingMode.Snap)
            {
                setBuildingMode(BuildingSystem.eBuildingMode.SnapFree);
                snapController.ClearSnapState();
                currentSnapPoint = null;
                currentPivotPoint = null;
                ClearSnappedTargetCache();
            }
            else if (mode == BuildingSystem.eBuildingMode.ManualSnap)
            {
                setBuildingMode(BuildingSystem.eBuildingMode.ManualSnapFree);
                snapController.ClearSnapState();
                currentSnapPoint = null;
                currentPivotPoint = null;
                ClearSnappedTargetCache();
            }

            return;
        }

        if (mode == BuildingSystem.eBuildingMode.SnapFree)//자동스냅
        {
            setBuildingMode(BuildingSystem.eBuildingMode.Snap);
        }
        else if (mode == BuildingSystem.eBuildingMode.ManualSnapFree)//수동스냅
        {
            setBuildingMode(BuildingSystem.eBuildingMode.ManualSnap);
            EnsureManualSnapPoint();
            AttachSnapIndicator();
        }
    }

    private bool IsOutsidePlacementRange()//마우스 위치와 플레이어 위치가 정해진 거리내에 있는 지
    {
        if (inputHandler.CurHitData.collider == null || playerTransform == null)
        {
            return true;
        }

        Vector3 flatMousePosition = new Vector3(inputHandler.MousePos.x, 0f, inputHandler.MousePos.z);
        Vector3 flatPlayerPosition = new Vector3(playerTransform.position.x, 0f, playerTransform.position.z);
        float maxDistanceSquared = maxPlacementDistance * maxPlacementDistance;
        return (flatMousePosition - flatPlayerPosition).sqrMagnitude > maxDistanceSquared;
    }

    private void SetRangeIndicator(bool isOutOfRange)
    {
        if (rangeIndicatorMaterial != null)
        {
            rangeIndicatorMaterial.SetColor("_Color", isOutOfRange ? outOfRangeColor : inRangeColor);
        }
    }

    private void ReturnCurrentPreviewToPool()//풀에 반환
    {
        GameObject materialObject = CurrentGameObject;
        if (materialObject != null)
        {
            currentMaterial?.ResetVisualTransform();//비쥬얼 용 tr 지역 위치 초기값으로 재설정
            materialObject.SetActive(false);//렌더 안되게
            materialManagement.ActivateColliderAndLayer(materialObject);//콜라이더 재활성화
            materialManagement.HideMaterial(materialObject);//풀에 반환
        }

        DetachSnapIndicator();
        ClearCurrentReferences(clearPreviousData: false);
        ClearSnappedTargetCache();
    }

    private void ClearCurrentReferences(bool clearPreviousData)
    {
        currentMaterial = null;
        currentTransform = null;
        currentSnapPoint = null;
        currentPivotPoint = null;
        pivotPosition = Vector3.zero;
        previousPosition = Vector3.zero;

        if (clearPreviousData)
        {
            previousMaterialData = null;
        }
    }

    private void SelectAnchorAtCurrentIndex()//현재 스냅포인트 idx에 맞는 스냅포인트 Tr가져옴
    {
        List<GameObject> anchors = currentMaterial != null ? currentMaterial.GetAnchors() : null;
        if (anchors == null || anchors.Count == 0)
        {
            currentSnapPoint = null;
            currentPivotPoint = null;
            currentSnapIndex = 0;
            return;
        }

        currentSnapIndex = WrapIndex(currentSnapIndex, anchors.Count);
        currentSnapPoint = currentMaterial.GetAnchorByIndx(currentSnapIndex);
        currentPivotPoint = currentSnapPoint;
    }

    private void EnsureManualSnapPoint()
    {
        if (currentSnapPoint != null)
        {
            return;
        }

        SelectAnchorAtCurrentIndex();
    }

    private void EnsureSnapIndicator()
    {
        if (snapIndicator != null)
        {
            return;
        }

        if (snapIndicatorPrefab == null)
        {
            if (!hasLoggedMissingSnapIndicator)
            {
                Debug.LogWarning("[BuildingPreviewController] Snap indicator prefab is not assigned. Manual snap visualization is disabled.");
                hasLoggedMissingSnapIndicator = true;
            }

            return;
        }

        snapIndicator = UnityEngine.Object.Instantiate(snapIndicatorPrefab);
    }

    private void AttachSnapIndicator()//건축물 스냅 포인트에 붙는 초록새 구 표시
    {
        EnsureSnapIndicator();
        if (snapIndicator == null)
        {
            return;
        }

        if (currentSnapPoint == null)
        {
            snapIndicator.transform.SetParent(null, false);
            snapIndicator.SetActive(false);
            return;
        }

        snapIndicator.transform.SetParent(currentSnapPoint.transform, false);
        snapIndicator.transform.localPosition = Vector3.zero;
        snapIndicator.transform.localRotation = Quaternion.identity;
        OnBuildingModeChanged(getBuildingMode());
    }

    private void DetachSnapIndicator()
    {
        if (snapIndicator == null)
        {
            return;
        }

        snapIndicator.transform.SetParent(null, false);
        snapIndicator.SetActive(false);
    }

    private void ClearSnappedTargetCache()
    {
        lastSnappedPivot = null;
        cachedSnappedTargetRoot = null;
    }

    private static bool IsManualSnapAnchor(GameObject anchor)//스냅 포인트인지 일반 피벗인지
    {
        return anchor != null &&
               (anchor.CompareTag(LayerAndTagConstants.Tag_Pivot) || anchor.CompareTag(LayerAndTagConstants.Tag_DoorPivot));
    }

    private static int WrapIndex(int index, int count)//스냅 포인트 인덱스 벗어나지 않고 돌게
    {
        if (count <= 0)
        {
            return 0;
        }

        int wrapped = index % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }
}
