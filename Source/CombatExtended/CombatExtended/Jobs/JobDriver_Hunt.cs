﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;

namespace CombatExtended
{
	/// <summary>
	/// Description of JobDriver_Hunt.
	/// </summary>
	public class JobDriver_Hunt : JobDriver
	{
		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOn(delegate
			{
				if (!CurJob.ignoreDesignations)
				{
					Pawn victim = Victim;
					if (victim != null && !victim.Dead && Map.designationManager.DesignationOn(victim, DesignationDefOf.Hunt) == null)
					{
						return true;
					}
				}
				return false;
			});
			
			yield return Toils_Reserve.Reserve(VictimInd, 1);
			
			var init = new Toil();
			init.initAction = delegate
			{
				jobStartTick = Find.TickManager.TicksGame;
			};
			
			yield return init;
			
			yield return Toils_Combat.TrySetJobToUseAttackVerb();
			
			var comp = pawn.equipment.Primary.TryGetComp<CompAmmoUser>();
			var startCollectCorpse = StartCollectCorpseToil();
			var gotoCastPos = GotoCastPosition(VictimInd, true).JumpIfDespawnedOrNull(VictimInd, startCollectCorpse).FailOn(() => Find.TickManager.TicksGame > jobStartTick + MaxHuntTicks);

			yield return gotoCastPos;
			
			var moveIfCannotHit = Toils_Jump.JumpIfTargetNotHittable(VictimInd, gotoCastPos);
			
			yield return moveIfCannotHit;
			
			yield return Toils_Jump.JumpIfTargetDownedDistant(VictimInd, gotoCastPos);
			
			yield return Toils_Jump.JumpIfTargetDespawnedOrNull(VictimInd, startCollectCorpse);
			
			yield return Toils_Combat.CastVerb(VictimInd, false).JumpIfDespawnedOrNull(VictimInd, startCollectCorpse)
				.FailOn(() => {
				        if (Find.TickManager.TicksGame <= jobStartTick + MaxHuntTicks)
				        {
			                if (comp == null 
			                    || !comp.useAmmo 
			                    || (comp.hasMagazine && comp.curMagCount > 0) 
			                    || comp.hasAmmo)
			                {
			                	return false;
			                }
				        }
				        return true;
				        });
			
			yield return Toils_Jump.Jump(moveIfCannotHit);
			
			yield return startCollectCorpse;
			
			yield return Toils_Goto.GotoCell(VictimInd, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(VictimInd).FailOnSomeonePhysicallyInteracting(VictimInd);
			
			yield return Toils_Haul.StartCarryThing(VictimInd);
			
			var carryToCell = Toils_Haul.CarryHauledThingToCell(StoreCellInd);
			
			yield return carryToCell;
			
			yield return Toils_Haul.PlaceHauledThingInCell(StoreCellInd, carryToCell, true);
		}
		
		const TargetIndex VictimInd = TargetIndex.A;

		const TargetIndex StoreCellInd = TargetIndex.B;

		const int MaxHuntTicks = 5000;

		int jobStartTick = -1;

		public Pawn Victim
		{
			get
			{
				Corpse corpse = Corpse;
				return corpse != null ? corpse.InnerPawn : (Pawn)CurJob.GetTarget(TargetIndex.A).Thing;
			}
		}

		Corpse Corpse
		{
			get
			{
				return CurJob.GetTarget(TargetIndex.A).Thing as Corpse;
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.LookValue<int>(ref jobStartTick, "jobStartTick", 0);
		}

		public override string GetReport()
		{
			return CurJob.def.reportString.Replace("TargetA", Victim.LabelShort);
		}

		//Copy of Verse.AI.Toils_CombatGotoCastPosition
		Toil GotoCastPosition(TargetIndex targetInd, bool closeIfDowned = false)
		{
			var toil = new Toil();
			toil.initAction = delegate
			{
				Pawn actor = toil.actor;
				Job curJob = actor.CurJob;
				Thing thing = curJob.GetTarget(targetInd).Thing;
				var pawnVictim = thing as Pawn;
				IntVec3 intVec;
				if (!CastPositionFinder.TryFindCastPosition(new CastPositionRequest
				{
					caster = toil.actor,
					target = thing,
					verb = curJob.verbToUse,
					maxRangeFromTarget = ((closeIfDowned && pawnVictim != null && pawnVictim.Downed)
					                      ? Mathf.Min(curJob.verbToUse.verbProps.range, (float)pawnVictim.RaceProps.executionRange)
					                      	//The following line is changed
					                      	: HuntRangePerBodysize(pawnVictim.RaceProps.baseBodySize, (float)pawnVictim.RaceProps.executionRange, curJob.verbToUse.verbProps.range)),
					wantCoverFromTarget = false
				}, out intVec))
				{
					toil.actor.jobs.EndCurrentJob(JobCondition.Incompletable, true);
					return;
				}
				toil.actor.pather.StartPath(intVec, PathEndMode.OnCell);
				actor.Map.pawnDestinationManager.ReserveDestinationFor(actor, intVec);
			};
			toil.FailOnDespawnedOrNull(targetInd);
			toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
			return toil;
		}
		
		//Fit for an attack range per body size curve.
		public static float HuntRangePerBodysize(float x, float executionRange, float gunRange)
		{
			return Mathf.Min(Mathf.Clamp(1 + 20 * (1 - Mathf.Exp(-0.65f * x)), executionRange, 20), gunRange);
		}
		
		Toil StartCollectCorpseToil()
		{
			var toil = new Toil();
			toil.initAction = delegate
			{
				if (Victim == null)
				{
					toil.actor.jobs.EndCurrentJob(JobCondition.Incompletable);
					return;
				}
				TaleRecorder.RecordTale(TaleDefOf.Hunted, new object[]
				{
					pawn,
					Victim
				});
				Corpse corpse = Victim.Corpse;
				if (corpse == null || !this.pawn.CanReserveAndReach(corpse, PathEndMode.ClosestTouch, Danger.Deadly, 1))
				{
					pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
					return;
				}
				corpse.SetForbidden(false, true);
				IntVec3 c;
				if (StoreUtility.TryFindBestBetterStoreCellFor(corpse, pawn, Map, StoragePriority.Unstored, pawn.Faction, out c))
				{
					pawn.Reserve(corpse, 1);
					pawn.Reserve(c, 1);
					pawn.CurJob.SetTarget(TargetIndex.B, c);
					pawn.CurJob.SetTarget(TargetIndex.A, corpse);
					pawn.CurJob.count = 1;
					pawn.CurJob.haulMode = HaulMode.ToCellStorage;
					return;
				}
				pawn.jobs.EndCurrentJob(JobCondition.Succeeded, true);
			};
			return toil;
		}
	}
}
