using System.Collections.Generic;
using System.Reflection;
using Harmony;
using RimWorld;
using Verse;

namespace CombatExtended.Harmony
{
    [HarmonyPatch(typeof(LordToil_Siege), "LordToilTick")]
    internal static class Harmony_LordToil_Siege
    {
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Log.Warning("CE-DEBUG: Transpiler Harmony-LordToil_Siege started, returning");
            return instructions.MethodReplacer(typeof(ThingDef).GetMethod("get_IsShell"),
                typeof(AmmoUtility).GetMethod(nameof(AmmoUtility.IsShell), BindingFlags.Public | BindingFlags.Static));
        }
    }
}
