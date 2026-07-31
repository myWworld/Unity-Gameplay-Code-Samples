using UnityEngine;
using MalbersAnimations.Controller.AI;
using MalbersAnimations.Controller;
using MalbersAnimations;

public class BTRunner : MonoBehaviour
{
    [Header("Boss Malbers Component")]
    [SerializeField] private MAnimal mAnimal;
    [SerializeField] private MAnimalAIControl mAnimalAIControl;

    public Node rootNode;
    [SerializeField] private BlackBoard blackBoard;
    [SerializeField] private BTNodeData rootNodeData;


    void Awake()
    {
        if(blackBoard == null)
        {
            blackBoard = GetComponent<BlackBoard>();
        }

        if (rootNode == null)
        {
            if (rootNodeData != null)
                rootNode = rootNodeData.CreateNode(blackBoard);
        }

        if(mAnimal == null)
        {
            mAnimal = GetComponent<MAnimal>();
        }

        if(mAnimalAIControl == null)
        {
            mAnimalAIControl = GetComponentInChildren<MAnimalAIControl>();
        }
    }

    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {


        if (rootNode != null)
        {


                rootNode.Evaluate();

        }
    }

    public void AbortTree()
    {
        if (rootNode != null)
        {
            rootNode.Stop();
        }
    }

    public MAnimal GetMAnimal() => mAnimal;

    public MAnimalAIControl GetMAnimalAIControl() => mAnimalAIControl;


    //#if UNITY_EDITOR
    //    private void OnDrawGizmos()
    //    {
    //        if (rootNode == null || blackBoard == null) return;
    //
    //        // 보스 머리 위에 현재 상태 표시
    //        string debugText = $"AI State: {rootNode.State}";
    //        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.5f, debugText);
    //
    //        // 사거리 시각화
    //        Gizmos.color = rootNode.State == NodeState.RUNNING ? Color.red : Color.green;
    //        Gizmos.DrawWireSphere(transform.position, 5f); // 5f는 예시, 실제 데이터 연동 가능
    //    }
    //#endif



}
