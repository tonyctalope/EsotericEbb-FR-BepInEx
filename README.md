# Esoteric Ebb - Traduction francaise

Traduction francaise statique de *Esoteric Ebb*.

Le zip de release ne demande pas BepInEx : il contient un patcher qui modifie les assets Unity locaux du joueur, avec sauvegarde et restauration.

## Etat

- Dialogues : 74 665 / 74 665
- UI : 17 / 17
- Quetes : 235 / 235
- Glossaire : 680 / 680
- Feats : 52 / 52
- SheetInfo : 252 / 252
- Validation locale : 75 901 entrees, 0 erreur, 2 avertissements de longueur

Le rapport complet est dans `docs/ETAT_LOCALISATION_FR.md`.

## Installation joueur

1. Telecharger le zip de release.
2. Extraire le contenu du zip a la racine du jeu, de facon a obtenir :

```text
Esoteric Ebb/
  translations/
  tools/
    StaticInkPatcher/
  Patch-French-Static.ps1
  Restore-Original-Assets.ps1
```

3. Fermer le jeu s'il est lance, puis appliquer le patch :

```powershell
powershell -ExecutionPolicy Bypass -File .\Patch-French-Static.ps1
```

Le patcher remplace les tables de localisation dans `resources.assets`, les dialogues Ink dans `sharedassets*.assets`, et les libelles de scene TextMeshPro connus comme le menu principal. Il cree une sauvegarde dans `EsotericEbb-FR-StaticBackup/` avant de modifier les fichiers.

4. Lancer le jeu.

Pour restaurer les assets originaux :

```powershell
powershell -ExecutionPolicy Bypass -File .\Restore-Original-Assets.ps1
```

## Build

Prerequis :

- .NET SDK 8 ou plus recent ;
- PowerShell pour les scripts.

Construire un zip de release :

```powershell
.\scripts\Build-Release.ps1 -Version 0.1.0
```

Le zip contient les traductions, le patcher statique Windows x64 et les scripts d'application/restauration.

Installer localement dans un dossier de jeu :

```powershell
.\scripts\Install-Local.ps1 -GamePath "E:\SteamLibrary\steamapps\common\Esoteric Ebb"
```

Regenerer les assets depuis le workspace de traduction :

```powershell
.\scripts\Refresh-Translations.ps1 -SourcePath "E:\SteamLibrary\steamapps\common\Esoteric Ebb\_translation_work\localized_textassets"
```

Si Python n'est pas dans le `PATH`, passer explicitement son chemin :

```powershell
.\scripts\Refresh-Translations.ps1 -SourcePath "...\localized_textassets" -PythonPath "C:\Python312\python.exe"
```

Si Windows bloque les scripts PowerShell :

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-Release.ps1 -Version 0.1.0
```

## Architecture

Le patcher statique remplace les tables Unity nommees `Dialogs`, `UIElements`, `QuestPoints`, `GlossaryTerms`, `Feats` et `SheetInfo`, couvre les dialogues Ink compiles dans `sharedassets*.assets`, puis patch aussi une liste blanche de textes de scene serialises dans les composants TextMeshPro.

Le depot conserve du code BepInEx historique pour debug/dev, mais le package joueur est statique-only.

Voir `docs/ARCHITECTURE.md` pour les details.

## Publier

Le code du patcher est sous licence MIT. Les traductions sont separees juridiquement : lire `LEGAL.md` avant une publication publique.

La checklist de release est dans `docs/PUBLISHING.md`. Le workflow `.github/workflows/release.yml` compile le patcher et attache automatiquement le zip d'installation quand une GitHub Release est publiee.

Pour initialiser le depot quand Git est disponible :

```powershell
git init
git add .
git commit -m "Initial French translation mod"
```
