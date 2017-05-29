using System.Reflection;
using Harmony;
using Verse;
using System;
using System.Reflection.Emit;
using System.Linq;

/* Note to those unfamiliar with Reflection/Harmony (like me, ProfoundDarkness), operands have some specific types and it's useful to know these to make good patches (Transpiler).
 * Below I'm noting the operators and what type of operand I've observed.
 * 
 * local variable operands who's index is < 4 (so _0, _1, _2, _3) seem to have an operand of null.
 * 
 * local variable operands (ex Ldloc_s, Stloc_s) == LocalVariableInfo
 * field operands (ex ldfld) == FieldInfo
 * method operands (ex call, callvirt) == MethodInfo
 * For branching, if the operand is a branch the label will be a Label? (Label isn't nullable but since it's in an object it must be nullable).
 * -Labels tend to look the same, use <instance_label>.GetHashCode() to determine WHICH label it is...
 */

namespace CombatExtended.Harmony
{
	public static class HarmonyBase
	{
		private static HarmonyInstance harmony = null;

        /// <summary>
        /// Fetch CombatExtended's instance of Harmony.
        /// </summary>
        /// <remarks>One should only have a single instance of Harmony per Assembly.</remarks>
        static internal HarmonyInstance instance
        {
            get
            {
                if (harmony == null)
                    harmony = harmony = HarmonyInstance.Create("CombatExtended.Harmony");
                return harmony;
            }
        }

        public static void InitPatches()
        {
            // Remove the remark on the following to debug all auto patches.
            HarmonyInstance.DEBUG = true;
            instance.PatchAll(Assembly.GetExecutingAssembly());
            // Keep the following remarked to also debug manual patches.
            HarmonyInstance.DEBUG = false;

            // Manual patches
            PatchThingOwner();
            PatchHediffWithComps();
        }

        #region Patch helper methods

        private static void PatchThingOwner()
        {
            // Need to patch ThingOwner<T> manually for all child classes of Thing
            var postfixTryAdd = typeof(Harmony_ThingOwner_TryAdd_Patch).GetMethod("Postfix");
            var postfixTake = typeof(Harmony_ThingOwner_Take_Patch).GetMethod("Postfix");
            var postfixRemove = typeof(Harmony_ThingOwner_Remove_Patch).GetMethod("Postfix");

            var baseType = typeof(Thing);
            var types = baseType.AllSubclassesNonAbstract().Add(baseType);
            foreach (Type current in types)
            {
                Log.Message(string.Concat("PatchThingOwner type: ", current)); //TODO: ask why this method of patching.
                var type = typeof(ThingOwner<>).MakeGenericType(current);
                instance.Patch(type.GetMethod("TryAdd", new Type[] { typeof(Thing), typeof(bool) }), null, new HarmonyMethod(postfixTryAdd));
                instance.Patch(type.GetMethod("Take", new Type[] { typeof(Thing), typeof(int) }), null, new HarmonyMethod(postfixTake));
                instance.Patch(type.GetMethod("Remove", new Type[] { typeof(Thing) }), null, new HarmonyMethod(postfixRemove));
            }
        }

        private static void PatchHediffWithComps()
        {
            var postfixBleedRate = typeof(Harmony_HediffWithComps_BleedRate_Patch).GetMethod("Postfix");
            var baseType = typeof(HediffWithComps);
            var types = baseType.AllSubclassesNonAbstract().Add(baseType);
            foreach(Type cur in types)
            {
                instance.Patch(cur.GetProperty("BleedRate").GetGetMethod(), null, new HarmonyMethod(postfixBleedRate));
            }
        }

        #endregion

        #region Utility_Methods
        /// <summary>
        /// branchOps is used by isBranch utility method.
        /// </summary>
        private static readonly OpCode[] branchOps = {
            OpCodes.Br, OpCodes.Br_S, OpCodes.Brfalse, OpCodes.Brfalse_S, OpCodes.Brtrue, OpCodes.Brtrue_S, // basic branches
            OpCodes.Bge, OpCodes.Bge_S, OpCodes.Bge_Un, OpCodes.Bge_Un_S, OpCodes.Bgt, OpCodes.Bgt_S, OpCodes.Bgt_Un, OpCodes.Bgt_Un_S, // Branch Greater
            OpCodes.Ble, OpCodes.Ble_S, OpCodes.Ble_Un, OpCodes.Ble_Un_S, OpCodes.Blt, OpCodes.Blt_S, OpCodes.Blt_Un, OpCodes.Blt_Un_S, // Branch Less
            OpCodes.Beq, OpCodes.Beq_S, OpCodes.Bne_Un, OpCodes.Bne_Un_S // Branch Equality
        };
        /// <summary>
        /// Simple check to see if the instruction is a branching instruction (and if so the operand should be a label)
        /// </summary>
        /// <param name="instruction">CodeInstruction provided by Harmony.</param>
        /// <returns>bool, true means it is a branching instruction, false is it's not.</returns>
        internal static bool isBranch(CodeInstruction instruction)
        {
            if (branchOps.Contains(instruction.opcode))
                return true;
            return false;
        }

        /// <summary>
        /// Utility function to convert a nullable bool (bool?) into a bool (primitive).
        /// </summary>
        /// <param name="input">bool? (nullable bool)</param>
        /// <returns>bool</returns>
        internal static bool doCast(bool? input)
        {
            if (!input.HasValue)
                return false;
            return (bool)input;
        }

        /// <summary>
        /// Return a CodeInstruction object with the correct opcode to fetch a local variable at a specific index.
        /// </summary>
        /// <param name="index">int value specifying the local variable index to fetch.</param>
        /// <param name="info">LocalVariableInfo optional if you've stored the metadata from a load/save instruction previously.</param>
        /// <returns>CodeInstruction object with the correct opcode to fetch a local variable into the evaluation stack.</returns>
        internal static CodeInstruction MakeLocalLoadInstruction(int index, LocalVariableInfo info = null)
        {
            // argument check...
            if (index < 0 || index > UInt16.MaxValue)
                throw new ArgumentException("Index must be greater than 0 and less than " + uint.MaxValue.ToString() + ".");

            // the first 4 are easy...  (ProfoundDarkness)NOTE: I've only ever observed these with null operands.
            switch (index)
            {
                case 0:
                    return new CodeInstruction(OpCodes.Ldloc_0);
                case 1:
                    return new CodeInstruction(OpCodes.Ldloc_1);
                case 2:
                    return new CodeInstruction(OpCodes.Ldloc_2);
                case 3:
                    return new CodeInstruction(OpCodes.Ldloc_3);
            }

            object objIndex;
            // proper type info for the other items.
            if (info != null && info.LocalIndex == index)
                objIndex = info;
            else
                objIndex = index;

            if (index > Byte.MaxValue) return new CodeInstruction(OpCodes.Ldloc, objIndex);
            return new CodeInstruction(OpCodes.Ldloc_S, objIndex);
        }

        /// <summary>
        /// Return the index of a local variable (based on storage opcode).
        /// </summary>
        /// <param name="instruction">CodeInstruction object from Harmony</param>
        /// <returns>int index of the local variable the instruction refers to. -1 if the opcode wasn't a recognized storage opcode.</returns>
        internal static int OpcodeStoreIndex(CodeInstruction instruction)
        {
            if (instruction.opcode == OpCodes.Stloc_0) return 0;
            if (instruction.opcode == OpCodes.Stloc_1) return 1;
            if (instruction.opcode == OpCodes.Stloc_2) return 2;
            if (instruction.opcode == OpCodes.Stloc_3) return 3;
            if (instruction.opcode == OpCodes.Stloc_S) // UInt8
                return (instruction.operand as LocalVariableInfo).LocalIndex;
            if (instruction.opcode == OpCodes.Stloc) // UInt16
                return (instruction.operand as LocalVariableInfo).LocalIndex;
            return -1; // error, unrecognized opcode so can check this if we DIDN'T get an apt opcode.
        }

        /// <summary>
        /// REturn the index of a local variable (based on load opcode).
        /// </summary>
        /// <param name="instruction">CodeInstruction object from Harmony</param>
        /// <returns>int index of the local variable the instruction refers to.  -1 if the opcode wasn't a recognized load opcode.</returns>
        internal static int OpcodeLoadIndex(CodeInstruction instruction)
        {
            if (instruction.opcode == OpCodes.Ldloc_0) return 0;
            if (instruction.opcode == OpCodes.Ldloc_1) return 1;
            if (instruction.opcode == OpCodes.Ldloc_2) return 2;
            if (instruction.opcode == OpCodes.Ldloc_3) return 3;
            if (instruction.opcode == OpCodes.Ldloc_S) // UInt8
                return (instruction.operand as LocalVariableInfo).LocalIndex;
            if (instruction.opcode == OpCodes.Ldloc) // UInt16
                return (instruction.operand as LocalVariableInfo).LocalIndex;
            return -1;
        }
        #endregion Utility_Methods
    }
}
