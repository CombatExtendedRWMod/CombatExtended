using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CombatExtended
{
    public class JobGiver_UpdateLoadout : ThinkNode_JobGiver
    {
        private enum ItemPriority : byte
        {
            None,
            Low,
            LowStock,
            Proximity
        }

        private const int proximitySearchRadius = 20;
        private const int maximumSearchRadius = 80;
        private const int ticksBeforeDropRaw = 40000;

        public override float GetPriority(Pawn pawn)
        {
        	if (pawn.HasExcessItem())
            {
                return 9.2f;
            }
            ItemPriority priority;
            Thing unused;
            int i;
			Pawn carriedBy;
            LoadoutSlot slot = GetPrioritySlot(pawn, out priority, out unused, out i, out carriedBy);
            if (slot == null)
            {
                return 0f;
            }
            if (priority == ItemPriority.Low) return 3f;

            TimeAssignmentDef assignment = (pawn.timetable != null) ? pawn.timetable.CurrentAssignment : TimeAssignmentDefOf.Anything;
            if (assignment == TimeAssignmentDefOf.Sleep) return 3f;

            return 9.2f;
        }

        /* GetPrioritySlot
         * Serves 2 purposes, 1: To figure out how important the job should be and 2: to figure out what the item is and how many to pickup.
         * To be specific, this is figuring out if something should be picked up and what to pickup.
         * 
         * With the addition of Generics, Specific (ThingDef) LoadoutSlots take precidence over Generic (LoadoutGenericDef) LoadoutSlots.
         * 
         * An easier route is to pickup all specifics first and where there is overlap we will often have things taken care of.
         * 
         * The better route is when we consider a Generic we should look to see if we have a Specific loadoutslot covering the same item.
		 * If there is a Specific fill it out instead of the Generic... we may end up coming back to Generic if there wasn't enough Thing picked up.
         */
        private LoadoutSlot GetPrioritySlot(Pawn pawn, out ItemPriority priority, out Thing closestThing, out int count, out Pawn carriedBy)
        {
            priority = ItemPriority.None;
            LoadoutSlot slot = null;
            closestThing = null;
            count = 0;
			carriedBy = null;
			List<LoadoutSlot> processed = new List<LoadoutSlot>();  // not sure if this is worth it, probably.

            CompInventory inventory = pawn.TryGetComp<CompInventory>();
            if (inventory != null && inventory.container != null)
            {
            	Loadout loadout = pawn.GetLoadout();
                if (loadout != null && !loadout.Slots.NullOrEmpty())
                {
                    foreach(LoadoutSlot curSlot in loadout.Slots)
                    {
                    	Thing curThing = null;
                    	ItemPriority curPriority = ItemPriority.None;
                    	Pawn curCarrier = null;
                    	
                    	if (curSlot.genericDef != null)
                    	{
                    		int genCount = 0;
                    		if (curSlot.countType == LoadoutCountType.dropExcess)
	                    		continue;	// Pickup code doesn't need to see if we need to pickup something that for a drop only slot.
                    		
                    		List<LoadoutSlot> specifics = loadout.Slots.Where(s => s.thingDef != null && curSlot.genericDef.lambda(s.thingDef)).ToList();
                    		foreach (LoadoutSlot specific in specifics)
                    		{
                    			// need to know position... if we did the specific already don't try again... if we haven't done it yet, do it now.
                    			if (loadout.Slots.IndexOf(specific) > loadout.Slots.IndexOf(curSlot))
                    			{
                    				// process the Specific
                    				FindPickup(pawn, inventory.container, curSlot, out curPriority, out curThing, out curCarrier);
                    				processed.Add(specific);
                    				if (curPriority > priority)
                    					break;
                    			}
                    			// find out how much the specific satisfies the generic count.
                    			genCount += inventory.container.TotalStackCountOfDef(specific.thingDef);
                    		}
                    		if (curPriority <= priority)
                    		{
	                    		if (genCount >= curSlot.count)
	                    			continue;
	                    		FindPickup(pawn, inventory.container, curSlot, out curPriority, out curThing, out curCarrier, curSlot.count);
                    		}
                    	} else { // if (curSlot.thingDef != null)
                    		if (processed.Contains(curSlot))
                    			continue;
                    		FindPickup(pawn, inventory.container, curSlot, out curPriority, out curThing, out curCarrier);
                    	}
                    	
                        if (curPriority > priority && curThing != null && inventory.CanFitInInventory(curThing, out count))
                        {
                            priority = curPriority;
                            slot = curSlot;
                            closestThing = curThing;
                            if (curCarrier != null)
                            	carriedBy = curCarrier;
                        }
                        if (priority >= ItemPriority.LowStock)
                        {
                            break;
                        }
                    }
                }
            }

            return slot;
        }
        
        private void FindPickup(Pawn pawn, ThingContainer container, LoadoutSlot curSlot, out ItemPriority curPriority, out Thing curThing, out Pawn curCarrier, int wantCount = -1)
        {
        	curPriority = ItemPriority.None;
        	curThing = null;
        	curCarrier = null;
        	wantCount = wantCount < 0 ? curSlot.count : wantCount;
        	
        	int numCarried = 0;
        	if (curSlot.genericDef != null)
        	{
        		// Get all things with matching defs ot the Generic lambda, extract the ThingDefs, only keep uniques, and sum the total carried of each def.
        		numCarried = container.Where(t => curSlot.genericDef.lambda(t.def)).Select<Thing, ThingDef>(t => t.def).Distinct()
        			.Sum(container.TotalStackCountOfDef);
        	} else {
	            numCarried = container.TotalStackCountOfDef(curSlot.thingDef);
        	}

            // Add currently equipped gun
            if (pawn.equipment != null && pawn.equipment.Primary != null)
            {
                if ((curSlot.thingDef != null && pawn.equipment.Primary.def == curSlot.thingDef) || 
                    (curSlot.genericDef != null && curSlot.genericDef.lambda(pawn.equipment.Primary.def)))
            		numCarried++;
            }
            Predicate<Thing> isFoodInPrison = (Thing t) => t.GetRoom().isPrisonCell && t.def.IsNutritionGivingIngestible && pawn.Faction.IsPlayer;
            if (numCarried < wantCount)
            {
            	// Hint: The following block defines how to find items... pay special attention to the Predicates below.
            	ThingRequest req;
            	if (curSlot.genericDef != null)
            		req = ThingRequest.ForGroup(ThingRequestGroup.HaulableEver);
            	else
            		req = curSlot.thingDef.Minifiable ? ThingRequest.ForGroup(ThingRequestGroup.MinifiedThing) : ThingRequest.ForDef(curSlot.thingDef);
            	Predicate<Thing> findItem;
            	if (curSlot.genericDef != null)
            		findItem = t => curSlot.genericDef.lambda(t.GetInnerIfMinified().def);
            	else
            		findItem = t => t.GetInnerIfMinified().def == curSlot.thingDef;
            	Predicate<Thing> search = t => findItem(t) && !t.IsForbidden(pawn) && pawn.CanReserve(t) && !isFoodInPrison(t);
            	
				// look for a thing near the pawn.
                curThing = GenClosest.ClosestThingReachable(
                    pawn.Position,
                    pawn.Map,
					req,
                    PathEndMode.ClosestTouch,
                    TraverseParms.For(pawn, Danger.None, TraverseMode.ByPawn),
                    proximitySearchRadius,
                    search);
                if (curThing != null) curPriority = ItemPriority.Proximity;
                else
                {
					// look for a thing basically anywhere on the map.
                    curThing = GenClosest.ClosestThingReachable(
                        pawn.Position, 
                        pawn.Map,
	                    req,
                        PathEndMode.ClosestTouch,
                        TraverseParms.For(pawn, Danger.None, TraverseMode.ByPawn),
                        maximumSearchRadius,
                        search);
					if (curThing == null && pawn.Map != null)
					{
						// look for a thing inside caravan pack animals and prisoners.  EXCLUDE other colonists to avoid looping state.
						List<Pawn> carriers = pawn.Map.mapPawns.AllPawns.Where(
							p => p.inventory.GetInnerContainer().Count > 0 && (p.RaceProps.packAnimal && p.Faction == pawn.Faction || p.IsPrisoner && p.HostFaction == pawn.Faction)).ToList();
						foreach (Pawn carrier in carriers)
						{
							Thing thing = carrier.inventory.GetInnerContainer().FirstOrDefault(t => findItem(t));
							if (thing != null)
							{
								curThing = thing;
								curCarrier = carrier;
								break;
							}
						}
					}
                    if (curThing != null)
                    {
                        if (!curSlot.thingDef.IsNutritionGivingIngestible && numCarried / wantCount <= 0.5f) curPriority = ItemPriority.LowStock;
                        else curPriority = ItemPriority.Low;
                    }
                }
            }
        }

        private bool CheckForExcessItems(Pawn pawn)
        {
            //if (pawn.CurJob != null && pawn.CurJob.def == JobDefOf.Tame) return false;
            CompInventory inventory = pawn.TryGetComp<CompInventory>();
            Loadout loadout = pawn.GetLoadout();
            if (inventory == null || inventory.container == null || loadout == null || loadout.Slots.NullOrEmpty())
            {
                return false;
            }
            if (inventory.container.Count > loadout.SlotCount + 1)
            {
                return true;
            }
            // Check to see if there is at least one loadout slot specifying currently equipped weapon
            ThingWithComps equipment = ((pawn.equipment == null) ? null : pawn.equipment.Primary) ?? null;
            if (equipment != null && !loadout.Slots.Any(slot => 
                                                        (slot.thingDef != null && slot.thingDef == equipment.GetInnerIfMinified().def && slot.count >= 1) ||
                                                        (slot.genericDef != null && slot.countType == LoadoutCountType.pickupDrop && slot.count >=1
                                                         && slot.genericDef.lambda(equipment.GetInnerIfMinified().def))))
            {
                return true;
            }

            // Go through each item in the inventory and see if its part of our loadout
            bool allowDropRaw = Find.TickManager.TicksGame > pawn.mindState?.lastInventoryRawFoodUseTick + ticksBeforeDropRaw;
            foreach (Thing thing in inventory.container)
            {
                if(allowDropRaw || !thing.def.IsNutritionGivingIngestible || thing.def.ingestible.preferability > FoodPreferability.RawTasty)
                {
                    //TODO: Incomplete handling.
                	LoadoutSlot slot = loadout.Slots.FirstOrDefault(x => x.thingDef == thing.def);
                    if (slot == null)
                    {
                        return true;
                    }
                    int numContained = inventory.container.TotalStackCountOfDef(thing.def);

                    // Add currently equipped gun
                    if (pawn.equipment != null && pawn.equipment.Primary != null)
                    {
                        //TODO: Incomplete handling.
                    	if (pawn.equipment.Primary.def == slot.thingDef)
                        {
                            numContained++;
                        }
                    }
                    //TODO: Incomplete handling.
                    if (slot.count < numContained)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            // Get inventory
            CompInventory inventory = pawn.TryGetComp<CompInventory>();
            if (inventory == null) return null;

            Loadout loadout = pawn.GetLoadout();
            if (loadout != null)
            {
            	Thing dropThing;
            	int dropCount;
            	bool isEquipment;
            	if (pawn.GetExcessItem(out dropThing, out dropCount, out isEquipment))
            	{
            		if (isEquipment)
            		{
	                    ThingWithComps droppedEq;
	                    if (pawn.equipment.TryDropEquipment(pawn.equipment.Primary, out droppedEq, pawn.Position, false))
	                    {
	                        return HaulAIUtility.HaulToStorageJob(pawn, droppedEq);
	                    }
            		} else {
	            		Thing droppedThing;
	            		if (inventory.container.TryDrop(dropThing, pawn.Position, pawn.Map, ThingPlaceMode.Near, dropCount, out droppedThing))
	            		{
	            			if (droppedThing != null)
	            			{
	            				return HaulAIUtility.HaulToStorageJob(pawn, droppedThing);
	            			}
	            			Log.Error(string.Concat(pawn, " tried dropping " + dropThing + " from loadout but resulting thing is null"));
	            		}
            		}
            	}
            	/*
				//new handling for dropping stuff in another file.  Keeping this as reference for now.

                // Find and drop excess items
                foreach (LoadoutSlot slot in loadout.Slots)
                {
                    //TODO: Incomplete handling.
                	int numContained = inventory.container.TotalStackCountOfDef(slot.thingDef);

                    // Add currently equipped gun
                    if (pawn.equipment != null && pawn.equipment.Primary != null)
                    {
                        //TODO: Incomplete handling.
                    	if (pawn.equipment.Primary.def == slot.thingDef)
                        {
                            numContained++;
                        }
                    }
                    // Drop excess items
                    if(numContained > slot.count)
                    {
                    	//TODO: Incomplete handling.
                    	Thing thing = inventory.container.FirstOrDefault(x => x.GetInnerIfMinified().def == slot.thingDef);
                        if (thing != null)
                        {
                            Thing droppedThing;
                            //TODO: Incomplete handling.
                            if (inventory.container.TryDrop(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near, numContained - slot.count, out droppedThing))
                            {
                                if (droppedThing != null)
                                {
                                    return HaulAIUtility.HaulToStorageJob(pawn, droppedThing);
                                }
                                Log.Error(pawn + " tried dropping " + thing + " from loadout but resulting thing is null");
                            }
                        }
                    }
                }

                // Try drop currently equipped weapon
                //TODO: Incomplete handling.
                if (pawn.equipment != null && pawn.equipment.Primary != null && !loadout.Slots.Any(slot => slot.thingDef == pawn.equipment.Primary.def && slot.count >= 1))
                {
                    ThingWithComps droppedEq;
                    if (pawn.equipment.TryDropEquipment(pawn.equipment.Primary, out droppedEq, pawn.Position, false))
                    {
                        return HaulAIUtility.HaulToStorageJob(pawn, droppedEq);
                    }
                }

                // Find excess items in inventory that are not part of our loadout
                bool allowDropRaw = Find.TickManager.TicksGame > pawn.mindState?.lastInventoryRawFoodUseTick + ticksBeforeDropRaw;
                //TODO: Incomplete handling.
                Thing thingToRemove = inventory.container.FirstOrDefault(t =>
                    (allowDropRaw || !t.def.IsNutritionGivingIngestible || t.def.ingestible.preferability > FoodPreferability.RawTasty)
                    && !loadout.Slots.Any(s => s.thingDef == t.GetInnerIfMinified().def));
                if (thingToRemove != null)
                {
                    Thing droppedThing;
                    if (inventory.container.TryDrop(thingToRemove, pawn.Position, pawn.Map, ThingPlaceMode.Near, thingToRemove.stackCount, out droppedThing))
                    {
                        return HaulAIUtility.HaulToStorageJob(pawn, droppedThing);
                    }
                    Log.Error(pawn + " tried dropping " + thingToRemove + " from inventory but resulting thing is null");
                }
*/

                // Find missing items
                ItemPriority priority;
                Thing closestThing;
                int count;
				Pawn carriedBy;
				bool doEquip = false;
                LoadoutSlot prioritySlot = GetPrioritySlot(pawn, out priority, out closestThing, out count, out carriedBy);
                // moved logic to detect if should equip vs put in inventory here...
                if (closestThing != null)
                {
                	if (closestThing.TryGetComp<CompEquippable>() != null
                        && (pawn.health != null && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                        && (pawn.equipment == null || pawn.equipment.Primary == null || !loadout.Slots.Any(s => s.thingDef == pawn.equipment.Primary.def
                                                                                                           || (s.genericDef != null && s.countType == LoadoutCountType.pickupDrop 
                                                                                                               && s.genericDef.lambda(pawn.equipment.Primary.def)))))
                		doEquip = true;
	                if (carriedBy == null)
	                {
	                    // Equip gun if unarmed or current gun is not in loadout
	                    if (doEquip)
	                    {
	                        return new Job(JobDefOf.Equip, closestThing);
	                    }
	                    // Take items into inventory if needed
	                    int numContained = inventory.container.TotalStackCountOfDef(closestThing.def);
	                    return new Job(JobDefOf.TakeInventory, closestThing) { count = Mathf.Min(closestThing.stackCount, prioritySlot.count - numContained, count) };
	                } else
	                {
	                	return new Job(CE_JobDefOf.TakeFromOther, closestThing, carriedBy, doEquip ? pawn : null) {
	                		//TODO: Incomplete handling.
	                		count = doEquip ? 1 : Mathf.Min(closestThing.stackCount, prioritySlot.count - inventory.container.TotalStackCountOfDef(closestThing.def), count)
	                	};
	                }
                }
            }
            return null;
        }
    }
}
