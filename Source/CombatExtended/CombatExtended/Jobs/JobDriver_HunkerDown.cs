using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace CombatExtended
{
    using CombatExtended.DefOfs;

    class JobDriver_HunkerDown : JobDriver
    {
        private const int getUpCheckInterval = 60;

        private bool willPee = false;


        public override PawnPosture Posture
        {
            get
            {
                return PawnPosture.LayingAny;
            }
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);

            //Define Toil
            Toil toilWait = new Toil();
            toilWait.initAction = () =>
            {
                toilWait.actor.pather.StopDead();
            };

            Toil toilNothing = new Toil();
            //toilNothing.initAction = () => {};
            toilNothing.defaultCompleteMode = ToilCompleteMode.Delay;
            toilNothing.defaultDuration = getUpCheckInterval;

            toilNothing.AddPreInitAction(
                delegate { this.willPee = Rand.Value > 0.8f; });

            toilNothing.tickAction = delegate
                {
                    if (this.willPee)
                    {

                            FilthMaker.MakeFilth(pawn.Position, this.pawn.Map, CE_ThingDefOf.FilthPee, pawn.LabelIndefinite(), 3);
                        this.willPee = true;
                    }
                };
            toilNothing.AddFinishAction(
                delegate
                    {
                        if (this.willPee)
                        {
                            TaleRecorder.RecordTale(CE_TaleDefOf.WetHimself, pawn);
                        }
                    });


            // Start Toil
            yield return toilWait;
            yield return toilNothing;
            yield return Toils_Jump.JumpIf(toilNothing, () =>
            {
                CompSuppressable comp = pawn.TryGetComp<CompSuppressable>();
                if (comp == null)
                {
                    return false;
                }
                if (!comp.CanReactToSuppression)
                {
                    return false;
                }
                return comp.IsHunkering;
            });
        }
    }
}
