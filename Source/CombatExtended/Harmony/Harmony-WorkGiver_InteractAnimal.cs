using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using Harmony;

namespace CombatExtended.Harmony
{
    [HarmonyPatch(typeof(WorkGiver_HunterHunt), "TakeFoodForAnimalInteractJob")]
    public class Harmony_WorkGiver_InteractAnimal_TakeFoodForAnimalInteractJob_Patch
    {
        public static void Postfix(WorkGiver_InteractAnimal __instance, ref Job __result, Pawn pawn, Pawn tamee)
        {
            if (__result != null)
            {
                // Check for inventory space
                int numToCarry = __result.count;
                CompInventory inventory = pawn.TryGetComp<CompInventory>();
                if (inventory != null)
                {
                    int maxCount;
                    if (inventory.CanFitInInventory(__result.targetA.Thing, out maxCount))
                    {
                        __result.count = Mathf.Min(numToCarry, maxCount);
                    }
                    else
                    {
                        Messages.Message("CE_TamerInventoryFull".Translate(), pawn, MessageSound.RejectInput);
                        __result = null;
                    }
                }
            }
        }
    }
}
