using System.Collections.Generic;
using UnityEngine;

public class SnapController : MonoBehaviour
{
    public struct SnapState
    {
        public bool isSnapped;
        public Vector3 pivotWorld;
        public Vector3 axisWorld;

        public SnapState(bool isSnapped, Vector3 pivotWorld, Vector3 axisWorld)
        {
            this.isSnapped = isSnapped;
            this.pivotWorld = pivotWorld;
            this.axisWorld = axisWorld;
        }
    }

    public enum eCellState { Empty, Occupied, Blocked }

    // Legacy grid fields are retained for scene/prefab serialization compatibility.
    public int gridWidth, gridHeight, gridDepth;
    public float cellSize = 1f;
    private eCellState[,,] grid;
    private GameObject[,,] installedMaterials;

    public float maxDistance = 6.7f;
    public float minDistance = 1.1f;
    public Vector3Int gridOffset;
    public float[] yHeight;
    public float GridHeight;

    [Header("Snap Search")]
    [SerializeField, Min(0.01f)] private float automaticSnapDistance = 0.4f;
    [SerializeField, Min(0.01f)] private float automaticSearchRadius = 3.5f;
    [SerializeField, Min(0.01f)] private float doorSearchRadius = 2f;
    [SerializeField, Min(0.01f)] private float manualSearchRadius = 0.6f;
    [SerializeField, Min(8)] private int overlapCapacity = 150;

    private GameObject player;
    private Collider[] hitColliders;
    private int pivotLayerMask;
    private Vector3 mousePositionWhenSnapped;
    private bool hasLoggedCapacityWarning;

    public GameObject bestWorldSnap;
    public bool isSnapped;
    public SnapState snapState = new SnapState(false, Vector3.zero, Vector3.up);

    private void Awake()
    {
        pivotLayerMask = LayerAndTagConstants.Mask_Pivot;
        EnsureOverlapBuffer();
        ResolvePlayer();
    }

    private void OnValidate()
    {
        maxDistance = Mathf.Max(0f, maxDistance);
        automaticSnapDistance = Mathf.Max(0.01f, automaticSnapDistance);
        automaticSearchRadius = Mathf.Max(0.01f, automaticSearchRadius);
        doorSearchRadius = Mathf.Max(0.01f, doorSearchRadius);
        manualSearchRadius = Mathf.Max(0.01f, manualSearchRadius);
        overlapCapacity = Mathf.Max(8, overlapCapacity);
    }

    public void InitializePlayer(Transform playerTransform)
    {
        if (playerTransform != null)
        {
            player = playerTransform.gameObject;
        }
        else
        {
            ResolvePlayer();
        }

        EnsureOverlapBuffer();
    }

    public bool CanPlaceMaterial(Vector3 worldPosition, GameObject materialObject)
    {
        if (player == null)
        {
            ResolvePlayer();
        }

        if (player == null)
        {
            return true;
        }

        Vector3 flatPosition = new Vector3(worldPosition.x, 0f, worldPosition.z);
        Vector3 flatPlayerPosition = new Vector3(
            player.transform.position.x,
            0f,
            player.transform.position.z);

        return (flatPosition - flatPlayerPosition).sqrMagnitude <= maxDistance * maxDistance;
    }

    public void UpdateAnchorAndMaterialPos(Transform materialTransform, Vector3 newPosition)
    {
        if (materialTransform == null ||
            !materialTransform.gameObject.TryGetComponent(out IMaterial material))
        {
            return;
        }

        GameObject pivot = material.GetPivot();
        if (pivot == null)
        {
            materialTransform.position = newPosition;
            return;
        }

        Vector3 offset = material.GetOffsetBetweenObjAndAnchor();
        pivot.transform.position = newPosition;
        materialTransform.position = newPosition + offset;
    }

    public GameObject GetPivot(Transform parentTransform)
    {
        if (parentTransform == null)
        {
            return null;
        }

        Transform pivot = parentTransform.Find("Pivot");
        return pivot != null ? pivot.gameObject : null;
    }

    public Vector3 AdjustMaterialWithClosestSnapPoint(
        Transform materialTransform,
        Vector3 newPosition,
        RaycastHit hitData,
        ref GameObject currentSnapPoint,
        ref GameObject currentPivotPoint,
        bool bIsFree = false,
        bool bIsSnaptime = false)
    {
        if (materialTransform == null ||
            !materialTransform.gameObject.TryGetComponent(out IMaterial material))
        {
            ClearSnapState();
            return newPosition;
        }

        List<GameObject> localAnchors = material.GetAnchors();
        if (localAnchors == null || localAnchors.Count == 0)
        {
            ClearSnapState();
            currentSnapPoint = null;
            currentPivotPoint = null;
            return newPosition;
        }

        bool isDoor = materialTransform.gameObject.CompareTag(LayerAndTagConstants.Tag_Door);
        GameObject heldSnap = null;
        Vector3 targetPosition = newPosition;
        bool snapped = false;

        if (!bIsFree && !isSnapped && bIsSnaptime)
        {
            bestWorldSnap = null;
            (heldSnap, targetPosition, snapped) = FindBestWorldSnapAnchor(
                materialTransform,
                localAnchors,
                newPosition,
                isDoor);

            if (snapped)
            {
                mousePositionWhenSnapped = newPosition;
            }
        }

        CheckAndReleaseSelfSnap(
            materialTransform,
            hitData,
            newPosition,
            targetPosition,
            ref heldSnap,
            ref snapped,
            material);

        GameObject heldPivot = FindBestPivot(
            materialTransform,
            material,
            localAnchors,
            hitData.normal);

        MaintainOrReleaseSnap(
            newPosition,
            currentSnapPoint,
            ref heldSnap,
            ref targetPosition,
            ref snapped);

        isSnapped = snapped;
        currentSnapPoint = heldSnap;
        currentPivotPoint = heldPivot ?? currentSnapPoint;
        UpdateSnapState(heldSnap);

        Transform offsetAnchor = heldSnap != null
            ? heldSnap.transform
            : currentPivotPoint != null ? currentPivotPoint.transform : null;

        return offsetAnchor != null
            ? AdjustPositionByLocalOffset(materialTransform, offsetAnchor, targetPosition)
            : newPosition;
    }

    public Vector3 AdjustMaterialWithCurSnapPoint(
        Transform currentSnapPoint,
        GameObject materialObject,
        Vector3 newPosition,
        RaycastHit hitData,
        bool bIsFree = false)
    {
        if (currentSnapPoint == null || materialObject == null ||
            !materialObject.TryGetComponent(out IMaterial material))
        {
            ClearSnapState();
            return newPosition;
        }

        Vector3 targetSnapPosition = newPosition;
        if (!bIsFree)
        {
            bestWorldSnap = null;
            targetSnapPosition = FindTargetWorldSnapPositionForManualMode(
                currentSnapPoint,
                newPosition,
                materialObject.transform);
            isSnapped = bestWorldSnap != null;
        }
        else
        {
            ClearSnapState();
        }

        UpdateSnapState(bestWorldSnap);
        return AdjustPositionByLocalOffset(
            materialObject.transform,
            currentSnapPoint,
            targetSnapPosition);
    }

    public Vector3 AdjustSnapOffset(Transform materialTransform, Vector3 newPosition, RaycastHit hitData)
    {
        if (materialTransform == null ||
            !materialTransform.gameObject.TryGetComponent(out IMaterial material))
        {
            return newPosition;
        }

        List<GameObject> anchors = material.GetAnchors();
        if (anchors == null)
        {
            return newPosition;
        }

        float bestScore = float.NegativeInfinity;
        GameObject bestAnchor = null;

        for (int i = 0; i < anchors.Count; i++)
        {
            GameObject anchor = anchors[i];
            if (anchor == null)
            {
                continue;
            }

            float dot = Vector3.Dot(anchor.transform.forward, hitData.normal);
            float reversedAccordance = (1f - dot) * 0.5f;
            float score = reversedAccordance * 0.5f;
            if (score > bestScore)
            {
                bestAnchor = anchor;
                bestScore = score;
            }
        }

        return bestAnchor != null
            ? newPosition - materialTransform.TransformDirection(bestAnchor.transform.localPosition)
            : newPosition;
    }

    public void ClearSnapState()
    {
        isSnapped = false;
        bestWorldSnap = null;
        snapState = new SnapState(false, Vector3.zero, Vector3.up);
    }

    private (GameObject bestLocalSnap, Vector3 targetPosition, bool snapped)
        FindBestWorldSnapAnchor(
            Transform materialTransform,
            List<GameObject> localAnchors,
            Vector3 newPosition,
            bool isDoor)
    {
        EnsureOverlapBuffer();

        GameObject bestLocalSnap = null;
        Vector3 targetPosition = newPosition;
        bool snapped = false;
        float minimumSquaredDistance = float.MaxValue;
        float snapDistanceSquared = automaticSnapDistance * automaticSnapDistance;
        float searchRadius = isDoor ? doorSearchRadius : automaticSearchRadius;

        int hitCount = Physics.OverlapSphereNonAlloc(
            materialTransform.position,
            searchRadius,
            hitColliders,
            pivotLayerMask);
        WarnIfBufferIsFull(hitCount);

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider collider = hitColliders[hitIndex];
            if (collider == null)
            {
                continue;
            }

            GameObject worldAnchor = collider.gameObject;
            if (worldAnchor == null ||
                worldAnchor.transform.IsChildOf(materialTransform) ||
                worldAnchor.CompareTag(LayerAndTagConstants.Tag_Snap))
            {
                continue;
            }

            if (isDoor)
            {
                if (!worldAnchor.CompareTag(LayerAndTagConstants.Tag_DoorPivot))
                {
                    continue;
                }

                GameObject root = worldAnchor.transform.root.gameObject;
                if (root.TryGetComponent<Door>(out _))
                {
                    continue;
                }
            }

            for (int localIndex = 0; localIndex < localAnchors.Count; localIndex++)
            {
                GameObject localAnchor = localAnchors[localIndex];
                if (localAnchor == null ||
                    localAnchor.CompareTag(LayerAndTagConstants.Tag_Snap) ||
                    (isDoor && !localAnchor.CompareTag(LayerAndTagConstants.Tag_DoorPivot)))
                {
                    continue;
                }

                float squaredDistance =
                    (worldAnchor.transform.position - localAnchor.transform.position).sqrMagnitude;
                if (squaredDistance <= snapDistanceSquared &&
                    squaredDistance < minimumSquaredDistance)
                {
                    minimumSquaredDistance = squaredDistance;
                    targetPosition = worldAnchor.transform.position;
                    bestLocalSnap = localAnchor;
                    bestWorldSnap = worldAnchor;
                    snapped = true;
                }
            }
        }

        return (bestLocalSnap, targetPosition, snapped);
    }

    private void CheckAndReleaseSelfSnap(
        Transform materialTransform,
        RaycastHit hitData,
        Vector3 newPosition,
        Vector3 targetPosition,
        ref GameObject heldSnap,
        ref bool snapped,
        IMaterial material)
    {
        GameObject hitObject = hitData.collider != null ? hitData.collider.gameObject : null;
        if (hitObject == null || !snapped || heldSnap == null)
        {
            return;
        }

        bool isPreviewSelf =
            hitObject == materialTransform.gameObject ||
            hitObject.transform.IsChildOf(materialTransform);
        if (!isPreviewSelf)
        {
            return;
        }

        Vector3 adjustedPosition = AdjustPositionByLocalOffset(
            materialTransform,
            heldSnap.transform,
            targetPosition);

        const float releaseDistance = 0.1f;
        if ((adjustedPosition - hitObject.transform.position).sqrMagnitude <
            releaseDistance * releaseDistance)
        {
            heldSnap = null;
            snapped = false;
            bestWorldSnap = null;
            mousePositionWhenSnapped = newPosition;
        }
    }

    private GameObject FindBestPivot(
        Transform materialTransform,
        IMaterial material,
        List<GameObject> localAnchors,
        Vector3 hitNormal)
    {
        float bestDirection = 3f;
        GameObject bestPivot = null;

        for (int i = 0; i < localAnchors.Count; i++)
        {
            GameObject anchor = localAnchors[i];
            if (anchor == null)
            {
                continue;
            }

            float direction = Vector3.Dot(anchor.transform.forward, hitNormal);
            if (direction < bestDirection)
            {
                bestDirection = direction;
                bestPivot = anchor;

                if (material.GetBuildingMaterialType() == eBuildingMaterial.Torch)
                {
                    material.ApplySpecialRotation(materialTransform, anchor);
                }
            }
            else if (Mathf.Approximately(direction, bestDirection) &&
                     anchor.CompareTag(LayerAndTagConstants.Tag_Snap))
            {
                bestPivot = anchor;
            }
        }

        return bestPivot;
    }

    private void MaintainOrReleaseSnap(
        Vector3 newPosition,
        GameObject currentSnapPoint,
        ref GameObject heldSnap,
        ref Vector3 targetPosition,
        ref bool snapped)
    {
        if (!isSnapped)
        {
            return;
        }

        float squaredDistance = (mousePositionWhenSnapped - newPosition).sqrMagnitude;
        float releaseDistanceSquared = automaticSnapDistance * automaticSnapDistance;
        if (squaredDistance > releaseDistanceSquared + 0.05f)
        {
            heldSnap = null;
            snapped = false;
            bestWorldSnap = null;
            mousePositionWhenSnapped = newPosition;
            return;
        }

        if (currentSnapPoint != null)
        {
            targetPosition = currentSnapPoint.transform.position;
            heldSnap = currentSnapPoint;
            snapped = true;
        }
    }

    private Vector3 FindTargetWorldSnapPositionForManualMode(
        Transform currentSnapPoint,
        Vector3 newPosition,
        Transform previewRoot)
    {
        EnsureOverlapBuffer();

        Vector3 bestPosition = newPosition;
        float snapDistanceSquared = automaticSnapDistance * automaticSnapDistance;
        float bestDirectionMatch = 2f;

        int hitCount = Physics.OverlapSphereNonAlloc(
            newPosition,
            manualSearchRadius,
            hitColliders,
            pivotLayerMask);
        WarnIfBufferIsFull(hitCount);

        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = hitColliders[i];
            if (collider == null ||
                collider.gameObject == currentSnapPoint.gameObject ||
                (previewRoot != null && collider.transform.IsChildOf(previewRoot)))
            {
                continue;
            }

            float squaredDistance = (newPosition - collider.transform.position).sqrMagnitude;
            if (squaredDistance > snapDistanceSquared)
            {
                continue;
            }

            float directionMatch = Vector3.Dot(
                currentSnapPoint.forward,
                collider.transform.forward);
            if (directionMatch < bestDirectionMatch)
            {
                bestDirectionMatch = directionMatch;
                bestWorldSnap = collider.gameObject;
                bestPosition = collider.transform.position;
            }
        }

        return bestPosition;
    }

    private Vector3 AdjustPositionByLocalOffset(
        Transform materialTransform,
        Transform snapPointTransform,
        Vector3 targetPivotPosition)
    {
        if (materialTransform == null || snapPointTransform == null)
        {
            return targetPivotPosition;
        }

        Vector3 localOffset = materialTransform.InverseTransformPoint(snapPointTransform.position);
        Vector3 worldOffset = materialTransform.rotation * localOffset;
        return targetPivotPosition - worldOffset;
    }

    private void UpdateSnapState(GameObject targetAnchor)
    {
        isSnapped = targetAnchor != null && bestWorldSnap != null;
        snapState = isSnapped
            ? new SnapState(
                true,
                bestWorldSnap.transform.position,
                bestWorldSnap.transform.forward)
            : new SnapState(false, Vector3.zero, Vector3.up);
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        GameObject found = GameObject.FindWithTag("Player");
        if (found != null)
        {
            player = found;
        }
    }

    private void EnsureOverlapBuffer()
    {
        int capacity = Mathf.Max(8, overlapCapacity);
        if (hitColliders == null || hitColliders.Length != capacity)
        {
            hitColliders = new Collider[capacity];
            hasLoggedCapacityWarning = false;
        }
    }

    private void WarnIfBufferIsFull(int hitCount)
    {
        if (!hasLoggedCapacityWarning &&
            hitColliders != null &&
            hitCount >= hitColliders.Length)
        {
            Debug.LogWarning(
                $"[SnapController] Physics query filled its {hitColliders.Length}-collider buffer. " +
                "Increase Overlap Capacity to avoid truncated snap candidates.");
            hasLoggedCapacityWarning = true;
        }
    }
}
