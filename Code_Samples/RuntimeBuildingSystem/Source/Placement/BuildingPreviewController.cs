using System;
using System.Collections.Generic;
using KWS;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Holds the lifetime and transform state of the currently previewed building material.
/// This is a plain runtime service, so no additional scene component is required.
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

    private IMaterial currentMaterial;
    private Transform currentTransform;
    private BuildingDataSO previousMaterialData;

    private GameObject currentSnapPoint;
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

    public bool Begin(BuildingDataSO data)
    {
        if (data == null || materialManagement == null)
        {
            return false;
        }

        if (HasMaterial)
        {
            previousMaterialData = currentMaterial.Data;
            ReturnCurrentPreviewToPool();
        }

        currentMaterial = materialManagement.GetMaterialFromPool(data, true);
        if (currentMaterial == null || currentMaterial.GetGameObject() == null)
        {
            ClearCurrentReferences(clearPreviousData: false);
            return false;
        }

        if (previousMaterialData == null || previousMaterialData != currentMaterial.Data)
        {
            currentSnapIndex = 0;
        }

        currentTransform = currentMaterial.GetGameObject().transform;
        previousPosition = currentTransform.position;
        SelectAnchorAtCurrentIndex();
        EnsureSnapIndicator();
        AttachSnapIndicator();
        ClearSnappedTargetCache();
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
        currentTransform.SetParent(null);

        if (desiredWorldRotation.HasValue)
        {
            currentTransform.rotation = desiredWorldRotation.Value;
        }

        materialManagement.DeActiveColliderAndLayer(materialObject);
        snapController.ClearSnapState();
        ClearSnappedTargetCache();
        materialObject.SetActive(true);

        inputHandler.UpdateInputData();
        UpdatePosition(isFirstSync: true, debug: false);
    }

    public void HideTemporarily()
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

        inputHandler.UpdateInputData();

        bool isOutOfRange = IsOutsidePlacementRange();
        SetRangeIndicator(isOutOfRange);

        GameObject materialObject = CurrentGameObject;
        if (materialObject != null && materialObject.activeSelf == isOutOfRange)
        {
            materialObject.SetActive(!isOutOfRange);
        }

        if (!isOutOfRange)
        {
            UpdatePosition(isFirstSync: false, debug: false);
        }
    }

    public void UpdatePosition(bool isFirstSync = false, bool debug = false)
    {
        if (!HasMaterial || currentTransform == null)
        {
            return;
        }

        ApplySnapFreeModifier();

        Vector3 targetPosition = CalculateTargetPosition(isFirstSync);
        targetPosition = AdjustSpecialPreviewPosition(targetPosition);
        pivotPosition = targetPosition;

        if (debug)
        {
            Debug.Log($"[BuildingPreviewController] Preview pivot: {pivotPosition}");
        }

        if (isInventoryOpen != null && isInventoryOpen() && playerTransform != null)
        {
            targetPosition = playerTransform.position + playerTransform.forward * 2f;
        }

        InterpolateVisualTo(targetPosition);
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

        Vector3 pivot = currentSnapPoint != null
            ? currentSnapPoint.transform.position
            : currentTransform.position;

        currentTransform.RotateAround(pivot, Vector3.up, angle);
    }

    public void ToggleSnapMode()
    {
        BuildingSystem.eBuildingMode mode = getBuildingMode();
        if (mode == BuildingSystem.eBuildingMode.Snap ||
            mode == BuildingSystem.eBuildingMode.SnapFree)
        {
            setBuildingMode(BuildingSystem.eBuildingMode.ManualSnap);
            CycleSnapPoint(0);
        }
        else
        {
            setBuildingMode(BuildingSystem.eBuildingMode.Snap);
            snapController.ClearSnapState();
            ClearSnappedTargetCache();
        }
    }

    public void CycleSnapPoint(int direction)
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
            if (IsManualSnapAnchor(candidate))
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

    public GameObject ResolveSnappedTargetRoot()
    {
        if (snapController == null || !snapController.isSnapped || snapController.bestWorldSnap == null)
        {
            ClearSnappedTargetCache();
            return null;
        }

        if (lastSnappedPivot == snapController.bestWorldSnap)
        {
            return cachedSnappedTargetRoot;
        }

        IMaterial parentMaterial = snapController.bestWorldSnap.GetComponentInParent<IMaterial>();
        cachedSnappedTargetRoot = parentMaterial != null ? parentMaterial.GetGameObject() : null;
        lastSnappedPivot = snapController.bestWorldSnap;
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

    public void Cancel()
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
            UnityEngine.Object.Destroy(snapIndicator);
            snapIndicator = null;
        }
    }

    private Vector3 CalculateTargetPosition(bool isFirstSync)
    {
        BuildingSystem.eBuildingMode mode = getBuildingMode();

        if (isFirstSync)
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

        switch (mode)
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
            targetPosition.y = waterHeight + boat.PreviewHeightOffset;
        }

        return targetPosition;
    }

    private void InterpolateVisualTo(Vector3 targetPosition)
    {
        Transform visual = currentMaterial.GetVisualMesh();
        Vector3 previousVisualWorldPosition = visual != null ? visual.position : currentTransform.position;
        Quaternion previousVisualWorldRotation = visual != null ? visual.rotation : currentTransform.rotation;

        currentTransform.position = targetPosition;

        if (visual == null)
        {
            return;
        }

        Vector3 targetWorldPosition = currentTransform.TransformPoint(currentMaterial.GetDefaultLocalPos());
        Quaternion targetWorldRotation = currentTransform.rotation * currentMaterial.GetDefaultLocalRot();
        float interpolation = visualInterpolationSpeed <= 0f
            ? 1f
            : Mathf.Clamp01(Time.deltaTime * visualInterpolationSpeed);

        visual.position = Vector3.Lerp(previousVisualWorldPosition, targetWorldPosition, interpolation);
        visual.rotation = Quaternion.Lerp(previousVisualWorldRotation, targetWorldRotation, interpolation);
    }

    private void ApplySnapFreeModifier()
    {
        bool modifierHeld = inputHandler.IsAnyKeyHeld(primarySnapFreeKey, secondarySnapFreeKey);
        BuildingSystem.eBuildingMode mode = getBuildingMode();

        if (modifierHeld)
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

        if (mode == BuildingSystem.eBuildingMode.SnapFree)
        {
            setBuildingMode(BuildingSystem.eBuildingMode.Snap);
        }
        else if (mode == BuildingSystem.eBuildingMode.ManualSnapFree)
        {
            setBuildingMode(BuildingSystem.eBuildingMode.ManualSnap);
            EnsureManualSnapPoint();
            AttachSnapIndicator();
        }
    }

    private bool IsOutsidePlacementRange()
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

    private void ReturnCurrentPreviewToPool()
    {
        GameObject materialObject = CurrentGameObject;
        if (materialObject != null)
        {
            currentMaterial?.ResetVisualTransform();
            materialObject.SetActive(false);
            materialManagement.ActivateColliderAndLayer(materialObject);
            materialManagement.HideMaterial(materialObject);
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

    private void SelectAnchorAtCurrentIndex()
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

    private void AttachSnapIndicator()
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

    private static bool IsManualSnapAnchor(GameObject anchor)
    {
        return anchor != null &&
               (anchor.CompareTag(LayerAndTagConstants.Tag_Pivot) ||
                anchor.CompareTag(LayerAndTagConstants.Tag_DoorPivot));
    }

    private static int WrapIndex(int index, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        int wrapped = index % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }
}
