using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace CombatExtended
{
    // this has been reduced to a thingCount at this point, with the exception of the added default count bit
    // -- Fluffy
    public class LoadoutSlot : IExposable
    {
        #region Fields

        private const int _defaultCount = 1;
        private int _count;
        private Def _def;
        private LoadoutCountType _countType = LoadoutCountType.pickupDrop; // default mode for new loadout slots.

        #endregion Fields

        #region Constructors

        public LoadoutSlot( ThingDef def, int count = 1 )
        {
            _count = count;
            _def = def;

            // increase default ammo count
            if ( def is AmmoDef )
                _count = ( (AmmoDef)def ).defaultAmmoCount;
            
            _count = _count < 1 ? 1 : _count;
        }
        
        public LoadoutSlot(LoadoutGenericDef def, int count = 0)
        {
        	if ( count < 1)
        		_count = def.defaultCount;
        	
        	_count = count < 1 ? _count = 1 : _count = count;
        	_countType = def.defaultCountType;
        	_def = def;
        }

        public LoadoutSlot()
        {
            // for scribe; if Count is set default will be overwritten. Def is always stored/loaded.
            _count = _defaultCount;
        }

        #endregion Constructors

        #region Properties

        public int count { get { return _count; } set { _count = value; } }
        public LoadoutCountType countType { get { return _countType; } set { _countType = value; } }
        public ThingDef thingDef { get { return (_def is ThingDef) ? (ThingDef)_def : null; } }
        public LoadoutGenericDef genericDef { get { return (_def is LoadoutGenericDef) ? (LoadoutGenericDef)_def : null; } }
        public Def def { get { return _def; } } // expose the def directly for things like copy/clone and new slots.
        
        // hide where the bulk/mass came from.  Higher level doesn't care as long as it has a number.
        public float bulk { get { return (thingDef != null ? thingDef.GetStatValueAbstract(CE_StatDefOf.Bulk) : genericDef.bulk); } }
        public float mass { get { return (thingDef != null ? thingDef.GetStatValueAbstract(StatDefOf.Mass) : genericDef.mass); } }
        
        //public ThingDef def { get { return _def; } set { _def = value; } }

        #endregion Properties

        #region Methods

        public void ExposeData()
        {
            Scribe_Values.LookValue( ref _count, "count", _defaultCount );
            Scribe_Defs.LookDef( ref _def, "def" );
        }
        
        // helpers so as to avoid repeating the logic elsewhere...
        public int getDropCount(int haveCount)
        {
        	return (haveCount <= count ? 0 : haveCount - count);
        }
        public int getPickupCount(int haveCount)
        {
        	if (_countType == LoadoutCountType.pickupDrop && haveCount < count)
        		return count - haveCount;
        	// else _countType == LoadoutCountType.dropExcess
        	return 0;
        }
        
        // Returns a new copy of this object.
        // _def doesn't need to be deep copied
        // Constructor can handle the work.
        public LoadoutSlot Copy()
        {
        	if (genericDef != null)
        		return new LoadoutSlot(genericDef, _count);
        	// else if (thingDef != null)
        	return new LoadoutSlot(thingDef, _count);
        }

        #endregion Methods
    }
}