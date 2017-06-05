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
    [HarmonyPatch(typeof(Verb), "CanHitFromCellIgnoringRange")]
    static class Harmony_Verb_LaunchProjectileCE
    {
        private static List<IntVec3> tempDestList = new List<IntVec3>();
        private static List<IntVec3> tempLeanShootSources = new List<IntVec3>();

        public static void Postfix(Verb __instance, ref bool __result, IntVec3 sourceCell, LocalTargetInfo targ, ref IntVec3 goodDest)
        {
            if(__instance is Verb_LaunchProjectileCE)
            {
                var verb = __instance as Verb_LaunchProjectileCE;
                if (targ.Thing != null)
                {
                    if (targ.Thing.Map != verb.caster.Map)
                    {
                        goodDest = IntVec3.Invalid;
                        __result = false;
                    }
                    ShootLeanUtility.CalcShootableCellsOf(tempDestList, targ.Thing);
                    for (int i = 0; i < tempDestList.Count; i++)
                    {
                        if (verb.CanHitCellFromCellIgnoringRange(sourceCell, tempDestList[i], targ.Thing, targ.Thing.def.Fillage == FillCategory.Full))
                        {
                            goodDest = tempDestList[i];
                            __result = true;
                        }
                    }
                }
                else if (verb.CanHitCellFromCellIgnoringRange(sourceCell, targ.Cell, targ.Thing))
                {
                    goodDest = targ.Cell;
                    __result = true;
                }
                goodDest = IntVec3.Invalid;
                __result = false;
            }
        }
    }
}
