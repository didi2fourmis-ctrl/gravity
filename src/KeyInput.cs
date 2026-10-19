using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GravityFlip
{
    /// <summary>
    /// Lecture du clavier. ULTRAKILL utilise le nouveau Input System de Unity : on y accède par
    /// réflexion pour ne pas dépendre de Unity.InputSystem.dll à la compilation. Si le jeu
    /// autorise encore l'ancien système, il sert de solution de repli.
    /// </summary>
    internal static class KeyInput
    {
        private static bool resolved;
        private static PropertyInfo currentKeyboard;
        private static PropertyInfo keyIndexer;
        private static PropertyInfo wasPressedThisFrame;
        private static Type keyType;
        private static bool legacyUnavailable;

        private static readonly Dictionary<string, object> KeyCache = new Dictionary<string, object>();
        private static readonly HashSet<string> WarnedNames = new HashSet<string>();

        public static bool WasPressed(string keyName)
        {
            if (string.IsNullOrEmpty(keyName) || keyName.Equals("None", StringComparison.OrdinalIgnoreCase))
                return false;

            Resolve();
            if (currentKeyboard != null)
                return WasPressedInputSystem(keyName);
            return WasPressedLegacy(keyName);
        }

        private static bool WasPressedInputSystem(string keyName)
        {
            var keyboard = currentKeyboard.GetValue(null, null);
            if (keyboard == null)
                return false;

            if (!KeyCache.TryGetValue(keyName, out var key))
            {
                try { key = Enum.Parse(keyType, keyName, true); }
                catch (ArgumentException) { key = null; Warn(keyName); }
                KeyCache[keyName] = key;
            }
            if (key == null)
                return false;

            var control = keyIndexer.GetValue(keyboard, new[] { key });
            if (control == null)
                return false;

            if (wasPressedThisFrame == null)
                wasPressedThisFrame = control.GetType().GetProperty("wasPressedThisFrame");
            return wasPressedThisFrame != null && (bool)wasPressedThisFrame.GetValue(control, null);
        }

        private static bool WasPressedLegacy(string keyName)
        {
            if (legacyUnavailable)
                return false;
            try
            {
                return Input.GetKeyDown((KeyCode)Enum.Parse(typeof(KeyCode), keyName, true));
            }
            catch (ArgumentException)
            {
                Warn(keyName);
                return false;
            }
            catch (InvalidOperationException)
            {
                legacyUnavailable = true;
                Plugin.Log.LogWarning("Aucun système de saisie clavier utilisable : les touches du mod sont désactivées.");
                return false;
            }
        }

        private static void Resolve()
        {
            if (resolved)
                return;
            resolved = true;

            Type keyboardType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "Unity.InputSystem")
                    continue;
                keyboardType = assembly.GetType("UnityEngine.InputSystem.Keyboard");
                keyType = assembly.GetType("UnityEngine.InputSystem.Key");
                break;
            }
            if (keyboardType == null || keyType == null)
                return;

            keyIndexer = keyboardType.GetProperty("Item", new[] { keyType });
            if (keyIndexer != null)
                currentKeyboard = keyboardType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
        }

        private static void Warn(string keyName)
        {
            if (WarnedNames.Add(keyName))
                Plugin.Log.LogWarning($"Touche inconnue dans la config : \"{keyName}\".");
        }
    }
}
