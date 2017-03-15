

using System.Collections.Generic;
using System.Linq;
using Verse;
namespace CombatExtended
{
	// HoldTracker handles remembering what a pawn should hold onto (when a pawn was forced to pick something up)
	// HoldTracker should ALWAYS be consulted when considering dropping an item.
	static class HoldTracker
	{
		//static readonly Dictionary<Pawn, TrackThing> tracker = new Dictionary<Pawn, TrackThing>();
		
        // The following method should end up being moved as part of the HoldTracker concept.
        /*
         * Concept: This handles finding an item in the inventory that should be dropped... should be expanded to handle HoldTracker too.
         * 
         * Need to consider both Specific and Generic loadoutslots.  In this case if the sum of specifics are larger than the generic count we keep.
         * Since this handles dropping and both (current) LoadoutCountTypes determine dropping checking mode is pointless.
         * 
         * Need a system similar to pickup in that Specifics which are covered by a Generic should be considered first... otherwise follow the order.
         * 
         * We use a bool to determine if there is work to do and output variables to store what was found to drop and how many.
         * Use an overload to basically just get a bool back.
         * 
         * May eventually want to spit out two lists (of Things and ints) or a dictionary<Thing,int> if we want to keep loadout code separate from HoldTracker.
         */ 
        static public bool HasExcessItem(this Pawn pawn)
        {
        	Thing ignore1;
        	int ignore2;
        	bool ignore3;
        	return GetExcessItem(pawn, out ignore1, out ignore2, out ignore3);
        }
        
        /*
         * Pickup is easy... drop is hard.  To drop a Thing I need to exhause both a loadout that specifies that thing as well as any Generics that cover it.
         * ... I can search loadout for a thingDef that matches the current considered Thing in inventory and then query all generic slots to see if they take
         *     ownership.
         * 
         * When refactoring for HoldTracker I haven't decided if I need a list or if this is sufficient...
         */
        static public bool GetExcessItem(this Pawn pawn, out Thing dropThing, out int dropCount, out bool isEquipment)
        {
        	int largestCount = 0;
        	CompInventory inventory = pawn.TryGetComp<CompInventory>();
        	Loadout loadout = pawn.GetLoadout();
        	dropThing = null;
        	dropCount = 0;
        	isEquipment = false;
        	
        	if (inventory == null || inventory.container == null || loadout == null || loadout.Slots.NullOrEmpty())
        		return false;
        	
        	// equipment needs to be handled differently...  Mostly it's a case that if the pawn has something equipped and a loadout doesn't cover it, drop it.
        	if (pawn.equipment?.Primary != null)
        	{
	        	LoadoutSlot eqSlot = loadout.Slots.FirstOrDefault(s => s.count >= 1 && ((s.thingDef != null && s.thingDef == pawn.equipment.Primary.def) 
        		                                                                        || (s.genericDef != null && s.genericDef.lambda(pawn.equipment.Primary.def))));
        		if (eqSlot == null)
        		{
        			isEquipment = true;
        			dropCount = 1; // ignored but set just in case someone uses it.
        			dropThing = pawn.equipment.Primary;
        		}
        	}
        	
        	if (!isEquipment)
        	{
	        	// Consider all Things in inventory.
	        	foreach (Thing thing in inventory.container)
	        	{
	        		int numContained = inventory.container.TotalStackCountOfDef(thing.def);
	        		
	        		// include equipment in the count so we know how much we have.
	    			if (pawn.equipment?.Primary?.def == thing.def)
	    				numContained++;
	        		
	        		// First see if we have a Specific slot which targets this Thing.
	        		LoadoutSlot specific = loadout.Slots.FirstOrDefault(x => x.thingDef == thing.def);
	        		if (specific != null)
	        			largestCount = specific.count;
	        		
	        		// now lets look at generics...
	        		foreach (LoadoutSlot generic in loadout.Slots.Where(s => s.genericDef != null && s.genericDef.lambda(thing.def)))
	        		{
	        			if (generic.count > largestCount)
	        				largestCount = generic.count;
	        		}
	        		
	        		// finally, determine how many to drop if any...
	        		if (numContained > largestCount)
	        		{
	        			dropCount = numContained - largestCount;
	        			dropThing = thing;
	        			break;
	        		}
	        		
	        		// and case where the item being considered isn't handled by any loadout.
	        		if (largestCount <= 0)
	        		{
	        			dropCount = numContained;
	        			dropThing = thing;
	        			break;
	        		}
	        	}
        	}
        	return (dropThing != null);
        }
	}
	
	/*
	 * Concept: Stores a Thing (or just the ThingID), an amount or count (how much to hold onto based on stack size picked up) and a timer.
	 * The timer (just the tick that the TrackThing was created) is because a pawn can be told to force carry something but they won't get it in
	 * their inventory instantly and periodically we scan the HoldTracker for things to remove from being held.  We need a timeout to prevent
	 * things from being removed too early.  Probably start with a day though realistically only the worst off pawn would fail to fetch something
	 * after 6 hours.
	 * 
	 */
	class TrackThing
	{
		
	}
}