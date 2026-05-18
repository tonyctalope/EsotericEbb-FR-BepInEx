# Publication

## Avant publication

1. Verifier les droits de distribution des textes traduits. Voir `LEGAL.md`.
2. Installer Git et .NET SDK.
3. Initialiser le depot :

```powershell
git init
git add .
git commit -m "Initial French translation mod"
```

4. Pousser vers GitHub ou une forge equivalente.
5. Verifier que l'onglet Actions passe au vert sur le workflow `CI`.

## Release automatique GitHub

Le workflow `.github/workflows/release.yml` s'execute quand une GitHub Release est publiee.

Procedure recommandee :

```powershell
git tag v0.1.0
git push origin main --tags
```

Ensuite, sur GitHub :

1. ouvrir `Releases` ;
2. creer une release depuis le tag `v0.1.0` ;
3. publier la release.

GitHub Actions va alors :

- restaurer les packages NuGet ;
- recuperer les references UnityEngine de compilation via `UnityEngine.Modules` ;
- compiler `EsotericEbbFrench.dll` en `Release` ;
- publier `StaticInkPatcher.exe` en Windows x64 self-contained ;
- construire le zip d'installation avec le mod, les traductions, le patcher et les scripts ;
- attacher automatiquement `EsotericEbb-FR-BepInEx-0.1.0.zip` a la release.

Le workflow peut aussi etre lance a la main via `workflow_dispatch`. Dans ce cas il produit un artefact Actions, mais n'attache rien a une release GitHub existante.

## Build local

Construire localement reste possible :

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-Release.ps1 -Version 0.1.0
```

## Release

Le zip attendu contient directement :

```text
BepInEx/
  plugins/
    EsotericEbbFrench/
      EsotericEbbFrench.dll
      translations/
tools/
  StaticInkPatcher/
    StaticInkPatcher.exe
Patch-French-Static.ps1
Restore-Original-Assets.ps1
```

Les joueurs doivent l'extraire a la racine du jeu, puis lancer `Patch-French-Static.ps1` une fois pour patcher les dialogues Ink. Le script refuse de patcher si le jeu est ouvert et cree `EsotericEbb-FR-StaticBackup/` pour permettre une restauration.

## Test minimum

- BepInEx genere `BepInEx/LogOutput.log`.
- Le log contient `Esoteric Ebb - Traduction francaise`.
- `Patch-French-Static.ps1` affiche un resume du type `Patch termine`.
- Une nouvelle partie affiche les dialogues, choix et textes UI en francais.
- Les accents et balises de style s'affichent correctement.
