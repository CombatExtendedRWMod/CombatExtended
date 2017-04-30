using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;
using Harmony;

namespace CombatExtended.Harmony
{
    [HarmonyPatch(typeof(Verb_MeleeAttack))]
    [HarmonyPatch("TryCastShot")]
    static class Harmony_Verb_MeleeAttack_TryCastShot_Patch
    {
        public static bool Prefix(Verb_MeleeAttack __instance, ref bool __result)
        {
            var verb_MeleeAttack = Traverse.Create(__instance);

            Pawn casterPawn = __instance.CasterPawn;
            if (casterPawn.stances.FullBodyBusy)
            {
                __result = false;
                return false;
            }
            LocalTargetInfo currentTarget = verb_MeleeAttack.Field("currentTarget").GetValue<LocalTargetInfo>();
            Thing thing = currentTarget.Thing;
            if (!__instance.CanHitTarget(thing))
            {
                Log.Warning(string.Concat(new object[]
                {
            casterPawn,
            " meleed ",
            thing,
            " from out of melee position."
                }));
            }
            casterPawn.Drawer.rotator.Face(thing.DrawPos);
            if (!verb_MeleeAttack.Method("IsTargetImmobile", currentTarget).GetValue<bool>() && casterPawn.skills != null)
            {
                casterPawn.skills.Learn(SkillDefOf.Melee, 250f, false);
            }
            SoundDef soundDef;
            if (Rand.Value < verb_MeleeAttack.Method("GetHitChance", thing).GetValue<float>())
            {
                __result = true;
                verb_MeleeAttack.Method("ApplyMeleeDamageToTarget", currentTarget);
                if (thing.def.category == ThingCategory.Building)
                {
                    soundDef = verb_MeleeAttack.Method("SoundHitBuilding").GetValue<SoundDef>();
                }
                else
                {
                    soundDef = verb_MeleeAttack.Method("SoundHitPawn").GetValue<SoundDef>();
                }
            }
            else
            {
                __result = false;
                soundDef = verb_MeleeAttack.Method("SoundMiss").GetValue<SoundDef>();
            }
            soundDef.PlayOneShot(new TargetInfo(thing.Position, casterPawn.Map, false));
            casterPawn.Drawer.Notify_MeleeAttackOn(thing);
            Pawn pawn = thing as Pawn;
            if (pawn != null && !pawn.Dead)
            {
                pawn.stances.StaggerFor(95);
                if (casterPawn.MentalStateDef != MentalStateDefOf.SocialFighting || pawn.MentalStateDef != MentalStateDefOf.SocialFighting)
                {
                    pawn.mindState.meleeThreat = casterPawn;
                    pawn.mindState.lastMeleeThreatHarmTick = Find.TickManager.TicksGame;
                }
            }
            casterPawn.Drawer.rotator.FaceCell(thing.Position);
            if (casterPawn.caller != null)
            {
                casterPawn.caller.Notify_DidMeleeAttack();
            }
            return false;
        }
    }
}
