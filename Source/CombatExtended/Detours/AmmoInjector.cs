﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using RimWorld;
using Verse;
using UnityEngine;
using CombatExtended.Detours;

namespace CombatExtended
{
    [StaticConstructorOnStartup]
    internal static class AmmoInjector
    {
        public static readonly FieldInfo _allRecipesCached = typeof(ThingDef).GetField("allRecipesCached", BindingFlags.Instance | BindingFlags.NonPublic);
    	
        private const string enableTradeTag = "CE_AutoEnableTrade";             // The trade tag which designates ammo defs for being automatically switched to Tradeability.Stockable
        private const string enableCraftingTag = "CE_AutoEnableCrafting";        // The trade tag which designates ammo defs for having their crafting recipes automatically added to the crafting table
        private static ThingDef ammoCraftingStationInt;                         // The crafting station to which ammo will be automatically added
        private static ThingDef ammoCraftingStation
        {
            get
            {
                if (ammoCraftingStationInt == null)
                    ammoCraftingStationInt = ThingDef.Named("AmmoBench");
                return ammoCraftingStationInt;
            }
        }

        static AmmoInjector()
        {
            LongEventHandler.QueueLongEvent(Inject, "LibraryStartup", false, null);
        }

        public static void Inject()
        {
            if (InjectAmmos())
            {
            	Log.Message("Combat Realism :: Ammo " + (ModSettings.enableAmmoSystem ? "injected" : "removed"));
            }
            else
            {
            	Log.Error("Combat Realism :: Ammo injector failed to get injected");
            }
        }

        public static bool InjectAmmos()
        {
        	bool enabled = ModSettings.enableAmmoSystem;
            if (enabled)
            {
            	// Initialize list of all weapons
            	CE_Utility.allWeaponDefs.Clear();
            	
	            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
	            {
	                if (def.IsWeapon && (def.canBeSpawningInventory || def.tradeability == Tradeability.Stockable || def.weaponTags.Contains("TurretGun")))
	                    CE_Utility.allWeaponDefs.Add(def);
	            }
	            if (CE_Utility.allWeaponDefs.NullOrEmpty())
	            {
	                Log.Warning("CE Ammo Injector found no weapon defs");
	                return true;
	            }
            }
            else
            {
        		//If the ammo system is not enabled and it appears that there are no weaponDefs at all ..
            	if (CE_Utility.allWeaponDefs.NullOrEmpty())
            	{
            		//.. return out of the method early because nothing has to be reversed ..
            		return true;
            	}
            	//.. else, continue the method.
            }
            
            var ammoDefs = new HashSet<AmmoDef>();
            
            // Find all ammo using guns
            foreach (ThingDef weaponDef in CE_Utility.allWeaponDefs)
            {
                CompProperties_AmmoUser props = weaponDef.GetCompProperties<CompProperties_AmmoUser>();
                if (props != null && props.ammoSet != null && !props.ammoSet.ammoTypes.NullOrEmpty())
                {
                	ammoDefs.UnionWith(props.ammoSet.ammoTypes.Where(x => x is AmmoDef).Select<ThingDef, AmmoDef>(x => x as AmmoDef));
                }
            }
            
            // Make sure to exclude all ammo things which double as weapons
			ammoDefs.RemoveWhere(CE_Utility.allWeaponDefs.Contains);
            
            bool canCraft = (ammoCraftingStation != null);
            
            if (!canCraft)
            {
            	Log.ErrorOnce("CE ammo injector crafting station is null", 84653201);
            }
            
            foreach (AmmoDef ammoDef in ammoDefs)
            {
            	// Toggle ammo visibility in the debug menu
                ammoDef.menuHidden = !enabled;
                ammoDef.destroyOnDrop = !enabled;
                
                // Toggle trading
                if (ammoDef.tradeTags.Contains(enableTradeTag))
                {
                	ammoDef.tradeability = enabled ? Tradeability.Stockable : Tradeability.Sellable;
                }
                
                // Toggle craftability
                if (canCraft && ammoDef.tradeTags.Contains(enableCraftingTag))
                {
                    RecipeDef recipe = DefDatabase<RecipeDef>.GetNamed(("Make" + ammoDef.defName), false);
                    if (recipe == null)
                    {
                        Log.Error("CE ammo injector found no recipe named Make" + ammoDef.defName);
                    }
                    else
                    {
                    	if (enabled)
                    	{
                    		recipe.recipeUsers.Add(ammoCraftingStation);
                    	}
                    	else
                    	{
                    		recipe.recipeUsers.RemoveAll(x => x.defName == ammoCraftingStation.defName);
                    	}
                    }
                }
            }
            
        	if (canCraft)
        	{
            	// Set ammoCraftingStation.AllRecipes to null so it will reset
				_allRecipesCached.SetValue(ammoCraftingStation, null);
				
				// Remove all bills which contain removed ammo types
				if (!enabled)
				{
					IEnumerable<Building> enumerable = Find.Maps.SelectMany(x => x.listerBuildings.AllBuildingsColonistOfDef(ammoCraftingStation));
					foreach (Building current in enumerable)
					{
						var billGiver = current as IBillGiver;
						if (billGiver != null)
						{
							for (int i = 0; i < billGiver.BillStack.Count; i++)
							{
								Bill bill = billGiver.BillStack[i];
								if (!ammoCraftingStation.AllRecipes.Exists(r => bill.recipe == r))
								{
									billGiver.BillStack.Delete(bill);
								}
							}
						}
					}
					
            		CE_Utility.allWeaponDefs.Clear();
				}
            }
            
            return true;
        }
    }
}
