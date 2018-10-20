using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;

namespace CombatExtended
{
    public class StatPart_LoadedAmmo : StatPart
    {

        public override string ExplanationPart(StatRequest req)
        {
            if (req.HasThing && req.Thing.TryGetComp<CompAmmoUser>() is CompAmmoUser compAmmo && compAmmo.UseAmmo)
            {
                float valOffset = compAmmo.CurMagCount * compAmmo.CurrentAmmo.GetStatValueAbstract(StatDefOf.Mass);
                return $"{"CE_LoadedAmmo".Translate()} : {parentStat.Worker.ValueToString(valOffset, false, ToStringNumberSense.Offset)}";
            }
            return null;
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.HasThing && req.Thing.TryGetComp<CompAmmoUser>() is CompAmmoUser compAmmo && compAmmo.UseAmmo)
            {
                val += compAmmo.CurMagCount * compAmmo.CurrentAmmo.GetStatValueAbstract(StatDefOf.Mass);
            }
        }
    }
}
