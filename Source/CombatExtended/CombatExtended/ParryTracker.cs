using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    public class ParryTracker : MapComponent
    {
        private struct ParryCounter
        {
            public int parries;
            public int ticks;

            public static ParryCounter MakeNew()
            {
                return new ParryCounter { parries = 0, ticks = Find.TickManager.TicksGame };
            }
        }

        private const int SkillPerParry = 4;    // Award another parry per this many skill levels
        private const int TicksToTimeout = 120; // Reset parry counter after this many ticks

        private Dictionary<Pawn, ParryCounter> parryTracker = new Dictionary<Pawn, ParryCounter>();

        public ParryTracker(Map map) : base(map)
        {
        }

        private int GetUsedParriesFor(Pawn pawn)
        {
            ParryCounter counter;
            if (!parryTracker.TryGetValue(pawn, out counter))
            {
                return 0;
            }
            return counter.parries;
        }

        public bool CheckCanParry(Pawn pawn)
        {
            if (pawn == null)
            {
                Log.Error("CE tried checking CanParry with Null-Pawn");
                return false;
            }

            // Check if our target is immobile
            if (!pawn.RaceProps.Humanlike || pawn.Downed || pawn.GetPosture() != PawnPosture.Standing || pawn.stances.stunner.Stunned || pawn.story.WorkTagIsDisabled(WorkTags.Violent))
            {
                return false;
            }

            int parriesLeft = Mathf.RoundToInt(pawn.skills.GetSkill(SkillDefOf.Melee).Level / SkillPerParry) - GetUsedParriesFor(pawn);
            return parriesLeft > 0;
        }

        public void RegisterParryFor(Pawn pawn)
        {
            ParryCounter counter;
            if (!parryTracker.TryGetValue(pawn, out counter))
            {
                // Register new pawn in tracker
                counter = ParryCounter.MakeNew();
                parryTracker.Add(pawn, counter);
            }
            counter.parries++;
        }

        public void ResetParriesFor(Pawn pawn)
        {
            parryTracker.Remove(pawn);
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % 10 == 0)
            {
                foreach (var entry in parryTracker.Where(kvp => Find.TickManager.TicksGame - kvp.Value.ticks >= TicksToTimeout).ToArray())
                {
                    parryTracker.Remove(entry.Key);
                }
            }
        }

        public override void ExposeData()
        {
            // TODO Save parryTracker
        }
    }
}
