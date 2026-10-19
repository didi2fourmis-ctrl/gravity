# Gravity Flip — mod ULTRAKILL

**Inverse la gravité.** Le plafond devient le sol : tu marches, glisses et sautes depuis le plafond,
et les ennemis éjectés, les cadavres, les pièces de la Marksman, les grenades et les projectiles
« tombent » vers le haut.

## Fonctionnalités

| Touche | Effet |
|---|---|
| **G** | Inverser / rétablir la gravité |
| **H** | Mode **chaos** : la gravité s'inverse toute seule toutes les 4 à 12 s |

- `Physics.gravity` est inversée : tout objet physique est concerné.
- **Le joueur se retourne vraiment** : il pivote de 180°, ses pieds se posent au plafond et la caméra
  reste à hauteur d'yeux. La vue est donc naturellement à l'envers. On y marche, glisse, saute et
  slamme comme sur un sol normal. La souris reste naturelle, le mod compense la rotation gauche/droite.
- **Les armes suivent la gravité** : dans le code du joueur, des armes et des projectiles, chaque
  « haut » et « bas » du jeu devient le haut et le bas de la gravité actuelle. Les pièces sont lancées
  vers le haut du joueur, et les grenades, boulets et clous tombent vers le plafond.
- **Indicateur** : il s'affiche juste au-dessus du HUD des armes, en bas à gauche, avec le compte à
  rebours du chaos. Sa position se règle (`PositionX`, `PositionY`).
- Tout se règle dans `BepInEx/config/fr.samandine.gravityflip.cfg` : touches, durée du
  retournement, délais du chaos, position de l'indicateur, remise à zéro à chaque niveau, classes du jeu
  concernées par la gravité.

## Installation (sans compiler)

1. Installer [BepInEx 5](https://thunderstore.io/c/ultrakill/p/BepInEx/BepInExPack/) dans ULTRAKILL
   (ou passer par r2modman / Thunderstore Mod Manager).
2. Télécharger **`GravityFlip.dll`** dans l'onglet
   [**Releases**](https://github.com/didi2fourmis-ctrl/gravity/releases) du dépôt.
3. La copier dans `ULTRAKILL/BepInEx/plugins/GravityFlip/`.
4. Lancer le jeu et appuyer sur **G**.

La DLL est compilée automatiquement par GitHub Actions à chaque modification de `main`, et la release
correspondant à `version_number` dans `manifest.json` est mise à jour. Pour publier une nouvelle version,
il suffit d'augmenter ce numéro (et `<Version>` dans `GravityFlip.csproj`).

## Compiler soi-même (facultatif)

Il suffit du [SDK .NET 8](https://dotnet.microsoft.com/download). Le jeu n'est **pas** nécessaire :
BepInEx et Unity viennent de NuGet, et les classes du jeu de coquilles vides (`stubs/`).

```bash
dotnet build GravityFlip.csproj -c Release
```

Si `ULTRAKILL/BepInEx/plugins` existe au chemin Steam par défaut, la DLL y est copiée
(sinon : `-p:GamePath="D:\SteamLibrary\steamapps\common\ULTRAKILL"`).

## Structure

- `src/Plugin.cs` : point d'entrée BepInEx, configuration, application des patches
- `src/GravityController.cs` : état de la gravité, mode chaos, indicateur
- `src/PlayerRoller.cs` : retournement du joueur (pieds au plafond) et compensation de la souris
- `src/GravityAware.cs` : réécrit les `Vector3.up/down` du joueur et des armes selon la gravité
- `src/CeilingWalk.cs` : détection du plafond et forçage de l'état « au sol »
- `src/Patches.cs` : patches Harmony qui inversent les impulsions verticales des sauts
- `src/KeyInput.cs` : lecture du clavier (Input System du jeu, par réflexion)
- `src/GameObjects.cs` : recherche du joueur et de la caméra
- `stubs/` : coquilles vides des classes du jeu, pour compiler sans le posséder
- `.github/workflows/build.yml` : compilation automatique et releases

## Limites connues

- **Pas encore testé en jeu**. Il repose sur des noms internes
  d'ULTRAKILL (`NewMovement.gc`, `NewMovement.Jump`/`WallJump`, `CameraController.rotationY`, noms des armes). Si une mise à jour les renomme, le mod l'indique dans la console BepInEx et
  désactive seulement la fonction touchée. Pour retrouver les bons noms, ouvrir `Assembly-CSharp.dll`
  avec dnSpy ou ILSpy.
- Une arme dont le code ne dit pas « haut » avec `Vector3.up` (par exemple `new Vector3(0, 1, 0)`) garde
  son comportement d'origine. Si une arme n'est pas affectée, ajoute le nom de sa classe dans `ClassesArmes`.
- Les ennemis qui marchent sur un NavMesh (la plupart des ennemis au sol) restent au sol. Seuls ceux
  qu'on éjecte ou qui deviennent physiques (ragdolls, ennemis projetés) partent vers le haut.

## Si quelque chose ne marche pas

Mets `AfficherLogs = true` dans la config, relance, inverse la gravité et colle-toi au plafond.
La console affiche chaque seconde `plafond touché : True/False` et `gc.onGround : True/False`.
Au démarrage, elle affiche aussi les lignes `Marche au plafond : ...` et `Gravité relative : ...`.
Envoie ces lignes : elles montrent ce qui bloque.
