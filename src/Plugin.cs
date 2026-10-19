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
        public const string PluginVersion = "1.2.1";

        internal static ManualLogSource Log;

        internal static ConfigEntry<string> ToggleKey;
        internal static ConfigEntry<string> ChaosKey;
        internal static ConfigEntry<float> FlipDuration;
        internal static ConfigEntry<float> ChaosMinDelay;
        internal static ConfigEntry<float> ChaosMaxDelay;
        internal static ConfigEntry<bool> FlipPlayer;
        internal static ConfigEntry<bool> InvertMouseX;
        internal static ConfigEntry<float> HudX;
        internal static ConfigEntry<float> HudY;
        internal static ConfigEntry<string> PlayerTypes;
        internal static ConfigEntry<string> WeaponTypes;
        internal static ConfigEntry<bool> ResetOnSceneLoad;
        internal static ConfigEntry<bool> ShowHud;
        internal static ConfigEntry<bool> CeilingWalk;
        internal static ConfigEntry<float> CeilingTolerance;
        internal static ConfigEntry<bool> DebugLogs;

        private void Awake()
        {
            Log = Logger;

            ToggleKey = Config.Bind("Contrôles", "ToucheInversion", "G",
                "Touche pour inverser / rétablir la gravité (nom de touche Unity : G, F1, Digit1, LeftAlt…).");
            ChaosKey = Config.Bind("Contrôles", "ToucheChaos", "H",
                "Active / désactive le mode chaos (la gravité s'inverse toute seule à intervalles aléatoires).");

            FlipDuration = Config.Bind("Joueur", "DureeRetournement", 0.35f,
                "Durée (secondes) du retournement du joueur quand la gravité change.");
            FlipPlayer = Config.Bind("Joueur", "RetournerJoueur", true,
                "Retourne le joueur tête en bas quand la gravité est inversée : ses pieds touchent le plafond.");
            InvertMouseX = Config.Bind("Joueur", "InverserSourisX", false,
                "Inverse la souris gauche/droite quand le joueur est retourné. À activer seulement si tourner semble à l'envers.");

            ShowHud = Config.Bind("Indicateur", "AfficherIndicateur", true,
                "Affiche un texte quand la gravité est inversée ou que le chaos est actif.");
            HudX = Config.Bind("Indicateur", "PositionX", 0.02f,
                "Position horizontale du texte (0 = bord gauche, 1 = bord droit).");
            HudY = Config.Bind("Indicateur", "PositionY", 0.74f,
                "Position verticale du BAS du texte (0 = haut de l'écran, 1 = bas). 0.74 = juste au-dessus du HUD des armes.");

            PlayerTypes = Config.Bind("Avancé", "ClassesJoueur", "NewMovement,GroundCheck,GroundCheckGroup,WallCheck",
                "Classes du jeu où « haut » et « bas » suivent la gravité (saut, slam, détection du sol).");
            WeaponTypes = Config.Bind("Avancé", "ClassesArmes",
                "Revolver,Coin,Shotgun,ShotgunHammer,Grenade,Nailgun,Nail,RocketLauncher,Cannonball,Railcannon,Harpoon,Chainsaw,Magnet,Punch,GunControl,WeaponCharges",
                "Classes des armes et projectiles où « haut » et « bas » suivent la gravité (pièces, grenades, clous…).");

            ChaosMinDelay = Config.Bind("Chaos", "DelaiMin", 4f, "Délai minimum (secondes) entre deux inversions en mode chaos.");
            ChaosMaxDelay = Config.Bind("Chaos", "DelaiMax", 12f, "Délai maximum (secondes) entre deux inversions en mode chaos.");

            ResetOnSceneLoad = Config.Bind("Général", "ReinitialiserAuChargement", true,
                "Remet la gravité à la normale à chaque chargement de niveau.");

            CeilingWalk = Config.Bind("Joueur", "MarcherAuPlafond", true,
                "Quand la gravité est inversée et que le joueur touche le plafond, le jeu le considère au sol (marche, glissade, saut).");
            CeilingTolerance = Config.Bind("Joueur", "TolerancePlafond", 0.3f,
                "Distance (mètres) entre le haut du joueur et le plafond en dessous de laquelle il est considéré au sol.");
            DebugLogs = Config.Bind("Général", "AfficherLogs", false,
                "Affiche chaque seconde dans la console si le joueur touche le plafond et si le jeu le croit au sol.");

            var harmony = ApplyPatches();
            UpVectorRewriter.Init(harmony);
            GroundForcer.Init(harmony);

            // Objet dédié qui survit aux changements de scène.
            var host = new GameObject("GravityFlip");
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<GravityController>();
            host.AddComponent<PlayerRoller>();
            host.AddComponent<CeilingContact>();

            Log.LogInfo($"{PluginName} {PluginVersion} chargé. [{ToggleKey.Value}] inverser la gravité, [{ChaosKey.Value}] mode chaos.");
        }

        // Chaque classe de patch est appliquée séparément : si l'une échoue
        // (mise à jour du jeu, méthode renommée…), les autres fonctionnent quand même.
        private static Harmony ApplyPatches()
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
            return harmony;
        }
    }
}
