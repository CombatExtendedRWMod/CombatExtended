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
        /// <summary>Charges added as a result of Things in the adders list; IGNORES currentAdderCharge</summary>
        private int curMagCountInt = 0;

        private Thing ammoToBeDeleted;

        public List<Thing> adders = new List<Thing>();
        public List<Thing> spentAdders = new List<Thing>();

        public Thing CurrentAdder => adders.FirstOrDefault();
        /// <summary>Excess (&gt;0) or deficit (&lt;0) of charges added by the CurrentAdder. If &lt;= -CurrentAdder's charges added, destroy CurrentAdder</summary>
        public int currentAdderCharge = 0;
        
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
        public bool ejectsCasings = false;
        public bool DiscardRounds => ejectsCasings
            && ((MainProjectile.projectile as ProjectilePropertiesCE)?.dropsCasings ?? false);
        
        /// <summary>Sum of charges of all adders and the deficit in the CurrentAdder -- concerns "firable charges", properly treats partially spent rounds</summary>
        public int CurChargeCount
        {
            get
            {
                return curMagCountInt + currentAdderCharge;
            }
        }
        /// <summary>Sum of charges of all adders -- concerns "loaded" rounds, counts partially spent rounds as "loaded"</summary>
        public int CurMagCount
        {
            get
            {
                return curMagCountInt;
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
        //JobDriver_Hunt (CurrentLink) CompInventory (AnyLink)
        public bool HasAndUsesCurrentAmmoOrMagazine => !UseAmmo || HasCurrentAmmoOrMagazine;
        public bool HasAndUsesAnyAmmoOrMagazine => !UseAmmo || HasAnyAmmoOrMagazine;
        //Used for bows (CurrentLink), TakeAndEquip (wrongly; AmmoSet), HasAndUsesAmmoOrMagazine.
        public bool HasCurrentAmmoOrMagazine => (HasMagazine && CurChargeCount > 0) || HasAmmoForCurrentLink;
        public bool HasAnyAmmoOrMagazine => (HasMagazine && CurChargeCount > 0) || HasAmmoForAmmoSet;
        public bool CanBeFiredNow
        {
            get
            {
                return HasMagazine
                    ? CurrentUser != null   //CurrentUser takes from BestUser, which checks whether CurChargeCount is sufficient to fire the projectile
                    : !UseAmmo || HasAmmoForCurrentLink;  //HasAmmo checks whether inventory has anything that could charge any AmmoLink
            }
        }
        //ASDF: Decide which methods call which version!
        public bool HasAmmoForAmmoSet => CompInventory?.ammoList.Any(x => Props.ammoSet.MaxCharge(x.def) > 0) ?? false;
        public bool HasAmmoForCurrentLink => CompInventory?.ammoList.Any(x => CurrentLink.CanAdd(x.def)) ?? false;
        public bool HasMagazine { get { return Props.magazineSize > 0; } }

        int currentLinkInt;
        public AmmoLink CurrentLink => Props.ammoSet.ammoTypes[currentLinkInt];
        int selectedLinkInt;
        public AmmoLink SelectedLink
        {
            get
            {
                return Props.ammoSet.ammoTypes[selectedLinkInt];
            }
        }
        public bool LinksMatch => selectedLinkInt == currentLinkInt;

        public ChargeUser CurrentUser => CurrentLink.BestUser(this);
        public ChargeUser latestUser;
        public ThingDef MainProjectile => CurrentUser?.projectiles.First().thingDef ?? null;

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
                    currentLinkInt = 0;
                    selectedLinkInt = 0;

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
            Scribe_Values.Look(ref currentAdderCharge, "currentAdderCharge", 0);

            Scribe_Values.Look<int>(ref currentLinkInt, "currentLinkInt", 0);
            Scribe_Values.Look<int>(ref selectedLinkInt, "selectedLinkInt", currentLinkInt);
            if (currentLinkInt > Props.ammoSet.ammoTypes.Count)     currentLinkInt = 0;
            if (selectedLinkInt > Props.ammoSet.ammoTypes.Count)    selectedLinkInt = 0;
            
            Scribe_Collections.Look<Thing>(ref adders, "adders");
            Scribe_Collections.Look<Thing>(ref spentAdders, "spentAdders");
            
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

        void PrintDebug(string tag)
        {
            Log.Message(tag
                + " Adder: " + string.Join(",", adders.Select((x, y) => "[" + y + "]" + x.def.defName + "=" + x.stackCount).ToArray())
                + " Spent: " + string.Join(",", spentAdders.Select((x, y) => "[" + y + "]" + x.def.defName + "=" + x.stackCount).ToArray())
                + " cMC: " + curMagCountInt
                + " cAC: " + currentAdderCharge);
        }

        #region Firing
        #region Bows
        //Of relevance to ammousers without mag (bows)
        public bool Notify_ShotFired()
        {
            if (ammoToBeDeleted != null)
            {
                ammoToBeDeleted.Destroy();
                ammoToBeDeleted = null;
                CompInventory.UpdateInventory();
                if (!HasCurrentAmmoOrMagazine)
                {
                    return false;
                }
            }
            return true;
        }

        //Of relevance to ammousers without mag (bows)
        public bool Notify_PostShotFired()
        {
            if (!HasCurrentAmmoOrMagazine)
            {
                DoOutOfAmmoAction();
                return false;
            }
            return true;
        }
        #endregion

        /// <summary>Check whether gun can fire</summary>
        /// <returns></returns>
        public bool PreFire()
        {
            PrintDebug("TryFire"); //ASDF remove

            if (Wielder == null && turret == null)
                Log.Error(parent.ToString() + " tried reducing its ammo count without a wielder");

            // If magazine is empty, return false
            if (CurChargeCount <= 0)
            {
                //if (!CurrentLink.allowUnderflow && curMagCountInt != 0)
                //    curMagCountInt = 0;

                //CurMagCount = 0;    //ASDF: Consider whether is accurate
                return false;
            }
            
            var chargesUsed = CurrentUser?.chargesUsed ?? 1;    //Fallback to 1 on guns without ammo.. but still a CompAmmoUser..?
            
            // Mag-less weapons feed directly from inventory
            if (!HasMagazine && (CurChargeCount - chargesUsed <= 0 && !CurrentLink.allowUnderflow))
                return LoadAmmo();

            return CanBeFiredNow;
        }
        
        /// <summary>
        /// Reduces ammo count and updates inventory if necessary, call this whenever ammo is consumed by the gun (e.g. firing a shot, clearing a jam).
        /// </summary>
        public void PostFire()
        {
            // --------
            // Past the possiblity of return false; Can now make changes
            // --------

            //The amount of charges used by the just-fired projectile
            var chargesUsed = CurrentUser?.chargesUsed ?? 1;    //Fallback to 1 on guns without ammo.. but still a CompAmmoUser..?
            
            // Reduce ammo count
            currentAdderCharge -= chargesUsed;

            //Deletes cA when appropriate
            if (HasMagazine && !TryFeed())
            {
                TryStartReload();
                return;
            }

            //Update inventory
            CompInventory?.UpdateInventory();

            if (CurChargeCount < 0) TryStartReload();
            return;
        }

        /// <summary>True: Reload succesful or unnecessary; False: Reload failed, not reloadable in some way</summary>
        /// <returns></returns>
        public bool TryFeed()
        {
            //Get current adder's charges added
            if (!CurrentLink.CanAdd(CurrentAdder.def, out var charge))
                return false;

            //Only feed when the current adder is fully depleted (deficit >= full charge)
            if (currentAdderCharge > -charge)
                return true;

            //Replenish currentAdder's charges with next adder
            while (currentAdderCharge < 0)
            {
                //Find new currentAdder
                if (CurrentAdder != null && CurrentLink.CanAdd(CurrentAdder.def, out var newChargeCount))
                {
                    //ASDF: "Has issue" -- sometimes returns amountDepleted = 0
                    var amountDepleted = CurrentLink.AmountForDeficit(CurrentAdder, -currentAdderCharge, true);

                    //Move count from currentAdder
                    currentAdderCharge += amountDepleted * newChargeCount;
                    curMagCountInt -= amountDepleted * newChargeCount;

                    //Remove count from adders -- either here or in UnloadAdder
                    if (amountDepleted > 0)
                    {
                        // Since SplitOff carries over the current Thing if the whole stack is split off,
                        // we need to keep a cache of the Get-value that's split off of. Without the cache,
                        // CurrentAdder.SplitOff() will remain within adders and there are two instances of
                        // the ammo found, introducing all sorts of issues

                        //Make cache of get-value
                        var currentAdder = CurrentAdder;

                        //Remove get-value source from list
                        if (amountDepleted == currentAdder.stackCount)
                            adders.RemoveAt(0);
                        
                        //Use cache for splitting off
                        UnloadAdder(currentAdder.SplitOff(amountDepleted), true);
                    }
                    else
                        return false;
                }
                //No new currentAdder found.. time for reloading
                else
                {
                    return false;
                }
            }

            return currentAdderCharge >= 0;
        }
        #endregion

        #region Reloading
        public void AddAdder(Thing inThing, bool toSpentAdders = false, bool updateInventory = true)
        {
            if (inThing == null)
                return;

            Thing thing = null;
            if (!toSpentAdders)
            {
                //Add appropriate number of charges (and check for all limitations etc. set out by the ammoSetDef)
                curMagCountInt += SelectedLink.LoadThing(inThing, this, out var count);
                thing = inThing.SplitOff(count);
            }
            else
                thing = inThing;

            if (thing != null)
            {
                var existingAdder = (toSpentAdders ? spentAdders : adders).Find(x => x.def == thing.def);

                if (existingAdder == null || !existingAdder.TryAbsorbStack(thing, true))
                    (toSpentAdders ? spentAdders : adders).Add(thing);
            }

            if (updateInventory)
                CompInventory?.UpdateInventory();

            //TODO: Handle inThing? Destroy it?
        }

        // really only used by pawns (JobDriver_Reload) at this point... TODO: Finish making sure this is only used by pawns and fix up the error checking.
        /// <summary>
        /// Overrides a Pawn's current activities to start reloading a gun or turret.  Has a code path to resume the interrupted job.
        /// </summary>
        public void TryStartReload()
        {
            PrintDebug("TryStartReload-Pre");   //ASDF Remove

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
                if (Wielder != null && !HasAmmoForCurrentLink)
                {
                    DoOutOfAmmoAction();
                    return;
                }
            }

            PrintDebug("TryStartReload-Post");   //ASDF Remove

            //Because reloadOneAtATime weapons don't dump their mag at the start of a reload, have to stop the reloading process here if the mag was already full
            if (Props.reloadOneAtATime && UseAmmo && LinksMatch && CurMagCount == Props.magazineSize)
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

        /// <summary>Load a specified ammo Thing</summary>
        /// <param name="ammo">Specified ammo</param>
        /// <param name="largestStack">Whether to maximize stack size if called without specified ammo (somehow)</param>
        public bool LoadAmmo(Thing ammo = null, bool largestStack = false)
        {
            if (Holder == null && turret == null)
            {
                Log.Error(parent.ToString() + " tried loading ammo with no owner");
                return false;
            }
            
            if (UseAmmo)
            {
                if (ammo == null)
                {
                    //If ammo is null, allow any change to selectedLink. Otherwise, code isn't run anyways
                    if (!TryFindAmmoInInventory(CompInventory, out ammo, largestStack, true))
                    {
                        DoOutOfAmmoAction();
                        return false;
                    }
                }
                
                //Ammo can still be null at this point
                if (ammo != null)
                    AddAdder(ammo);
            }
            else
            {
                //ASDF: Instead, increase curMagCountInt by a value found in the CurrentLink or some other part of the AmmoSetDef.. or default to 1
                curMagCountInt = (Props.reloadOneAtATime) ? (curMagCountInt + 1) : Props.magazineSize;
            }

            CompInventory?.UpdateInventory();

            if (turret != null) turret.isReloading = false;
            if (parent.def.soundInteract != null) parent.def.soundInteract.PlayOneShot(new TargetInfo(Position, Find.CurrentMap, false));

            return true;
        }
        
        public bool TryFindAmmoInInventory(CompInventory inventory, out Thing ammoThing, bool largestStack = false, bool setSelectedLink = false)
        {
            ammoThing = null;

            if (inventory == null || inventory.ammoList.NullOrEmpty())
                return false;

            // Try finding suitable ammoThing for currently set ammo first
            ammoThing = SelectedLink.BestAdder(inventory.ammoList, this, out var _, largestStack);

            if (ammoThing != null)
                return true;

            //CurMagCount appropriate -- loaded ammo
            if (Props.reloadOneAtATime && CurMagCount > 0)
            {
                //Current mag already has a few rounds in, and the inventory doesn't have any more of that type.
                //If we let this method pick a new selectedAmmo below, it would convert the already loaded rounds to a different type,
                //so for OneAtATime weapons, we stop the process here here.
                return false;
            }

            // Try finding ammo from different type
            if (setSelectedLink)
            {
                for (int i = 0; i < Props.ammoSet.ammoTypes.Count; i++)
                {
                    if (i == selectedLinkInt)
                        continue;
                    
                    ammoThing = Props.ammoSet.ammoTypes[i].BestAdder(CompInventory.ammoList, this, out var _, largestStack);

                    if (ammoThing != null)
                    {
                        SwitchLink(Props.ammoSet.ammoTypes[i]);
                        return true;
                    }
                }
            }
            return false;
        }
        #endregion

        #region Switching ammo
        public bool SwitchLink(AmmoLink newLink, bool forced = false)
        {
            Log.Message("1");
            //If a new link is supplied, try setting it (if possible)
            if (newLink != null)
            { 
                if (!Props.ammoSet.ammoTypes.Contains(newLink))
                    return false;
            
                var indexOf = Props.ammoSet.ammoTypes.IndexOf(newLink);

                if (indexOf == selectedLinkInt && !forced)
                    return false;

                selectedLinkInt = indexOf;
            }
            Log.Message("2");

            if (LinksMatch)
                return false;

            Log.Message("3");
            //If no charges are loaded, simple and guaranteed switch
            if ((CurChargeCount == 0 && currentAdderCharge == 0))
            {
                //Allow for switching links
                currentLinkInt = selectedLinkInt;
                return true;
            }

            Log.Message("4");
            //Check whether all currently loaded charges fit in the other link ..
            int magDifference = 0;
            bool canMatch = true;
            foreach (var adder in adders)
            {
                if (SelectedLink.CanAdd(adder.def, out var cpuNew) && CurrentLink.CanAdd(adder.def, out var cpuCur))
                    magDifference += adder.stackCount * (cpuNew - cpuCur);
                else
                {
                    canMatch = false;
                    break;
                }
            }

            Log.Message("5");
            //.. and whether the difference in charges loaded by the adders in both links is acceptable
            if (canMatch && curMagCountInt + magDifference <= Props.magazineSize)
            {
                currentLinkInt = selectedLinkInt;
                return true;
            }
            Log.Message("6");

            //Otherwise... no simple conversion possible. Need to unload and reload.
            if (forced && TryUnload())
            {
                //Call this method again

                currentLinkInt = selectedLinkInt;
                return true;
            }

            //ASDF --> Convert to non-instant ammo change when any adders are present in magazine, when cAC is negative or positive..
            //ASDF TODO: Forced parameter
            //Try and reload and such

            //if (!HasMagazine)
            //    currentLinkInt = selectedLinkInt;

            return false;
        }
        #endregion

        #region Unloading
        /*bool FindNewBestAdder(out int newChargeCount)
          {
              var bestThing = CurrentLink.BestAdder(adders, this, out newChargeCount, false);

              if (bestThing == null)
                  return false;

              //Order newThing to front of list
              var indexOf = adders.IndexOf(bestThing);
              if (indexOf != 0)
              {
                  for (int i = indexOf; i > 0; i--)
                      adders[i] = adders[i - 1];

                  adders[0] = bestThing;
              }

              //Order spent thing to front of list

              return true;
          }*/

        void UnloadAdder(Thing thing = null, bool isSpent = false)
        {
            PrintDebug("UnloadAdder-Pre");   //ASDF Remove

            bool isCurrentAdder = false;
            if (thing == null)
            {
                if (CurrentAdder != null && CurrentAdder.stackCount > 0)
                {
                    //Important to split off only one, since everything fed to UnloadAdder is consumed
                    thing = CurrentAdder;
                    isCurrentAdder = true;
                }
            }

            if (thing == null || thing.stackCount < 1)
                return;

            //Handle currently-used adder ----> This is checked for SelectedLink instead...
            var spentThing = CurrentLink.UnloadAdder(thing, this, ref isSpent);

            //spentThing is any of:
            // - thing
            // - thing, but isSpent == true
            // - null
            // - a spent thing w/ stackCount = thing.stackCount

            //Cannot be null, since thing != null
            if (spentThing == thing)
            {
                //Return to where it came from (adders)
                if (!isSpent)
                    return;

                //Split off one and add to spentAdders
                if (isCurrentAdder && thing.stackCount > 1)
                {
                    spentThing = thing.SplitOff(1);
                }
                else
                    adders.Remove(spentThing);

                //Adds spentThing to spentAdders
                AddAdder(spentThing, true);

                PrintDebug("UnloadAdder-Same");   //ASDF Remove

                return;
            }

            //Must destroy thing entirely or in part
            if (spentThing == null)
            {
                if (isCurrentAdder && thing.stackCount > 1)
                    spentThing = thing.SplitOff(1);
                else
                    spentThing = thing;

                adders.Remove(spentThing);
                spentThing.Destroy();

                PrintDebug("UnloadAdder-Null");   //ASDF Remove

                return;
            }

            //If the current thing has to be destroyed
            if (spentThing != thing)
            {
                if (isCurrentAdder && thing.stackCount > 1)
                    spentThing.stackCount = 1;

                adders.Remove(thing);
                thing.Destroy();
                AddAdder(spentThing, true);

                PrintDebug("UnloadAdder-Different");   //ASDF Remove

                return;
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
        public bool TryUnload(bool forceUnload = false)
        {
            return TryUnload(out var _, forceUnload, false);
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="droppedAmmo"></param>
        /// <param name="forceUnload"></param>
        /// <param name="convertAllToThingList"></param>
        /// <returns>Whether we're in "a bad state", e.g unloading failed</returns>
        public bool TryUnload(out List<Thing> droppedAmmo, bool forceUnload = false, bool convertAllToThingList = false)
        {
            droppedAmmo = null;

                if (!HasMagazine || (Holder == null && turret == null && !convertAllToThingList))
                    return false; // nothing to do as we are in a bad state;

                if (!UseAmmo)
                    return true; // nothing to do but we aren't in a bad state either.  Claim success.
                
                //For reloadOneAtATime weapons that haven't been explicitly told to unload, and aren't switching their ammo type, skip unloading.
                //The big advantage of a shotguns' reload mechanism is that you can add more shells without unloading the already loaded ones.
                if (Props.reloadOneAtATime && !forceUnload && !convertAllToThingList && LinksMatch && turret == null)
                    return true;

            //-- -- Add remaining ammo back in the inventory -- --
            //Returns current adder to adders or to spentAdders
            
            //ASDF: Should be used for the CURRENT LINK. Errors happen due to SELECTED LINK being used
            UnloadAdder();

            PrintDebug("TryUnload-Pre");   //ASDF Remove

            droppedAmmo = new List<Thing>();

            //Clear adders
            if (CurMagCount > 0)
            {
                //Unload adders
                for (int i = adders.Count - 1; i > -1; i--)
                {
                    var thing = adders[i];

                    //Handle remaining magcount from current adders
                    if (CurrentLink.CanAdd(thing.def, out var cpu))
                    {
                        curMagCountInt -= cpu * thing.stackCount;
                    }

                    if (convertAllToThingList)
                    {
                        droppedAmmo.Add(thing);
                        adders.RemoveAt(i);
                        continue;
                    }

                    var prevCount = thing.stackCount;

                    // Can't store ammo       || Inventory can't hold ALL ammo ...
                    if (CompInventory == null || prevCount != CompInventory.container.TryAdd(thing, thing.stackCount))
                    {
                        //.. then, drop remainder

                        // NOTE: If we get here from ThingContainer.TryAdd() it will have modified the ammoThing.stackCount to what it couldn't take.
                        if (GenThing.TryDropAndSetForbidden(thing, Position, Map, ThingPlaceMode.Near, out var droppedUnusedAmmo, turret.Faction != Faction.OfPlayer))
                            droppedAmmo.Add(droppedUnusedAmmo);
                        else
                        {
                            Log.Warning(String.Concat(this.GetType().Assembly.GetName().Name + " :: " + this.GetType().Name + " :: ",
                                                        "Unable to drop ", thing.LabelCap, " on the ground, thing was destroyed."));
                        }
                    }

                    adders.RemoveAt(i);
                }
            }

            //Clear spent adders
            if (!spentAdders.NullOrEmpty())
            {
                //Destructive, backwards iteration of spentAdders
                for (int i = spentAdders.Count - 1; i > -1; i--)
                {
                    var thing = spentAdders[i];

                    if (thing == null)
                    {
                        spentAdders.RemoveAt(i);
                        continue;
                    }

                    //Just destroy completely
                    if (CurrentLink.IsSpentAdder(thing.def))
                    {
                        thing.Destroy();
                        spentAdders.RemoveAt(i);
                        continue;
                    }

                    if (convertAllToThingList)
                    {
                        droppedAmmo.Add(thing);
                        spentAdders.RemoveAt(i);
                        continue;
                    }

                    var prevCount = thing.stackCount;

                    // Can't store ammo       || Inventory can't hold ALL ammo ...
                    if (CompInventory == null || prevCount != CompInventory.container.TryAdd(thing, thing.stackCount))
                    {
                        //.. then, drop remainder

                        // NOTE: If we get here from ThingContainer.TryAdd() it will have modified the ammoThing.stackCount to what it couldn't take.
                        if (GenThing.TryDropAndSetForbidden(thing, Position, Map, ThingPlaceMode.Near, out var droppedUnusedAmmo, turret.Faction != Faction.OfPlayer))
                            droppedAmmo.Add(droppedUnusedAmmo);
                        else
                            Log.Warning(String.Concat(this.GetType().Assembly.GetName().Name + " :: " + this.GetType().Name + " :: ",
                                                        "Unable to drop ", thing.LabelCap, " on the ground, thing was destroyed."));
                    }

                    //Just be sure it is removed from this list
                    spentAdders.RemoveAt(i);
                }
            }
            
            // Update inventory
            CompInventory?.UpdateInventory();

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
        /// <param name="newAdderDef">Currently loaded ammo type will be set to this, null will load currently selected type.</param>
        public void ResetAmmoCount(ThingDef newAdderDef = null)
        {
            if (newAdderDef != null)
            {
                currentLinkInt = Props.ammoSet.ammoTypes.IndexOf(Props.ammoSet.Containing(newAdderDef));
                selectedLinkInt = currentLinkInt;
            }

            //Generate ammo if not given
            if (UseAmmo)
            {
                //Remove all stored charge information
                for (int i = adders.Count - 1; i > -1; i--)
                {
                    adders[i].Destroy();
                    adders.RemoveAt(i);
                }
                for (int i = spentAdders.Count - 1; i > -1; i--)
                {
                    spentAdders[i].Destroy();
                    spentAdders.RemoveAt(i);
                }
                
                currentAdderCharge = 0; //ASDF: Correct assignment
                curMagCountInt = 0;

                if (newAdderDef == null)
                    newAdderDef = CurrentLink.iconAdder;

                var stackToAccountFor = CurrentLink.AmountForDeficit(newAdderDef, Props.magazineSize, true);

                //Fill adders with newAmmo
                while (stackToAccountFor > 0)
                {
                    var toLoadMag = ThingMaker.MakeThing(newAdderDef);
                    var count = Math.Min(newAdderDef.stackLimit, stackToAccountFor);
                    toLoadMag.stackCount = count;
                    stackToAccountFor -= count;

                    //Handles curMagCountInt increase
                    AddAdder(toLoadMag, false, true);
                }
            }
            else
            {
                curMagCountInt = Props.magazineSize;
            }

            CompInventory?.UpdateInventory();
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
