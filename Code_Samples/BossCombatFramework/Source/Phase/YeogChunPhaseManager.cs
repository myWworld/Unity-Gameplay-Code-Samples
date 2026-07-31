using MalbersAnimations;

public class YeogChunPhaseManager : PhaseManager
{

    public ModeID actionID;
    public int fakeDeathID;
    protected override void OnPhase2Transition()
    {

    }

    protected override void phase1HpEmptyEvent()
    {
        phase2TransitionDelay = 10f;
        base.phase1HpEmptyEvent();

        bTRunner.AbortTree();
        bossAnimEventBridge.AttackCleanUp();

        if (mAnimal != null)
        {
            mAnimal.Mode_Interrupt_Forced();
            mAnimal.Mode_Stop(true);
            mAnimal.State_Force(StateEnum.FakeDeath);
        }
    }



}
