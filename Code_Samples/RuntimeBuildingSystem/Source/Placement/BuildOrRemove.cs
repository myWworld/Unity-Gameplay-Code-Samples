
using System.Collections.Generic;
using UnityEngine;

public class BuildOrRemove : MonoBehaviour
{

    private SnapController snapController = null;
    private Color originalColor;
    private Color removeOrigColor; // Original color of the removal candidate.

    private Dictionary<string, Material> materials;
    // Material instances are avoided here; highlight feedback is applied through layers/property blocks.

    private MaterialPropertyBlock propertyBlock;


    private PartialNavMeshBuilder partialNavMeshBuilder;

    public GameObject previewObject; // Currently displayed placement preview.
    private IMaterial previewIMaterial; // Cached material interface for the preview object.
    private GameObject highlightedObject;

    public GameObject removeCandidate; // Object currently highlighted as removable.
    private GameObject removeObject; // Confirmed removal target.


    private Vector3 lastSnapPosition; // Last committed snap position.

    BuildingMaterialManagement buildingMaterialManagement = null;
    StructuralIntegritySolver structuralIntegritySolver = null;

    [Header("Integrity Colors (Hologram)")]

    private int layerGreen;
    private int layerYellow;
    private int layerRed;
    private int layerBlue;

    private int originalLayerForRemovable = -1;
    private int originalLayer = -1;
    // 아주 밝은 형광 연두색
    public Color maxSupportColor = new Color(0.2f, 1.0f, 0.5f, 0.45f);

    // 밝은 레몬/파스텔 노란색
    public Color midSupportColor = new Color(1.0f, 0.9f, 0.2f, 0.45f);

    // 칙칙하지 않은 밝은 파스텔 빨간색
    public Color minSupportColor = new Color(1.0f, 0.4f, 0.4f, 0.45f);

    void Start()
    {
        buildingMaterialManagement = GetComponent<BuildingMaterialManagement>();
        structuralIntegritySolver = GetComponent<StructuralIntegritySolver>();
        partialNavMeshBuilder = this.GetComponent<PartialNavMeshBuilder>();
        snapController = GetComponent<SnapController>();
        materials = new Dictionary<string, Material>();

        if (snapController == null)
            UnityEngine.Debug.LogWarning("[BuildOrRemove] SnapController dependency was not found.");

        if (buildingMaterialManagement == null)
            UnityEngine.Debug.LogWarning("[BuildOrRemove] BuildingMaterialManagement dependency was not found.");

        StoreMaterial();

        layerGreen = LayerAndTagConstants.Layer_HighlightGreen;
        layerRed = LayerAndTagConstants.Layer_HighlightRed;
        layerYellow = LayerAndTagConstants.Layer_HighlightYellow;
        layerBlue = LayerAndTagConstants.Layer_HighlightBlue;
    }

    private void StoreMaterial()
    {
        var parentPrefabs = buildingMaterialManagement.GetBuildingPrefabs();
        propertyBlock = new MaterialPropertyBlock();

        foreach (var pair in parentPrefabs)
        {

            BuildingDataSO data = pair.Key;

            IMaterial imat = data.prefab.GetComponent<IMaterial>();

            if (imat != null && imat.GetVisualMesh() != null)
            {
                if (imat.MaterialRenderers.Count > 0)
                {
                    foreach (var rend in imat.MaterialRenderers)
                    {

                        materials[data.materialName] = rend.sharedMaterial;

                        break;//같은 머테리얼을 공유함
                    }
                }
            }


        }
    }

    public void PlaceMaterial(Vector3 MousePos)
    {
        snapController.UpdateAnchorAndMaterialPos(previewObject.transform, MousePos);
        RuntimePlacedBuildingMarker.Ensure(previewObject);

        if(previewObject.CompareTag("Walkable"))
        {
            partialNavMeshBuilder.UpdateNavMeshAt(previewObject); // Update the affected local NavMesh region.
        }


        RestoreOrigLayer(highlightedObject);

    }


    public void RemoveMaterial(GameObject newRemoveObject) // Remove the selected building piece and recalculate support.
    {
        if (newRemoveObject == null) return;

        GameObject removeTarget = newRemoveObject;
        if (BuildingColliderUtility.TryResolveMaterialRoot(newRemoveObject, out GameObject materialRoot, out _))
        {
            removeTarget = materialRoot;
        }

        int objLayer = removeTarget.layer;

        if (objLayer != LayerAndTagConstants.Layer_Building &&
           objLayer != LayerAndTagConstants.Layer_Door )
           //objLayer != LayerAndTagConstants.Layer_Worktable &&
           //objLayer != LayerAndTagConstants.Layer_Furnace &&
           //objLayer != LayerAndTagConstants.Layer_Agungi)
            return;



        ResetRemoveCandidate();

        if (removeTarget.TryGetComponent(out IMaterial imat))
        {
            structuralIntegritySolver.HandleMaterialPropagate(imat,buildingMaterialManagement);
            imat.ItemDrop();
        }


        buildingMaterialManagement.HideMaterial(removeTarget);


    }


    public void UpdatePreview(GameObject curObject, GameObject target)
    {

        previewObject = curObject;

        if (highlightedObject != null && (target == null || target != highlightedObject))
            RestoreOrigLayer(highlightedObject);

        if (target != null)
            highlightedObject = target;
        else
            highlightedObject = previewObject;

    }

    public bool UpdatePreviewColor(bool canPlace, float supportVal)
    {
        if (previewObject != null)
        {

            if (canPlace)
            {
                if (supportVal > 0.4f)
                {
                    ChangeHighlightLayer(previewObject, layerGreen);
                }
                else
                {
                    ChangeHighlightLayer(previewObject, layerYellow);
                }

                return true;
            }
            else
            {
                ChangeHighlightLayer(previewObject, layerRed);
                return false;
            }
        }

        return false;
    }

    public bool UpdatePreviewHighlight(bool canPlace, float supportVal)
    {
        if (previewObject != null)
        {

            if (canPlace)
            {
                if (supportVal > 0.4f)
                {
                    ChangeHighlightLayer(highlightedObject, layerGreen);
                }
                else
                {
                    ChangeHighlightLayer(highlightedObject, layerYellow);
                }

                return true;
            }
            else
            {
                ChangeHighlightLayer(highlightedObject, layerRed);
                return false;
            }
        }

        return false;


    }


    public void ChangeHighlightLayer(GameObject target, int targetLayer)
    {
        if (target == null) return;



        if (target.TryGetComponent(out IMaterial imat) && imat.MaterialRenderers != null)
        {

            foreach (Renderer rend in imat.MaterialRenderers)
            {
                if (rend != null)
                {

                    if (originalLayer == -1)
                    {
                        originalLayer = rend.gameObject.layer;
                    }

                    rend.gameObject.layer = targetLayer;
                }
            }
        }
    }

    public void RestoreOrigLayer(GameObject target)
    {
        if (target == null || originalLayer == -1) return;



        if (target.TryGetComponent(out IMaterial imat) && imat.MaterialRenderers != null)
        {
            foreach (Renderer rend in imat.MaterialRenderers)
            {
                if (rend != null)
                {

                    rend.gameObject.layer = originalLayer;
                }
            }
        }

        originalLayer = -1;
    }
    public void RemoveCandidateColorChange(GameObject target, Color color)
    {
        if (BuildingColliderUtility.TryResolveMaterialRoot(target, out GameObject materialRoot, out _))
        {
            target = materialRoot;
        }

        IMaterial material;

        if (!target.TryGetComponent(out material))
        {
            material = target.GetComponentInParent<IMaterial>();
        }

        if (material == null) return;

        target = material.GetGameObject();

        if (target != removeCandidate)
        {
            if (removeCandidate != null)
            {
                RestoreOrigLayer(removeCandidate);

            }
            ChangeHighlightLayer(target, layerBlue);
            removeCandidate = target;
        }
    }

    public void ResetRemoveCandidate()
    {
        if (removeCandidate != null)
        {
            RestoreOrigLayer(removeCandidate);
            removeCandidate = null;
        }
    }
    public void ChangeColor(GameObject target, Color newColor)
    {
        if (target == null) return;
        IMaterial imat = null;


        if (target.TryGetComponent(out  imat) && imat.MaterialRenderers != null)
        {
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();

            foreach (Renderer rend in imat.MaterialRenderers)
            {
                if (rend != null)
                {
                    rend.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor("_BaseColor", newColor);
                    rend.SetPropertyBlock(propertyBlock);
                }
            }
        }
    }

    public void RestoreOrigColor(GameObject target)
    {
        if (target == null) return;

        if (target.TryGetComponent(out IMaterial imat) && imat.MaterialRenderers != null)
        {
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();

            foreach (Renderer rend in imat.MaterialRenderers)
            {
                if (rend != null)
                {
                    propertyBlock.Clear();
                    rend.SetPropertyBlock(propertyBlock);
                }
            }
        }
    }

    public void ResetRemoveObject()
    {
        RestoreOrigLayer(removeObject);
    }


    public void ResetHighlitedObject()
    {
        RestoreOrigLayer(highlightedObject);
    }
}
