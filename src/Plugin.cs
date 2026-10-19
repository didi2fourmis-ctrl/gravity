using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace GravityFlip
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "fr.samandine.gravityflip";
        public const string PluginName = "Gravity Flip";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<string> ToggleKey;
        internal static ConfigEntry<string> ChaosKey;
        internal static ConfigEntry<float> FlipDuration;
        internal static ConfigEntry<float> ChaosMinDelay;
        internal static ConfigEntry<float> ChaosMaxDelay;
        internal static ConfigEntry<bool> FlipCamera;
        internal static ConfigEntry<bool> InvertMouseY;
        internal static ConfigEntry<bool> ResetOnSceneLoad;
        internal static ConfigEntry<bool> ShowHud;

        private void Awake()
        {
            Log = Logger;

            ToggleKey = Config.Bind("Contrôles", "ToucheInversion", "G",
                "Touche pour inverser / rétablir la gravité (nom de touche Unity : G, F1, Digit1, LeftAlt…).");
            ChaosKey = Config.Bind("Contrôles", "ToucheChaos", "H",
                "Active / désactive le mode chaos (la gravité s'inverse toute seule à intervalles aléatoires).");

            FlipDuration = Config.Bind("Visuel", "DureeRetournement", 0.35f,
                "Durée (secondes) de l'animation de retournement de la caméra.");
            FlipCamera = Config.Bind("Visuel", "RetournerCamera", true,
                "Retourne l'image à l'écran quand la gravité est inversée (le plafond devient le sol).");
            InvertMouseY = Config.Bind("Visuel", "InverserSourisY", true,
                "Inverse l'axe Y de la souris quand l'image est retournée, pour que viser reste naturel.");
            ShowHud = Config.Bind("Visuel", "AfficherIndicateur", true,
                "Affiche un indicateur en haut de l'écran quand la gravité est inversée ou que le chaos est actif.");

            ChaosMinDelay = Config.Bind("Chaos", "DelaiMin", 4f, "Délai minimum (secondes) entre deux inversions en mode chaos.");
            ChaosMaxDelay = Config.Bind("Chaos", "DelaiMax", 12f, "Délai maximum (secondes) entre deux inversions en mode chaos.");

            ResetOnSceneLoad = Config.Bind("Général", "ReinitialiserAuChargement", true,
                "Remet la gravité à la normale à chaque chargement de niveau.");

            ApplyPatches();

            // Objet dédié qui survit aux changements de scène.
            var host = new GameObject("GravityFlip");
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<GravityController>();
            host.AddComponent<CameraFlipper>();

            Log.LogInfo($"{PluginName} {PluginVersion} chargé. [{ToggleKey.Value}] inverser la gravité, [{ChaosKey.Value}] mode chaos.");
        }

        // Chaque classe de patch est appliquée séparément : si l'une échoue
        // (mise à jour du jeu, méthode renommée…), les autres fonctionnent quand même.
        private static void ApplyPatches()
        {
            var harmony = new Harmony(PluginGuid);
            foreach (var type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0)
                    continue;
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception e)
                {
                    Log.LogWarning($"Patch {type.Name} non appliqué : {e.Message}");
                }
            }
        }
    }
}
