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
        public bool HasAndUsesAnyAmmoOrMagazine => !UseAmmo || HasAnyAmmoOrMagazine;
        //Used for bows (CurrentLink), TakeAndEquip (wrongly; AmmoSet), HasAndUsesAmmoOrMagazine.
        public bool HasCurrentAmmoOrMagazine => (HasMagazine && CurChargeCount > 0) || HasAmmoForLink(CurrentLink);
        public bool HasAnyAmmoOrMagazine => (HasMagazine && CurChargeCount > 0) || HasAmmoForAmmoSet;
        public bool CanBeFiredNow
        {
            get
            {
                //HasMagazine, !UseAmmo: CurrentUser != null        (likely incorrect? CurrentUser is always null without UseAmmo maybe?)
                //HasMagazine, UseAmmo: CurrentUser != null
                //  - Positive amount of charges remaining
                //  - Allow underflow or charges remaining cover users' charge cost
                //!HasMagazine, !UseAmmo: true                      (probably correct, test with something like that)
                //!HasMagazine, UseAmmo: HasAmmoForCurrentLink      (bow, seems correct)

                return HasMagazine
                    ? CurrentUser != null   //CurrentUser takes from BestUser, which checks whether CurChargeCount is sufficient to fire the projectile
                    : !UseAmmo || HasAmmoForLink(CurrentLink);  //HasAmmo checks whether inventory has anything that could charge any AmmoLink
            }
        }
        //ASDF: Decide which methods call which version!
        public bool HasAmmoForAmmoSet => CompInventory?.ammoList.Any(x => Props.ammoSet.MaxCharge(x.def) > 0) ?? false;
        public bool HasMagazine { get { return Props.magazineSize > 0; } }
        
        public bool HasAmmoForLink(AmmoLink link)
        {
            return CompInventory?.ammoList.Any(x => link.CanAdd(x.def)) ?? false;
        }

        public bool forcedLinkSelect = false;

        int currentLinkInt;
        public AmmoLink CurrentLink => Props.ammoSet.ammoTypes[currentLinkInt];
        int selectedLinkInt;
        public AmmoLink SelectedLink => Props.ammoSet.ammoTypes[selectedLinkInt];

        public bool LinksMatch => selectedLinkInt == currentLinkInt;

        public ChargeUser CurrentUser => CurrentLink.BestUser(this);
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
                + " CurLink: " + CurrentLink.labelCap
                + " SelLink: " + SelectedLink.labelCap
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
            //ASDF: Bows should store reference to inventory item in CurrentAdder instead

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
            
            //ASDF: Prevent CurrentAdder calls in certain cases
            
            //The amount of charges used by the just-fired projectile
            var chargesUsed = CurrentUser?.chargesUsed ?? 1;    //Fallback to 1 on guns without ammo.. but still a CompAmmoUser..?
            
            // Reduce ammo count
            currentAdderCharge -= chargesUsed;

            //Possibilities:
            //-  UseAmmo,  HasMagazine (guns)
            //-  UseAmmo, !HasMagazine (bows)
            //- !UseAmmo,  HasMagazine (guns with ammo disabled)
            //- !UseAmmo, !HasMagazine (bows with ammo disabled)

            // TryFeed handles all possibilities
            //Alternative call to CanBeFiredNow -- after TryFeed has changed the CurrentUser, this can be called -- although probably unnecessary?
            if ((!TryFeed() || !CanBeFiredNow)
                //-  UseAmmo,  HasMagazine (guns)
                && PreReload())
            {
                TryStartReload();
                return;
            }
            
            //Update inventory
            CompInventory?.UpdateInventory();

            //ASDF: This call should probably be removed
          //if (CurChargeCount < 0 && PreReload()) TryStartReload();
            return;
        }

        // Only called on PostFire, for HasMagazine true/false, HasAmmo true/false
        /// <summary>True: Feeding from magazine succesful or unnecessary; False: Feeding from magazine failed, not feedable in some way</summary>
        /// <returns></returns>
        public bool TryFeed()
        {
            //Ammo disabled on guns/bows, or doesn't use ammo (Verb_ShootCEOneUse)
            if (!UseAmmo)
            {
                //ASDF: TODO, consider charge usage with ammo disabled!
                //No need to feed -- for bows, guns, shootOneUse
                if (currentAdderCharge > -1)
                    return true;
                
                //Guns with ammo system disabled
                if (HasMagazine)
                {
                    //curMagCountInt is too small to supply all of currentAdderCharge: feeding failed (so, try reloading)
                    if (CurChargeCount < 0)
                        return false;

                    curMagCountInt += currentAdderCharge;   //Subtracts, since cAC <= -1
                    currentAdderCharge = 0;
                    return true;
                }

                //shootOneUse or bow -- try reload if cAC <= -1
                return false;
            }

            //----
            // The remainder USES AMMO -- Bows (no mag) or guns (mag) with ammo system enabled
            //----

            if (CurrentAdder == null)
            {
                Log.Error("CombatExtended :: CurrentAdder is NULL within TryFeed, this should not happen (did PostFire already destroy CurrentAdder?)");
                return false;
            }

            //Get current adder's charges added -- this is called on PostFire, so CurrentAdder will be the one that was used to fire CurrentUser
            if (!CurrentLink.CanAdd(CurrentAdder.def, out var charge))
            {
                Log.Error("CombatExtended :: CurrentAdder ("+CurrentAdder.LabelCap+") cannot be loaded into CompAmmoUser's CurrentLink ("+CurrentLink.labelCap+", set="+Props.ammoSet.defName+")");
                return false;
            }

            //CurrentAdder does NOT need to be fed (= destroyed), still has charges remaining
            if (currentAdderCharge > -charge)
                return true;

            // ------------
            // Big-ass feeding loop (HasMagazine OR !HasMagazine)
            // ------------
            //Only feed (= destroy CurrentAdder) when the current adder is fully depleted (deficit >= full charge)
            while (currentAdderCharge < 0)
            {
                //The CurrentAdder is fully depleted (either the magazine is empty, or the inventory-based adder is depleted) -- reload
                if (CurrentAdder == null)
                    return false;

                //To get the charges in the CurrentAdder. If false, something strange is going on in the adders list (shouldn't happen)
                if (CurrentLink.CanAdd(CurrentAdder.def, out var newChargeCount))
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
                    //If the amountDepleted is zero, this can be because allowUnderflow is false -- which is as-designed. No error state, but incomplete feeding
                    else
                        break;
                }
                //No new currentAdder found.. time for reloading
                else
                {
                    Log.Error("CombatExtended :: CurrentAdder (" + CurrentAdder.LabelCap + ") cannot be loaded into CompAmmoUser's CurrentLink (" + CurrentLink.labelCap + ", set=" + Props.ammoSet.defName + ")");
                    return false;   //Something strange going on in the magazine: CurrentAdder is not loadable
                }
            }

            return currentAdderCharge >= 0;
        }
        #endregion

        #region Reloading
        // Verb_LaunchProjectileCE (Available, TryCastShot)
        // Verb (TryStartCastOn)
        // CompAmmo.PostFire, Verb.TryStartCastOn (harmony), Verb_LaunchProjectile.Available and .TryCastShot
        // CompAmmo GizmoExtras: Started by player
        // JobGiver_CheckReload (TryGiveJob)
        /// <summary>Return whether a reload job should start</summary>
        /// <returns></returns>
        public bool PreReload(AmmoLink link = null)
        {
            PrintDebug("PreReload-Pre");   //ASDF Remove
            
            #region Checks
            if (!HasMagazine)
            {
                if (!CanBeFiredNow)     //E.g UseAmmo (bow) && !HasAmmoForCurrentLink 
                    DoOutOfAmmoAction();

                return false;
            }

            if (Wielder == null && turret == null)
                return false;

            // secondary branch for if we ended up being called up by a turret somehow...
            if (turret != null)
            {
                turret.TryOrderReload();
                return false;
            }

            // R&G compatibility, prevents an initial attempt to reload while moving
            if (Wielder.stances.curStance.GetType() == rgStance)
                return false;
            #endregion

            /*
            if (comp.UseAmmo && !LinksMatch && !comp.SwitchLink(link, false))
                return null;
            */

            if (UseAmmo)
            {
                if (UnloadCanImproveMagazine())
                    TryUnload(0, false, true);

                //First, automatically unload if appropriate due to switched links
                if (LinksMatch || !SwitchLink(link, false, forcedLinkSelect)) //sets link
                {
                    //Second, see whether any magazine changes would be favourable
                    if (UnloadCanImproveMagazine())
                    {
                        //ASDF: Add out-var int indicating amount of ammo to reload to
                        TryUnload(0, false, true);
                    }
                }

                // Check for ammo
                if (Wielder != null && !HasAmmoForLink(CurrentLink)
                    && !TryFindAmmoInInventory(CompInventory, out var _, false, true))  //Switch to other ammo if current is gone
                {
                    DoOutOfAmmoAction();
                    return false;
                }
            }

            PrintDebug("PreReload-Post");   //ASDF Remove

            //Because reloadOneAtATime weapons don't dump their mag at the start of a reload, have to stop the reloading process here if the mag was already full
            if (Props.reloadOneAtATime && UseAmmo && LinksMatch && CurMagCount == Props.magazineSize)
                return false;

            return true;
        }

        // really only used by pawns (JobDriver_Reload) at this point... TODO: Finish making sure this is only used by pawns and fix up the error checking.
        /// <summary>
        /// Overrides a Pawn's current activities to start reloading a gun or turret.  Has a code path to resume the interrupted job.
        /// </summary>
        public void TryStartReload()
        {
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

        bool UnloadCanImproveMagazine()
        {
            Log.Message("R");

            //Try to switch to SelectedLink
            if (!LinksMatch && SwitchLink(null, true, forcedLinkSelect)) //switches to SelLink
                return true;

            Log.Message("S");

            //Check for case X % MagSize != 0
            //Check if any change in reloading could fix CurrentLink's magazine being partially filled

            //Problem to solve: c1 * [0, .., X] + c2 * [0, .., Y] + c3 * [0, .., Z] + .. = m
            var m = Props.magazineSize - CurMagCount;

            if (m <= 0)
                return false;

            Log.Message("T");

            // Find inventory ammo small enough to resolve deficit
            var candidates = CompInventory?.ammoList?.Where(x => CurrentLink.CanAdd(x.def, out var cpu) && cpu <= m);

            //1. If no array, or [c1, c2, .., cn] > m -- cannot fill
            if (!candidates.Any())
            {
                //1.1. Test if unloading any n adders would help reach m ASDF TODO


                return false;
            }

            Log.Message("U");

            // Test using overflow/underflow logic
            if (CurrentAdder != null)
            {
                var amountDepleted = CurrentLink.AmountForDeficit(CurrentAdder, Props.magazineSize - CurChargeCount, true);
                if (amountDepleted <= 0)
                    return false;
            }

            Log.Message("W");

            return true;
        }

        void ImproveMagazine()
        {
            //1. If c1 * X + c2 * Y + c3 * Z + .. < m, load everything


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
            if (!LinksMatch)
            {
                ammoThing = SelectedLink.BestAdder(inventory.ammoList, this, out var _, largestStack);

                if (ammoThing != null)
                    return true;
            }
            
            // Otherwise find ammo for currently set link
            ammoThing = CurrentLink.BestAdder(inventory.ammoList, this, out var count, largestStack, false);

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
                    if (i == selectedLinkInt || i == currentLinkInt)
                        continue;
                    
                    ammoThing = Props.ammoSet.ammoTypes[i].BestAdder(CompInventory.ammoList, this, out var _, largestStack);

                    if (ammoThing != null)
                        return SwitchLink(Props.ammoSet.ammoTypes[i], false, forcedLinkSelect); //sets link
                }
            }
            return false;
        }

        private void DoOutOfAmmoAction()
        {
            if (ShouldThrowMote)
            {
                MoteMaker.ThrowText(Position.ToVector3Shifted(), Find.CurrentMap, "CE_OutOfAmmo".Translate() + "!");
            }
            if (Wielder != null && CompInventory != null && (Wielder.CurJob == null || Wielder.CurJob.def != JobDefOf.Hunt)) CompInventory.SwitchToNextViableWeapon();
        }
        #endregion

        #region Adding, Deleting, Switching ammo
        public bool SwitchLink(AmmoLink newLink, bool forced = false, bool playerForced = false)
        {
            this.PrintDebug("SwitchLink-Pre");
            //If a new link is supplied, try setting it (if possible)
            if (newLink != null)
            {
                Log.Message("1a");
                if (!Props.ammoSet.ammoTypes.Contains(newLink))
                    return false;

                var indexOf = Props.ammoSet.ammoTypes.IndexOf(newLink);

                Log.Message("1b");
                if (!forced && indexOf == selectedLinkInt)
                    return false;

                Log.Message("1c");
                selectedLinkInt = indexOf; //change
                forcedLinkSelect = playerForced;
                this.PrintDebug("SwitchLink-Change");

                //If the links already match, no need to switch at all
                if (LinksMatch)
                    return true;
            }

            Log.Message("3");

            //If no charges are loaded, simple and guaranteed switch
            if ((CurChargeCount == 0 && currentAdderCharge == 0))
            {
                //Allow for switching links
                currentLinkInt = selectedLinkInt; //matching
                forcedLinkSelect = false;
                this.PrintDebug("SwitchLink-Change2");
                return true;
            }

            Log.Message("4");
            //Check whether all currently loaded charges fit in the other link ..
            int magDifference = 0;
            bool canMatch = adders.All(x => {
                if (SelectedLink.CanAdd(x.def, out var cpuNew) && CurrentLink.CanAdd(x.def, out var cpuCur))
                {
                    magDifference += x.stackCount * (cpuNew - cpuCur);
                    return true;
                }
                else
                    return false;
            });

            Log.Message("5");
            //.. and whether the difference in charges loaded by the adders in both links is acceptable
            if (canMatch)
            {
                //Mag difference is small, or Unload adders until magDifference is relieved and retry
                if (curMagCountInt + magDifference <= Props.magazineSize
                    || (forced && TryUnload(Props.magazineSize - magDifference, true, false)))
                {
                    currentLinkInt = selectedLinkInt; //matching
                    forcedLinkSelect = false;
                    this.PrintDebug("SwitchLink-Change3");

                    //Recalculate curMagCountInt
                    curMagCountInt = adders.Sum(x => CurrentLink.CanAdd(x.def, out var cpu) ? cpu : 0);
                    
                    return true;
                }
            }

            Log.Message("6");

            //TODO: Switch out the cartridges that cannot be loaded in the other link -- if possible

            //Otherwise... no simple conversion possible. Need to unload fully
            if (!LinksMatch && forced && TryUnload(0, false, true))
            {
                //Call this method again

                currentLinkInt = selectedLinkInt; //matching
                forcedLinkSelect = false;
                this.PrintDebug("SwitchLink-Change4");

                return true;
            }

            Log.Message("7");

            //ASDF --> Convert to non-instant ammo change when any adders are present in magazine, when cAC is negative or positive..
            //ASDF: Try and reload

            //if (!HasMagazine)
            //    currentLinkInt = selectedLinkInt;

            if (forced)
            {
                Log.Error("Couldn't switch links even when forced, see state:");
                PrintDebug("SwitchLink-ErrorState");
            }

            return false;
        }

        public void AddAdder(Thing inThing, bool toSpentAdders = false, bool updateInventory = true)
        {
            if (inThing == null)
                return;

            Thing thing = null;
            if (!toSpentAdders)
            {
                //Add appropriate number of charges (and check for all limitations etc. set out by the ammoSetDef)
                curMagCountInt += SelectedLink.LoadThing(inThing, this, out var count);

                //ASDF: This part might cause issues too?
                if (count <= 0)
                    return;

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

        /*public void DeleteAdder(Thing inThing, bool fromAdders = true, bool updateInventory = true)
          {
              if (inThing == null)
                  return;

              if (fromAdders && CurrentLink.CanAdd(inThing.def, out var cpu))
                  curMagCountInt -= cpu * inThing.stackCount;

              (fromAdders ? adders : spentAdders).Remove(inThing);
              inThing.Destroy();

              if (updateInventory)
                  CompInventory?.UpdateInventory();
          }*/
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

        /// <summary>Unloads adder according to CURRENTLINK</summary>
        /// <param name="thing"></param>
        /// <param name="isSpent"></param>
        /// <param name="forUnloading">True: Check against max. charge contained in adder; False: Check against 0</param>
        void UnloadAdder(Thing thing = null, bool isSpent = false, bool forUnloading = false)
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
            {
                PrintDebug("UnloadAdder-NullThing");   //ASDF Remove
                return;
            }

            //Handle currently-used adder ----> This is checked for SelectedLink instead...
            var spentThing = CurrentLink.UnloadAdder(thing, this, ref isSpent, forUnloading);

            //spentThing is any of:
            // - thing
            // - thing, but isSpent == true
            // - null
            // - a spent thing w/ stackCount = thing.stackCount

            //Cannot be null, since thing != null
            if (spentThing == thing)
            {
                //Return to where it came from (adders); AND take cpu for later use
                if (CurrentLink.CanAdd(thing.def, out var cpu) && !isSpent)
                {
                    PrintDebug("UnloadAdder-Returned");   //ASDF Remove
                    return;
                }

                //Split off one and add to spentAdders
                if (isCurrentAdder && thing.stackCount > 1)
                {
                    spentThing = thing.SplitOff(1);
                }
                else
                    adders.Remove(spentThing);

                if (isCurrentAdder)
                    curMagCountInt -= cpu;

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

                PrintDebug("UnloadAdder-Destroy");   //ASDF Remove

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
        
        // JobGiver_CheckReload (TryGiveJob part), CompAmmo (TryStartReload, SwitchLink), Command_Reload (Unload command)
        /// <summary>
        /// Used to unload the weapon.  Ammo will be dumped to the unloading Pawn's inventory or the ground if insufficient space.  Any ammo that can't be dropped
        /// on the ground is destroyed (with a warning).
        /// </summary>
        /// <returns>bool, true indicates the weapon was already in an unloaded state or the unload was successful.  False indicates an error state.</returns>
        /// <remarks>
        /// Failure to unload occurs if the weapon doesn't use a magazine.
        /// </remarks>
        public bool TryUnload(int toAmount = 0, bool forceUnload = false, bool includingSpent = false)
        {
            return TryUnload(out var _, toAmount, forceUnload, false, includingSpent);
        }
        
        //JobDriver_ReloadTurret, Harmony-Thing (smeltProducts), JobGiver_CheckReload, CompAmmoUser (SwitchLink, TryStartReload), Command_Reload (Unload command)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="droppedAmmo"></param>
        /// <param name="forceUnload"></param>
        /// <param name="convertAllToThingList"></param>
        /// <returns>Whether we're in "a bad state", e.g unloading failed</returns>
        public bool TryUnload(out List<Thing> droppedAmmo, int toAmount = 0, bool forceUnload = false, bool convertAllToThingList = false, bool includingSpent = false)
        {
            droppedAmmo = null;

            // HasMagazine  UseAmmo
            // HasMagazine !UseAmmo
            //!HasMagazine  UseAmmo --> shouldn't be here
            //!HasMagazine !UseAmmo --> shouldn't be here

            #region Checks
            if (!HasMagazine || (Holder == null && turret == null && !convertAllToThingList))
                    return false; // nothing to do as we are in a bad state;

                if (!UseAmmo)
                    return true; // nothing to do but we aren't in a bad state either.  Claim success.
                
                //For reloadOneAtATime weapons that haven't been explicitly told to unload, and aren't switching their ammo type, skip unloading.
                //The big advantage of a shotguns' reload mechanism is that you can add more shells without unloading the already loaded ones.
                if (Props.reloadOneAtATime && !forceUnload && !convertAllToThingList && LinksMatch && turret == null)
                    return true;

            #endregion

            //-- -- Add remaining ammo back in the inventory -- --
            //Returns current adder to adders or to spentAdders
            //ASDF: Should be used for the CURRENT LINK. Errors happen due to SELECTED LINK being used
            UnloadAdder();

            PrintDebug("TryUnload-Pre");   //ASDF Remove

            droppedAmmo = new List<Thing>();

            //---------------
            // Unload all spent adders
            //---------------

            if (includingSpent && !spentAdders.NullOrEmpty())
            {
                //Destructive, backwards iteration of spentAdders
                for (int j = spentAdders.Count - 1; j > -1; j--)
                {
                    var thing = spentAdders[j];

                    if (thing == null)
                    {
                        spentAdders.RemoveAt(j);
                        continue;
                    }

                    //Just destroy completely
                    if (CurrentLink.IsSpentAdder(thing.def))
                    {
                        thing.Destroy();
                        spentAdders.RemoveAt(j);
                        continue;
                    }

                    if (convertAllToThingList)
                    {
                        droppedAmmo.Add(thing);
                        spentAdders.RemoveAt(j);
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
                    spentAdders.RemoveAt(j);
                }
            }

            //---------------
            // Unload all adders
            //---------------

            //Spent adders should still be unloaded, but not all of them all the time...

            // Adders are fine
            if (curMagCountInt == toAmount && currentAdderCharge == 0)
            {
                CompInventory?.UpdateInventory();
                return true;
            }

            var i = adders.Count - 1;
            bool carefulUnload = true;
            while (CurMagCount > toAmount)
            {
                if (i < 0)
                {
                    i = adders.Count - 1;

                    //Two iterations attempted, break
                    if (!carefulUnload)
                    {
                        Log.Error("Stopped TryUnload: could not unload, with adders remaining: "
                            + string.Join(",", adders.Select((x,y) => "["+y+"]"+x.def.defName+"="+x.stackCount).ToArray()));
                        break;
                    }

                    carefulUnload = false;
                }
                Log.Message("II");
                
                var thing = (i < adders.Count) ? adders[i] : null;

                if (thing == null)
                {
                    i--;
                    continue;
                }
                Log.Message("III");

                var amountForDeficit = CurrentLink.AmountForDeficit(thing, CurMagCount - toAmount, false, carefulUnload);

                if (amountForDeficit <= 0)
                {
                    i--;
                    continue;
                }
                Log.Message("IV");

                //Handle remaining magcount from current adders
                if (CurrentLink.CanAdd(thing.def, out var cpu))
                    curMagCountInt -= cpu * amountForDeficit;

                Thing newThing = null;

                //Part of stack must be removed
                if (amountForDeficit < thing.stackCount && thing.stackCount > 1)
                {
                    //Allows partial removal of Thing in one iteration (toAmount small)
                    newThing = thing.SplitOff(amountForDeficit);
                }
                //Whole stack must be removed
                else
                {
                    adders.RemoveAt(i);
                    newThing = thing;
                }

                if (convertAllToThingList)
                {
                    droppedAmmo.Add(newThing);
                    i--;
                    continue;
                }

                Log.Message("V");

                var prevCount = newThing.stackCount;

                // Can't store ammo       || Inventory can't hold ALL ammo ...
                if (CompInventory == null || prevCount != CompInventory.container.TryAdd(newThing, newThing.stackCount))
                {
                    //.. then, drop remainder

                    // NOTE: If we get here from ThingContainer.TryAdd() it will have modified the ammoThing.stackCount to what it couldn't take.
                    if (GenThing.TryDropAndSetForbidden(newThing, Position, Map, ThingPlaceMode.Near, out var droppedUnusedAmmo, turret.Faction != Faction.OfPlayer))
                        droppedAmmo.Add(droppedUnusedAmmo);
                    else
                    {
                        Log.Warning(String.Concat(this.GetType().Assembly.GetName().Name + " :: " + this.GetType().Name + " :: ",
                                                    "Unable to drop ", newThing.LabelCap, " on the ground, thing was destroyed."));
                    }
                }
                Log.Message("VI");

                i--;
            }

            // Update inventory
            CompInventory?.UpdateInventory();

            return true;
        }
        #endregion

        #region Initialization
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
                    selectedLinkInt = 0; //initialization

                    /*if (currentAmmoInt == null)
                          currentAmmoInt = (AmmoDef)Props.ammoSet.ammoTypes[0].adders.MinBy(x => x.count).thingDef;
                      if (selectedAmmo == null)
                          selectedAmmo = currentAmmoInt;*/
                }
            }
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
                selectedLinkInt = currentLinkInt; //initialization
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
        #endregion

        #region Comp-related
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            GizmoAmmoStatus ammoStatusGizmo = new GizmoAmmoStatus { compAmmo = this };
            yield return ammoStatusGizmo;

            if ((Wielder != null && Wielder.Faction == Faction.OfPlayer) || (turret != null && turret.Faction == Faction.OfPlayer && (turret.MannableComp != null || UseAmmo)))
            {
                Action action = null;
                if (Wielder != null) action = delegate { if (PreReload()) TryStartReload(); };
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

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref curMagCountInt, "count", 0);
            Scribe_Values.Look(ref currentAdderCharge, "currentAdderCharge", 0);

            Scribe_Values.Look<int>(ref currentLinkInt, "currentLinkInt", 0);
            Scribe_Values.Look<int>(ref selectedLinkInt, "selectedLinkInt", currentLinkInt);
            if (currentLinkInt > Props.ammoSet.ammoTypes.Count) currentLinkInt = 0;
            if (selectedLinkInt > Props.ammoSet.ammoTypes.Count) selectedLinkInt = 0;

            Scribe_Collections.Look<Thing>(ref adders, "adders");
            Scribe_Collections.Look<Thing>(ref spentAdders, "spentAdders");

            ejectsCasings = parent.def.Verbs.Select(x => x as VerbPropertiesCE).First()?.ejectsCasings ?? true;
        }

        public override string TransformLabel(string label)
        {
            string ammoSet = UseAmmo && Controller.settings.ShowCaliberOnGuns ? " (" + Props.ammoSet.LabelCap + ") " : "";
            return label + ammoSet;
        }
        #endregion

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
