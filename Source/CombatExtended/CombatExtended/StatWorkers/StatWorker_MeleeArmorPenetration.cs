﻿using System;
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
            var tools = req.Thing?.def.tools;
            if (tools.NullOrEmpty())
            {
                return 0f;
            }
            if (tools.Any(x=> !(x is ToolCE)))
            {
                Log.Error($"Trying to get stat MeleePenetration from {req.Thing.def.defName} which has no support for Combat Extended.");
                return 0f;
            }

            float totalSelectionWeight = 0f;
            foreach (var tool in tools)
            {
                totalSelectionWeight += tool.chanceFactor;
            }
            float totalAveragePen = 0f;
            foreach (ToolCE tool in tools)
            {
                var weightFactor = tool.chanceFactor / totalSelectionWeight;
                totalAveragePen += weightFactor * tool.armorPenetration;
            }
            var penMult = req.Thing.GetStatValue(CE_StatDefOf.MeleePenetrationFactor);
            return totalAveragePen * penMult;
        }

        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            return GetMeleePenetration(req);
        }

        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
        {
            if (req.Thing?.def.tools.NullOrEmpty() ?? true)
            {
                return base.GetExplanationUnfinalized(req, numberSense);
            }

            var stringBuilder = new StringBuilder();
            var penMult = req.Thing.GetStatValue(CE_StatDefOf.MeleePenetrationFactor);
            foreach (ToolCE tool in req.Thing.def.tools)
            {
                var maneuvers = DefDatabase<ManeuverDef>.AllDefsListForReading.Where(d => tool.capacities.Contains(d.requiredCapacity));
                var maneuverString = "(";
                foreach(var maneuver in maneuvers)
                {
                    maneuverString += maneuver.ToString() + "/";
                }
                maneuverString = maneuverString.TrimmedToLength(maneuverString.Length - 1) + ")";
                stringBuilder.AppendLine("  Tool: " + tool.ToString() + " " + maneuverString);
                stringBuilder.AppendLine("    Base penetration: " + tool.armorPenetration.ToStringByStyle(ToStringStyle.FloatMaxTwo));
                stringBuilder.AppendLine("    Weapon multiplier: " + penMult.ToStringByStyle(ToStringStyle.PercentZero));
                stringBuilder.AppendLine(string.Format("    Final value: {0} x {1} = {2}",
                    tool.armorPenetration.ToStringByStyle(ToStringStyle.FloatMaxTwo),
                    penMult.ToStringByStyle(ToStringStyle.FloatMaxTwo),
                    (tool.armorPenetration * penMult).ToStringByStyle(ToStringStyle.FloatMaxTwo)));
                stringBuilder.AppendLine();
            }
            return stringBuilder.ToString();
        }
    }
}
