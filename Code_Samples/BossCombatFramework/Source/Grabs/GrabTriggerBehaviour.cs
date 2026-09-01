using UnityEngine;
using System.Collections;

namespace MalbersAnimations.Controller
{
    public class GrabTriggerBehaviour : StateMachineBehaviour
    {
        [MinMaxRange(0, 1)]
        public RangedFloat grabWindow = new(0.3f, 0.5f); 

        private GrabManager grabManager;
        public int[] grabColliderIndices = { 0 };
        private bool isOn, isOff;

        override public void OnStateEnter(Animator anim, AnimatorStateInfo stateInfo, int layerIndex)
        {
            isOn = isOff = false;
   
            if (grabManager == null) grabManager = anim.GetComponent<GrabManager>();
        }

        override public void OnStateUpdate(Animator anim, AnimatorStateInfo state, int layer)
        {
            var time = state.normalizedTime % 1;

         
            if (!isOn && (time >= grabWindow.minValue))
            {
                foreach (int index in grabColliderIndices)
                {
                    grabManager.SetGrabWindowActive(true, index);
                }

                isOn = true;
            }

            if (!isOff && (time >= grabWindow.maxValue))
            {
                foreach (int index in grabColliderIndices)
                {
                    grabManager.SetGrabWindowActive(false, index);
                }
                isOff = true;
            }
        }

        override public void OnStateExit(Animator anim, AnimatorStateInfo state, int layer)
        {
            if (!isOff && grabManager != null)
            {
                foreach (int index in grabColliderIndices)
                {
                    grabManager.SetGrabWindowActive(false, index);
                }
            }
            isOn = isOff = false;
        }
    }
}