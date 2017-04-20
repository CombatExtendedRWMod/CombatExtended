using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using Harmony.ILCopying;
using Verse;
using Verse.AI;

namespace CombatExtended.Harmony
{
    /*
     * Targetting the Verse.AI.JobDriver_Wait.CheckForAutoAttack()
     * Target Line:
     *  Thing thing = AttackTargetFinder.BestShootTargetFromCurrentPosition(this.pawn, null, verb.verbProps.range, verb.verbProps.minRange, targetScanFlag);
     *  
     * Basically modify that line to read something like:
     *  Predicate<Thing> predicate = GetValidTargetPredicate(verb)
     *  Thing thing = AttackTargetFinder.BestShootTargetFromCurrentPosition(this.pawn, predicate, verb.verbProps.range, verb.verbProps.minRange, targetScanFlag);
     */

    // (ProfoundDarkness) btw: I don't know how to insert a new local variable just yet...  The rest should be pretty easy.

    [HarmonyPatch(typeof(JobDriver_Wait))]
    [HarmonyPatch("CheckForAutoAttack")]
    static class Harmony_JobDriverWait_CheckForAutoAttack_Patch
    {
        static readonly string logPrefix = Assembly.GetExecutingAssembly().GetName().Name + " :: " + typeof(Harmony_JobDriverWait_CheckForAutoAttack_Patch).Name + " :: ";
        static DynamicMethod Patched_ClosestThingTarget_Global = null;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            /* (ProfoundDarkness) Rough outline...
             * First find what the highest local variable index is (current code base is 12 but that might change).
             * Also while finding that locate the call to BestShootTargetFromCurrentPosition().
             * When found need to search backwards for an LdNull opcode as well as a label.
             * The ldNull is where our new local variable goes (replacement) and right after the label is where we insert our call to get the predicate.
             * 
             * 
             * Just realized... it's an argument stack so should be able to just load the verb on and then make the call.
             * The call should consume 1 argument and then leave a return value on the arg stack...
             */

            int verbLocalIndex = -1;
            int indexKeyCall = 0;

            // turn instructions into a list so we can walk through it variably (instead of linear only).

            List<CodeInstruction> code = instructions.ToList();

            // walk forward to find some key information.
            for (int i = 0; i < code.Count(); i++)
            {
                // look for the verb instanciation/storage.
                {
                    MethodBase method = null;
                    if (code[i].opcode == OpCodes.Callvirt && (method = code[i].operand as MethodBase) != null && method.DeclaringType == typeof(Pawn) && method.Name == "TryGetAttackVerb"
                        && code.Count() >= i + 1)
                        verbLocalIndex = OpcodeStoreIndex(code[i + 1]);
                }

                // see if we've found the instruction index of the key call.
                if (code[i].opcode == OpCodes.Call && (code[i].operand as MethodInfo) != null)
                {
                   MethodInfo method = code[i].operand as MethodInfo;
                    if (method.DeclaringType == typeof(AttackTargetFinder) && method.Name == "BestShootTargetFromCurrentPosition")
                    {
                        indexKeyCall = i;
                        break;
                    }
                }
            }

            Log.Message(string.Concat("verbLocalIndex ", verbLocalIndex));
            // walk backwards from the key call to locate the null load and replace it with our call.
            for (int i = indexKeyCall; i >= 0; i--)
            {
                if (code[i].opcode == OpCodes.Ldnull)
                {
                    CodeInstruction tmp = MakeLocalLoadInstruction(verbLocalIndex);
                    Log.Message(string.Concat("Got hit, replacing instruction with: ", tmp.opcode, " type: ", (tmp.operand == null ? "null" : tmp.operand.GetType().ToString() + " = " + tmp.operand)));
                    code[i++] = MakeLocalLoadInstruction(verbLocalIndex);
                    code.Insert(i, new CodeInstruction(OpCodes.Call, typeof(Harmony_JobDriverWait_CheckForAutoAttack_Patch).GetMethod("GetValidTargetPredicate", AccessTools.all)));
                    break;
                }
            }

            //for (int i = 0; i < code.Count(); i++)
            //    Log.Message(string.Concat(code[i].opcode, " - type(", (code[i].operand == null ? "null" : code[i].GetType().ToString()), ") = ", code[i].operand));


            //// old
            //List<HarmonyMethod> methods = typeof(JobDriver_Wait).GetHarmonyMethods();
            //HarmonyMethod method = methods.First<HarmonyMethod>();
            //(method.method as DynamicMethod).GetILGenerator();
            //(method.method as MethodBase).GetILGenerator();

            // walk forward to find some key information.
            //for (int i = 0; i < code.Count(); i++)
            //{
            // update the largest found local variable index.
            //    int tmp = 0;
            //if ((tmp = OpcodeStoreIndex(code[i])) >= 0 && tmp > largestLocalIndex)
            //    largestLocalIndex = tmp;

            // try to find the verb index...


            // see if we've found the instruction index of the key call.
            //    if (code[i].opcode == OpCodes.Call && (code[i].operand as MethodInfo) != null)
            //    {
            //MethodInfo method = code[i].operand as MethodInfo;
            //        if (method.DeclaringType == typeof(AttackTargetFinder) && method.Name == "BestShootTargetFromCurrentPosition")
            //            indexKeyCall = i;
            //    }
            //}

            // walk backwards from the key call to locate the null (replacement) and the label (insertion point)
            //for (int i = indexKeyCall; i >= 0; i--)
            //{
            // replacement detection.  Want to replace the null with load of our predicate.
            //if (code[i].opcode == OpCodes.Ldnull)
            //    code[i] = MakeLocalLoadInstruction(largestLocalIndex + 1);

            // insertion detection.  Want to insert after the label (looking upwards from key method). (label is metadata so ldarg.0...)
            //if (code[i].opcode == OpCodes.Ldarg_0)
            //{
            //code.Insert(i, new CodeInstruction)
            //code.Insert(i, new CodeInstruction(OpCodes.Call, ));
            //}
            //}

            return code;
        }

        /// <summary>
        /// Creates a store instruction to store a variable from the evaluation stack.
        /// </summary>
        /// <param name="index">index that the variable is supposed to go to.</param>
        /// <param name="storageType">Type of the object to store.</param>
        /// <returns>CodeInstruction with the correct opcode and possibly operand of the object to be stored.</returns>
        static CodeInstruction MakeLocalStoreInstruction(int index, Type storageType)
        {
            //DynamicMethod tmeth = new DynamicMethod("garbage", null, null);
            //ILGenerator ilGen = tmeth.GetILGenerator();
            //LocalBuilder var = ilGen.DeclareLocal(typeof(Predicate<Thing>));

            // argument check...
            if (index < 0 || index > UInt16.MaxValue)
                throw new ArgumentException("Index must be greater than 0 and less than " + uint.MaxValue.ToString() + ".");
            
            // the first 4 are easy...
            switch (index)
            {
                case 0:
                    return new CodeInstruction(OpCodes.Stloc_0);
                case 1:
                    return new CodeInstruction(OpCodes.Stloc_1);
                case 2:
                    return new CodeInstruction(OpCodes.Stloc_2);
                case 3:
                    return new CodeInstruction(OpCodes.Stloc_3);
            }

            // proper type info for the other items.
            if (index > Byte.MaxValue) return new CodeInstruction(OpCodes.Stloc_S, storageType);
            return new CodeInstruction(OpCodes.Stloc, storageType);
        }

        /// <summary>
        /// Return a CodeInstruction object with the correct opcode to fetch a local variable at a specific index.
        /// </summary>
        /// <param name="index">int value specifying the local variable index to fetch.</param>
        /// <returns>CodeInstruction object with the correct opcode to fetch a local variable into the evaluation stack.</returns>
        static CodeInstruction MakeLocalLoadInstruction(int index)
        {
            // argument check...
            if (index < 0 || index > UInt16.MaxValue)
                throw new ArgumentException("Index must be greater than 0 and less than " + uint.MaxValue.ToString() + ".");

            // the first 4 are easy...
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

            // proper type info for the other items.
            if (index > Byte.MaxValue) return new CodeInstruction(OpCodes.Ldloc, index);
            return new CodeInstruction(OpCodes.Ldloc_S, index);
        }

        /// <summary>
        /// Return the index of a local variable (based on storage opcode).
        /// </summary>
        /// <param name="instruction">CodeInstruction object from Harmony</param>
        /// <returns>int index of the local variable the instruction refers to. -1 if the opcode wasn't a storage opcode.</returns>
        static int OpcodeStoreIndex(CodeInstruction instruction)
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
        /// Returns a predicate for valid targets if the verb is a Verb_LaunchProjectileCE or descendent.
        /// </summary>
        /// <param name="verb">Verb that is to be checked for type and used for valid target checking</param>
        /// <returns>Predicate of type Thing which indicates if that thing is a valid target for the pawn.</returns>
        static Predicate<Thing> GetValidTargetPredicate(Verb verb)
        {
            Predicate<Thing> pred = null;
            if ((verb as Verb_LaunchProjectileCE) != null)
            {
                Verb_LaunchProjectileCE verbCE = verb as Verb_LaunchProjectileCE;
                pred = t => verbCE.CanHitTargetFrom(verb.caster.Position, new LocalTargetInfo(t));
            }

            return pred;
        }
    }
}