﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace CombatExtended
{
    public class CompAmmoUser : CompRangedGizmoGiver
    {
        #region Fields

        private int curMagCountInt;
        private LocalTargetInfo storedTarget = null;
        private JobDef storedJobDef = null;
        private AmmoDef currentAmmoInt = null;
        public AmmoDef selectedAmmo;
        
        private Thing ammoToBeDeleted;

        public Building_TurretGunCE turret;         // Cross-linked from CE turret

        #endregion

        #region Properties

        public CompProperties_AmmoUser Props
        {
            get
            {
                return (CompProperties_AmmoUser)props;
            }
        }

        public int curMagCount
        {
            get
            {
                return curMagCountInt;
            }
        }
        public CompEquippable compEquippable
        {
            get { return parent.GetComp<CompEquippable>(); }
        }
        public Pawn wielder
        {
            get
            {
                if (compEquippable == null || compEquippable.PrimaryVerb == null)
                {
                    return null;
                }
                return compEquippable.PrimaryVerb.CasterPawn;
            }
        }
		public Pawn holder
		{
			get
			{
				return wielder ?? (compEquippable.parent.holdingContainer?.owner as Pawn_InventoryTracker)?.pawn;
			}
		}
        public bool useAmmo
        {
            get
            {
                return ModSettings.enableAmmoSystem && Props.ammoSet != null;
            }
        }
        public bool hasAndUsesAmmoOrMagazine
        {
        	get
        	{
        		return !useAmmo || hasAmmoOrMagazine;
        	}
        }
        public bool hasAmmoOrMagazine
        {
        	get
        	{
        		return (hasMagazine && curMagCount > 0) || hasAmmo;
        	}
        }
        public bool canBeFiredNow
        {
        	get
        	{
        		return !useAmmo || ((hasMagazine && curMagCount > 0) || (!hasMagazine && hasAmmo));
        	}
        }
        public bool hasAmmo
        {
            get
            {
				return compInventory != null && compInventory.ammoList.Any(x => Props.ammoSet.ammoTypes.Any(a => a.ammo == x.def));
            }
        }
        public bool hasMagazine { get { return Props.magazineSize > 0; } }
        public AmmoDef currentAmmo
        {
            get
            {
                return useAmmo ? currentAmmoInt : null;
            }
        }
        public ThingDef CurAmmoProjectile => Props.ammoSet?.ammoTypes?.FirstOrDefault(x => x.ammo == currentAmmo).projectile;
        public CompInventory compInventory
        {
            get
            {
                return holder.TryGetComp<CompInventory>();
            }
        }
        private IntVec3 position
        {
            get
            {
                if (wielder != null) return wielder.Position;
                else if (turret != null) return turret.Position;
                else if (holder != null) return holder.Position;
                else return parent.Position;
            }
        }

        #endregion Properties

        #region Methods

        public override void Initialize(CompProperties vprops)
        {
            base.Initialize(vprops);

            curMagCountInt = Props.spawnUnloaded && useAmmo ? 0 : Props.magazineSize;

            // Initialize ammo with default if none is set
            if (useAmmo)
            {
                if (Props.ammoSet.ammoTypes.NullOrEmpty())
                {
                    Log.Error(parent.Label + " has no available ammo types");
                }
                else
                {
                    if (currentAmmoInt == null)
                        currentAmmoInt = (AmmoDef)Props.ammoSet.ammoTypes[0].ammo;
                    if (selectedAmmo == null)
                        selectedAmmo = currentAmmoInt;
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.LookValue(ref curMagCountInt, "count", 0);
            Scribe_Defs.LookDef(ref currentAmmoInt, "currentAmmo");
            Scribe_Defs.LookDef(ref selectedAmmo, "selectedAmmo");
        }

        private void AssignJobToWielder(Job job)
        {
            if (wielder.drafter != null)
            {
                wielder.jobs.TryTakeOrderedJob(job);
            }
            else
            {
                ExternalPawnDrafter.TakeOrderedJob(wielder, job);
            }
        }

        public bool Notify_ShotFired()
        {
        	if (ammoToBeDeleted != null)
        	{
        		ammoToBeDeleted.Destroy();
        		ammoToBeDeleted = null;
                compInventory.UpdateInventory();
	            if (!hasAmmoOrMagazine)
	            {
	            	return false;
	            }
        	}
            return true;
        }
        
        public bool Notify_PostShotFired()
        {
            if (!hasAmmoOrMagazine)
            {
                DoOutOfAmmoAction();
                return false;
            }
            return true;
        }
        
        /// <summary>
        /// Reduces ammo count and updates inventory if necessary, call this whenever ammo is consumed by the gun (e.g. firing a shot, clearing a jam)
        /// </summary>
        public bool TryReduceAmmoCount()
        {
            if (wielder == null && turret == null)
            {
                Log.Error(parent.ToString() + " tried reducing its ammo count without a wielder");
            }

            // Mag-less weapons feed directly from inventory
            if (!hasMagazine)
            {
                if (useAmmo)
                {
                    if (!TryFindAmmoInInventory(out ammoToBeDeleted))
                    {
                        return false;
                    }
					
                    if (ammoToBeDeleted.stackCount > 1)
                        ammoToBeDeleted = ammoToBeDeleted.SplitOff(1);
                }
                return true;
            }
            // If magazine is empty, return false
            if (curMagCountInt <= 0)
            {
                curMagCountInt = 0;
                return false;
            }
            // Reduce ammo count and update inventory
            curMagCountInt--;
            if (compInventory != null)
            {
                compInventory.UpdateInventory();
            }
            if (curMagCountInt < 0) TryStartReload();
            return true;
        }
		
        // really only used by pawns (JobDriver_Reload) at this point... TODO: Finish making sure this is only used by pawns and fix up the error checking.
        /// <summary>
        /// Overrides a Pawn's current activities to start reloading a gun or turret.  Has a code path to resume the interrupted job.
        /// </summary>
        public void TryStartReload()
        {
            if (!hasMagazine)
            {
                return;
            }
            if (wielder == null && turret == null)
            	return;

            if (useAmmo)
            {
            	TryUnload();
            	
                // Check for ammo
                if (wielder != null && !hasAmmo)
                {
                    DoOutOfAmmoAction();
                    return;
                }
            }

            // Issue reload job
            if (wielder != null)
            {
            	Job reloadJob = TryMakeReloadJob();
            	if (reloadJob == null)
            		return;
            	reloadJob.playerForced = true;

                // Store the current job so we can reassign it later
                if (wielder.Faction == Faction.OfPlayer
                    && wielder.CurJob != null
                       && (wielder.CurJob.def == JobDefOf.AttackStatic || wielder.CurJob.def == JobDefOf.Goto || wielder.CurJob.def == JobDefOf.Hunt))
                {
                    if (wielder.CurJob.targetA.HasThing) storedTarget = new LocalTargetInfo(wielder.CurJob.targetA.Thing);
                    else storedTarget = new LocalTargetInfo(wielder.CurJob.targetA.Cell);
                    storedJobDef = wielder.CurJob.def;
                }
                AssignJobToWielder(reloadJob);
            }
        }
        
        // used by both turrets (JobDriver_ReloadTurret) and pawns (JobDriver_Reload).
        /// <summary>
        /// Used to unload the weapon.  Ammo will be dumped to the unloading Pawn's inventory or the ground if insufficient space.  Any ammo that can't be dropped
        /// on the ground is destroyed (with a warning).
        /// </summary>
        /// <returns>bool, true indicates the weapon was already in an unloaded state or the unload was successful.  False indicates an error state.</returns>
        /// <remarks>
        /// Failure to unload occurs if the weapon doesn't use a magazine.
        /// </remarks>
        public bool TryUnload()
        {
        	if (!hasMagazine || (holder == null && turret == null))
        		return false; // nothing to do as we are in a bad state;
        	
        	if (!useAmmo || curMagCountInt == 0)
        		return true; // nothing to do but we aren't in a bad state either.  Claim success.
        		
            // Add remaining ammo back to inventory
            Thing ammoThing = ThingMaker.MakeThing(currentAmmoInt);
            ammoThing.stackCount = curMagCountInt;
            bool doDrop = false;

            if (compInventory != null)
            	doDrop = (curMagCountInt != compInventory.container.TryAdd(ammoThing, ammoThing.stackCount)); // TryAdd should report how many ammoThing.stackCount it stored.
            else
            	doDrop = true; // Inventory was null so no place to shift the ammo besides the ground.
            
            if (doDrop)
            {
            	// NOTE: If we get here from ThingContainer.TryAdd() it will have modified the ammoThing.stackCount to what it couldn't take.
                Thing outThing;
                if (!GenThing.TryDropAndSetForbidden(ammoThing, position, Find.VisibleMap, ThingPlaceMode.Near, out outThing, turret.Faction != Faction.OfPlayer))
                {
                	Log.Warning(String.Concat(this.GetType().Assembly.GetName().Name + " :: " + this.GetType().Name + " :: ",
                	                         "Unable to drop ", ammoThing.LabelCap, " on the ground, thing was destroyed."));
                }
            }
            
            // don't forget to set the clip to empty...
            curMagCountInt = 0;
            
            return true;
        }
        
        /// <summary>
        /// Used to fetch a reload job for the weapon this comp is on.  Sets storedInfo to null (as if no job being replaced).
        /// </summary>
        /// <returns>Job using JobDriver_Reload</returns>
        /// <remarks>TryUnload() should be called before this in most cases.</remarks>
        public Job TryMakeReloadJob()
        {
        	if (!hasMagazine || (holder == null && turret == null))
        		return null; // the job couldn't be created.
        	
            // blank the stored job details so we don't try to start another job after reloading.  Up to caller to set these after getting the reload job from us.
        	storedTarget = null;
            storedJobDef = null;
            
            return new Job(CE_JobDefOf.ReloadWeapon, holder, parent);
        }
        
        private void DoOutOfAmmoAction()
        {
            if (Props.throwMote)
            {
                MoteMaker.ThrowText(position.ToVector3Shifted(), Find.VisibleMap, "CE_OutOfAmmo".Translate() + "!");
            }
            if (wielder != null && compInventory != null && (wielder.CurJob == null || wielder.CurJob.def != JobDefOf.Hunt)) compInventory.SwitchToNextViableWeapon();
        }

        public void LoadAmmo(Thing ammo = null)
        {
            if (holder == null && turret == null)
			{
                Log.Error(parent.ToString() + " tried loading ammo with no owner");
                return;
            }

            int newMagCount;
            if (useAmmo)
            {
                Thing ammoThing;
                bool ammoFromInventory = false;
                if (ammo == null)
                {
                    if (!TryFindAmmoInInventory(out ammoThing))
                    {
                        DoOutOfAmmoAction();
                        return;
                    }
                    ammoFromInventory = true;
                }
                else
                {
                    ammoThing = ammo;
                }
                currentAmmoInt = (AmmoDef)ammoThing.def;
                if (Props.magazineSize < ammoThing.stackCount)
                {
                    newMagCount = Props.magazineSize;
                    ammoThing.stackCount -= Props.magazineSize;
                    if (compInventory != null) compInventory.UpdateInventory();
                }
                else
                {
                    newMagCount = ammoThing.stackCount;
                    if (ammoFromInventory)
                    {
                        compInventory.container.Remove(ammoThing);
                    }
                    else if (!ammoThing.Destroyed)
                    {
                        ammoThing.Destroy();
                    }
                }
            }
            else
            {
                newMagCount = Props.magazineSize;
            }
            curMagCountInt = newMagCount;
            if (turret != null) turret.isReloading = false;
            if (parent.def.soundInteract != null) parent.def.soundInteract.PlayOneShot(new TargetInfo(position,  Find.VisibleMap, false));
        }

        private bool TryFindAmmoInInventory(out Thing ammoThing)
        {
            ammoThing = null;
            if (compInventory == null)
            {
                return false;
            }

            // Try finding suitable ammoThing for currently set ammo first
            ammoThing = compInventory.ammoList.Find(thing => thing.def == selectedAmmo);
            if (ammoThing != null)
            {
                return true;
            }

            // Try finding ammo from different type
            foreach (AmmoLink link in Props.ammoSet.ammoTypes)
            {
                ammoThing = compInventory.ammoList.Find(thing => thing.def == link.ammo);
                if (ammoThing != null)
                {
                    selectedAmmo = (AmmoDef)link.ammo;
                    return true;
                }
            }
            return false;
        }

        public void TryContinuePreviousJob()
        {
            //If a job is stored, assign it
            if (storedTarget != null && storedJobDef != null)
            {
                AssignJobToWielder(new Job(storedJobDef, storedTarget));

                //Clear out stored job after assignment
                storedTarget = null;
                storedJobDef = null;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            GizmoAmmoStatus ammoStatusGizmo = new GizmoAmmoStatus { compAmmo = this };
            yield return ammoStatusGizmo;

            if ((wielder != null && wielder.Faction == Faction.OfPlayer) || (turret != null && turret.Faction == Faction.OfPlayer))
            {
                Action action = null;
                if (wielder != null) action = delegate { TryStartReload(); };
                else if (turret != null && turret.GetMannableComp() != null) action = turret.OrderReload;

                // Check for teaching opportunities
                string tag;
                if(turret == null)
                {
                    if (hasMagazine) tag = "CE_Reload"; // Teach reloading weapons with magazines
                    else tag = "CE_ReloadNoMag";    // Teach about mag-less weapons
                }
                else
                {
                    if (turret.GetMannableComp() == null) tag = "CE_ReloadAuto";  // Teach about auto-turrets
                    else tag = "CE_ReloadManned";    // Teach about reloading manned turrets
                }
                LessonAutoActivator.TeachOpportunity(ConceptDef.Named(tag), turret, OpportunityType.GoodToKnow);

                Command_Reload reloadCommandGizmo = new Command_Reload
                {
                    compAmmo = this,
                    action = action,
                    defaultLabel = hasMagazine ? "CE_ReloadLabel".Translate() : "",
                    defaultDesc = "CE_ReloadDesc".Translate(),
                    icon = currentAmmo == null ? ContentFinder<Texture2D>.Get("UI/Buttons/Reload", true) : Def_Extensions.IconTexture(selectedAmmo),
                    tutorTag = tag
                };
                yield return reloadCommandGizmo;
            }
        }

		public override string TransformLabel(string label)
		{
            string ammoSet = useAmmo && ModSettings.showCaliberOnGuns ? " (" + Props.ammoSet.LabelCap + ") " : "";
            return  label + ammoSet;
		}

        public override string GetDescriptionPart()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("CE_MagazineSize".Translate() + ": " + GenText.ToStringByStyle(Props.magazineSize, ToStringStyle.Integer));
            stringBuilder.AppendLine("CE_ReloadTime".Translate() + ": " + GenText.ToStringByStyle((Props.reloadTicks / 60), ToStringStyle.Integer) + " s");
            if (useAmmo)
            {
                // Append various ammo stats
                stringBuilder.AppendLine("CE_AmmoSet".Translate() + ": " + Props.ammoSet.LabelCap + "\n");
                foreach(var cur in Props.ammoSet.ammoTypes)
                {
                    string label = string.IsNullOrEmpty(cur.ammo.ammoClass.LabelCapShort) ? cur.ammo.ammoClass.LabelCap : cur.ammo.ammoClass.LabelCapShort;
                    stringBuilder.AppendLine(label + ":\n" + cur.projectile.GetProjectileReadout());
                }
            }
            return stringBuilder.ToString();
        }

        #endregion Methods
    }
}