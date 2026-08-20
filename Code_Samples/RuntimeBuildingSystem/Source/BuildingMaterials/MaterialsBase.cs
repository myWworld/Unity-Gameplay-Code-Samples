using System.Collections.Generic;
using UnityEngine;
using MalbersAnimations.InventorySystem;

public abstract class MaterialsBase : MonoBehaviour, IMaterial
{
    [SerializeField] protected BuildingDataSO buildingData;

    [SerializeField] protected IMaterial parentPrefab;
    [SerializeField] protected Vector3 offset;

    [SerializeField] protected List<GameObject> anchors = new List<GameObject>();
    protected BuildingMaterialManagement buildingMaterialManagement = null;


    [SerializeField] protected List<IMaterial> parents = new List<IMaterial>();
    [SerializeField] protected List<IMaterial> connectedChildren = new List<IMaterial>();
    [SerializeField] protected bool bIsGrounded = false;
    protected float supportValue;

    private Dictionary<string, int> requirementsCache = null;

    [Header("Visual Interpolation")]
    [SerializeField] protected Transform visualMesh;

    private GameObject pivot;


    public BuildingDataSO Data => buildingData;

    public List<IMaterial> Parents => parents;
    public List<IMaterial> ConnectedChildren => connectedChildren;

    private Transform cachedTr;
    private Collider[] cachedCollider = new Collider[50];
    private IMaterial cachedMat;

    public Dictionary<string, int> RequirementsForMat
    {
        get
        {
            if (requirementsCache == null)
            {
                requirementsCache = new Dictionary<string, int>();
                if (buildingData != null && buildingData.requirements != null)
                {
                    foreach (ResourceRequirement requirement in buildingData.requirements)
                    {
                        if (!string.IsNullOrWhiteSpace(requirement.itemName) && requirement.count > 0)
                        {
                            requirementsCache[requirement.itemName] = requirement.count;
                        }
                    }
                }
            }

            return requirementsCache;
        }
    }

    public float SupportValue
    {
        get => supportValue;
        set => supportValue = value;
    }

    public List<Renderer> materialRenderers;
   public List<Renderer> MaterialRenderers
    {
        get
        {
            return materialRenderers;
        }
    }

    public float MaxSupportWeight => buildingData != null ? buildingData.maxSupportWeight : 0.1f;
    public MaterialType GetMaterialType() => buildingData != null ? buildingData.materialType : MaterialType.End;
    public eBuildingMaterial GetBuildingMaterialType() => buildingData != null ? buildingData.buildingMaterial : eBuildingMaterial.End;

    public bool bGrounded
    {
        get => bIsGrounded;
        set => bIsGrounded = value;
    }


    protected virtual void Start()
    {
        if (buildingMaterialManagement == null)
        {
            buildingMaterialManagement = FindFirstObjectByType<BuildingMaterialManagement>();
        }

        EnsureCachedReferences();
    }

    private Vector3 defaultLocalPos;
    private Quaternion defaultLocalRot;

    protected virtual void Awake()
    {
        if (visualMesh != null)
        {
            defaultLocalPos = visualMesh.localPosition;
            defaultLocalRot = visualMesh.localRotation;

            Renderer[] allRenderers = visualMesh.GetComponentsInChildren<Renderer>(true);
            materialRenderers = new List<Renderer>(allRenderers);

        }
    }

    public Transform GetVisualMesh() => visualMesh;

    public void ResetVisualTransform()
    {
        if (visualMesh != null)
        {
            visualMesh.localPosition = defaultLocalPos;
            visualMesh.localRotation = defaultLocalRot;
        }
    }
    public Vector3 GetDefaultLocalPos() => defaultLocalPos;
    public Quaternion GetDefaultLocalRot() => defaultLocalRot;




    public virtual GameObject GetGameObject()
    {
        if (this == null || this.gameObject == null)
            return null;

        return this.gameObject;
    }

    public virtual void SetParentPrefab(IMaterial parentPrefab)
    {
        this.parentPrefab = parentPrefab; // 부모 객체 설정
    }

    public virtual IMaterial GetParentPrefab()
    {
        if (parentPrefab == null)
        {
    
            return null;
        }
        return this.parentPrefab; // 부모 프리팹 반환
    }



    public virtual void SetPivot(GameObject gameObject)
    {
        this.pivot = gameObject;

        if (pivot != null && this.gameObject != null)
        {
            this.offset = pivot.transform.position - this.gameObject.transform.position;
        }
    }

    public virtual GameObject GetPivot()
    {
        return this.pivot;
    }
    public virtual Vector3 GetOffsetBetweenObjAndAnchor()
    {
        return this.offset;
    }

    public virtual void UpdateOffset()
    {
        Transform pivotTransform = transform.Find("pivotPos");
        if (pivotTransform != null)
        {
            offset = pivotTransform.position - transform.position;
        }
    }

    public virtual List<GameObject> GetAnchors()
    {
        return this.anchors;
    }

    public virtual GameObject GetAnchorByIndx(int idx)
    {
        if (idx < 0 || idx >= anchors.Count)
        {
            UnityEngine.Debug.LogWarning("Anchor index out of range.");
            return null;
        }

        return anchors[idx];
    }

    public virtual void OnHpEmpty()
    {
        ItemDrop();
        buildingMaterialManagement.HideMaterial(this.GetGameObject());
    }


    public void ItemDrop()
    {
        if (buildingMaterialManagement == null)
        {
            buildingMaterialManagement = FindFirstObjectByType<BuildingMaterialManagement>();
        }

        EnsureCachedReferences();
        if (buildingMaterialManagement == null || cachedTr == null)
        {
            return;
        }

        int cnt = Physics.OverlapSphereNonAlloc(
            cachedTr.position,
            3.0f,
            cachedCollider,
            LayerMask.GetMask("BUILDING"));


        for (int i = 0; i < cnt; i++)
        {
            Collider col = cachedCollider[i];

            if (!BuildingColliderUtility.TryResolveMaterialRoot(col, out GameObject materialRoot, out IMaterial material))
                continue;

            if (material is Torch torch && !torch.HasSupported(GetGameObject()))
            {
                // Preserve the original torch cleanup path. Torch may have project-specific
                // pooling/destruction behavior outside this public source snapshot.
                Destroy(materialRoot);
            }
        }

        List<GameObject> items = buildingMaterialManagement.GetReqMaterialItems();
        if (items == null)
        {
            return;
        }

        foreach (GameObject item in items)
        {
            if (item == null)
            {
                continue;
            }

            InventoryItem itemComp = item.GetComponent<InventoryItem>();
            if (itemComp == null || itemComp.inventoryItem == null)
            {
                continue;
            }

            string itemName = itemComp.inventoryItem.itemName;

            if (RequirementsForMat.ContainsKey(itemName) == false)
                continue;

            int reqCount = RequirementsForMat[itemName];
            for (int i = 0; i < reqCount; i++)
            {
                dropReqirementsRandomly(item);
            }
        }
    }

   public virtual void ApplySpecialRotation(Transform materialTr, GameObject targetAnchor)
    {
    }




    private void dropReqirementsRandomly(GameObject item)
    {
        if (item == null || cachedTr == null)
        {
            return;
        }

        Transform tr = cachedTr;
        Vector3 randomDir = tr.forward * UnityEngine.Random.Range(-1f, 1f) +
                           tr.right * UnityEngine.Random.Range(-1f, 1f) +
                           tr.up * UnityEngine.Random.Range(1.0f, 2.0f);


        Vector3 spawnPos = transform.position;
        spawnPos.y += 0.5f;

        GameObject droppedItem = Instantiate(item, spawnPos, Quaternion.identity);
        Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(randomDir * UnityEngine.Random.Range(2f, 3f), ForceMode.Impulse);
        }
    }

    private void EnsureCachedReferences()
    {
        if (cachedTr == null)
        {
            cachedTr = transform;
        }

        if (cachedMat == null)
        {
            cachedMat = GetComponent<IMaterial>();
        }
    }
}
