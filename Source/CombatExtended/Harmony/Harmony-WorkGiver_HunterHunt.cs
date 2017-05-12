using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;
using Harmony;

namespace CombatExtended.Harmony
{
    [HarmonyPatch(typeof(WorkGiver_HunterHunt), "HasHuntingWeapon")]
    public class Harmony_WorkGiver_HunterHunt_HasHuntingWeapon_Patch
    {
        public static void Postfix(WorkGiver_HunterHunt __instance, ref bool __result, Pawn p)
        {
            if (__result)
            {
                ThingWithComps eq = p.equipment.Primary;
                if (eq.def.IsRangedWeapon)
                {
                    CompAmmoUser comp = p.equipment.Primary.TryGetComp<CompAmmoUser>();
                    __result = comp == null || comp.canBeFiredNow || comp.hasAmmo;
                }
                // TODO Add conditional for allow melee hunting setting
                else if (eq.def.IsMeleeWeapon)
                {
                    __result = true;
                }
            }
        }
    }
}
