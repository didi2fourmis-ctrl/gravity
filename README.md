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
- **Joueur** : le détecteur de sol est déplacé au sommet du corps, ce qui permet de tenir au plafond.
  Sauts et walljumps sont inversés : on saute vers le sol d'origine.
- **Caméra** : l'image se retourne à la verticale avec une petite animation. C'est un vrai miroir et non
  une rotation, donc gauche et droite ne changent pas. L'axe Y de la souris est inversé automatiquement
  pour que la visée reste naturelle.
- Un indicateur s'affiche en haut de l'écran (gravité inversée, compte à rebours du chaos).
- Tout se règle dans `BepInEx/config/fr.samandine.gravityflip.cfg` : touches, durée de
  l'animation, délais du chaos, caméra, souris, remise à zéro à chaque niveau.

## Installation (sans compiler)

1. Installer [BepInEx 5](https://thunderstore.io/c/ultrakill/p/BepInEx/BepInExPack/) dans ULTRAKILL
   (ou passer par r2modman / Thunderstore Mod Manager).
2. Télécharger **`GravityFlip.dll`** dans l'onglet
   [**Releases**](https://github.com/didi2fourmis-ctrl/gravity/releases) du dépôt.
3. La copier dans `ULTRAKILL/BepInEx/plugins/GravityFlip/`.
4. Lancer le jeu et appuyer sur **G**.

La DLL est compilée automatiquement par GitHub Actions à chaque modification. Pour publier une
nouvelle version : créer un tag `v1.0.1` (par exemple), la release est générée toute seule.

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
- `src/GravityController.cs` : état de la gravité, mode chaos, détecteur de sol du joueur, indicateur
- `src/CameraFlipper.cs` : retournement de l'image (matrice de projection + culling), souris Y
- `src/Patches.cs` : patches Harmony qui inversent les impulsions verticales des sauts
- `src/KeyInput.cs` : lecture du clavier (Input System du jeu, par réflexion)
- `src/GameObjects.cs` : recherche du joueur et de la caméra
- `stubs/` : coquilles vides des classes du jeu, pour compiler sans le posséder
- `.github/workflows/build.yml` : compilation automatique et releases

## Limites connues

- **Pas encore testé en jeu**. Il repose sur des noms internes
  d'ULTRAKILL (`NewMovement.gc`, `NewMovement.Jump`/`WallJump`, `CameraController.reverseY`). Si une mise à jour les renomme, le mod l'indique dans la console BepInEx et
  désactive seulement la fonction touchée. Pour retrouver les bons noms, ouvrir `Assembly-CSharp.dll`
  avec dnSpy ou ILSpy.
- Le **slam** (accroupi en l'air) et le **dash** vertical gardent leur direction d'origine.
- Les ennemis qui marchent sur un NavMesh (la plupart des ennemis au sol) restent au sol. Seuls ceux
  qu'on éjecte ou qui deviennent physiques (ragdolls, ennemis projetés) partent vers le haut.
- Si l'arme flotte « à l'endroit » sur une image retournée, c'est voulu : elle est rendue par une
  caméra séparée.
