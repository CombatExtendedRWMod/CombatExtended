using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;

namespace CombatExtended
{
    /* Regarding multi reservation...
     * As long as it's all the same job reserving the animal (sourceInd) it works out fine.
     * Need to check if the target still has the desired thing because if a pawn can take the entire stack then the thing object's container is changed rather than destroyed.
     * It's when a pawn takes a partial stack that new thing objects are created and the difference is deducted from the previously existing thing's stack count.
     */
	public class JobDriver_TakeFromOther : JobDriver
	{
		private TargetIndex thingInd = TargetIndex.A;
		private TargetIndex sourceInd = TargetIndex.B;
		private TargetIndex flagInd = TargetIndex.C;
		
		private Thing targetItem
		{
			get {
				return CurJob.GetTarget(thingInd).Thing;
			}
		}
		private Pawn takePawn
		{
			get {
				return (Pawn)CurJob.GetTarget(sourceInd).Thing;
			}
		}
		private bool doEquip
		{
			get
			{
				return CurJob.GetTarget(flagInd).HasThing;
			}
		}

		public override string GetReport()
		{
			string text = CE_JobDefOf.TakeFromOther.reportString;
			text = text.Replace("FlagC", doEquip ? "CE_TakeFromOther_Equipping".Translate() : "CE_TakeFromOther_Taking".Translate());
			text = text.Replace("TargetA", targetItem.Label);
			text = text.Replace("TargetB", takePawn.LabelShort);
			return text;
		}

        private bool DeadTakePawn()
        {
            return takePawn.Dead;
        }
		
		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedNullOrForbidden(sourceInd);
            this.FailOnDestroyedNullOrForbidden(thingInd);
            this.FailOn(DeadTakePawn);
            // We could set a slightly more sane value here which would prevent a hoard of pawns moving from pack animal to pack animal...
            // Also can enforce limits via JobGiver keeping track of how many things it's given away from each pawn, it's a small case though...
			yield return Toils_Reserve.Reserve(sourceInd, int.MaxValue, 0, null);
			yield return Toils_Goto.GotoThing(sourceInd, PathEndMode.Touch);
			yield return Toils_General.Wait(10);
			yield return new Toil {
				initAction = delegate
				{
                    // if the targetItem is no longer in the takePawn's inventory then another pawn already took it and we fail...
                    if (takePawn.inventory.innerContainer.Contains(targetItem))
                    {
                        int amount = targetItem.stackCount < CurJob.count ? targetItem.stackCount : CurJob.count;
                        takePawn.inventory.innerContainer.TryTransferToContainer(targetItem, pawn.inventory.innerContainer, amount);
                        if (doEquip)
                        {
                            CompInventory compInventory = pawn.TryGetComp<CompInventory>();
                            if (compInventory != null)
                                compInventory.TrySwitchToWeapon((ThingWithComps)targetItem);
                        }
                    } else
                    {
                        this.EndJobWith(JobCondition.Incompletable);
                    }
				}
			};
            yield return Toils_Reserve.Release(sourceInd);
		}
	}
}