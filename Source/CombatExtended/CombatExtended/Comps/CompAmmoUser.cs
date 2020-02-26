using System;
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

        private int lastLoadedMagCountInt = 0;
        private int curMagCountInt = 0;
      //private AmmoDef currentAmmoInt = null;
      //private AmmoDef selectedAmmo;

        private Thing ammoToBeDeleted;

        public Building_TurretGunCE turret;         // Cross-linked from CE turret

        internal static Type rgStance = null;       // RunAndGun compatibility, set in relevent patch if needed
        #endregion

        #region Properties

        public CompProperties_AmmoUser Props
        {
            get
            {
                return (CompProperties_AmmoUser)props;
            }
        }

        /// <summary>Cached whether gun ejects cases for faster SpentRounds calculation</summary>
        bool ejectsCasings = false;
        //Used by StatPart_LoadedAmmo to calculate remaining weight of e.g casings or spent batteries
        public int SpentRounds
        {
            get
            {
                return (ejectsCasings && ((CurrentUser.projectiles.First().thingDef.projectile as ProjectilePropertiesCE)?.dropsCasings ?? false))
                    ? 0 : Math.Max(0, lastLoadedMagCountInt - curMagCountInt);
            }
            set
            {
                lastLoadedMagCountInt = value;
            }
        }

        public int CurMagCount
        {
            get
            {
                return curMagCountInt;
            }
            set
            {
                if (curMagCountInt != value && value >= 0)
                {
                    curMagCountInt = value;
                    lastLoadedMagCountInt = Mathf.Max(lastLoadedMagCountInt, value);

                    if (CompInventory != null) CompInventory.UpdateInventory();     //Must be positioned after curMagCountInt is updated, because it relies on that value
                }
            }
        }
        public CompEquippable CompEquippable
        {
            get { return parent.GetComp<CompEquippable>(); }
        }
        public Pawn Wielder
        {
            get
            {
                if (CompEquippable == null 
                    || CompEquippable.PrimaryVerb == null 
                    || CompEquippable.PrimaryVerb.caster == null
                    || ((CompEquippable?.parent?.ParentHolder as Pawn_InventoryTracker)?.pawn is Pawn holderPawn && holderPawn != CompEquippable?.PrimaryVerb?.CasterPawn))
                {
                    return null;
                }
                return CompEquippable.PrimaryVerb.CasterPawn;
            }
        }
        public Pawn Holder
        {
            get
            {
                return Wielder ?? (CompEquippable.parent.ParentHolder as Pawn_InventoryTracker)?.pawn;
            }
        }
        public bool UseAmmo
        {
            get
            {
                return Controller.settings.EnableAmmoSystem && Props.ammoSet != null;
            }
        }
        public bool HasAndUsesAmmoOrMagazine
        {
            get
            {
                return !UseAmmo || HasAmmoOrMagazine;
            }
        }
        public bool HasAmmoOrMagazine
        {
            get
            {
                return (HasMagazine && CurMagCount > 0) || HasAmmo;
            }
        }
        public bool CanBeFiredNow
        {
            get
            {
                return (HasMagazine && CurMagCount > 0) || (!HasMagazine && (HasAmmo || !UseAmmo));
            }
        }
        //TODO: Split into HasAmmo(for current link) and HasAmmo(for any link)
        public bool HasAmmo => TryFindAnyAmmoInInventory(CompInventory);
        public bool HasMagazine { get { return Props.magazineSize > 0; } }
      /*public AmmoDef CurrentAmmo
        {
            get
            {
                return UseAmmo ? currentAmmoInt : null;
            }
        }*/

        AmmoLink currentLinkInt;
        public AmmoLink CurrentLink => currentLinkInt;
        AmmoLink selectedLinkInt;
        public AmmoLink SelectedLink
        {
            get
            {
                return selectedLinkInt;
            }
            set
            {
                selectedLinkInt = value;

                if (!HasMagazine && CurrentLink != value)
                    currentLinkInt = value;
            }
        }
        
        public ChargeUser CurrentUser => CurrentLink.BestUser(this);

      //public AmmoLink CurrentLink => Props.ammoSet?.ammoTypes?
      //            .Where(x => x.adders.Any(y => y.ammo.thingDef == CurrentAmmo) && x.amount <= CurMagCount)
      //            .MaxByWithFallback(x => x.amount);

        //Shouldn't exist
      //public ThingDef CurAmmoProjectile => CurrentLink?.projectile
      //        ?? parent.def.Verbs.FirstOrDefault().defaultProjectile;
        public CompInventory CompInventory
        {
            get
            {
                return Holder.TryGetComp<CompInventory>();
            }
        }
        private IntVec3 Position
        {
            get
            {
                if (Wielder != null) return Wielder.Position;
                else if (turret != null) return turret.Position;
                else if (Holder != null) return Holder.Position;
                else return parent.Position;
            }
        }
        private Map Map
        {
            get
            {
                if (Holder != null) return Holder.MapHeld;
                else if (turret != null) return turret.MapHeld;
                else return parent.MapHeld;
            }
        }
        public bool ShouldThrowMote => Props.throwMote && Props.magazineSize > 1;

        /* Shouldn't exist
        public AmmoDef SelectedAmmo
        {
            get
            {
                return selectedAmmo;
            }
            set
            {
                selectedAmmo = value;
                if (!HasMagazine && CurrentAmmo != value)
                {
                    currentAmmoInt = value;
                }
            }
        }
        */

        #endregion Properties

        #region Methods

        public override void Initialize(CompProperties vprops)
        {
            base.Initialize(vprops);

            //curMagCountInt = Props.spawnUnloaded && UseAmmo ? 0 : Props.magazineSize;
            ejectsCasings = parent.def.Verbs.Select(x => x as VerbPropertiesCE).First()?.ejectsCasings ?? true;

            // Initialize ammo with default if none is set
            if (UseAmmo)
            {
                if (Props.ammoSet.ammoTypes.NullOrEmpty())
                {
                    Log.Error(parent.Label + " has no available ammo types");
                }
                else
                {
                    currentLinkInt = Props.ammoSet.ammoTypes[0];
                    selectedLinkInt = currentLinkInt;

                  /*if (currentAmmoInt == null)
                        currentAmmoInt = (AmmoDef)Props.ammoSet.ammoTypes[0].adders.MinBy(x => x.count).thingDef;
                    if (selectedAmmo == null)
                        selectedAmmo = currentAmmoInt;*/
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref curMagCountInt, "count", 0);

            var currentLinkIndex = Props.ammoSet.ammoTypes.IndexOf(CurrentLink);
            Scribe_Values.Look(ref currentLinkIndex, "currentLinkInd", 0);
            currentLinkInt = currentLinkIndex < Props.ammoSet.ammoTypes.Count
                ? Props.ammoSet.ammoTypes[currentLinkIndex]
                : Props.ammoSet.ammoTypes[0];

            var selectedLinkIndex = Props.ammoSet.ammoTypes.IndexOf(SelectedLink);
            Scribe_Values.Look(ref selectedLinkIndex, "selectedLinkInd", currentLinkIndex);
            selectedLinkInt = selectedLinkIndex < Props.ammoSet.ammoTypes.Count
                ? Props.ammoSet.ammoTypes[selectedLinkIndex]
                : currentLinkInt;

            var val = SpentRounds;
            Scribe_Values.Look(ref val, "conservedRounds", 0);
            lastLoadedMagCountInt = curMagCountInt + val;

            ejectsCasings = parent.def.Verbs.Select(x => x as VerbPropertiesCE).First()?.ejectsCasings ?? true;
        }

        private void AssignJobToWielder(Job job)
        {
            if (Wielder.drafter != null)
            {
                Wielder.jobs.TryTakeOrderedJob(job);
            }
            else
            {
                ExternalPawnDrafter.TakeOrderedJob(Wielder, job);
            }
        }

        #region Firing
        public bool Notify_ShotFired()
        {
            if (ammoToBeDeleted != null)
            {
                ammoToBeDeleted.Destroy();
                ammoToBeDeleted = null;
                CompInventory.UpdateInventory();
                if (!HasAmmoOrMagazine)
                {
                    return false;
                }
            }
            return true;
        }

        public bool Notify_PostShotFired()
        {
            if (!HasAmmoOrMagazine)
            {
                DoOutOfAmmoAction();
                return false;
            }
            return true;
        }

        /// <summary>
        /// Reduces ammo count and updates inventory if necessary, call this whenever ammo is consumed by the gun (e.g. firing a shot, clearing a jam).
        /// </summary>
        public bool TryReduceAmmoCount()
        {
            var ammoConsumedPerShot = (CurrentUser?.chargesUsed ?? 1);

            if (Wielder == null && turret == null)
            {
                Log.Error(parent.ToString() + " tried reducing its ammo count without a wielder");
            }

            // Mag-less weapons feed directly from inventory
            if (!HasMagazine)
            {
                if (UseAmmo && CurMagCount <= 0)
                {
                    if (!TryFindAmmoInInventory(CompInventory, out ammoToBeDeleted, false, true))
                    {
                        return false;
                    }

                    //Set CurMagCount since it changes CurrentLink return value
                    curMagCountInt += CurrentLink.adders.First(x => x.thingDef == ammoToBeDeleted.def).count;
                    
                    ammoConsumedPerShot = (CurrentUser?.chargesUsed ?? 1);

                    if (ammoToBeDeleted.stackCount > ammoConsumedPerShot)
                        ammoToBeDeleted = ammoToBeDeleted.SplitOff(ammoConsumedPerShot);
                }
                return true;
            }
            // If magazine is empty, return false
            if (curMagCountInt <= 0)
            {
                CurMagCount = 0;
                lastLoadedMagCountInt = 0;
                return false;
            }
            // Reduce ammo count and update inventory
            CurMagCount -= ammoConsumedPerShot;
            
            if (curMagCountInt < 0) TryStartReload();
            return true;
        }
        #endregion

        #region Reloading
        // really only used by pawns (JobDriver_Reload) at this point... TODO: Finish making sure this is only used by pawns and fix up the error checking.
        /// <summary>
        /// Overrides a Pawn's current activities to start reloading a gun or turret.  Has a code path to resume the interrupted job.
        /// </summary>
        public void TryStartReload()
        {
            #region Checks
            if (!HasMagazine)
            {
                if (!CanBeFiredNow)
                {
                    DoOutOfAmmoAction();
                }
                return;
            }
            if (Wielder == null && turret == null)
                return;

            // secondary branch for if we ended up being called up by a turret somehow...
            if (turret != null)
            {
                turret.TryOrderReload();
                return;
            }

            // R&G compatibility, prevents an initial attempt to reload while moving
            if (Wielder.stances.curStance.GetType() == rgStance)
                return;
            #endregion

            if (UseAmmo)
            {
                TryUnload();

                // Check for ammo
                if (Wielder != null && !HasAmmo)
                {
                    DoOutOfAmmoAction();
                    return;
                }
            }

            //Because reloadOneAtATime weapons don't dump their mag at the start of a reload, have to stop the reloading process here if the mag was already full
            if (Props.reloadOneAtATime && UseAmmo && selectedLinkInt == currentLinkInt && CurMagCount == Props.magazineSize)
                return;

            // Issue reload job
            if (Wielder != null)
            {
                Job reloadJob = TryMakeReloadJob();
                if (reloadJob == null)
                    return;
                reloadJob.playerForced = true;
                Wielder.jobs.StartJob(reloadJob, JobCondition.InterruptForced, null, Wielder.CurJob?.def != reloadJob.def, true);
            }
        }

        /// <summary>
        /// Used to fetch a reload job for the weapon this comp is on.  Sets storedInfo to null (as if no job being replaced).
        /// </summary>
        /// <returns>Job using JobDriver_Reload</returns>
        /// <remarks>TryUnload() should be called before this in most cases.</remarks>
        public Job TryMakeReloadJob()
        {
            if (!HasMagazine || (Holder == null && turret == null))
                return null; // the job couldn't be created.

            return new Job(CE_JobDefOf.ReloadWeapon, Holder, parent);
        }

        public void LoadAmmo(Thing ammo = null, bool largestStack = false)
        {
            if (Holder == null && turret == null)
            {
                Log.Error(parent.ToString() + " tried loading ammo with no owner");
                return;
            }

            int newMagCount;
            if (UseAmmo)
            {
                bool ammoFromInventory = false;

                if (ammo == null)
                {
                    if (!TryFindAmmoInInventory(CompInventory, out ammo, largestStack))
                    {
                        DoOutOfAmmoAction();
                        return;
                    }
                    ammoFromInventory = true;
                }

                var inInventory = CompInventory?.ammoList ?? null;

                while (curMagCountInt < Props.magazineSize)
                {
                    curMagCountInt += SelectedLink.ReloadNext(CompInventory?.ammoList, this, out var defCount);

                }

              //currentAmmoInt = (AmmoDef)ammoThing.def;
                

                // If there's more ammo in inventory than the weapon can hold, or if there's greater than 1 bullet in inventory if reloading one at a time
                if ((Props.reloadOneAtATime ? 1 : Props.magazineSize) < ammo.stackCount)
                {
                    if (Props.reloadOneAtATime)
                    {
                        newMagCount = curMagCountInt + 1;
                        ammo.stackCount--;
                    }
                    else
                    {
                        newMagCount = Props.magazineSize;
                        ammo.stackCount -= Props.magazineSize;
                    }
                }

                // If there's less ammo in inventory than the weapon can hold, or if there's only one bullet left if reloading one at a time
                else
                {
                    newMagCount = (Props.reloadOneAtATime) ? curMagCountInt + 1 : ammo.stackCount;
                    if (ammoFromInventory)
                    {
                        CompInventory.container.Remove(ammo);
                    }
                    else if (!ammo.Destroyed)
                    {
                        ammo.Destroy();
                    }
                }
            }
            else
            {
                newMagCount = (Props.reloadOneAtATime) ? (curMagCountInt + 1) : Props.magazineSize;
            }
            CurMagCount = newMagCount;
            if (turret != null) turret.isReloading = false;
            if (parent.def.soundInteract != null) parent.def.soundInteract.PlayOneShot(new TargetInfo(Position, Find.CurrentMap, false));
        }

        public bool TryFindAnyAmmoInInventory(CompInventory inventory)
        {
            if (inventory == null)
                return false;

            return inventory.ammoList.Any(x => Props.ammoSet.MaxCharge(x.def) != -1);
        }

        public bool TryFindAmmoInInventory(CompInventory inventory, out Thing ammoThing, bool largestStack = false, bool setSelectedLink = false)
        {
            ammoThing = null;

            if (inventory == null)
                return false;

            // Try finding suitable ammoThing for currently set ammo first
            var selectedAmmo = inventory?.ammoList?.Where(x => SelectedLink.CanAdd(x.def)) ?? null;
            if (selectedAmmo != null)
                ammoThing = SelectedLink.BestAdder(inventory.ammoList, this, out var _, largestStack);

            if (ammoThing != null)
                return true;

            //TODO: Store currently loaded

            if (Props.reloadOneAtATime && CurMagCount > 0)
            {
                //Current mag already has a few rounds in, and the inventory doesn't have any more of that type.
                //If we let this method pick a new selectedAmmo below, it would convert the already loaded rounds to a different type,
                //so for OneAtATime weapons, we stop the process here here.
                return false;
            }

            // Try finding ammo from different type
            foreach (AmmoLink link in Props.ammoSet.ammoTypes)
            {
                if (link == SelectedLink)
                    continue;

                ammoThing = link.BestAdder(CompInventory.ammoList, this, out var _, largestStack);

                if (ammoThing != null)
                {
                    if (setSelectedLink)
                        SelectedLink = link;

                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Unloading
        // used by both turrets (JobDriver_ReloadTurret) and pawns (JobDriver_Reload).
        /// <summary>
        /// Used to unload the weapon.  Ammo will be dumped to the unloading Pawn's inventory or the ground if insufficient space.  Any ammo that can't be dropped
        /// on the ground is destroyed (with a warning).
        /// </summary>
        /// <returns>bool, true indicates the weapon was already in an unloaded state or the unload was successful.  False indicates an error state.</returns>
        /// <remarks>
        /// Failure to unload occurs if the weapon doesn't use a magazine.
        /// </remarks>
        public bool TryUnload(bool forceUnload = false)
        {
            return TryUnload(out var _, forceUnload);
        }

        public bool TryUnload(out List<Thing> droppedAmmo, bool forceUnload = false, bool convertAllToThingList = false)
        {
            droppedAmmo = new List<Thing>();

            if (!HasMagazine || (Holder == null && turret == null && !convertAllToThingList))
                return false; // nothing to do as we are in a bad state;

            if (!UseAmmo || curMagCountInt == 0)
                return true; // nothing to do but we aren't in a bad state either.  Claim success.

            //For reloadOneAtATime weapons that haven't been explicitly told to unload, and aren't switching their ammo type, skip unloading.
            //The big advantage of a shotguns' reload mechanism is that you can add more shells without unloading the already loaded ones.
            if (Props.reloadOneAtATime && !forceUnload && !convertAllToThingList && selectedLinkInt == currentLinkInt && turret == null)
                return true;

            while (curMagCountInt > 0)
            {
                // amount of charges lowered; defCount: thing description to drop
                curMagCountInt -= CurrentLink.UnloadNext(this, out ThingDefCount defCount);

                // No ammo needs to be spawned
                if (defCount == null)
                    continue;

                // Otherwise, spawn ammo
                Thing ammoThing = ThingMaker.MakeThing(defCount.ThingDef);
                ammoThing.stackCount = defCount.Count;

                if (convertAllToThingList)
                {
                    droppedAmmo.Add(ammoThing);
                    continue;
                }

                // Can't store ammo       || Inventory can't hold ALL ammo ...
                if (CompInventory == null || defCount.Count != CompInventory.container.TryAdd(ammoThing, ammoThing.stackCount))
                {
                    //.. then, drop remainder

                    // NOTE: If we get here from ThingContainer.TryAdd() it will have modified the ammoThing.stackCount to what it couldn't take.
                    if (GenThing.TryDropAndSetForbidden(ammoThing, Position, Map, ThingPlaceMode.Near, out var droppedUnusedAmmo, turret.Faction != Faction.OfPlayer))
                        droppedAmmo.Add(droppedUnusedAmmo);
                    else
                        Log.Warning(String.Concat(this.GetType().Assembly.GetName().Name + " :: " + this.GetType().Name + " :: ",
                                                    "Unable to drop ", ammoThing.LabelCap, " on the ground, thing was destroyed."));
                }
            }

            // Update the CurMagCount (thus updating inventory)
            CurMagCount = curMagCountInt;

            return true;
        }
        #endregion

        private void DoOutOfAmmoAction()
        {
            if (ShouldThrowMote)
            {
                MoteMaker.ThrowText(Position.ToVector3Shifted(), Find.CurrentMap, "CE_OutOfAmmo".Translate() + "!");
            }
            if (Wielder != null && CompInventory != null && (Wielder.CurJob == null || Wielder.CurJob.def != JobDefOf.Hunt)) CompInventory.SwitchToNextViableWeapon();
        }

        /// <summary>
        /// Resets current ammo count to a full magazine. Intended use is pawn/turret generation where we want raiders/enemy turrets to spawn with loaded magazines. DO NOT
        /// use for regular reloads, those should be handled through LoadAmmo() instead.
        /// </summary>
        /// <param name="newAmmo">Currently loaded ammo type will be set to this, null will load currently selected type.</param>
        public void ResetAmmoCount(AmmoDef newAmmo = null)
        {
            if (newAmmo != null)
            {
                var associatedLink = Props.ammoSet.Containing(newAmmo);
                currentLinkInt = Props.ammoSet.Containing(newAmmo);
                selectedLinkInt = currentLinkInt;
            }
            CurMagCount = Props.magazineSize;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            GizmoAmmoStatus ammoStatusGizmo = new GizmoAmmoStatus { compAmmo = this };
            yield return ammoStatusGizmo;

            if ((Wielder != null && Wielder.Faction == Faction.OfPlayer) || (turret != null && turret.Faction == Faction.OfPlayer && (turret.MannableComp != null || UseAmmo)))
            {
                Action action = null;
                if (Wielder != null) action = TryStartReload;
                else if (turret?.MannableComp != null) action = turret.TryOrderReload;

                // Check for teaching opportunities
                string tag;
                if (turret == null)
                {
                    if (HasMagazine) tag = "CE_Reload"; // Teach reloading weapons with magazines
                    else tag = "CE_ReloadNoMag";    // Teach about mag-less weapons
                }
                else
                {
                    if (turret.MannableComp == null) tag = "CE_ReloadAuto";  // Teach about auto-turrets
                    else tag = "CE_ReloadManned";    // Teach about reloading manned turrets
                }
                LessonAutoActivator.TeachOpportunity(ConceptDef.Named(tag), turret, OpportunityType.GoodToKnow);

                Command_Reload reloadCommandGizmo = new Command_Reload
                {
                    compAmmo = this,
                    action = action,
                    defaultLabel = HasMagazine ? "CE_ReloadLabel".Translate() : "",
                    defaultDesc = "CE_ReloadDesc".Translate(),
                    icon = (CurrentLink == null) ? ContentFinder<Texture2D>.Get("UI/Buttons/Reload", true) : SelectedLink.iconAdder.IconTexture(),
                    tutorTag = tag
                };
                yield return reloadCommandGizmo;
            }
        }

        public override string TransformLabel(string label)
        {
            string ammoSet = UseAmmo && Controller.settings.ShowCaliberOnGuns ? " (" + Props.ammoSet.LabelCap + ") " : "";
            return label + ammoSet;
        }

        /*
        public override string GetDescriptionPart()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("CE_MagazineSize".Translate() + ": " + GenText.ToStringByStyle(Props.magazineSize, ToStringStyle.Integer));
            stringBuilder.AppendLine("CE_ReloadTime".Translate() + ": " + GenText.ToStringByStyle((Props.reloadTime), ToStringStyle.FloatTwo) + " s");
            if (UseAmmo)
            {
                // Append various ammo stats
                stringBuilder.AppendLine("CE_AmmoSet".Translate() + ": " + Props.ammoSet.LabelCap + "\n");
                foreach(var cur in Props.ammoSet.ammoTypes)
                {
                    string label = string.IsNullOrEmpty(cur.ammo.ammoClass.LabelCapShort) ? cur.ammo.ammoClass.LabelCap : cur.ammo.ammoClass.LabelCapShort;
                    stringBuilder.AppendLine(label + ":\n" + cur.projectile.GetProjectileReadout());
                }
            }
            return stringBuilder.ToString().TrimEndNewlines();
        }
        */

        #endregion Methods
    }
}
