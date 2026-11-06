# Consignes pour Claude

- **Compiler systématiquement** après chaque modification du mod, avant de commit :
  `dotnet build GravityFlip.csproj -c Release`. Ne jamais pousser un code qui ne compile pas.
- Si `dotnet` manque : `apt-get install -y dotnet-sdk-8.0`. Les paquets BepInEx viennent de
  `nuget.bepinex.dev` (autorisé dans l'environnement cloud « gravity »).
- Envoyer ensuite la DLL compilée `bin/Release/GravityFlip.dll` à l'utilisatrice / l'utilisateur.
- Pour une nouvelle version, augmenter le numéro dans `src/Plugin.cs` (`PluginVersion`),
  `manifest.json` (`version_number`) et `GravityFlip.csproj` (`<Version>`).
