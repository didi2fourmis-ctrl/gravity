using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GravityFlip
{
    /// <summary>
    /// Pendant un saut du joueur, toute impulsion verticale appliquée à son Rigidbody est
    /// inversée quand la gravité l'est : on saute "vers le bas" depuis le plafond.
    /// </summary>
    internal static class JumpScope
    {
        internal static int Depth;
        internal static Rigidbody PlayerBody;

        internal static bool ShouldMirror(Rigidbody body) =>
            Depth > 0 && GravityController.Inverted && body == PlayerBody;
    }

    [HarmonyPatch]
    internal static class NewMovementJumpPatch
    {
        private static readonly string[] JumpMethods = { "Jump", "WallJump" };

        private static IEnumerable<MethodBase> Candidates() =>
            AccessTools.GetDeclaredMethods(typeof(NewMovement))
                .Where(m => JumpMethods.Contains(m.Name))
                .Cast<MethodBase>();

        private static bool Prepare()
        {
            bool found = Candidates().Any();
            if (!found)
                Plugin.Log.LogWarning("Méthodes de saut introuvables : les sauts ne seront pas inversés.");
            return found;
        }

        private static IEnumerable<MethodBase> TargetMethods() => Candidates();

        private static void Prefix(NewMovement __instance)
        {
            JumpScope.Depth++;
            JumpScope.PlayerBody = __instance.GetComponent<Rigidbody>();
        }

        private static void Finalizer()
        {
            if (JumpScope.Depth > 0)
                JumpScope.Depth--;
        }
    }

    [HarmonyPatch(typeof(Rigidbody), nameof(Rigidbody.AddForce), typeof(Vector3), typeof(ForceMode))]
    internal static class RigidbodyAddForcePatch
    {
        private static void Prefix(Rigidbody __instance, ref Vector3 __0)
        {
            if (JumpScope.ShouldMirror(__instance))
                __0.y = -__0.y;
        }
    }

    [HarmonyPatch(typeof(Rigidbody), nameof(Rigidbody.velocity), MethodType.Setter)]
    internal static class RigidbodyVelocityPatch
    {
        private static void Prefix(Rigidbody __instance, ref Vector3 __0)
        {
            if (JumpScope.ShouldMirror(__instance))
                __0.y = -__0.y;
        }
    }
}
