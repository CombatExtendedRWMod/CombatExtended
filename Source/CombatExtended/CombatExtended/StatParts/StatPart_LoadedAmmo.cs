using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    public class StatPart_LoadedAmmo : StatPart
    {
        float cartridges = 0f;
        float spentCartridges = 0f;
        float magazine = 0f;

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (TryGetValue(req, out float num))
                val += num;
        }

        public override string ExplanationPart(StatRequest req)
        {
            StringBuilder stringBuilder = new StringBuilder();

            if (TryGetValue(req, out float _))
            {
                if (cartridges != 0f)
                    stringBuilder.AppendLine("CE_StatsReport_LoadedAmmo".Translate() + ": " + parentStat.ValueToString(cartridges));

                if (spentCartridges != 0f)
                    stringBuilder.AppendLine("CE_StatsReport_SpentAmmo".Translate() + ": " + parentStat.ValueToString(spentCartridges));

                if (magazine != 0f)
                    stringBuilder.AppendLine("CE_MagazineBulk".Translate() + ": " + parentStat.ValueToString(magazine));

                return stringBuilder.ToString().TrimEndNewlines();
            }

            return null;
        }

        public bool TryGetValue(StatRequest req, out float num)
        {
            cartridges = 0f;
            spentCartridges = 0f;
            magazine = 0f;
            
            if (req.HasThing)
            {
                var ammoUser = req.Thing.TryGetComp<CompAmmoUser>();
                if (ammoUser != null && ammoUser.CurrentAmmo != null)
                {
                    var numSingle = ammoUser.CurrentLink.spentThingDef.thingDef.GetStatValueAbstract(parentStat);

                    //TODO: Consider the full contents of the AmmoUser - store contents and iterate here

                    cartridges = numSingle * //ammoUser.CurMagCount;

                    if (Controller.settings.EnableAmmoSystem && parentStat == StatDefOf.Mass)
                        spentCartridges = ammoUser.SpentRounds * numSingle;
                    else if (parentStat == CE_StatDefOf.Bulk)
                    {
                        cartridges *= ammoUser.Props.loadedAmmoBulkFactor;

                        if (ammoUser.HasMagazine && ammoUser.CurMagCount > 0)
                            magazine = ammoUser.Props.magazineBulk;
                    }
                }
            }
            num = cartridges + spentCartridges + magazine;
            return num != 0f;
        }
    }
}