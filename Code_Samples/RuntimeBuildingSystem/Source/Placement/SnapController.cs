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

     //과거 그리드 방식
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

    public bool CanPlaceMaterial(Vector3 worldPosition, GameObject materialObject)//정해진 거리내에 있는지 체크
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
        Vector3 flatPlayerPosition = new Vector3(player.transform.position.x, 0f, player.transform.position.z);

        return (flatPosition - flatPlayerPosition).sqrMagnitude <= maxDistance * maxDistance;
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
        Transform materialTransform, Vector3 newPosition, RaycastHit hitData,
        ref GameObject currentSnapPoint,
        ref GameObject currentPivotPoint,
        bool bIsFree = false,
        bool bIsSnaptime = false)//자동 스냅에서 가장 가까운 스냅 포인트 찾아 위치 가져오기
    {
        if (materialTransform == null || !materialTransform.gameObject.TryGetComponent(out IMaterial material))
        {
            ClearSnapState();
            return newPosition;
        }

        List<GameObject> localAnchors = material.GetAnchors();//현재 홀딩 중인 자재의 앵커들 가져옴

        if (localAnchors == null || localAnchors.Count == 0)//앵커 없을 시 들어온 위치 그대로 반환
        {
            ClearSnapState();
            currentSnapPoint = null;
            currentPivotPoint = null;
            return newPosition;
        }

        bool isDoor = materialTransform.gameObject.CompareTag(LayerAndTagConstants.Tag_Door);//문일 경우 경첩피벗에 스냅 되도록 하기 위해
        GameObject heldSnap = null;
        Vector3 targetPosition = newPosition;//초기엔 마우스 위치
        bool snapped = false;

        if (!bIsFree && !isSnapped && bIsSnaptime)//스냅을 해야하는 경우
        {
            bestWorldSnap = null;
            (heldSnap, targetPosition, snapped) = FindBestWorldSnapAnchor(materialTransform, localAnchors, newPosition, isDoor);//가장 가까운 스냅 포인트 가져오기

            if (snapped)
            {
                mousePositionWhenSnapped = newPosition;//스냅이후엔 스냅 당시 마우스 위치 저장해서 이후 일정거리 이상 움직일시 스냅해제 되도록
            }
        }

        CheckAndReleaseSelfSnap(materialTransform, hitData, newPosition, targetPosition, ref heldSnap, ref snapped, material);//자기 자신인지 체크

        GameObject heldPivot = FindBestPivot(materialTransform, material, localAnchors, hitData.normal);//이동시 자재의 어떤 면이 바닥이나 벽에 닿을 지 최적 계산 후 가져오기

        MaintainOrReleaseSnap(newPosition, currentSnapPoint, ref heldSnap, ref targetPosition, ref snapped); //스냅 당시 마우스 위치랑 현재 위치 비교해서 스냅 취소할지 말지 결정

        isSnapped = snapped;
        currentSnapPoint = heldSnap;
        currentPivotPoint = heldPivot ?? currentSnapPoint;

        UpdateSnapState(heldSnap);//스냅 상태 업데이트

        Transform offsetAnchor = heldSnap != null
            ? heldSnap.transform : currentPivotPoint != null ? currentPivotPoint.transform : null;//스냅 된게 없으면 피벗(닿은 면의 중심)에서 위치 가져옴

        return offsetAnchor != null
            ? AdjustPositionByLocalOffset(materialTransform, offsetAnchor, targetPosition) : newPosition;//스냅이나 피벗이 아닌 실제 이동 점을 타겟 위치로 맞추도록 보정하여 반환
    }

    public Vector3 AdjustMaterialWithCurSnapPoint(
        Transform currentSnapPoint, GameObject materialObject, Vector3 newPosition,
        RaycastHit hitData,
        bool bIsFree = false)//수동스냅에서 지정한 스냅 포인트에서 가장 가까운 타겟 위치 가져옴
    {
        if (currentSnapPoint == null || materialObject == null ||
            !materialObject.TryGetComponent(out IMaterial material))
        {
            ClearSnapState();
            return newPosition;
        }

        Vector3 targetSnapPosition = newPosition;//처음엔 마우스 위치

        if (!bIsFree)
        {
            bestWorldSnap = null;
            targetSnapPosition = FindTargetWorldSnapPositionForManualMode(currentSnapPoint, newPosition, materialObject.transform);//지정 스냅 포인트 기준 가장 가까운 스냅포인트 위치
            isSnapped = bestWorldSnap != null;
        }
        else
        {
            ClearSnapState();
        }

        UpdateSnapState(bestWorldSnap);

        return AdjustPositionByLocalOffset(materialObject.transform, currentSnapPoint, targetSnapPosition);//실제 자재 위치로 보정
    }


    public void ClearSnapState()//스냅 상태 초기화
    {
        isSnapped = false;
        bestWorldSnap = null;
        snapState = new SnapState(false, Vector3.zero, Vector3.up);
    }



    private (GameObject bestLocalSnap, Vector3 targetPosition, bool snapped)
        FindBestWorldSnapAnchor(Transform materialTransform,List<GameObject> localAnchors, Vector3 newPosition, bool isDoor)//자동 스냅 최적 스냅 포인트 위치 찾기
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
            pivotLayerMask);//자재 기준 주변 피벗 레이어(==스냅 포인트)인 애들 찾아옴

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
            }//이동면으로 사용되는 지점이나 내 자식으로부터 온거는 건너뜀

            if (isDoor)//문일 경우 경첩 피벗일 경우만 해당
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

            for (int localIndex = 0; localIndex < localAnchors.Count; localIndex++)//현재 홀딩중인 자재를 돌며 가장 가까운 스냅포인트 저장
            {
                GameObject localAnchor = localAnchors[localIndex];

                if (localAnchor == null ||
                    localAnchor.CompareTag(LayerAndTagConstants.Tag_Snap) ||
                    (isDoor && !localAnchor.CompareTag(LayerAndTagConstants.Tag_DoorPivot)))
                {
                    continue;//홀딩 자재의 스냅포인트가 아닐시 검사 X
                }

                float squaredDistance = (worldAnchor.transform.position - localAnchor.transform.position).sqrMagnitude;

                if (squaredDistance <= snapDistanceSquared &&
                    squaredDistance < minimumSquaredDistance) //가장 가까운 지점을 스냅포인트로 지정
                {
                    minimumSquaredDistance = squaredDistance;
                    targetPosition = worldAnchor.transform.position;
                    bestLocalSnap = localAnchor;//내 자재의 스냅포인트
                    bestWorldSnap = worldAnchor;//내가 붙을 자재의 스냅포인트
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
            hitObject.transform.IsChildOf(materialTransform);//내 객체에 속하거나 나일때
        if (!isPreviewSelf)//건너뜀
        {
            return;
        }

        Vector3 adjustedPosition = AdjustPositionByLocalOffset(materialTransform, heldSnap.transform, targetPosition);

        const float releaseDistance = 0.1f;
        if ((adjustedPosition - hitObject.transform.position).sqrMagnitude < releaseDistance * releaseDistance)
        {
            heldSnap = null;
            snapped = false;
            bestWorldSnap = null;
            mousePositionWhenSnapped = newPosition;
        }
    }

    private GameObject FindBestPivot(
        Transform materialTransform, IMaterial material,
        List<GameObject> localAnchors, Vector3 hitNormal)//자재의 이동면으로 가장 적합한 피벗 찾기
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

            float direction = Vector3.Dot(anchor.transform.forward, hitNormal);//표면의 법선과 미리 등록해둔 앵커의 forward를 내적해
                                                                                //가장 정반대일 수록 최우선 타겟
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
        ref bool snapped)//마우스가 일정거리 이상 멀어졌는지 체크
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
        Transform previewRoot)//지정된 스냅 포인트와 가장 가까운 스냅포인트 찾기
    {
        EnsureOverlapBuffer();

        Vector3 bestPosition = newPosition;
        float snapDistanceSquared = automaticSnapDistance * automaticSnapDistance;
        float bestDirectionMatch = 2f;

        int hitCount = Physics.OverlapSphereNonAlloc(
            newPosition,
            manualSearchRadius,
            hitColliders,
            pivotLayerMask);//마우스 위치 주변에 스냅 포인트 가져오기

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

            if (squaredDistance > snapDistanceSquared)//거리 우선
            {
                continue;
            }

            //float directionMatch = Vector3.Dot(currentSnapPoint.forward, collider.transform.forward);//방향으로 체크해서 우선순위 정하는거로 확장 가능

            //if (directionMatch < bestDirectionMatch)
            //{
                bestDirectionMatch = directionMatch;
                bestWorldSnap = collider.gameObject;
                bestPosition = collider.transform.position;
            //}
        }

        return bestPosition;
    }

    private Vector3 AdjustPositionByLocalOffset(
        Transform materialTransform, Transform snapPointTransform, Vector3 targetPivotPosition)//스냅포인트 지역좌표를 월드 좌표로 변환 후 보정하여 실제로 자재가 이동해야할 위치 구함
    {
        if (materialTransform == null || snapPointTransform == null)
        {
            return targetPivotPosition;
        }

        Vector3 localOffset = materialTransform.InverseTransformPoint(snapPointTransform.position);//홀딩 자재를 기준 스냅 포인트 로컬 좌표 구함
        Vector3 worldOffset = materialTransform.rotation * localOffset;//로컬좌표를 월드회전 행렬연산으로 월드 좌표로 변환
        return targetPivotPosition - worldOffset;//목표 위치 - 스냅 포인트 오프셋 / 구해서 스냅 포인트끼리 붙은 것처럼 보이게
    }

    private void UpdateSnapState(GameObject targetAnchor)
    {
        isSnapped = targetAnchor != null && bestWorldSnap != null;
        snapState = isSnapped
            ? new SnapState(true, bestWorldSnap.transform.position, bestWorldSnap.transform.forward)
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
