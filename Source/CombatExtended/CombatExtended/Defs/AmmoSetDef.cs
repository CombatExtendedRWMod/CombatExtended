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

        Dictionary<ThingDef, int> maxChargeAdded = new Dictionary<ThingDef, int>();

        public int MaxCharge(ThingDef def)
        {
            if (!maxChargeAdded.ContainsKey(def) || maxChargeAdded[def] == -2)
            {
                var ammo = ammoTypes.SelectMany(x => x.adders)?.Where(x => x.thingDef == def) ?? null;
                maxChargeAdded.Add(def, ammo?.MaxByWithFallback(x => x.count)?.count ?? -1);
            }

            return maxChargeAdded[def];
        }

        public AmmoLink Containing(ThingDef def)
        {
            return ammoTypes.Where(x => x.CanAdd(def)).FirstOrDefault();
        }

        public AmmoLink Containing(ThingDefCount defCount)
        {
            return ammoTypes.Where(x => x.adders.Contains(defCount)).FirstOrDefault();
        }

        public override void ResolveReferences()
        {
            bool couldBeMultiUser = true;

            ammoTypes.ForEach(x => {

                //Assign short-hand XML constructs appropriately before checking adders etc.
                x.ResolveReferences();
                
                couldBeMultiUser = couldBeMultiUser
                    && (x.adders.Count == 1 && x.users.Count == 1);
            });
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var err in base.ConfigErrors())
                yield return err;

            for (int i = 0; i < ammoTypes.Count; i++)
            {
                foreach (var err in ammoTypes[i].ConfigErrors())
                    yield return "for type with index "+i+", " + err;
            }
        }
    }
}
