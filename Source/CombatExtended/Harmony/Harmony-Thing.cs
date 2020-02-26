using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using Verse;

namespace CombatExtended.Harmony
{
    [HarmonyPatch(typeof(Thing), "SmeltProducts")]
    public class Harmony_Thing_SmeltProducts
    {
        public static void Postfix(Thing __instance, ref IEnumerable<Thing> __result)
        {
            var ammoUser = (__instance as ThingWithComps)?.TryGetComp<CompAmmoUser>();

            if (ammoUser != null && (ammoUser.HasMagazine && ammoUser.CurMagCount > 0 && ammoUser.CurrentLink != null))
            {
                if (ammoUser.TryUnload(out var list, true, true))
                    __result = __result.Concat(list);
            }
        }
    }
}
