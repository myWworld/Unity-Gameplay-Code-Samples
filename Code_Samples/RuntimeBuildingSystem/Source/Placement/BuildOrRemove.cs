using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Low-level world commit and visual feedback component.
/// High-level placement/removal orchestration lives in BuildingPlacementService and BuildingRemovalService.
/// </summary>
public class BuildOrRemove : MonoBehaviour
{
    [Header("Integrity Colors (Hologram)")]
    public Color maxSupportColor = new Color(0.2f, 1f, 0.5f, 0.45f);
    public Color midSupportColor = new Color(1f, 0.9f, 0.2f, 0.45f);
    public Color minSupportColor = new Color(1f, 0.4f, 0.4f, 0.45f);

    public GameObject previewObject;
    public GameObject removeCandidate;

    private SnapController snapController;
    private PartialNavMeshBuilder partialNavMeshBuilder;
    private BuildingMaterialManagement buildingMaterialManagement;
    private StructuralIntegritySolver structuralIntegritySolver;

    private GameObject highlightedObject;
    private MaterialPropertyBlock propertyBlock;

    private int layerGreen;
    private int layerYellow;
    private int layerRed;
    private int layerBlue;

    private readonly LayerHighlightState previewHighlight = new LayerHighlightState();
    private readonly LayerHighlightState removalHighlight = new LayerHighlightState();

    private void Awake()
    {
        ResolveDependencies();
        propertyBlock = new MaterialPropertyBlock();

        layerGreen = LayerAndTagConstants.Layer_HighlightGreen;
        layerRed = LayerAndTagConstants.Layer_HighlightRed;
        layerYellow = LayerAndTagConstants.Layer_HighlightYellow;
        layerBlue = LayerAndTagConstants.Layer_HighlightBlue;
    }

    private void OnDisable()
    {
        previewHighlight.Restore();
        removalHighlight.Restore();
        highlightedObject = null;
        removeCandidate = null;
    }

    public void InitializeDependencies(
        BuildingMaterialManagement materialManagement,
        StructuralIntegritySolver integritySolver,
        SnapController snap,
        PartialNavMeshBuilder navMeshBuilder = null)
    {
        if (materialManagement != null)
        {
            buildingMaterialManagement = materialManagement;
        }

        if (integritySolver != null)
        {
            structuralIntegritySolver = integritySolver;
        }

        if (snap != null)
        {
            snapController = snap;
        }

        if (navMeshBuilder != null)
        {
            partialNavMeshBuilder = navMeshBuilder;
        }
        else if (partialNavMeshBuilder == null)
        {
            partialNavMeshBuilder = GetComponent<PartialNavMeshBuilder>();
        }
    }

    public void PlaceMaterial(Vector3 pivotPosition)
    {
        PlaceMaterial(previewObject, pivotPosition);
    }

    public bool PlaceMaterial(GameObject materialObject, Vector3 pivotPosition)
    {
        ResolveDependencies();
        if (materialObject == null || snapController == null)
        {
            return false;
        }

        previewObject = materialObject;
        snapController.UpdateAnchorAndMaterialPos(materialObject.transform, pivotPosition);
        RuntimePlacedBuildingMarker.Ensure(materialObject);

        if (materialObject.CompareTag("Walkable") && partialNavMeshBuilder != null)
        {
            partialNavMeshBuilder.UpdateNavMeshAt(materialObject);
        }

        previewHighlight.Restore();
        highlightedObject = null;
        return true;
    }

    public void RemoveMaterial(GameObject target)
    {
        TryRemoveMaterial(target);
    }

    public bool TryRemoveMaterial(GameObject target)
    {
        ResolveDependencies();
        if (target == null || buildingMaterialManagement == null || structuralIntegritySolver == null)
        {
            return false;
        }

        GameObject removeTarget = ResolveMaterialRoot(target);
        if (removeTarget == null || !IsRemovableLayer(removeTarget.layer))
        {
            return false;
        }

        if (!TryGetMaterial(removeTarget, out IMaterial material))
        {
            return false;
        }

        ResetRemoveCandidate();
        structuralIntegritySolver.HandleMaterialPropagate(material, buildingMaterialManagement);
        material.ItemDrop();
        buildingMaterialManagement.HideMaterial(removeTarget);
        return true;
    }

    public void UpdatePreview(GameObject currentObject, GameObject target)
    {
        previewObject = currentObject;
        GameObject nextHighlightTarget = target != null ? target : previewObject;

        if (highlightedObject != nextHighlightTarget)
        {
            previewHighlight.Restore();
            highlightedObject = nextHighlightTarget;
        }
    }

    public bool UpdatePreviewColor(bool canPlace, float supportValue)
    {
        if (previewObject == null)
        {
            return false;
        }

        ApplyPreviewHighlight(previewObject, canPlace, supportValue);
        return canPlace;
    }

    public bool UpdatePreviewHighlight(bool canPlace, float supportValue)
    {
        if (previewObject == null || highlightedObject == null)
        {
            return false;
        }

        ApplyPreviewHighlight(highlightedObject, canPlace, supportValue);
        return canPlace;
    }

    public void SetHighlightTarget(GameObject targetRoot)
    {
        if (targetRoot == null)
        {
            ResetHighlitedObject();
            return;
        }

        highlightedObject = targetRoot;
    }

    public void ChangeHighlightLayer(GameObject target, int targetLayer)
    {
        previewHighlight.Apply(target, targetLayer);
    }

    public void RestoreOrigLayer(GameObject target)
    {
        previewHighlight.RestoreIfTarget(target);
        removalHighlight.RestoreIfTarget(target);
    }

    public void RemoveCandidateColorChange(GameObject target, Color color)
    {
        GameObject materialRoot = ResolveMaterialRoot(target);
        if (materialRoot == null || !TryGetMaterial(materialRoot, out IMaterial material))
        {
            return;
        }

        GameObject resolvedTarget = material.GetGameObject();
        if (resolvedTarget == removeCandidate)
        {
            return;
        }

        removalHighlight.Restore();
        removalHighlight.Apply(resolvedTarget, layerBlue);
        removeCandidate = resolvedTarget;
    }

    public void ResetRemoveCandidate()
    {
        removalHighlight.Restore();
        removeCandidate = null;
    }

    public void ChangeColor(GameObject target, Color newColor)
    {
        if (!TryGetMaterial(target, out IMaterial material) || material.MaterialRenderers == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        for (int i = 0; i < material.MaterialRenderers.Count; i++)
        {
            Renderer renderer = material.MaterialRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", newColor);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    public void RestoreOrigColor(GameObject target)
    {
        if (!TryGetMaterial(target, out IMaterial material) || material.MaterialRenderers == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        for (int i = 0; i < material.MaterialRenderers.Count; i++)
        {
            Renderer renderer = material.MaterialRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            propertyBlock.Clear();
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    public void ResetRemoveObject()
    {
        ResetRemoveCandidate();
    }

    public void ResetHighlitedObject()
    {
        previewHighlight.Restore();
        highlightedObject = null;
    }

    private void ApplyPreviewHighlight(GameObject target, bool canPlace, float supportValue)
    {
        int layer = !canPlace
            ? layerRed
            : supportValue > 0.4f ? layerGreen : layerYellow;
        previewHighlight.Apply(target, layer);
    }

    private void ResolveDependencies()
    {
        if (buildingMaterialManagement == null)
        {
            buildingMaterialManagement = GetComponent<BuildingMaterialManagement>();
        }

        if (structuralIntegritySolver == null)
        {
            structuralIntegritySolver = GetComponent<StructuralIntegritySolver>();
        }

        if (partialNavMeshBuilder == null)
        {
            partialNavMeshBuilder = GetComponent<PartialNavMeshBuilder>();
        }

        if (snapController == null)
        {
            snapController = GetComponent<SnapController>();
        }
    }

    private static bool IsRemovableLayer(int layer)
    {
        return layer == LayerAndTagConstants.Layer_Building ||
               layer == LayerAndTagConstants.Layer_Door;
    }

    private static GameObject ResolveMaterialRoot(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        if (BuildingColliderUtility.TryResolveMaterialRoot(
                target,
                out GameObject materialRoot,
                out _))
        {
            return materialRoot;
        }

        return target;
    }

    private static bool TryGetMaterial(GameObject target, out IMaterial material)
    {
        material = null;
        if (target == null)
        {
            return false;
        }

        if (target.TryGetComponent(out material))
        {
            return material != null;
        }

        material = target.GetComponentInParent<IMaterial>();
        return material != null;
    }

    private sealed class LayerHighlightState
    {
        private readonly Dictionary<Renderer, int> originalLayers = new Dictionary<Renderer, int>();
        private GameObject target;

        public void Apply(GameObject nextTarget, int layer)
        {
            if (nextTarget == null || !TryGetMaterial(nextTarget, out IMaterial material) ||
                material.MaterialRenderers == null)
            {
                Restore();
                return;
            }

            if (target != nextTarget)
            {
                Restore();
                target = nextTarget;
            }

            for (int i = 0; i < material.MaterialRenderers.Count; i++)
            {
                Renderer renderer = material.MaterialRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!originalLayers.ContainsKey(renderer))
                {
                    originalLayers.Add(renderer, renderer.gameObject.layer);
                }

                renderer.gameObject.layer = layer;
            }
        }

        public void RestoreIfTarget(GameObject candidate)
        {
            if (candidate == null || candidate == target)
            {
                Restore();
            }
        }

        public void Restore()
        {
            foreach (KeyValuePair<Renderer, int> pair in originalLayers)
            {
                if (pair.Key != null)
                {
                    pair.Key.gameObject.layer = pair.Value;
                }
            }

            originalLayers.Clear();
            target = null;
        }
    }
}
