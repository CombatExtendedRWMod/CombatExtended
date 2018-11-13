using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Harmony;
using Verse;
using RimWorld;
using UnityEngine;
using System.Reflection;

namespace CombatExtended.Harmony
{
	/* Dev Notes:
     * Goal is to change the RoundToInt into RoundToCeil.
     */

    [HarmonyPatch(typeof(TradeDeal), "UpdateCurrencyCount")]
    static class Harmony_TradeDeal_UpdateCurrencyCount
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Log.Warning("CE-DEBUG: Transpiler Harmony-TradeDeal started");
            MethodBase from = typeof(Mathf).GetMethod("RoundToInt", AccessTools.all);
            MethodBase to = typeof(Mathf).GetMethod("CeilToInt", AccessTools.all);
            return instructions.MethodReplacer(from, to);
        }
    }
}
