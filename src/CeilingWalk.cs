using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GravityFlip
{
    /// <summary>
    /// Détecte si le joueur touche le plafond, avec un SphereCast vers le haut depuis son corps.
    /// Tourne avant les scripts du jeu, à chaque image et à chaque pas de physique.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class CeilingContact : MonoBehaviour
    {
        public static bool Touching { get; private set; }

        /// <summary>Après un saut, on arrête de forcer le contact pour laisser le joueur décoller.</summary>
        public static float JumpGraceUntil;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private static int environmentMask;

        public static bool ShouldForceGround =>
            Plugin.CeilingWalk.Value && GravityController.Inverted && Touching && Time.time >= JumpGraceUntil;

        private void Update() => Refresh();
        private void FixedUpdate() => Refresh();

        private static void Refresh()
        {
            Touching = false;
            if (!GravityController.Inverted)
                return;

            var player = GameObjects.Player;
            if (player == null || !player.isActiveAndEnabled)
                return;

            var capsule = player.GetComponent<CapsuleCollider>();
            var body = player.GetComponent<Rigidbody>();
            Transform t = player.transform;

            Vector3 center = capsule != null ? t.TransformPoint(capsule.center) : t.position;
            float scale = Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.z));
            float radius = capsule != null ? capsule.radius * scale * 0.9f : 0.45f;
            float halfHeight = capsule != null ? capsule.height * Mathf.Abs(t.lossyScale.y) * 0.5f : 1.25f;
            float distance = Mathf.Max(0f, halfHeight - radius) + Plugin.CeilingTolerance.Value;

            if (environmentMask == 0)
                environmentMask = BuildEnvironmentMask(player.gameObject.layer);

            int count = Physics.SphereCastNonAlloc(center, radius, Vector3.up, Hits, distance,
                environmentMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit.collider == null)
                    continue;
                if (body != null && hit.collider.attachedRigidbody == body)
                    continue;
                if (hit.collider.transform.IsChildOf(t))
                    continue;
                // Une surface qui fait face vers le bas (normale vers le bas) est un plafond praticable.
                if (hit.distance > 0f && hit.normal.y > -0.5f)
                    continue;
                Touching = true;
                return;
            }
        }

        // Les calques du décor d'ULTRAKILL. S'ils n'existent pas, on prend tout sauf le calque du joueur.
        private static int BuildEnvironmentMask(int playerLayer)
        {
            int mask = LayerMask.GetMask("Environment", "Outdoors", "EnvironmentBaked", "OutdoorsBaked");
            if (mask == 0)
                mask = Physics.DefaultRaycastLayers & ~(1 << playerLayer);
            Plugin.Log.LogInfo($"Détection du plafond : masque de calques 0x{mask:X}.");
            return mask;
        }
    }

    /// <summary>
    /// Fait croire au jeu que le joueur est au sol quand il touche le plafond. On ne connaît pas
    /// le code exact du détecteur de sol : on cherche, par réflexion, tous ses booléens et propriétés
    /// du type « onGround » / « touchingGround », et on les force à vrai après chacune de ses méthodes Unity.
    /// </summary>
    internal static class GroundForcer
    {
        private static readonly string[] GroundNames = { "onground", "touchingground", "grounded" };
        private static readonly string[] UnityMessages =
            { "Update", "FixedUpdate", "LateUpdate", "OnTriggerEnter", "OnTriggerStay", "OnTriggerExit", "OnCollisionStay" };

        private static FieldInfo[] groundFields = new FieldInfo[0];

        public static void Init(Harmony harmony)
        {
            var gcField = AccessTools.Field(typeof(NewMovement), "gc");
            if (gcField == null)
            {
                Plugin.Log.LogWarning("NewMovement.gc introuvable : marche au plafond impossible.");
                return;
            }

            var types = new List<Type> { gcField.FieldType, typeof(NewMovement) };
            int patched = 0;

            foreach (var type in types)
            {
                var fields = AccessTools.GetDeclaredFields(type)
                    .Where(f => !f.IsStatic && f.FieldType == typeof(bool) && IsGroundName(f.Name))
                    .ToArray();
                if (type != typeof(NewMovement))
                    groundFields = groundFields.Concat(fields).ToArray();

                foreach (var property in AccessTools.GetDeclaredProperties(type))
                {
                    if (property.PropertyType != typeof(bool) || !IsGroundName(property.Name))
                        continue;
                    var getter = property.GetGetMethod(true);
                    if (getter == null || getter.IsStatic)
                        continue;
                    if (TryPatch(harmony, getter, nameof(GetterPostfix)))
                        patched++;
                }
            }

            foreach (var name in UnityMessages)
            {
                var method = AccessTools.DeclaredMethod(gcField.FieldType, name);
                if (method != null && TryPatch(harmony, method, nameof(MessagePostfix)))
                    patched++;
            }

            Plugin.Log.LogInfo($"Marche au plafond : détecteur {gcField.FieldType.Name}, champs forcés " +
                $"[{string.Join(", ", groundFields.Select(f => f.Name))}], {patched} méthode(s) patchée(s).");
        }

        public static void ForceNow(object groundCheck)
        {
            if (groundCheck == null)
                return;
            foreach (var field in groundFields)
                field.SetValue(groundCheck, true);
        }

        private static bool IsGroundName(string name)
        {
            string lower = name.ToLowerInvariant().Trim('<', '>', '_');
            return GroundNames.Any(g => lower == g || lower.StartsWith(g + ">"));
        }

        private static bool TryPatch(Harmony harmony, MethodBase method, string postfix)
        {
            try
            {
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(GroundForcer), postfix));
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Patch {method.DeclaringType?.Name}.{method.Name} non appliqué : {e.Message}");
                return false;
            }
        }

        private static void MessagePostfix(object __instance)
        {
            if (CeilingContact.ShouldForceGround)
                ForceNow(__instance);
        }

        private static void GetterPostfix(ref bool __result)
        {
            if (CeilingContact.ShouldForceGround)
                __result = true;
        }
    }
}
