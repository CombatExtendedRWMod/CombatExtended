using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

/* first pass of this conversion is to see if we still need the ScribeHelpers or not...  Supposedly not.
 */

namespace CombatExtended
{
    /*
    public class EList<T> : List<T>, IExposable
    {
        public void ExposeData()
        {
            //Type genericList = typeof(List<>);
            //Type type = typeof(T);
            //Type listType = genericList.MakeGenericType(type);
            //var list = Activator.CreateInstance(listType);

            List<T> list = Activator.CreateInstance(typeof(List<>).MakeGenericType(typeof(T))) as List<T>;
            list = base.ToArray().ToList();
            Type listType = list.GetType();
            Scribe_Values.Look<Type>(ref listType, "EListType");
            if (Scribe.mode == LoadSaveMode.Saving || (Scribe.mode == LoadSaveMode.LoadingVars && listType == base.GetType()))
                Scribe_Collections.Look<T>(ref list, "EList", LookMode.Reference);
        }
    }
    */

    public class LoadoutManager : GameComponent
    {
        #region Fields
        private Dictionary<Pawn, Loadout> _assignedLoadouts = new Dictionary<Pawn, Loadout>();
        private List<Loadout> _loadouts = new List<Loadout>();
        private Dictionary<Pawn, List<HoldRecord>> _tracker = new Dictionary<Pawn, List<HoldRecord>>();  // track what the pawn is holding to not drop.
        private static LoadoutManager _active;
        #endregion Fields

        #region Constructors
        // constructor called on new/first game.
        public LoadoutManager(Game game)
        {
            // create a default empty loadout
            // there needs to be at least one default tagged loadout at all times
            _loadouts.Add(new Loadout("CE_EmptyLoadoutName".Translate(), 1) { canBeDeleted = false, defaultLoadout = true });
        }
        // constructor called on Load Game.  When this gets called there can actually be 2 instances of our Component...
        public LoadoutManager()
        {
        }
        #endregion Constructors

        #region Properties
        public Dictionary<Pawn, Loadout> AssignedLoadouts => _assignedLoadouts;

        public Loadout DefaultLoadout { get { return _loadouts.First(l => l.defaultLoadout); } }

        public List<Loadout> Loadouts => _loadouts;

        public static LoadoutManager active { get { return _active; } }
        #endregion Properties

        #region Override Methods
        // called basically when the game is ready to be played.  So after NewGame and after GameLoad.
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            _active = Current.Game.GetComponent<LoadoutManager>();
            Log.Message("Loadout FinalizeInit");
        }
        // called when RW is doing anything with UI
        /*public override void GameComponentOnGUI()
        {
            base.GameComponentOnGUI();
            Log.Message("Loadout GameComponentOnGUI");
        } */
        // Seems to occur when game state changes
        /*public override void GameComponentUpdate()
        {
            base.GameComponentUpdate();
            Log.Message("Loadout GameComponentUpdate");
        }*/
        // called every game tick.
        /*public override void GameComponentTick()
        {
            base.GameComponentTick();
            Log.Message("Loadout GameComponentTick");
        }*/
        // An instance of GameComponent can see this called in 2 situations.
        // 1: When a new instance is created as part of a game load, happens before FinalizeInit but after ExposeData.
        // 2: When an existing instance is being closed down due to the player choosing to load a game from within a game.
        /*public override void LoadedGame()
        {
            base.LoadedGame();
            Log.Message("Loadout LoadedGame");
        }*/
        // Called when a new game is started. (ie from main menu)
        /*public override void StartedNewGame()
        {
            base.StartedNewGame();
            Log.Message("Loadout StartedNewGame");
        }*/
        // Called when saving a game and durring the constructor of new instance of the object durring game load.
        /// <summary>
        /// Load/Save handler.
        /// </summary>
        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                PurgeHoldTrackerRolls();
                PurgeLoadoutRolls();
            }

            Scribe_Collections.Look(ref _loadouts, "loadouts", LookMode.Deep);
            Scribe_Collections.Look<Pawn, Loadout>(ref _assignedLoadouts, "assignments", LookMode.Reference, LookMode.Reference);
            //bool hasTrackers = _tracker.Keys.Any();
            //Scribe_Values.Look<bool>(ref hasTrackers, "HasTrackers");
            //if (hasTrackers)
            //{
                //TODO: Need to convert the List<HoldRecord> into a definite type (that functions like a list) so that we can do LookMode.Deep and have IExplosable above.
                //Scribe_Collections.Look<Pawn, List<HoldRecord>>(ref _tracker, "trackers", LookMode.Reference, LookMode.Deep);
            //}

            /*

            // (ProfoundDarkness) btw I did try to just store the dictionary but that code path just doesn't seem to work very well.  I couldn't get it to work so I modeled my dictionary saving on Fluffy's method.

            // clear out pawns that don't fit anymore.
            if (Scribe.mode == LoadSaveMode.Saving)
                PurgeHoldTrackerRolls();

            // convert the dictionary to a list of key/value objects when saving.
            if (Scribe.mode == LoadSaveMode.Saving)
                Instance._assignedHoldTrackerScribeHelper = Instance._tracker.Select(pair => new HoldTrackerAssignment() { pawn = pair.Key, recs = pair.Value }).ToList();
            
            // load/save our helper list.
            Scribe_Collections.Look(ref Instance._assignedHoldTrackerScribeHelper, "trackers", LookMode.Deep);

            // when loading, cleanup the list of invalid references and then convert it back to a dictionary.
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // removes assignments that for some reason have a null value.
                IEnumerable<HoldTrackerAssignment> temp = Instance._assignedHoldTrackerScribeHelper
                    .Where(a => (a.Valid == true) && (!a.pawn.Dead) && (!a.pawn.DestroyedOrNull()));
                Instance._tracker = temp.ToDictionary(k => k.pawn, v => v.recs);
            }



            // ---Original loadout scribe below code here---

            ////// List of pawns that are out of the map
            ////List<string> pawnsOutOfMap_IDs = new List<string>();

            // scribe available loadouts
            Scribe_Collections.Look(ref Instance._loadouts, "loadouts", LookMode.Deep);

            //scribe loadout assignments (for some reason using the dictionary directly doesn't work -- Fluffy)
            // create list of scribe helper objects
            if (Scribe.mode == LoadSaveMode.Saving)
                Instance._assignedLoadoutsScribeHelper = Instance._assignedLoadouts.Select(pair => new LoadoutAssignment() { pawn = pair.Key, loadout = pair.Value }).ToList();

            //scribe that list
            Scribe_Collections.Look(ref Instance._assignedLoadoutsScribeHelper, "assignments", LookMode.Deep);


            ////if (Scribe.mode == LoadSaveMode.LoadingVars)
            ////{
            ////    //Test if any pawns are out of the map; if yes, remove them from loadout assignments
            ////    // TO DO: verify if they retain their loadouts after they come back on the map!
            ////    List<Thing> lstPawnsOutOfMap = new List<Thing>();
            ////    Scribe_Collections.LookList(ref lstPawnsOutOfMap, "PawnsOutOfMap", LookMode.Deep);

            ////    if (lstPawnsOutOfMap != null && lstPawnsOutOfMap.Count > 0)
            ////    {
            ////        foreach (Thing th in lstPawnsOutOfMap)
            ////        {
            ////            Pawn p = th as Pawn;
            ////            if (p != null)
            ////            {
            ////                pawnsOutOfMap_IDs.Add(p.GetUniqueLoadID());
            ////            }
            ////        }

            ////        lstPawnsOutOfMap = null;
            ////    }
            ////    else
            ////    {
            ////        lstPawnsOutOfMap = null;
            ////        pawnsOutOfMap_IDs = null;
            ////    }
            ////}

            // convert back into useable dictionary
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // removes assignments that for some reason have a null value.
                IEnumerable<LoadoutAssignment> temp = Instance._assignedLoadoutsScribeHelper
                    .Where(a => (a.Valid == true) && (!a.pawn.Dead) && (!a.pawn.DestroyedOrNull()));

                ////// removes assignments for colonists not on the map!
                ////// TO DO: test if their loadouts get reassigned when they come back on the map!
                ////if (pawnsOutOfMap_IDs != null && pawnsOutOfMap_IDs.Count > 0)
                ////{
                ////    temp = temp.Where(x => (x != null) && !(pawnsOutOfMap_IDs.Contains(x.pawn.GetUniqueLoadID())) );
                ////}

                Instance._assignedLoadouts = temp.ToDictionary(k => k.pawn, v => v.loadout);
            }
            */
        }
        #endregion Override Methods

        #region Methods

        /// <summary>
        /// Returns a List of HoldRecords for the Pawn.
        /// </summary>
        /// <param name="pawn">Pawn to get the List for.</param>
        /// <returns>List of HoldRecords or null if the pawn has none.</returns>
        public List<HoldRecord> GetHoldRecords(Pawn pawn) // Rename Try?
        {
            List<HoldRecord> recs;
            if (_tracker.TryGetValue(pawn, out recs))
                return recs;
            return null;
        }

        /// <summary>
        /// Utility to clean up HoldTracker entries for pawns which are dead or who no longer have HoldRecords.  Useful pre-save.
        /// </summary>
        public void PurgeHoldTrackerRolls()
        {
            List<Pawn> removeList = new List<Pawn>(_tracker.Keys.Count);
            foreach (Pawn pawn in _tracker.Keys)
            {
                if (pawn.Dead)
                    removeList.Add(pawn); // remove dead pawns from the rolls
                else if (!_tracker[pawn].Any())
                    removeList.Add(pawn); // remove pawns with no HoldRecords stored.
                else if (pawn.DestroyedOrNull())
                    removeList.Add(pawn); // remove pawns have been destroyed or are null.
            }
            foreach (Pawn pawn in removeList)
                _tracker.Remove(pawn);
        }

        /// <summary>
        /// Similar to the PurgeHoldTrackerRolls, clean up loadout data, mostly used pre-save but can be used elsewhere.
        /// </summary>
        public void PurgeLoadoutRolls()
        {
            List<Pawn> removeList = new List<Pawn>(_assignedLoadouts.Keys.Count);
            foreach (Pawn pawn in _assignedLoadouts.Keys)
            {
                if (pawn.Dead)
                    removeList.Add(pawn);   // remove dead pawns from the rolls, they should become null pawns on game save.
                else if (pawn.DestroyedOrNull())
                    removeList.Add(pawn);   // remove pawns that have been destroyed or are null.
            }
            foreach (Pawn pawn in removeList)
                _assignedLoadouts.Remove(pawn);
        }

        /// <summary>
        /// Adds a List of HoldRecord to the indicated Pawn.
        /// </summary>
        /// <param name="pawn">Pawn for whome new List should be stored.</param>
        /// <param name="newRecords">List of HoldRecord that should be attached to pawn.</param>
        public void AddHoldRecords(Pawn pawn, List<HoldRecord> newRecords)
        {
            _tracker.Add(pawn, newRecords);
        }

        public void AddLoadout(Loadout loadout)
        {
            _loadouts.Add(loadout);
        }

        public void RemoveLoadout(Loadout loadout)
        {
            // assign default loadout to pawns that used to use this loadout
            List<Pawn> obsolete = AssignedLoadouts.Where(kvp => kvp.Value == loadout).Select(kvp => kvp.Key).ToList(); // ToList separates this from the dictionary, ienumerable in this case would break as we change the relationship.
            foreach (Pawn id in obsolete)
                AssignedLoadouts[id] = DefaultLoadout;

            _loadouts.Remove(loadout);
        }

        /// <summary>
        /// Used to ensure that future retrievals of loadouts are sorted.  Doesn't need to be called often, just right before fetching and only when it matters.
        /// </summary>
        public void SortLoadouts()
        {
            _loadouts.Sort();
        }

        internal static int GetUniqueID()
        {
            LoadoutManager manager = Current.Game.GetComponent<LoadoutManager>();
            if (manager != null && manager.Loadouts.Any())
                return manager.Loadouts.Max(l => l.uniqueID) + 1;
            else
                return 1;
        }

        internal static string GetUniqueLabel()
        {
            return GetUniqueLabel("CE_DefaultLoadoutName".Translate());
        }

        internal static string GetUniqueLabel(string head)
        {
            LoadoutManager manager = Current.Game.GetComponent<LoadoutManager>();
            string label;
            int i = 1;
            if (manager != null)
            {
                do
                {
                    label = head + i++;
                }
                while (manager.Loadouts.Any(l => l.label == label));
            } else
                label = head + i++;
            return label;
        }

        internal Loadout GetLoadoutById(int id)
        {
            return Loadouts.Find(x => x.uniqueID == id);
        }
        #endregion Methods
    }

    /*
    public class LoadoutManager : MapComponent
    {
        #region Fields
        private static LoadoutManager _instance;
        private Dictionary<Pawn, Loadout> _assignedLoadouts = new Dictionary<Pawn, Loadout>();
        private List<LoadoutAssignment> _assignedLoadoutsScribeHelper = new List<LoadoutAssignment>();
        private List<Loadout> _loadouts = new List<Loadout>();
		private Dictionary<Pawn, List<HoldRecord>> _tracker = new Dictionary<Pawn, List<HoldRecord>>(); // track what the pawn is holding to not drop.
		private List<HoldTrackerAssignment> _assignedHoldTrackerScribeHelper = new List<HoldTrackerAssignment>();
		
        #endregion Fields

        #region Constructors

        public LoadoutManager(Map map) : base(map)
        {
            // create a default empty loadout
            // there needs to be at least one default tagged loadout at all times
            _loadouts.Add( new Loadout( "CE_EmptyLoadoutName".Translate(), 1 ) { canBeDeleted = false, defaultLoadout = true } );
        }

        #endregion Constructors

        #region Properties

        public static LoadoutManager Instance
        {
            get
            {
                Map map = Find.VisibleMap;
                if ( _instance == null )
                    _instance = new LoadoutManager(map);
                return _instance;
            }
        }

        public static Dictionary<Pawn, Loadout> AssignedLoadouts { get { return Instance._assignedLoadouts; } }

        public static Loadout DefaultLoadout { get { return Instance._loadouts.First( l => l.defaultLoadout ); } }

        public static List<Loadout> Loadouts { get { return Instance._loadouts; } }
        
        #endregion Properties

        #region Methods
        
        /// <summary>
        /// Returns a List of HoldRecords for the Pawn.
        /// </summary>
        /// <param name="pawn">Pawn to get the List for.</param>
        /// <returns>List of HoldRecords or null if the pawn has none.</returns>
        public static List<HoldRecord> GetHoldRecords(Pawn pawn)
        {
        	List<HoldRecord> recs;
        	if (Instance._tracker.TryGetValue(pawn, out recs))
        		return recs;
        	return null;
        }
        
        /// <summary>
        /// Utility to clean up HoldTracker entries for pawns which are dead or who no longer have HoldRecords.  Useful pre-save.
        /// </summary>
        public static void PurgeHoldTrackerRolls()
        {
        	List<Pawn> removeList = new List<Pawn>(Instance._tracker.Keys.Count);
        	foreach (Pawn pawn in Instance._tracker.Keys)
        	{
        		if (pawn.Dead)
    	         	removeList.Add(pawn); // remove dead pawns from the rolls
        		else if (!Instance._tracker[pawn].Any()) // remove pawns with no HoldRecords stored.
        			removeList.Add(pawn);
        	}
        	foreach (Pawn pawn in removeList)
        		Instance._tracker.Remove(pawn);
        }
        
        /// <summary>
        /// Adds a List of HoldRecord to the indicated Pawn.
        /// </summary>
        /// <param name="pawn">Pawn for whome new List should be stored.</param>
        /// <param name="newRecords">List of HoldRecord that should be attached to pawn.</param>
        public static void AddHoldRecords(Pawn pawn, List<HoldRecord> newRecords)
        {
        	Instance._tracker.Add(pawn, newRecords);
        }
        
        public static void AddLoadout( Loadout loadout )
        {
            Instance._loadouts.Add( loadout );
        }
        
        public static void RemoveLoadout( Loadout loadout )
        {
			// assign default loadout to pawns that used to use this loadout
			List<Pawn> obsolete = AssignedLoadouts.Where(kvp => kvp.Value == loadout).Select(kvp => kvp.Key).ToList(); // ToList separates this from the dictionary, ienumerable in this case would break as we change the relationship.
			foreach (Pawn id in obsolete)
				AssignedLoadouts[id] = DefaultLoadout;

			Instance._loadouts.Remove(loadout);
		}
        
        /// <summary>
        /// Used to ensure that future retrievals of loadouts are sorted.  Doesn't need to be called often, just right before fetching and only when it matters.
        /// </summary>
        public static void SortLoadouts()
        {
        	Instance._loadouts.Sort();
        }

        /// <summary>
        /// Load/Save handler.
        /// </summary>
        public override void ExposeData()
        {
        	// (ProfoundDarkness) btw I did try to just store the dictionary but that code path just doesn't seem to work very well.  I couldn't get it to work so I modeled my dictionary saving on Fluffy's method.
        	
        	// clear out pawns that don't fit anymore.
        	if (Scribe.mode == LoadSaveMode.Saving)
        		PurgeHoldTrackerRolls();
        	
        	// convert the dictionary to a list of key/value objects when saving.
        	if (Scribe.mode == LoadSaveMode.Saving)
        		Instance._assignedHoldTrackerScribeHelper = Instance._tracker.Select(pair => new HoldTrackerAssignment() { pawn = pair.Key, recs = pair.Value }).ToList();
        	
        	// load/save our helper list.
        	Scribe_Collections.Look(ref Instance._assignedHoldTrackerScribeHelper, "trackers", LookMode.Deep);
        	
        	// when loading, cleanup the list of invalid references and then convert it back to a dictionary.
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // removes assignments that for some reason have a null value.
                IEnumerable<HoldTrackerAssignment> temp = Instance._assignedHoldTrackerScribeHelper
                    .Where(a => (a.Valid == true) && (!a.pawn.Dead) && (!a.pawn.DestroyedOrNull()) );
                Instance._tracker = temp.ToDictionary(k => k.pawn, v => v.recs );
            }
            
            
            
            // ---Original loadout scribe below code here---

            ////// List of pawns that are out of the map
            ////List<string> pawnsOutOfMap_IDs = new List<string>();

            // scribe available loadouts
            Scribe_Collections.Look( ref Instance._loadouts, "loadouts", LookMode.Deep );

            //scribe loadout assignments (for some reason using the dictionary directly doesn't work -- Fluffy)
            // create list of scribe helper objects
            if ( Scribe.mode == LoadSaveMode.Saving )
                Instance._assignedLoadoutsScribeHelper = Instance._assignedLoadouts.Select(pair => new LoadoutAssignment() { pawn = pair.Key, loadout = pair.Value }).ToList();

            //scribe that list
            Scribe_Collections.Look( ref Instance._assignedLoadoutsScribeHelper, "assignments", LookMode.Deep );


            ////if (Scribe.mode == LoadSaveMode.LoadingVars)
            ////{
            ////    //Test if any pawns are out of the map; if yes, remove them from loadout assignments
            ////    // TO DO: verify if they retain their loadouts after they come back on the map!
            ////    List<Thing> lstPawnsOutOfMap = new List<Thing>();
            ////    Scribe_Collections.LookList(ref lstPawnsOutOfMap, "PawnsOutOfMap", LookMode.Deep);

            ////    if (lstPawnsOutOfMap != null && lstPawnsOutOfMap.Count > 0)
            ////    {
            ////        foreach (Thing th in lstPawnsOutOfMap)
            ////        {
            ////            Pawn p = th as Pawn;
            ////            if (p != null)
            ////            {
            ////                pawnsOutOfMap_IDs.Add(p.GetUniqueLoadID());
            ////            }
            ////        }

            ////        lstPawnsOutOfMap = null;
            ////    }
            ////    else
            ////    {
            ////        lstPawnsOutOfMap = null;
            ////        pawnsOutOfMap_IDs = null;
            ////    }
            ////}

            // convert back into useable dictionary
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // removes assignments that for some reason have a null value.
                IEnumerable<LoadoutAssignment> temp = Instance._assignedLoadoutsScribeHelper
                    .Where(a => (a.Valid == true) && (!a.pawn.Dead) && (!a.pawn.DestroyedOrNull()) );

                ////// removes assignments for colonists not on the map!
                ////// TO DO: test if their loadouts get reassigned when they come back on the map!
                ////if (pawnsOutOfMap_IDs != null && pawnsOutOfMap_IDs.Count > 0)
                ////{
                ////    temp = temp.Where(x => (x != null) && !(pawnsOutOfMap_IDs.Contains(x.pawn.GetUniqueLoadID())) );
                ////}

                Instance._assignedLoadouts = temp.ToDictionary(k => k.pawn, v => v.loadout );
            }
        }

        internal static int GetUniqueID()
        {
            if ( Loadouts.Any() )
                return Loadouts.Max( l => l.uniqueID ) + 1;
            else
                return 1;
        }
        
        internal static string GetUniqueLabel()
        {
        	return GetUniqueLabel("CE_DefaultLoadoutName".Translate());
        }

        internal static string GetUniqueLabel(string head)
        {
            string label;
            int i = 1;
            do
            {
                label = head + i++;
            }
            while ( Loadouts.Any( l => l.label == label ) );
            return label;
        }

        internal static Loadout GetLoadoutById(int id)
        {
            return Loadouts.Find(x => x.uniqueID == id);
        }

        #endregion Methods
    }
    */
}