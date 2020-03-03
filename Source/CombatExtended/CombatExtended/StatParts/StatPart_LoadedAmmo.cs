using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using System.Linq;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    public class StatPart_LoadedAmmo : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (TryGetValue(req, out float num))
                val += num;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!Recalculate(req.Thing, out var cartridges, out var spentCartridges, out var magazine))
                return null;

            StringBuilder stringBuilder = new StringBuilder();
            
            if (cartridges > 0)
            {
                var cartridgeCount = req.Thing.TryGetComp<CompAmmoUser>().adders?.Sum(x => x.stackCount) ?? 0;

                stringBuilder.AppendLine("CE_StatsReport_LoadedAmmo".Translate()
                    + (cartridgeCount > 1 ? (" (x" + cartridgeCount + "): ") : ": ")
                    + parentStat.ValueToString(cartridges));
            }

            if (spentCartridges > 0)
            {
                var spentCartridgeCount = req.Thing.TryGetComp<CompAmmoUser>().spentAdders?.Sum(x => x.stackCount) ?? 0;

                stringBuilder.AppendLine("CE_StatsReport_SpentAmmo".Translate()
                    + (spentCartridgeCount > 1 ? (" (x" + spentCartridgeCount + "): ") : ": ")
                    + parentStat.ValueToString(spentCartridges));
            }

            if (magazine != 0f)
                stringBuilder.AppendLine("CE_MagazineBulk".Translate() + ": " + parentStat.ValueToString(magazine));

            return stringBuilder.ToString().TrimEndNewlines();
        }

        public bool Recalculate(Thing reqThing, out float cartridges, out float spentCartridges, out float magazine)
        {
            cartridges = 0f;
            spentCartridges = 0f;
            magazine = 0f;

            if (Controller.settings.EnableAmmoSystem && reqThing != null)
            {
                // Consider the full contents of the AmmoUser
                var ammoUser = reqThing.TryGetComp<CompAmmoUser>();
                if (ammoUser != null && ammoUser.HasMagazine)
                {
                    foreach (var adder in ammoUser.adders)
                    {
                        cartridges += adder.def.GetStatValueAbstract(parentStat) * (float)adder.stackCount;
                    }

                    //Magazine bulk and/or bulkFactor on the gun
                    if (parentStat == CE_StatDefOf.Bulk)
                    {
                        cartridges *= ammoUser.Props.loadedAmmoBulkFactor;

                        if (ammoUser.CurMagCount > 0)
                            magazine = ammoUser.Props.magazineBulk;
                    }

                    foreach (var adder in ammoUser.spentAdders)
                    {
                        spentCartridges += adder.def.GetStatValueAbstract(parentStat) * (float)adder.stackCount
                            * ((parentStat == StatDefOf.Mass && ammoUser.CurrentLink.IsSpentAdder(adder.def))
                                ? (adder.def as AmmoDef)?.conservedMassFactorWhenFired ?? 1f : 1f);
                    }

                    return true;
                }
            }
            return false;
        }

        public bool TryGetValue(StatRequest req, out float num)
        {
            Recalculate(req.Thing, out var cartridges, out var spentCartridges, out var magazine);

            num = cartridges + spentCartridges + magazine;
            return num != 0f;
        }
    }
}