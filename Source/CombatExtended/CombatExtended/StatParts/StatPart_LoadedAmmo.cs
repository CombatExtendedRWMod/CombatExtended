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
            
            if (Controller.settings.EnableAmmoSystem && req.HasThing)
            {
                // Consider the full contents of the AmmoUser
                var ammoUser = req.Thing.TryGetComp<CompAmmoUser>();
                if (ammoUser != null && ammoUser.CurrentLink != null && ammoUser.CurrentAdder != null)
                {
                    //Add currently loaded cartridge (more complex)
                  //var isSpentAdder = ammoUser.CurrentLink.IsSpentAdder(ammoUser.CurrentAdder.def);

                    for (int i = 0; i < ammoUser.adders.Count; i++)
                    {
                        cartridges += ammoUser.adders[i].GetStatValue(parentStat) *
                            (float)(ammoUser.adders[i].stackCount);
                    }
                    
                    //Magazine bulk and/or bulkFactor on the gun
                    if (parentStat == CE_StatDefOf.Bulk)
                    {
                        cartridges *= ammoUser.Props.loadedAmmoBulkFactor;

                        if (ammoUser.HasMagazine && ammoUser.CurMagCount > 0)
                            magazine = ammoUser.Props.magazineBulk;
                    }

                    //Add all spent cartridges
                    for (int i = 0; i < ammoUser.spentAdders.Count; i++)
                    {
                        cartridges += ammoUser.spentAdders[i].GetStatValue(parentStat) *
                            (float)(ammoUser.spentAdders[i].stackCount)
                            * ((parentStat == StatDefOf.Mass && ammoUser.CurrentLink.IsSpentAdder(ammoUser.spentAdders[i].def))
                                ? (ammoUser.spentAdders[i].def as AmmoDef)?.conservedMassFactorWhenFired ?? 1f
                                : 1f);
                    }
                }
            }
            num = cartridges + spentCartridges + magazine;
            return num != 0f;
        }
    }
}