using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    public class AmmoSetDef : Def
    {
        public List<AmmoLink> ammoTypes;

        public Dictionary<ThingDef, int> maxChargeAdded = new Dictionary<ThingDef, int>();

        public int MaxCharge(ThingDef def)
        {
            if (!maxChargeAdded.ContainsKey(def))
            {
                var ammo = ammoTypes.SelectMany(x => x.adders).Where(x => x.thingDef == def);
                maxChargeAdded.Add(def, ammo?.MaxBy(x => x.count).count ?? -1);
            }

            return maxChargeAdded[def];
        }

        public AmmoLink Containing(ThingDef def)
        {
            return ammoTypes.Where(x => x.adders.Any(y => y.thingDef == def)).FirstOrDefault();
        }

        public override void ResolveReferences()
        {
            ammoTypes.ForEach(x => {
                if (x.iconAdder == null)        x.iconAdder = x.adders.MaxBy(y => y.count).thingDef;
                if (x.defaultAmmoCount == -1)   x.defaultAmmoCount = ((x.iconAdder as AmmoDef)?.defaultAmmoCount ?? 1);
                if (x.ammoClass == null)        x.ammoClass = (x.iconAdder as AmmoDef).ammoClass;
                if (x.labelCap.NullOrEmpty() && x.labelCapShort.NullOrEmpty())
                {
                    x.labelCap = x.ammoClass.LabelCap;
                    x.labelCapShort = x.ammoClass.LabelCapShort;
                }

            });
        }
    }
}
