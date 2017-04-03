using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

/* Concept:
 * to have the pawn check it's currently equipped and inventory weapons for if they need a reload.
 * Should also try and check if any weapons have ammo which doesn't have a loadout defined and reload.
 */

/* List of things that need to be checked:
 * Does any gun have a low ammo state?
 * -Do we have ammo in the inventory to fill it up?
 * Is any gun loaded with ammo that we don't have a loadout for?
 * -Do we have ammo in inventory for that gun and have a loadout?
 */

/* Rough Algorithm
 * Need Things so the collection of ammo users that use magazines.  Also need a collection of ammo (ThingDef is OK here).
 * For each weapon (that fits above),
 * -If we have no ammo in inventory that the gun is loaded with, check loadouts/holdtracker for a clip's worth of ammo that the gun contains.
 * --Find ammo the gun uses that we have a clip's worth in inventory (should check it against loadout/holdtracker as well)
 * -If weapon is low on ammo and we have enough in inventory to fill it up.
 * 
 * If either of the above are true, trigger a reload.
 */

namespace CombatExtended
{
	public class JobGiver_CheckReload : ThinkNode_JobGiver
	{
		#region Fields
		
		// lower priority = less likely to be done.  Need to give this a priority below JobGiver_Update Loadout's high priority setting.
		const float reloadPriority = 9.1f; // 9.2 is high priority, 3 is lower priority.
		
		#endregion Fields
		
		#region ThinkNode_JobGiver
		
		/// <summary>
		/// Informs rimworld how important this job is relative to other potential jobs for the pawn.
		/// </summary>
		/// <param name="pawn">Pawn that this job is to be considered.</param>
		/// <returns>float value indicating importance of the job.</returns>
		public override float GetPriority(Pawn pawn)
		{
			// need to generate the job here which involves replicating some code from TryStartReload...
			
			// this MUST not even try to do anything if the inventory state isn't in a good state.
			// There are 3 states that JobGiver_UpdateLoadout can end in and I need to know which of those 3 states are active.
			
			// would be nice to communicate with JobGiver_UpdateLoadout but it will suffice to set our priority below theirs.
			
			ThingWithComps ignore1;
			AmmoDef ignore2;
			if (!pawn.Drafted && DoReloadCheck(pawn, out ignore1, out ignore2))
				return 9.1f;
            
			return 0.0f;
		}
		
		/// <summary>
		/// If RimWorld decides the pawn should do this job, this is called to get the pawn working on it.
		/// </summary>
		/// <param name="pawn">Pawn that the job is supposed to take place on.</param>
		/// <returns>Job that the pawn is to be working on.</returns>
		protected override Job TryGiveJob(Pawn pawn)
		{
			// Basically a clone of CompAmmoUser.TryStartReload()
			// Ideally we would ask the comp for the job instead of letting the comp apply it to the pawn.
			
			ThingWithComps gun;
			AmmoDef ammo;
			Job reloadJob = null;
			
			if (DoReloadCheck(pawn, out gun, out ammo))
			{
				CompAmmoUser comp = gun.TryGetComp<CompAmmoUser>();
				CompInventory compInventory = pawn.TryGetComp<CompInventory>();
				// we relied on DoReloadCheck() to do error checking of many variables.
				
				// Add remaining ammo back to inventory
				if (comp.curMagCount > 0)
				{
					Thing ammoThing = ThingMaker.MakeThing(comp.currentAmmo);
					ammoThing.stackCount = comp.curMagCount;
					comp.TryStartReload(true); // comp.curMagCount = 0;
					int remaining = compInventory.container.TryAdd(ammoThing, ammoThing.stackCount);
					if (remaining > 0)
					{
						ammoThing.stackCount = remaining;
						Thing outThing = null;
						GenThing.TryDropAndSetForbidden(ammoThing, pawn.Position, Find.VisibleMap, ThingPlaceMode.Near, out outThing, pawn.Faction != Faction.OfPlayer);
					}
				}
				
				if (comp.currentAmmo != ammo)
				{
					comp.selectedAmmo = ammo;
				}
				
	            // Issue reload job
	            reloadJob = new Job(CE_JobDefOf.ReloadWeapon, pawn, gun);
			}
			return reloadJob;
		}
		
		#endregion
		
		#region Methods
		
		/// <summary>
		/// Check's the pawn's equipment and inventory for weapons that could use a reload.
		/// </summary>
		/// <param name="pawn">Pawn to check the equipment and inventory of.</param>
		/// <param name="reloadWeapon">Thing weapon which needs to be reloaded.</param>
		/// <param name="reloadAmmo">AmmoDef ammo to reload the gun with.</param>
		/// <returns>Bool value indicating if the job needs to be done.</returns>
		private bool DoReloadCheck(Pawn pawn, out ThingWithComps reloadWeapon, out AmmoDef reloadAmmo)
		{
			reloadWeapon = null;
			reloadAmmo = null;
			
			// First need to create the collections that will be searched.
			List<ThingWithComps> guns = new List<ThingWithComps>();
			List<AmmoDef> ammo = new List<AmmoDef>(); // may be unnecessary.  Should be able to ask the pawn's inventory for a thing of def.
			ThingContainer inventory = pawn.TryGetComp<CompInventory>()?.container;
			Loadout loadout = pawn.GetLoadout();
			CompAmmoUser tmpComp;
			
			if (inventory == null || loadout == null || loadout.Slots.NullOrEmpty())
				return false; // There isn't any work to do since the pawn doesn't have a CE Inventory or doesn't have a loadout/has an empty loadout.
			
			if ((tmpComp = pawn.equipment?.Primary?.TryGetComp<CompAmmoUser>()) != null && tmpComp.hasMagazine && tmpComp.useAmmo)
				guns.Add(pawn.equipment.Primary);
			
			foreach (Thing thing in pawn.inventory.innerContainer)
			{
				if (thing is ThingWithComps && (tmpComp = thing.TryGetComp<CompAmmoUser>()) != null && tmpComp.hasMagazine && tmpComp.useAmmo)
					guns.Add(thing as ThingWithComps);
			}
			
			if (guns.NullOrEmpty())
				return false; // There isn't any work to do since the pawn doesn't have any ammo using guns.
			
			// look at each gun...
			foreach (ThingWithComps gun in guns)
			{
				// Get key stats of the weapon.
				tmpComp = gun.TryGetComp<CompAmmoUser>();
				AmmoDef ammoType = tmpComp.currentAmmo;
				int ammoAmount = tmpComp.curMagCount;
				int magazineSize = tmpComp.Props.magazineSize;
				
				// Is the gun loaded with ammo not in a Loadout/HoldTracker?
				if (!TrackingSatisfied(pawn, ammoType, magazineSize))
				{
					// Do we have ammo in the inventory that the gun uses which satisfies requirements? (expensive)
					AmmoDef matchAmmo = tmpComp.Props.ammoSet.ammoTypes
						.Where(al => al.ammo != ammoType)
						.Select(al => al.ammo)
						.FirstOrDefault(ad => TrackingSatisfied(pawn, ad, magazineSize) 
						                && inventory.TotalStackCountOfDef(ad) >= magazineSize);
					
					if (matchAmmo != null)
					{
						reloadWeapon = gun;
						reloadAmmo = matchAmmo;
						return true;
					}
				}
				
				// Is the gun low on ammo?
				if (tmpComp.curMagCount < magazineSize)
				{
					// Do we have enough ammo in the inventory to top it off?
					if (inventory.TotalStackCountOfDef(ammoType) >= (magazineSize - tmpComp.curMagCount))
					{
						reloadWeapon = gun;
						reloadAmmo = ammoType;
						return true;
					} else {
						// There wasn't enough in the inventory to top it off.  At this point we know the loadout is satisfied for this ammo...
						// We could do a more strict check to see if the pawn's loadout is satisfied to pick up ammo and if not swap to another ammo...?
					}
				}
				
			}
			
			return false;
		}
		
		/// <summary>
		/// Basically counts the amount of ThingDef that a loadout can have.  Assumes Pawn has a Loadout (caller checked this already).
		/// </summary>
		/// <param name="pawn">Pawn who's loadout and holdtracker is to be inspected.</param>
		/// <param name = "def">ThingDef to match</param>
		/// <param name = "amount">int amount of ThingDef desired to be covered by Loadout/HoldTracker</param>
		/// <returns>int amount of that def that can be had.</returns>
		private bool TrackingSatisfied(Pawn pawn, ThingDef def, int amount)
		{
			// first check loadouts...
			Loadout loadout = pawn.GetLoadout();
			
			foreach (LoadoutSlot slot in loadout.Slots)
			{
				if (slot.thingDef != null)
				{
					if (slot.thingDef == def)
						amount -= slot.count;
				} else if (slot.genericDef != null)
				{
					if (slot.genericDef.lambda(def))
						amount -= slot.count;
				}
				if (amount <= 0)
					return true;
			}
			
			// if we got here, also check holdRecords.
			List<HoldRecord> records = pawn.GetHoldRecords();
			
			if (records.NullOrEmpty())
			{
				foreach (HoldRecord rec in records)
				{
					if (rec.thingDef == def)
						amount -= rec.count;
				}
			}
			
			return false;
		}
		
		#endregion Methods
	}
}