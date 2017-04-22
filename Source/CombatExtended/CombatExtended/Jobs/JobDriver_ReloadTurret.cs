using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CombatExtended
{
    public class JobDriver_ReloadTurret : JobDriver
    {
        private Building_TurretGunCE _turret;
        private Building_TurretGunCE turret
        {
            get
            {
                if (_turret == null)
                    _turret = TargetThingA as Building_TurretGunCE;
                return _turret;
            }
        }

        private AmmoThing _ammo;
        private AmmoThing ammo
        {
            get
            {
                if (_ammo == null)
                    _ammo = TargetThingB as AmmoThing;
                return _ammo;
            }
        }

        private CompAmmoUser _compReloader;
        private CompAmmoUser compReloader
        {
            get
            {
                if (_compReloader == null)
                    _compReloader = turret.compAmmo; // assumes turret has been error checked already...
                return _compReloader;
            }
        }

        private string errorBase { get { return this.GetType().Assembly.GetName().Name + " :: " + this.GetType().Name + " :: "; } }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // order of these errors matters some...
            if (turret == null)
            {
                Log.Error(string.Concat(errorBase, "TargetThingA isn't a Building_TurretGunCE"));
                yield return null;
            }
           	if (compReloader == null)
            {
                Log.Error(string.Concat(errorBase, "TargetThingA (Building_TurretGunCE) is missing it's CompAmmoUser."));
                yield return null;
            }
            if (ammo == null)
            {
                Log.Error(string.Concat(errorBase, "TargetThingB is either null or not an AmmoThing."));
                yield return null;
            }

            if (pawn.Faction != Faction.OfPlayer)
                this.FailOnDestroyedOrNull(TargetIndex.A);
            else
                this.FailOnDestroyedNullOrForbidden(TargetIndex.A);

            // get the turret's interaction cell and set it to TargetIndex.C
            
            turret.isReloading = true;

            if (compReloader.useAmmo)
            {
                if (pawn.Faction != Faction.OfPlayer)
                {
                    ammo.SetForbidden(false, false);
                    this.FailOnDestroyedOrNull(TargetIndex.B);
                }
                else
                {
                    this.FailOnDestroyedNullOrForbidden(TargetIndex.B);
                }

                // Haul ammo
                yield return Toils_Reserve.Reserve(TargetIndex.A, 1);
                yield return Toils_Reserve.Reserve(TargetIndex.B, 1);
                yield return Toils_Goto.GotoCell(ammo.Position, PathEndMode.ClosestTouch);
                yield return Toils_Haul.StartCarryThing(TargetIndex.B);
                yield return Toils_Goto.GotoCell(turret.Position, PathEndMode.ClosestTouch);
                yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.A, null, false);
            }

            // Wait in place
            Toil waitToil = new Toil() { actor = pawn };
            waitToil.initAction = delegate
            {
                waitToil.actor.pather.StopDead();
                if (compReloader.Props.throwMote)
                    MoteMaker.ThrowText(turret.Position.ToVector3Shifted(), Find.VisibleMap, "CE_ReloadingMote".Translate());
                compReloader.TryUnload();
            };
            waitToil.defaultCompleteMode = ToilCompleteMode.Delay;
            waitToil.defaultDuration = Mathf.CeilToInt(compReloader.Props.reloadTicks / pawn.GetStatValue(CE_StatDefOf.ReloadSpeed));
            yield return waitToil.WithProgressBarToilDelay(TargetIndex.A);

            //Actual reloader
            Toil reloadToil = new Toil();
            reloadToil.defaultCompleteMode = ToilCompleteMode.Instant;
            reloadToil.initAction = delegate
            {
                compReloader.LoadAmmo(ammo);
            };
            if (compReloader.useAmmo) reloadToil.EndOnDespawnedOrNull(TargetIndex.B);
            yield return reloadToil;
        }
    }
}
