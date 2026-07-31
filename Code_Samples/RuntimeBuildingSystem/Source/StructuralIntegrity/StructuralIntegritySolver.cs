using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class StructuralIntegritySolver : MonoBehaviour
{
    public BuildingMaterialManagement buildingMaterialManagement;

    private readonly HashSet<IMaterial> cluster = new HashSet<IMaterial>();
    private readonly Queue<IMaterial> bfsQueue = new Queue<IMaterial>();
    private readonly Queue<IMaterial> removalQueue = new Queue<IMaterial>();
    private readonly List<IMaterial> neighbors = new List<IMaterial>();
    private readonly List<IMaterial> currNeighbors = new List<IMaterial>();
    private readonly List<IMaterial> neighborsGather = new List<IMaterial>();

    private readonly Collider[] hitColliders = new Collider[50];
    private int buildingLayerMask;

    void Awake()
    {
        buildingLayerMask = LayerAndTagConstants.Mask_BuildingAndDoor;
        buildingMaterialManagement = GetComponent<BuildingMaterialManagement>();
    }
    public float PredictSupportValue(Vector3 previewPos, GameObject previewObj, BuildingMaterialManagement manager)
    {
        if (previewObj == null) return 0f;

        IMaterial previewMat = previewObj.GetComponent<IMaterial>();


        if (manager.IsTouchingGroundAt(previewPos, previewObj))
        {
            return 1.2f; // 땅에 닿으면 즉시 통과
        }

        Vector3 positionOffset = previewPos - previewObj.transform.position;
        List<GameObject> anchors = previewMat.GetAnchors();

        float maxPredictedSupport = 0f;

        foreach (var anchor in anchors)
        {
            if (anchor == null) continue;

            // 앵커가 실제로 배치될 미래 위치 계산
            Vector3 futureAnchorPos = anchor.transform.position + positionOffset;

            int hitCnt = Physics.OverlapSphereNonAlloc(futureAnchorPos, 0.4f, hitColliders, buildingLayerMask);

            for (int i = 0; i < hitCnt; i++)
            {
                Collider col = hitColliders[i];

                // 프리뷰 오브젝트 자신은 제외
                if (BuildingColliderUtility.IsSelfOrProxyOf(col, previewObj)) continue;

                if (BuildingColliderUtility.TryResolveMaterialRoot(col, out _, out IMaterial neighborMat))
                {
                    // 이웃의 지지력
                    float neighborSupport = neighborMat.SupportValue;

                    // 이웃에서 나에게 하중을 전달할 때의 감쇠율 적용
                    float decay = GetDecayValueByMType(neighborMat);
                    float predicted = neighborSupport * decay;

                    // 연결될 여러 이웃 중 가장 높은 지지력을 제공하는 값을 채택
                    if (predicted > maxPredictedSupport)
                    {
                        maxPredictedSupport = predicted;
                    }
                }
            }
        }

        return maxPredictedSupport; // 예상 지지력 반환
    }

    public void HandleMaterialPlacement(IMaterial placedMat)
    {
        if (placedMat == null || placedMat.GetGameObject() == null) return;

        bfsQueue.Clear();
        bfsQueue.Enqueue(placedMat);

        while (bfsQueue.Count > 0)
        {
            IMaterial curr = bfsQueue.Dequeue();
            float currSupport = curr.SupportValue;

            neighbors.Clear();

            neighbors.AddRange(curr.ConnectedChildren);
            neighbors.AddRange(curr.Parents);

            foreach (var next in neighbors)
            {
                if (next == null || next.GetGameObject() == null) continue;


                float decay = GetDecayValueByMType(curr);
                float offeredSupport = currSupport * decay;


                if (offeredSupport > next.SupportValue)
                {
                    next.SupportValue = offeredSupport;
                    bfsQueue.Enqueue(next);
                }
            }
        }
    }

    public void HandleMaterialPropagate(IMaterial targetMat, BuildingMaterialManagement manager, bool IsDecrease = false, float minSupport = 0.25f)
    {
        if (targetMat == null) return;

        cluster.Clear();
        bfsQueue.Clear();
        removalQueue.Clear();
        neighbors.Clear();
        currNeighbors.Clear();

        neighbors.AddRange(targetMat.ConnectedChildren);
        neighbors.AddRange(targetMat.Parents);

        if (IsDecrease)
            neighbors.Add(targetMat);
        else
            RemoveTargetFromItsParentAndChild(targetMat);

        foreach (var neighbor in neighbors)
        {
            if (neighbor != null && neighbor.GetGameObject() != null && !cluster.Contains(neighbor))
            {
                GatherCluster(neighbor, cluster);
            }
        }

        foreach (var mat in cluster)//모든 자재담아온 후 지지력 0으로 만듦
        {
            mat.SupportValue = 0f;
        }

        foreach (var mat in cluster)
        {
            if (manager.IsTouchingGround(mat.GetGameObject()))//땅에 지지가 되는 애들은 베이스 지지력 받음과 동시에 bfs에서 시작점이 된다
            {
                mat.SupportValue = 1.2f; // GetBaseSupport
                bfsQueue.Enqueue(mat);
            }
        }

        Profiler.BeginSample("Support Propagation BFS");
        while (bfsQueue.Count > 0)//지지력 재계산
        {
            IMaterial curr = bfsQueue.Dequeue();
            float currSupport = curr.SupportValue;
            currNeighbors.Clear();
            currNeighbors.AddRange(curr.ConnectedChildren);
            currNeighbors.AddRange(curr.Parents);

            foreach (var next in currNeighbors)
            {
                if (next == null || next.GetGameObject() == null) continue;

                float decay = GetDecayValueByMType(curr);
                float nextSupport = currSupport * decay;

                if (nextSupport > next.SupportValue)
                {
                    next.SupportValue = nextSupport;
                    bfsQueue.Enqueue(next);
                }
            }
        }
        Profiler.EndSample();

        foreach (var mat in cluster)//지지력 업데이트 된 애들 중 최소 지지력 보다 작은 애들 다 지우기
        {
            if (mat.SupportValue < minSupport)
            {
                removalQueue.Enqueue(mat);
            }
        }

        if (removalQueue.Count > 0)
        {
            Queue<IMaterial> queueSnapshot = new Queue<IMaterial>(removalQueue);
            StartCoroutine(CollapseWithDelay(queueSnapshot, 0.15f));
        }
    }

    private void GatherCluster(IMaterial startNode, HashSet<IMaterial> cluster)
    {
        bfsQueue.Clear();
        bfsQueue.Enqueue(startNode);
        cluster.Add(startNode);

        while (bfsQueue.Count > 0)
        {
            var curr = bfsQueue.Dequeue();
            neighborsGather.Clear();
            neighborsGather.AddRange(curr.ConnectedChildren);
            neighborsGather.AddRange(curr.Parents);

            foreach (var link in neighborsGather)
            {
                if (link != null && link.GetGameObject() != null && !cluster.Contains(link))
                {
                    cluster.Add(link);
                    bfsQueue.Enqueue(link);
                }
            }
        }
    }

    private IEnumerator CollapseWithDelay(Queue<IMaterial> queue, float delay)
    {
        while (queue.Count > 0)
        {
            IMaterial target = queue.Dequeue();
            if (target == null || target.GetGameObject() == null) continue;

            //Debug.Log($"건축물 붕괴: {target.GetGameObject().name}");
            RemoveTargetFromItsParentAndChild(target);
            buildingMaterialManagement.DestroyProcess(target);
            yield return new WaitForSeconds(delay);
        }
    }

    public void RemoveTargetFromItsParentAndChild(IMaterial newIMaterial)
    {
        if (newIMaterial == null) return;

        foreach (IMaterial child in newIMaterial.ConnectedChildren)
        {
            if (child != null) child.Parents.Remove(newIMaterial);
        }
        foreach (IMaterial parent in newIMaterial.Parents)
        {
            if (parent != null) parent.ConnectedChildren.Remove(newIMaterial);
        }
    }

    float GetBaseSupport(IMaterial mat)
    {
        return 1.2f;
    }

    private float GetDecayValueByMType(IMaterial mat)
    {
        switch (mat.GetBuildingMaterialType())
        {
            case eBuildingMaterial.Pole:
            case eBuildingMaterial.HalfPole:
            case eBuildingMaterial.BaseRockBig:
            case eBuildingMaterial.BaseRockSmall:

                return 0.945f;

            case eBuildingMaterial.HalfPole25:
            case eBuildingMaterial.HalfPole45:
            case eBuildingMaterial.HalfPole65:
            case eBuildingMaterial.Pole25:
            case eBuildingMaterial.Pole45:
            case eBuildingMaterial.Pole65:

                return 0.94f;

            case eBuildingMaterial.Pole90:
            case eBuildingMaterial.HalfPole90:

                return 0.93f;

            default:
                return 0.75f;
        }
    }

    public void ClearParentAndChildren(IMaterial newIMaterial)
    {
        if (newIMaterial == null)
            return;

        foreach (IMaterial child in newIMaterial.ConnectedChildren)
        {
            if (child != null)
            {
                child.Parents.Remove(newIMaterial);
            }
        }

        foreach (IMaterial parent in newIMaterial.Parents)
        {
            if (parent != null)
            {
                parent.ConnectedChildren.Remove(newIMaterial);
            }
        }

        newIMaterial.Parents.Clear();
        newIMaterial.ConnectedChildren.Clear();
    }


    public void UpdateParentsAndChildren(IMaterial newIMaterial)
    {


        if (newIMaterial == null)
            return;

        GameObject targetObj = newIMaterial.GetGameObject();
        if (targetObj == null)
            return;

        foreach (GameObject anchor in newIMaterial.GetAnchors())
        {


            Vector3 anchorPos = anchor.transform.position;
            int hitCount = Physics.OverlapSphereNonAlloc(anchorPos, 0.4f, hitColliders, buildingLayerMask);

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = hitColliders[i];

                if (BuildingColliderUtility.IsSelfOrProxyOf(col, targetObj))
                    continue;

                if (!BuildingColliderUtility.TryResolveMaterialRoot(col, out GameObject materialRoot, out IMaterial imat))
                    continue;

                float deltaY = materialRoot.transform.position.y - targetObj.transform.position.y;

                if (Mathf.Abs(deltaY) <= 0.05f) //높이 비슷할 경우 먼저있던애가 부모
                {
                    ConnectParentAndChild(imat, newIMaterial);
                }
                else if (deltaY > 0.05)
                {
                    ConnectParentAndChild(newIMaterial, imat);

                }
                else
                {
                    // 아래에 있음 → 내가 자식

                    ConnectParentAndChild(imat, newIMaterial);

                }
            }
        }

    }

    public void ConnectParentAndChild(IMaterial parent, IMaterial child)
    {
        if (parent == null || child == null || parent == child) return;

        //  UnityEngine.Debug.Log($"부모는{parent.GetGameObject().name} 자식은{child.GetGameObject().name}");

        if (!parent.ConnectedChildren.Contains(child))
            parent.ConnectedChildren.Add(child);

        if (!child.Parents.Contains(parent))
            child.Parents.Add(parent);
    }

}
