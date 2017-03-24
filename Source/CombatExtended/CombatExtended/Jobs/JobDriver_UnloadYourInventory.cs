﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using RimWorld;
using Verse;
using Verse.AI;

namespace CombatExtended
{
	/// <summary>
	/// This class gets used when a Pawn returning from a Caravan is unloading it's own inventory.
	/// Class is mostly copied from Rimworld with adjustments to have it ask components of CombatExtended for what and how much to drop from the player's own invetory.
	/// </summary>
	public class JobDriver_UnloadYourInventory : JobDriver
	{
		private const TargetIndex ItemToHaulInd = TargetIndex.A;

		private const TargetIndex StoreCellInd = TargetIndex.B;

		private const int UnloadDuration = 10;
		
		private int amountToDrop;

		[DebuggerHidden]
		protected override IEnumerable<Toil> MakeNewToils()
		{
			yield return Toils_General.Wait(10);
			yield return new Toil
			{
				initAction = delegate
				{
					Thing dropThing;
					int dropCount;
					if (!this.pawn.inventory.UnloadEverything || !this.pawn.GetAnythingForDrop(out dropThing, out dropCount))
					{
						this.EndJobWith(JobCondition.Succeeded);
					}
					else
					{
						IntVec3 c;
						if (!StoreUtility.TryFindStoreCellNearColonyDesperate(dropThing, this.pawn, out c))
						{
							this.pawn.inventory.innerContainer.TryDrop(dropThing, this.pawn.Position, this.pawn.Map, ThingPlaceMode.Near, dropCount, out dropThing);
							this.EndJobWith(JobCondition.Succeeded);
						}
						else
						{
							this.CurJob.SetTarget(TargetIndex.A, dropThing);
							this.CurJob.SetTarget(TargetIndex.B, c);
							amountToDrop = dropCount;
						}
					}
				}
			};
			yield return Toils_Reserve.Reserve(TargetIndex.B, 1);
			yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.Touch);
			yield return new Toil
			{
				initAction = delegate
				{
					Thing thing = this.CurJob.GetTarget(TargetIndex.A).Thing;
					if (thing == null || !this.pawn.inventory.innerContainer.Contains(thing))
					{
						this.EndJobWith(JobCondition.Incompletable);
						return;
					}
					if (!this.pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || !thing.def.EverStoreable)
					{
						this.pawn.inventory.innerContainer.TryDrop(thing, this.pawn.Position, this.pawn.Map, ThingPlaceMode.Near, amountToDrop, out thing);
						this.EndJobWith(JobCondition.Succeeded);
					}
					else
					{
						this.pawn.inventory.innerContainer.TransferToContainer(thing, this.pawn.carryTracker.innerContainer, amountToDrop, out thing);
						this.CurJob.count = amountToDrop;
						this.CurJob.SetTarget(TargetIndex.A, thing);
					}
					thing.SetForbidden(false, false);
					
					if (!this.pawn.HasAnythingForDrop())
					{
						this.pawn.inventory.UnloadEverything = false;
					}
				}
			};
			Toil carryToCell = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
			yield return carryToCell;
			yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, carryToCell, true);
		}
	}
}
