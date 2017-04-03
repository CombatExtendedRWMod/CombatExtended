using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CombatExtended
{
    public class JobDriver_Reload : JobDriver
    {
        private CompAmmoUser _compReloader;
        private CompAmmoUser compReloader
        {
            get
            {
                if (_compReloader == null) _compReloader = TargetThingB.TryGetComp<CompAmmoUser>();
                return _compReloader;
            }
        }

        private bool HasNoGunOrAmmo()
        {
            if (TargetThingB.DestroyedOrNull() || pawn.equipment == null || pawn.equipment.Primary == null || pawn.equipment.Primary != TargetThingB)
                return true;

            CompAmmoUser comp = pawn.equipment.Primary.TryGetComp<CompAmmoUser>();
            //return comp != null && comp.useAmmo && !comp.hasAmmo;
            return comp != null && !comp.hasAndUsesAmmoOrMagazine;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (compReloader == null)
            {
                Log.Error(pawn + " tried to do reload job without compReloader");
                yield return null;
            }

            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnMentalState(TargetIndex.A);
            this.FailOn(HasNoGunOrAmmo);
            
            // moved from JobDriver_Reload...
            IntVec3 position;
            if (compReloader.wielder == null)
            {
                if (compReloader.turret == null)
                	throw new System.ArgumentException("JobDriver_Reload :: Both compReloader.wielder and compReloader.turret are null.  Either a Pawn held weapon or a Turret are required for this job.");
                compReloader.turret.isReloading = true;
                position = compReloader.turret.Position;
            }
            else
            {
                position = compReloader.wielder.Position;
            }
            
            // Throw mote
            if (compReloader.Props.throwMote)
            {
                MoteMaker.ThrowText(position.ToVector3Shifted(), Find.VisibleMap, "CE_ReloadingMote".Translate());
            }

            //Toil of do-nothing		
            Toil waitToil = new Toil();
            waitToil.initAction = () => waitToil.actor.pather.StopDead();
            waitToil.defaultCompleteMode = ToilCompleteMode.Delay;
            waitToil.defaultDuration = Mathf.CeilToInt(compReloader.Props.reloadTicks / pawn.GetStatValue(CE_StatDefOf.ReloadSpeed));
            yield return waitToil.WithProgressBarToilDelay(TargetIndex.A);

            //Actual reloader
            Toil reloadToil = new Toil();
            reloadToil.AddFinishAction(() => compReloader.LoadAmmo());
            yield return reloadToil;

            //Continue previous job if possible
            Toil continueToil = new Toil
            {
                initAction = () => compReloader.TryContinuePreviousJob(),
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return continueToil;
        }
    }
}