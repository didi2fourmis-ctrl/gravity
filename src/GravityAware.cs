using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GravityFlip
{
    /// <summary>Le « haut » et le « bas » selon la gravité actuelle.</summary>
    public static class GravityDirection
    {
        public static Vector3 Up() => GravityController.Inverted ? Vector3.down : Vector3.up;
        public static Vector3 Down() => GravityController.Inverted ? Vector3.up : Vector3.down;
    }

    /// <summary>
    /// Réécrit le code du jeu : dans les classes du joueur, des armes et des projectiles,
    /// chaque <c>Vector3.up</c> / <c>Vector3.down</c> devient le haut / bas de la gravité actuelle.
    /// Sauts, slam, lancer de pièce, chute des grenades, rebonds… suivent donc la gravité.
    /// </summary>
    internal static class UpVectorRewriter
    {
        private static readonly MethodInfo VectorUp = AccessTools.PropertyGetter(typeof(Vector3), nameof(Vector3.up));
        private static readonly MethodInfo VectorDown = AccessTools.PropertyGetter(typeof(Vector3), nameof(Vector3.down));
        private static readonly MethodInfo GravityUp = AccessTools.Method(typeof(GravityDirection), nameof(GravityDirection.Up));
        private static readonly MethodInfo GravityDown = AccessTools.Method(typeof(GravityDirection), nameof(GravityDirection.Down));

        public static void Init(Harmony harmony)
        {
            var names = new List<string>();
            names.AddRange(Split(Plugin.PlayerTypes.Value));
            names.AddRange(Split(Plugin.WeaponTypes.Value));

            var types = new List<Type>();
            var gcField = AccessTools.Field(typeof(NewMovement), "gc");
            if (gcField != null)
                types.Add(gcField.FieldType);

            var missing = new List<string>();
            foreach (var name in names.Distinct())
            {
                var type = AccessTools.TypeByName(name);
                if (type == null)
                    missing.Add(name);
                else if (!types.Contains(type))
                    types.Add(type);
            }

            int count = 0;
            foreach (var type in types.SelectMany(WithNestedTypes))
            {
                foreach (var method in AccessTools.GetDeclaredMethods(type))
                {
                    if (method.IsAbstract || method.ContainsGenericParameters || !UsesUpOrDown(method))
                        continue;
                    try
                    {
                        harmony.Patch(method, transpiler: new HarmonyMethod(typeof(UpVectorRewriter), nameof(Transpiler)));
                        count++;
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning($"Réécriture de {type.Name}.{method.Name} impossible : {e.Message}");
                    }
                }
            }

            Plugin.Log.LogInfo($"Gravité relative : {count} méthode(s) réécrite(s) dans " +
                $"{string.Join(", ", types.Select(t => t.Name))}.");
            if (missing.Count > 0)
                Plugin.Log.LogInfo($"Classes absentes de cette version du jeu (ignorées) : {string.Join(", ", missing)}.");
        }

        private static IEnumerable<string> Split(string list) =>
            (list ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        // Les coroutines et lambdas sont compilées dans des classes imbriquées.
        private static IEnumerable<Type> WithNestedTypes(Type type)
        {
            yield return type;
            foreach (var nested in type.GetNestedTypes(AccessTools.all))
                foreach (var inner in WithNestedTypes(nested))
                    yield return inner;
        }

        private static bool UsesUpOrDown(MethodBase method)
        {
            try
            {
                return PatchProcessor.GetOriginalInstructions(method)
                    .Any(i => i.Calls(VectorUp) || i.Calls(VectorDown));
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(VectorUp))
                    instruction.operand = GravityUp;
                else if (instruction.Calls(VectorDown))
                    instruction.operand = GravityDown;
                yield return instruction;
            }
        }
    }
}
