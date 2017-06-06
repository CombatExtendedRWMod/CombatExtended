using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    public class StatWorker_MeleeArmorPenetration : StatWorker
    {
        private float GetMeleePenetration(StatRequest req)
        {
            Pawn pawn = req.Thing as Pawn;
            if (pawn == null)
            {
                return 0f;
            }
            List<VerbEntry> updatedAvailableVerbsList = pawn.meleeVerbs.GetUpdatedAvailableVerbsList();
            if (updatedAvailableVerbsList.Count == 0)
            {
                return 0f;
            }
            float totalSelectionWeight = 0f;
            for (int i = 0; i < updatedAvailableVerbsList.Count; i++)
            {
                totalSelectionWeight += updatedAvailableVerbsList[i].SelectionWeight;
            }
            float totalAveragePen = 0f;
            foreach (VerbEntry current in updatedAvailableVerbsList)
            {
                var propsCE = current.verb.verbProps as VerbPropertiesCE;
                if (propsCE != null)
                {
                    ThingWithComps ownerEquipment = current.verb.ownerEquipment;
                    var weightFactor = current.SelectionWeight / totalSelectionWeight;
                    totalAveragePen += weightFactor * propsCE.meleeArmorPenetration;
                }
            }
            return totalAveragePen;
        }

        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            return GetMeleePenetration(req);
        }

        public override string GetExplanation(StatRequest req, ToStringNumberSense numberSense)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("CE_StatsReport_MeleePenetration".Translate() + " (" + "AverageOfAllAttacks".Translate() + ")");
            stringBuilder.AppendLine("  " + GetMeleePenetration(req).ToString("0.##"));
            return stringBuilder.ToString();
        }
    }
}
