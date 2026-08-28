using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class StructuralIntegritySolver : MonoBehaviour //지지력 관련 계산을 하는 클래스
{
    [Header("Dependencies")]
    public BuildingMaterialManagement buildingMaterialManagement;

    [Header("Support Settings")]
    [SerializeField, Min(0f)] private float baseSupportValue = 1.2f;
    [SerializeField, Min(0f)] private float minimumSupportValue = 0.25f;
    [SerializeField, Range(0f, 1f)] private float defaultDecay = 0.75f;
    [SerializeField, Range(0f, 1f)] private float verticalSupportDecay = 0.945f;
    [SerializeField, Range(0f, 1f)] private float angledSupportDecay = 0.94f;
    [SerializeField, Range(0f, 1f)] private float horizontalSupportDecay = 0.93f;
    [SerializeField, Min(0.01f)] private float connectionRadius = 0.4f;
    [SerializeField, Min(0f)] private float collapseDelay = 0.15f;
    [SerializeField, Min(0f)] private float propagationEpsilon = 0.0001f;

    [Header("Physics Query Capacity")]
    [SerializeField, Min(8)] private int queryCapacity = 50;

    private readonly HashSet<IMaterial> cluster = new HashSet<IMaterial>();
    private readonly Queue<IMaterial> bfsQueue = new Queue<IMaterial>();
    private readonly Queue<IMaterial> removalQueue = new Queue<IMaterial>();
    private readonly Queue<IMaterial> pendingCollapseQueue = new Queue<IMaterial>();
    private readonly HashSet<IMaterial> pendingCollapseSet = new HashSet<IMaterial>();
    private readonly List<IMaterial> neighbors = new List<IMaterial>();
    private readonly List<IMaterial> currentNeighbors = new List<IMaterial>();
    private readonly List<IMaterial> gatherNeighbors = new List<IMaterial>();

    private Collider[] hitColliders;
    private int buildingLayerMask;
    private Coroutine collapseRoutine;
    private bool hasLoggedQueryCapacityWarning;

    public float BaseSupportValue => baseSupportValue;
    public float MinimumSupportValue => minimumSupportValue;

    private void Awake()
    {
        buildingLayerMask = LayerAndTagConstants.Mask_BuildingAndDoor;
        EnsureQueryBuffer();

        if (buildingMaterialManagement == null)
        {
            buildingMaterialManagement = GetComponent<BuildingMaterialManagement>();
        }
    }

    private void OnValidate()
    {
        baseSupportValue = Mathf.Max(0f, baseSupportValue);
        minimumSupportValue = Mathf.Max(0f, minimumSupportValue);
        defaultDecay = Mathf.Clamp01(defaultDecay);
        verticalSupportDecay = Mathf.Clamp01(verticalSupportDecay);
        angledSupportDecay = Mathf.Clamp01(angledSupportDecay);
        horizontalSupportDecay = Mathf.Clamp01(horizontalSupportDecay);
        connectionRadius = Mathf.Max(0.01f, connectionRadius);
        collapseDelay = Mathf.Max(0f, collapseDelay);
        propagationEpsilon = Mathf.Max(0f, propagationEpsilon);
        queryCapacity = Mathf.Max(8, queryCapacity);
    }

    private void OnDisable()
    {
        if (collapseRoutine != null)
        {
            StopCoroutine(collapseRoutine);
            collapseRoutine = null;
        }

        pendingCollapseQueue.Clear();
        pendingCollapseSet.Clear();
    }

    public void InitializeDependencies(BuildingMaterialManagement manager)
    {
        if (manager != null)
        {
            buildingMaterialManagement = manager;
        }

        EnsureQueryBuffer();
    }

    public float PredictSupportValue(Vector3 previewPosition, GameObject previewObject, BuildingMaterialManagement manager)//전체 탐색 안하고 배치할 자재 근처 지지력 통해 예측
    {
        if (previewObject == null)
        {
            return 0f;
        }

        manager = manager != null ? manager : buildingMaterialManagement;
        if (manager == null || !previewObject.TryGetComponent(out IMaterial previewMaterial))
        {
            return 0f;
        }

        if (manager.IsTouchingGroundAt(previewPosition, previewObject))//땅에 설치할 경우 바로 기반지지력
        {
            return baseSupportValue;
        }

        List<GameObject> anchors = previewMaterial.GetAnchors();//현재 홀딩 자재 앵커들 가져옴
        if (anchors == null || anchors.Count == 0)
        {
            return 0f;
        }

        EnsureQueryBuffer();
        Vector3 positionOffset = previewPosition - previewObject.transform.position;//프리뷰 오브젝트가 현재 위치에서 실제 배치 예정 위치까지 얼마나 이동해야 하는지
        float maximumPredictedSupport = 0f;

        for (int anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex++)
        {
            GameObject anchor = anchors[anchorIndex];
            if (anchor == null)
            {
                continue;
            }

            Vector3 futureAnchorPosition = anchor.transform.position + positionOffset;//오프셋을 더해 앵커의 미래 위치계산

            int hitCount = Physics.OverlapSphereNonAlloc(
                futureAnchorPosition,
                connectionRadius,
                hitColliders,
                buildingLayerMask);//앵커의 미래 위치에서 근처 자재 가져옴

            WarnIfQueryBufferIsFull(hitCount);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider collider = hitColliders[hitIndex];
                if (collider == null || BuildingColliderUtility.IsSelfOrProxyOf(collider, previewObject))
                {
                    continue;
                }

                if (!BuildingColliderUtility.TryResolveMaterialRoot(collider, out _, out IMaterial neighborMaterial)
                    || neighborMaterial == null)
                {
                    continue;
                }

                float predictedSupport = neighborMaterial.SupportValue * GetDecayValueByMaterialType(neighborMaterial);

                if (predictedSupport > maximumPredictedSupport)//최대지지력을 얻을 수 있는 쪽으로 작동
                {
                    maximumPredictedSupport = predictedSupport;
                }
            }
        }

        return maximumPredictedSupport;
    }

    public void HandleMaterialPlacement(IMaterial placedMaterial)//배치시 지지력 전파
    {
        if (!IsValidMaterial(placedMaterial))
        {
            return;
        }

        bfsQueue.Clear();
        bfsQueue.Enqueue(placedMaterial);//배치된 자재에서 시작
        PropagateMaximumSupport(bfsQueue);
    }

    public void HandleMaterialPropagate(
        IMaterial targetMaterial,
        BuildingMaterialManagement manager,
        bool isDecrease = false,
        float minSupport = -1f)//비지력 전파
    {
        if (!IsValidMaterial(targetMaterial))
        {
            return;
        }

        manager = manager != null ? manager : buildingMaterialManagement;
        if (manager == null)
        {
            return;
        }

        float supportThreshold = minSupport >= 0f ? minSupport : minimumSupportValue;

        cluster.Clear();
        bfsQueue.Clear();
        removalQueue.Clear();
        neighbors.Clear();
        currentNeighbors.Clear();

        AddNeighbors(targetMaterial, neighbors);//타겟 자재와 연결된 자재만 모두 등록(타겟 자재는 등록 X)

        if (isDecrease)
        {
            neighbors.Add(targetMaterial);//삭제가 아닌 감소일 경우 타겟 자재도 포함
        }
        else
        {
            RemoveTargetFromItsParentAndChild(targetMaterial);//삭제일 경우 현재 자재를 그래프 관계에서 끊어냄
        }

        for (int i = 0; i < neighbors.Count; i++)
        {
            IMaterial neighbor = neighbors[i];
            if (IsValidMaterial(neighbor) && !cluster.Contains(neighbor))
            {
                GatherCluster(neighbor);//연결된 자재 모아옴
            }
        }

        foreach (IMaterial material in cluster)//모두 0으로 지지력 초기화
        {
            material.SupportValue = 0f;
        }

        foreach (IMaterial material in cluster)//땅에 있는 자재를 시작점으로 다중 시작점
        {
            if (manager.IsTouchingGround(material.GetGameObject()))
            {
                material.SupportValue = baseSupportValue;
                bfsQueue.Enqueue(material);
            }
        }

        Profiler.BeginSample("Support Propagation BFS");
        PropagateMaximumSupport(bfsQueue);//지지력이 증가하는 경우만 업데이트 하도록 그래프탐색
        Profiler.EndSample();

        foreach (IMaterial material in cluster)//지지력이 기준 미달인 자재만 따로 모음
        {
            if (material.SupportValue < supportThreshold)
            {
                removalQueue.Enqueue(material);
            }
        }

        EnqueueCollapses(removalQueue);
    }

    public void RemoveTargetFromItsParentAndChild(IMaterial material)//해당 자재 지지력 그래프에서 제거
    {
        if (material == null)
        {
            return;
        }

        List<IMaterial> children = material.ConnectedChildren;
        if (children != null)
        {
            for (int i = 0; i < children.Count; i++)
            {
                IMaterial child = children[i];
                child?.Parents?.Remove(material);
            }
        }

        List<IMaterial> parents = material.Parents;
        if (parents != null)
        {
            for (int i = 0; i < parents.Count; i++)
            {
                IMaterial parent = parents[i];
                parent?.ConnectedChildren?.Remove(material);
            }
        }

        children?.Clear();
        parents?.Clear();
    }

    public void ClearParentAndChildren(IMaterial material)
    {
        RemoveTargetFromItsParentAndChild(material);
    }

    public void UpdateParentsAndChildren(IMaterial newMaterial)//위치 및 선후 관계에 따라 부모 자식  구분해서 등록
    {
        if (!IsValidMaterial(newMaterial))
        {
            return;
        }

        GameObject targetObject = newMaterial.GetGameObject();
        List<GameObject> anchors = newMaterial.GetAnchors();
        if (anchors == null || anchors.Count == 0)
        {
            return;
        }

        EnsureQueryBuffer();

        for (int anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex++)
        {
            GameObject anchor = anchors[anchorIndex];
            if (anchor == null)
            {
                continue;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(
                anchor.transform.position,
                connectionRadius,
                hitColliders,
                buildingLayerMask);
            WarnIfQueryBufferIsFull(hitCount);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider collider = hitColliders[hitIndex];
                if (collider == null || BuildingColliderUtility.IsSelfOrProxyOf(collider, targetObject))
                {
                    continue;
                }

                if (!BuildingColliderUtility.TryResolveMaterialRoot(
                        collider,
                        out GameObject materialRoot,
                        out IMaterial neighborMaterial) ||
                    materialRoot == null || neighborMaterial == null)
                {
                    continue;
                }

                float deltaY = materialRoot.transform.position.y - targetObject.transform.position.y;
                if (Mathf.Abs(deltaY) <= 0.05f)
                {
                    ConnectParentAndChild(neighborMaterial, newMaterial);
                }
                else if (deltaY > 0.05f)
                {
                    ConnectParentAndChild(newMaterial, neighborMaterial);
                }
                else
                {
                    ConnectParentAndChild(neighborMaterial, newMaterial);
                }
            }
        }
    }

    public void ConnectParentAndChild(IMaterial parent, IMaterial child)//그래프에 추가
    {
        if (parent == null || child == null || parent == child)
        {
            return;
        }

        if (!parent.ConnectedChildren.Contains(child))
        {
            parent.ConnectedChildren.Add(child);
        }

        if (!child.Parents.Contains(parent))
        {
            child.Parents.Add(parent);
        }
    }

    private void PropagateMaximumSupport(Queue<IMaterial> queue)//지지력이 올라가는 경우만 업데이트
    {
        while (queue.Count > 0)
        {
            IMaterial current = queue.Dequeue();
            if (!IsValidMaterial(current))
            {
                continue;
            }

            float offeredSupport = current.SupportValue * GetDecayValueByMaterialType(current);//다음 자재 지지력
            currentNeighbors.Clear();
            AddNeighbors(current, currentNeighbors);

            for (int i = 0; i < currentNeighbors.Count; i++)
            {
                IMaterial next = currentNeighbors[i];
                if (!IsValidMaterial(next) ||
                    offeredSupport <= next.SupportValue + propagationEpsilon)
                {
                    continue;
                }

                next.SupportValue = offeredSupport;
                queue.Enqueue(next);
            }
        }
    }

    private void GatherCluster(IMaterial startNode)//시작 노드와 연결된 자재 목록 가져옴
    {
        bfsQueue.Clear();
        bfsQueue.Enqueue(startNode);
        cluster.Add(startNode);

        while (bfsQueue.Count > 0)
        {
            IMaterial current = bfsQueue.Dequeue();
            gatherNeighbors.Clear();
            AddNeighbors(current, gatherNeighbors);

            for (int i = 0; i < gatherNeighbors.Count; i++)
            {
                IMaterial linkedMaterial = gatherNeighbors[i];

                if (IsValidMaterial(linkedMaterial) && cluster.Add(linkedMaterial))
                {
                    bfsQueue.Enqueue(linkedMaterial);
                }
            }
        }
    }

    private void EnqueueCollapses(Queue<IMaterial> collapsedMaterials)//연쇄 붕괴 
    {
        while (collapsedMaterials.Count > 0)//지지력 기준 미달 모으기
        {
            IMaterial material = collapsedMaterials.Dequeue();
            if (IsValidMaterial(material) && pendingCollapseSet.Add(material))
            {
                pendingCollapseQueue.Enqueue(material);
            }
        }

        if (pendingCollapseQueue.Count > 0 && collapseRoutine == null)
        {
            collapseRoutine = StartCoroutine(CollapsePendingWithDelay());//연쇄 붕괴 시작
        }
    }

    private IEnumerator CollapsePendingWithDelay()//붕괴 로직
    {
        WaitForSeconds wait = collapseDelay > 0f ? new WaitForSeconds(collapseDelay) : null;

        while (pendingCollapseQueue.Count > 0)
        {
            IMaterial target = pendingCollapseQueue.Dequeue();
            pendingCollapseSet.Remove(target);

            if (IsValidMaterial(target) && target.SupportValue < minimumSupportValue)
            {
                RemoveTargetFromItsParentAndChild(target);//그래프 연결 해제
                buildingMaterialManagement?.DestroyProcess(target);//풀 반환
            }

            if (wait != null)
            {
                yield return wait;
            }
            else
            {
                yield return null;
            }
        }

        collapseRoutine = null;
    }

    private float GetDecayValueByMaterialType(IMaterial material)
    {
        switch (material.GetBuildingMaterialType())
        {
            case eBuildingMaterial.Pole:
            case eBuildingMaterial.HalfPole:
            case eBuildingMaterial.BaseRockBig:
            case eBuildingMaterial.BaseRockSmall:
                return verticalSupportDecay;

            case eBuildingMaterial.HalfPole25:
            case eBuildingMaterial.HalfPole45:
            case eBuildingMaterial.HalfPole65:
            case eBuildingMaterial.Pole25:
            case eBuildingMaterial.Pole45:
            case eBuildingMaterial.Pole65:
                return angledSupportDecay;

            case eBuildingMaterial.Pole90:
            case eBuildingMaterial.HalfPole90:
                return horizontalSupportDecay;

            default:
                return defaultDecay;
        }
    }

    private void AddNeighbors(IMaterial material, List<IMaterial> destination)
    {
        if (material == null || destination == null)
        {
            return;
        }

        if (material.ConnectedChildren != null)
        {
            destination.AddRange(material.ConnectedChildren);
        }

        if (material.Parents != null)
        {
            destination.AddRange(material.Parents);
        }
    }

    private void EnsureQueryBuffer()//버퍼 할당
    {
        int capacity = Mathf.Max(8, queryCapacity);
        if (hitColliders == null || hitColliders.Length != capacity)
        {
            hitColliders = new Collider[capacity];
            hasLoggedQueryCapacityWarning = false;
        }
    }

    private void WarnIfQueryBufferIsFull(int hitCount)
    {
        if (!hasLoggedQueryCapacityWarning &&
            hitColliders != null &&
            hitCount >= hitColliders.Length)
        {
            Debug.LogWarning(
                $"[StructuralIntegritySolver] Physics query filled its {hitColliders.Length}-collider buffer. " +
                "Increase Query Capacity to avoid truncated neighbor results.");
            hasLoggedQueryCapacityWarning = true;
        }
    }

    private static bool IsValidMaterial(IMaterial material)
    {
        return material != null && material.GetGameObject() != null;
    }
}
