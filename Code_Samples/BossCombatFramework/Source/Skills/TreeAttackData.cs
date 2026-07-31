using UnityEngine;
[System.Serializable]
public class TreeAttackData
{
    public GameObject rootObj;
    public Transform transform;
    public AttackTrigger trigger;

    private Vector3 initialScale;
    private Vector3 randomRotate = new Vector3(0, 1f, 0);


    public void Init(GameObject prefab, Vector3 pos)
    {

        rootObj = GameObject.Instantiate(prefab, pos, Quaternion.identity);
        initialScale = rootObj.transform.localScale;
        transform = rootObj.transform;
        rootObj.TryGetComponent<AttackTrigger>(out trigger);
        rootObj.SetActive(false);

    }

    public void DeActivate()
    {
        if (rootObj != null)
        {
            rootObj.SetActive(false);

            if (trigger != null)
                trigger.HasDealtDamage = false;
        }
    }

    public void ActivateUnderGround(Vector3 spawnPos, float offset)
    {
        if (rootObj != null)
        {
            transform.localScale = initialScale;
            spawnPos.y -= offset;
            transform.position = spawnPos;
            transform.localRotation = Quaternion.Euler(randomRotate * Random.Range(0f, 360f));

            rootObj.SetActive(true);
        }
    }

    public void Activate(Vector3 spawnPos)
    {
        if (rootObj != null)
        {
            transform.localScale = initialScale;
            transform.position = spawnPos;
            rootObj.SetActive(true);
        }
    }

}
