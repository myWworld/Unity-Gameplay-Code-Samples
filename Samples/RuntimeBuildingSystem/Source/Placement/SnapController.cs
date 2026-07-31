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

    public int gridWidth, gridHeight, gridDepth;
    public float cellSize = 1.0f; // 각 셀의 크기
    private eCellState[,,] grid; //그리드 위치정보에 따라서 해당 위치의 상태 표시
    private GameObject[,,] installedMaterials;

    public float maxDistance = 6.7f;
    public float minDistance = 1.1f;

    private GameObject player;

    public Vector3Int gridOffset;
    public GameObject bestWorldSnap = null;

    public float[] yHeight;
    private int curYGrid;

    public SnapState snapState = new SnapState(false, Vector3.zero, Vector3.up);

    public float GridHeight; //자재에 맞게 1x1 , 2 x 1이런식으로 리소스 제작할경우 딱 맞게 가능할 듯

    [Header("Cache Vars for Overlap")]
    private readonly Collider[] hitColliders = new Collider[150];
    private int pivotLayerMask;


    void Start()
    {
        player = GameObject.FindWithTag("Player"); // "TargetTag"라는 태그를 가진 오브젝트 찾기
        pivotLayerMask = LayerAndTagConstants.Mask_Pivot;
    }

    public bool CanPlaceMaterial(Vector3 wolrdPos, GameObject obj) //현재 마우스가 가리키는 곳에 자재를 배치할 수 있는지(=empty) 판단하는데 사용
    {

        if (player != null)
        {
            Vector3 flatMousePos = new Vector3(wolrdPos.x, 0f, wolrdPos.z);
            Vector3 flatPlayerPos = new Vector3(player.transform.position.x, 0f, player.transform.position.z);

            float sqrDist = (flatMousePos - flatPlayerPos).sqrMagnitude;
            float sqrMaxDistance = maxDistance * maxDistance;

            if (sqrDist > sqrMaxDistance) // 범위 벗어나면 배치 불가
                return false;

        }

        return true;
    }



    public void UpdateAnchorAndMaterialPos(Transform materialTr, Vector3 newPos)
    {
        if (materialTr.gameObject.TryGetComponent(out IMaterial material))
        {
            GameObject pivot = material.GetPivot();

            if (pivot != null)
            {
                Vector3 offset = material.GetOffsetBetweenObjAndAnchor();
                pivot.transform.position = newPos;
                materialTr.position = newPos + offset;

            }
        }

    }

    public GameObject GetPivot(Transform parentTr)
    {
        Transform tr = parentTr.Find("Pivot");
        return tr.gameObject;
    }

    public bool isSnapped = false;
    private Vector3 MousePosWhenSnapped = Vector3.zero; // 스냅된 위치 저장
    private float limitDistance = 0.5f; // 스냅 포인트와의 거리 제한

    public Vector3 AdjustMaterialWithClosestSnapPoint(Transform materialTr, Vector3 newPos, RaycastHit hitData, ref GameObject curSnapPoint, ref GameObject curPivotPoint, bool bIsFree = false, bool bIsSnaptime = false)
    {
        IMaterial material = materialTr.gameObject.GetComponent<IMaterial>();
        List<GameObject> curMatSnapPoints = material.GetAnchors();
        bool isDoor = materialTr.gameObject.CompareTag(LayerAndTagConstants.Tag_Door);

        GameObject heldSnap = null;
        Vector3 targetPos = newPos;
        bool snapped = false;

        // 1. 스냅 모드일 경우: 가장 가까운 월드 스냅 포인트 찾기
        if (!bIsFree && !isSnapped && bIsSnaptime)
        {
            (heldSnap, targetPos, snapped) = FindBestWorldSnapAnchor(materialTr, curMatSnapPoints, newPos, isDoor);
            if (snapped) MousePosWhenSnapped = newPos;
        }

        // 같은 오브젝트에 레이캐스트가 맞았을 경우 스냅 해제
        CheckAndReleaseSelfSnap(materialTr, hitData, newPos, targetPos, ref heldSnap, ref snapped, material);

        // 표면의 노멀(Normal) 벡터와 일치하는 최적의 피벗 찾기
        GameObject heldPivot = FindBestPivot(materialTr, material, curMatSnapPoints, hitData.normal);

        // 기존 스냅 상태 유지 혹은 마우스 거리에 따른 해제
        MaintainOrReleaseSnap(newPos, curSnapPoint, ref heldSnap, ref targetPos, ref snapped);

        // 최종 상태
        isSnapped = snapped;
        snapState.isSnapped = isSnapped;
        curSnapPoint = heldSnap;
        curPivotPoint = heldPivot ?? curSnapPoint; // 피벗이 없으면 스냅포인트를 피벗으로 사용



        // 위치 보정 후 반환 (heldSnap이 없으면 피벗 기준으로, 있으면 스냅포인트 기준으로 계산)
        if (heldSnap == null)
        {

            return posAdjustByLocalOffset(materialTr, curPivotPoint.transform, newPos, material);
        }


        return posAdjustByLocalOffset(materialTr, heldSnap.transform, targetPos, material);
    }
    public Vector3 AdjustMaterialWithCurSnapPoint(Transform curSnapPoint, GameObject materialObj, Vector3 newPos, RaycastHit hitData, bool bIsFree = false)
    {
        if (curSnapPoint == null || !materialObj.TryGetComponent(out IMaterial material))
            return newPos;// 자재가 아니면 마우스 위치 그대로 반환

        // 초기 타겟 위치는 마우스 위치
        Vector3 targetSnapPos = newPos;

        // 자유 모드가 아닐 경우, 주변에 맞물릴 수 있는 월드 스냅 포인트 탐색
        if (!bIsFree)
        {
            // 이전 프레임의 타겟을 지우고 탐색 시작
            bestWorldSnap = null;

            targetSnapPos = FindTargetWorldSnapPosForManualMode(curSnapPoint, newPos);

            // 스냅 됐을 경우 체크
            if (bestWorldSnap != null)
            {
                isSnapped = true;
                snapState.isSnapped = true;
            }
            else
            {
                isSnapped = false;
                snapState.isSnapped = false;
            }
        }
        else // 자유 모드(bIsFree)일 때는 강제로 스냅 해제
        {
            isSnapped = false;
            snapState.isSnapped = false;
            bestWorldSnap = null;
        }

        return posAdjustByLocalOffset(materialObj.transform, curSnapPoint, targetSnapPos , material);
    }

    private Vector3 posAdjustByLocalOffset(Transform curMaterialTr, Transform curSnapPointTr, Vector3 pivotPos, IMaterial mat)
    {

        Quaternion newRot = curMaterialTr.rotation;

        // pivot → curSnapPoint 로컬 오프셋
        Vector3 localOffset = curMaterialTr.InverseTransformPoint(curSnapPointTr.position);

        // 월드 오프셋 (현재 회전을 그대로 사용)
        Vector3 worldOffset = newRot * localOffset;

        // 최종 위치 = targetSnap에서 오프셋만큼 빼기
        Vector3 finalPos = pivotPos - worldOffset;


        return finalPos;
    }



    public Vector3 AdjustSnapOffset(Transform materialTr, Vector3 newPos, RaycastHit hitData) //앵커 오프셋을 통해서 업데이트 시켜준다.
    {
        IMaterial material = materialTr.gameObject.GetComponent<IMaterial>();
        List<GameObject> curMatSnapPoints = material.GetAnchors();
        float score = -99999f;

        GameObject heldSnap = null;

        foreach (var curMat in curMatSnapPoints)
        {

            float newDist = 1f;// Vector3.Distance(curMat.transform.position, newPos);
            float dot = Vector3.Dot(curMat.transform.forward, hitData.normal);
            float reversedAccordance = (1f - dot) * 0.5f;

            float newScore = (1 - newDist) + (reversedAccordance * 0.5f);

            if (score < newScore)
            {
                heldSnap = curMat;
                score = newScore;
            }

        }

        if (heldSnap == null)
        {
            // UnityEngine.Debug.Log("heldSnap이 null입니다. 스냅 포인트를 찾지 못했습니다.");
            return newPos; // heldSnap이 없으면 그냥 새 위치 반환
        }

        Vector3 localOffset = heldSnap.transform.localPosition;

        return newPos - materialTr.TransformDirection(localOffset);
    }


    // 현 자재 앵커와 겹치는 가장 가까운 월드 앵커 찾기
    private (GameObject bestLocalSnap, Vector3 targetPos, bool snapped) FindBestWorldSnapAnchor(Transform materialTr, List<GameObject> curMatSnapPoints, Vector3 newPos, bool isDoor)
    {
        GameObject bestLocalSnap = null;

        Vector3 targetPos = newPos;
        bool snapped = false;

        float minDistance = float.MaxValue;
        float UNSNAP_DIST = 0.4f;
        float sqrUnsnapDist = UNSNAP_DIST * UNSNAP_DIST;

        float searchRadius = isDoor ? 2.0f : 3.5f;
        int hitCount = Physics.OverlapSphereNonAlloc(materialTr.position, searchRadius, hitColliders, pivotLayerMask);


        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hitColliders[i];
            GameObject worldAnchor = col.gameObject;

            if (worldAnchor == null || worldAnchor == materialTr.gameObject || worldAnchor.CompareTag(LayerAndTagConstants.Tag_Snap)) continue;
            if (isDoor)
            {
                if(!worldAnchor.CompareTag(LayerAndTagConstants.Tag_DoorPivot)) continue;
                else
                {
                   var root = worldAnchor.transform.root.gameObject;
                    if(root.TryGetComponent<Door>(out _)) continue;
                }

            }

            // 내 자재의 모든 앵커를 순회하며 가장 가까운 짝을 찾음
            foreach (var localAnchor in curMatSnapPoints)
            {
                // 로컬 앵커 기본 예외 처리
                if (localAnchor == null) continue;

                if (localAnchor.CompareTag(LayerAndTagConstants.Tag_Snap)) continue;
                if (isDoor && !localAnchor.CompareTag(LayerAndTagConstants.Tag_DoorPivot)) continue;

                // sqrMagnitude를 이용한 빠른 거리 계산 (루트 연산 방지)
                float sqrDist = (worldAnchor.transform.position - localAnchor.transform.position).sqrMagnitude;

                // 스냅 허용 거리 이내이면서, 지금까지 찾은 거리보다 더 가깝다면 갱신
                if (sqrDist <= sqrUnsnapDist && sqrDist < minDistance)
                {
                    minDistance = sqrDist;
                    targetPos = worldAnchor.transform.position;
                    bestLocalSnap = localAnchor;
                    bestWorldSnap = worldAnchor;
                    snapped = true;
                }
            }
        }

        return (bestLocalSnap, targetPos, snapped);
    }

    // 자신과 똑같은 오브젝트를 가리켰을 때 스냅이 꼬이는 현상 방지
    private void CheckAndReleaseSelfSnap(Transform materialTr, RaycastHit hitData, Vector3 newPos, Vector3 targetPos, ref GameObject heldSnap, ref bool snapped, IMaterial mat)
    {
        GameObject hitObject = hitData.collider?.gameObject;

        if (hitObject == null)
            return;

        bool isPreviewSelf =
            hitObject == materialTr.gameObject ||
            hitObject.transform.IsChildOf(materialTr);

        // 다른 동일 프리팹 인스턴스는 자기 자신이 아님
        if (!isPreviewSelf)
            return;

        if (!snapped || heldSnap == null)
            return;

        Vector3 previousPosition =
            posAdjustByLocalOffset(
                materialTr,
                heldSnap.transform,
                targetPos,
                mat);

        const float releaseDistance = 0.1f;

        if ((previousPosition - hitObject.transform.position).sqrMagnitude <
            releaseDistance * releaseDistance)
        {
            heldSnap = null;
            snapped = false;
            MousePosWhenSnapped = newPos;
        }

    }

    // 벽면이나 바닥의 Normal 각도에 맞춰서 가장 적합한 회전축(Pivot) 찾기
    private GameObject FindBestPivot(Transform materialTr, IMaterial material, List<GameObject> curMatSnapPoints, Vector3 hitNormal)
    {
        float direcAccordance = 3.0f;
        GameObject heldPivot = null;

        foreach (var curMatSnapPoint in curMatSnapPoints)
        {
            float newdirec = Vector3.Dot(curMatSnapPoint.transform.forward, hitNormal);

            if (newdirec < direcAccordance)
            {
                direcAccordance = newdirec;
                heldPivot = curMatSnapPoint;

                if (material.GetBuildingMaterialType() == eBuildingMaterial.Torch)
                {
                    material.ApplySpecialRotation(materialTr, curMatSnapPoint);
                }
            }
            else if (newdirec == direcAccordance && curMatSnapPoint.CompareTag(LayerAndTagConstants.Tag_Snap))
            {
                heldPivot = curMatSnapPoint;
            }
        }
        return heldPivot;
    }

    // 마우스를 너무 멀리 옮기면 스냅 풀어주기
    private void MaintainOrReleaseSnap(Vector3 newPos, GameObject curSnapPoint, ref GameObject heldSnap, ref Vector3 targetPos, ref bool snapped)
    {
        if (isSnapped)
        {
            float sqrDist = (MousePosWhenSnapped - newPos).sqrMagnitude;
            float sqrUnsnapDist = 0.4f * 0.4f; // UNSNAP_DIST

            if (sqrDist > sqrUnsnapDist + 0.05f)
            {
                heldSnap = null;
                limitDistance = 0.5f;
                snapped = false;
                MousePosWhenSnapped = newPos;
            }
            else if (curSnapPoint != null)
            {
                targetPos = curSnapPoint.transform.position;
                heldSnap = curSnapPoint;
                snapped = true;
            }
        }
    }

    private Vector3 FindTargetWorldSnapPosForManualMode(Transform curSnapPoint, Vector3 newPos)
    {
        Vector3 bestPos = newPos;

        float detectRadius = 0.6f;
        float UNSNAP_DIST = 0.4f;
        float sqrUnsnapDist = UNSNAP_DIST * UNSNAP_DIST;

        float bestDirectionMatch = 2f; // 방향 일치도 (최솟값을 찾기 위해 큰 값으로 초기화)

        // 마우스 위치 주변의 피벗들 검색
        int hitCount = Physics.OverlapSphereNonAlloc(newPos, detectRadius, hitColliders, pivotLayerMask);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hitColliders[i];
            if (col == null || col.gameObject == curSnapPoint.gameObject) continue; // 자기 자신 무시

            // 거리 검사 (너무 멀면 스냅 무시)
            float sqrDist = (newPos - col.transform.position).sqrMagnitude;
            if (sqrDist > sqrUnsnapDist) continue;

            // 방향 검사: 내 스냅 포인트의 앞면(forward)과 타겟의 앞면 각도 비교
            float directionMatch = Vector3.Dot(curSnapPoint.forward, col.transform.forward);

            // 더 방향이 잘 맞는(값이 작은) 피벗이 있다면 타겟 갱신
            if (bestDirectionMatch > directionMatch)
            {
                bestDirectionMatch = directionMatch;
                bestWorldSnap = col.gameObject;
                bestPos = col.transform.position;
            }
        }

        return bestPos; // 마땅한 게 없으면 원래 마우스 위치(newPos) 그대로 반환
    }

    public void ClearSnapState()
    {
        isSnapped = false;
        bestWorldSnap = null;
        snapState.isSnapped = false;
    }


}
