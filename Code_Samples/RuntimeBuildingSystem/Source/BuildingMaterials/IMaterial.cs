using System.Collections.Generic;
using UnityEngine;

public enum MaterialType
{
    Sand,
    Wood,
    Stone,
    Metal,
    Fire,
    End,
}

public enum eBuildingMaterial
{

    Floor2by2,
    Wall1by1,
    Wall1by2,
    Wall2by1,
    Wall2by2,
    WallTri2by2,
    WallTri2by1,
    WallTri1by2,
    WallTriReverse2by2,
    WallTriReverse2by1,
    WallTriReverse1by2,
    DoorFrame,
    Door,
    Stair25,
    Stair45,


    HalfPole,
    HalfPole25,
    HalfPole45,
    HalfPole65,
    HalfPole90,
    Pole,
    Pole25,
    Pole45,
    Pole65,
    Pole90,

    RoofCover25,
    RoofCover45,
    RoofCorner25,
    RoofCorner45,

    RoofTopCover1,
    RoofTopCover2,

    Torch,

    BaseRockBig,
    BaseRockSmall,
    BaseSandFloorBig,
    BaseSandFloorSmall,
    DoorFrameRight,
    DoorFrameLeft,

    Window1by1,
    Floor1by1,
    RoofCornerRev25,
    WorkTable,
    Furnace,
    Agungi,
    Bed,
    HonAltar,
    Boat,
    End,
}



public interface IMaterial//자재들의 공통적인 특성을 관리하는 인터페이스
{
    BuildingDataSO Data { get; }

    List<IMaterial> Parents { get; }
    List<IMaterial> ConnectedChildren { get; }

    float SupportValue { get; set; }
    float MaxSupportWeight { get; }

    bool bGrounded { get; set; }

    Dictionary<string, int> RequirementsForMat { get; }

    List<Renderer> MaterialRenderers { get; }

    MaterialType GetMaterialType();
    eBuildingMaterial GetBuildingMaterialType();

    GameObject GetGameObject();


    void SetParentPrefab(IMaterial parentPrefab);
    IMaterial GetParentPrefab();



    void SetPivot(GameObject gameObject);
    GameObject GetPivot();

    Vector3 GetOffsetBetweenObjAndAnchor();


    void UpdateOffset();


    List<GameObject> GetAnchors();


    GameObject GetAnchorByIndx(int idx);

    void ItemDrop();
    void ApplySpecialRotation(Transform materialTr, GameObject targetAnchor);

    Transform GetVisualMesh();
    void ResetVisualTransform();

    public Vector3 GetDefaultLocalPos();
    public Quaternion GetDefaultLocalRot();


}
