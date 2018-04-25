using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    public class StatWorker_Caliber : StatWorker
    {
        public override bool ShouldShowFor(BuildableDef eDef)
        {
            var thingDef = eDef as ThingDef;
            return thingDef?.GetCompProperties<CompProperties_AmmoUser>()?.ammoSet != null;
        }

        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
        {
            StringBuilder stringBuilder = new StringBuilder();
            var ammoProps = (req.Def as ThingDef)?.GetCompProperties<CompProperties_AmmoUser>();
            if (ammoProps != null)
            {
                if (ammoProps.changeableBarrels != null)
                {
                    foreach(CompProperties_AmmoUser.ChangeableBarrel barrel in ammoProps.changeableBarrels)
                    {
                        writeSingleCaliber(stringBuilder,barrel.ammoSet,barrel.magazineSize);
                        stringBuilder.AppendLine();
                    }
                }
                else
                {
                    writeSingleCaliber(stringBuilder,ammoProps.ammoSet,ammoProps.magazineSize);
                }
            }
            return stringBuilder.ToString().TrimEndNewlines();
        }

        public override string GetStatDrawEntryLabel(StatDef stat, float value, ToStringNumberSense numberSense, StatRequest optionalReq)
        {
            CompProperties_AmmoUser ammoProps = (optionalReq.Def as ThingDef)?.GetCompProperties<CompProperties_AmmoUser>();
            StringBuilder stringBuilder = new StringBuilder();
            if (ammoProps!=null && ammoProps.changeableBarrels != null)
            {
                ammoProps.changeableBarrels.Aggregate(false,(x, y) => 
                {
                    if (x == true) stringBuilder.Append(',');
                    stringBuilder.Append(y.ammoSet.LabelCap);
                    return true;
                });
            }
            else
            {
                stringBuilder.Append(ammoProps?.ammoSet.LabelCap);
            }
            return stringBuilder.ToString();
        }

        private void writeSingleCaliber(StringBuilder stringBuilder, AmmoSetDef ammoSet, int magazineSize)
        {
            // Append various ammo stats
                stringBuilder.AppendLine(ammoSet.LabelCap);
                stringBuilder.AppendLine(string.Format("CE_MagazineSize".Translate()+": "+magazineSize) + "\n");
                foreach (var cur in ammoSet.ammoTypes)
                {
                    string label = string.IsNullOrEmpty(cur.ammo.ammoClass.LabelCapShort) ? cur.ammo.ammoClass.LabelCap : cur.ammo.ammoClass.LabelCapShort;
                    stringBuilder.AppendLine(label + ":\n" + cur.projectile.GetProjectileReadout());
                }
        }
    }
}
