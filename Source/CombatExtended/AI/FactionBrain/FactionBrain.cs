﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace CombatExtended.AI
{
	public enum FactionState : byte
	{

	}

	public class FactionBrain
	{
		#region FACTION PRIVATE VARIABLES

		//These are what make a faction a faction
		//TODO cash the all types of faction for events

		private IEnumerable<Pawn> factionInjuredPawns;

		private IEnumerable<Pawn> factionFreePawns;

		private IEnumerable<Pawn> factionPawns;

		private Dictionary<Pawn, CombatRole> factionPawnsTypes;

		//TODO CleanUp and cache all DataNeeded

		#endregion

		//------------------------------------------------//
		//------------------------------------------------//

		#region FACTION REALONY VARIABLES

		//These are what make a faction a faction
		//TODO cash the all types of faction for events

		private readonly Map map;

		private readonly Faction faction;

		//TODO CleanUp and cache all DataNeeded

		#endregion

		public FactionBrain(Map map, Faction faction)
		{
			this.map
				= map;

			this.faction
				= faction;
		}

		public void TickFaction()
		{

		}
	}
}
