using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using RimWorld;
using Verse;


/*
 * Concept: Since we aren't using xml to store the data generate a generic for the following:
 * -for each gun in CombatExtended (hopefully this will pick up any guns someone adds in another mod)
 * -for Meals
 * -for Raw foodstuff
 * -for Drugs.
 * 
 * Additionally, effort should be taken so that Generics do NOT overlap as the code won't handle that case well (and isn't an easy case).
 */

namespace CombatExtended
{
	[StaticConstructorOnStartup]
	public class LoadoutGenericDef : Verse.Def
	{
		#region Fields
		public LoadoutCountType defaultCountType = LoadoutCountType.dropExcess; // default: drop anything more than (default)Count.
		public int defaultCount = 1;
		private Predicate<ThingDef> _lambda;
		private float _bulk;
		private float _mass;
		private bool _cachedVars = false;
		#endregion
		
		#region Constructors
		static LoadoutGenericDef()
		{
			List<LoadoutGenericDef> defs = new List<LoadoutGenericDef>();
			// get the simple cases out of the way...
			LoadoutGenericDef generic = new LoadoutGenericDef();
			generic.defName = "GenericMeal";
			generic.description = "Generic Loadout for Meals.  Intended for compatibility with pawns automatically picking up a meal for themself.";
			generic.label = "Any Meal";
			generic._lambda = td => td.IsNutritionGivingIngestible && td.ingestible.preferability >= FoodPreferability.MealAwful && !td.IsDrug;
			
			defs.Add(generic);
			
			Log.Message(string.Concat("CombatExtended :: LoadoutGenericDef :: ", generic.LabelCap, " list: ", string.Join(", ", DefDatabase<ThingDef>.AllDefs.Where(t => generic.lambda(t)).Select(t => t.label).ToArray())));
			
			generic = new LoadoutGenericDef();
			generic.defName = "GenericRawFood";
			generic.description = "Generic Loadout for Raw Food.  Intended for compatibility with pawns automatically picking up raw food to train animals.";
			generic.label = "Any Raw Food";
			generic.defaultCount = 20; // not really sure what this should be so setting to 20.  Ideally would be a bit larger than typical pickup.
			generic._lambda = td => td.IsNutritionGivingIngestible && td.ingestible.preferability <= FoodPreferability.RawTasty && td.plant == null && !td.IsDrug && !td.IsCorpse;
			
			defs.Add(generic);
			
			Log.Message(string.Concat("CombatExtended :: LoadoutGenericDef :: ", generic.LabelCap, " list: ", string.Join(", ", DefDatabase<ThingDef>.AllDefs.Where(t => generic.lambda(t)).Select(t => t.label).ToArray())));
			
			generic = new LoadoutGenericDef();
			generic.defName = "GenericDrugs";
			generic.description = "Generic Loadout for Drugs.  Intended for compatibility with pawns automatically picking up drugs in compliance with drug policies.";
			generic.label = "Any Drugs";
			// not really sure what defaultCount should be so leaving unset.
			generic._lambda = td => td.IsDrug;
			
			defs.Add(generic);
			
			Log.Message(string.Concat("CombatExtended :: LoadoutGenericDef :: ", generic.LabelCap, " list: ", string.Join(", ", DefDatabase<ThingDef>.AllDefs.Where(t => generic.lambda(t)).Select(t => t.label).ToArray())));
			
			// now for the guns and ammo...
			
			// Get a list of guns that are player acquireable (not menuHidden but could also go with not dropOnDeath) which are ammo users with the CE verb.
			List<ThingDef> guns = DefDatabase<ThingDef>.AllDefs.Where(td => !td.menuHidden &&
			                                                          td.HasComp(typeof(CompAmmoUser)) && 
			                                                          td.Verbs.FirstOrDefault(v => v is VerbPropertiesCE) != null).ToList();
			
			foreach (ThingDef gun in guns)
			{
				// make sure the gun has ammo defined...
				if (gun.GetCompProperties<CompProperties_AmmoUser>().ammoSet.ammoTypes.Count <= 0)
					continue;
				generic = new LoadoutGenericDef();
				generic.defName = "GenericAmmo-" + gun.defName;
				generic.description = "Generic Loadout ammo for " + gun.LabelCap + ". Intended for generic collection of ammo for given gun.";
				generic.label = "Any Ammo for " + gun.LabelCap;
				//Consider all ammos that the gun can fire, take the average.  Could also use the min or max...
				generic.defaultCount = Convert.ToInt32(Math.Floor(DefDatabase<AmmoDef>.AllDefs
				                                                  .Where(ad => gun.GetCompProperties<CompProperties_AmmoUser>().ammoSet.ammoTypes.Contains(ad))
				                                                  .Average(ad => ad.defaultAmmoCount)));
				generic.defaultCountType = LoadoutCountType.pickupDrop; // we want ammo to get picked up.
				generic._lambda = td => td is AmmoDef && gun.GetCompProperties<CompProperties_AmmoUser>().ammoSet.ammoTypes.Contains(td);
				defs.Add(generic);
			}
			
			DefDatabase<LoadoutGenericDef>.Add(defs);
		}
		
		#endregion Constructors
		
		#region Properties
		
		public Predicate<ThingDef> lambda { get { return _lambda; } }
		
		// Since bulk/mass are per def and not per Loadout put them as part of the def even though they are determined programatically.
		public float bulk
		{
			get
			{
				if (!_cachedVars)
					updateVars();
				return _bulk;
			}
		}
		
		public float mass
		{
			get
			{
				if (!_cachedVars)
					updateVars();
				return _mass;
			}
		}
		#endregion Properties
		
		#region Methods
		
		// Handles updating the bulk/mass update.  Basically find the heaviest thing that matches the lambda, since that could be expensive the result is cached.
		// The cache is not saved so that if a variable changes between save->load new info will be used.
		private void updateVars()
		{
			IEnumerable<ThingDef> matches;
			matches = DefDatabase<ThingDef>.AllDefs.Where(td => lambda(td));
        	_bulk = matches.Max(t => t.GetStatValueAbstract(CE_StatDefOf.Bulk));
        	_mass = matches.Max(t => t.GetStatValueAbstract(StatDefOf.Mass));
        	_cachedVars = true;
		}
		
		#endregion Methods
	}
}