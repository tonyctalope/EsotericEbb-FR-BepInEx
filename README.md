# Esoteric Ebb - Traduction francaise

Mod BepInEx IL2CPP pour charger une traduction francaise complete de *Esoteric Ebb* sans modifier les fichiers du jeu.

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

1. Installer BepInEx 6 IL2CPP x64 dans le dossier du jeu : <https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html>
2. Telecharger le zip de release du mod.
3. Extraire le contenu du zip a la racine du jeu, de facon a obtenir :

```text
Esoteric Ebb/
  BepInEx/
    plugins/
      EsotericEbbFrench/
        EsotericEbbFrench.dll
        translations/
```

4. Lancer le jeu.

Par defaut, le mod utilise le profil `german-slot`, qui copie le francais dans l'emplacement allemand pour maximiser la compatibilite. Si le jeu expose une vraie option francaise, lancer une fois le jeu puis modifier :

```text
BepInEx/config/fr.esotericebb.translation.cfg
```

et mettre :

```ini
Profile = fr-columns
```

## Build

Prerequis :

- .NET SDK 8 ou plus recent ;
- BepInEx 6 IL2CPP installe cote joueur ;
- PowerShell pour les scripts.

Le depot inclut `NuGet.config` avec le feed officiel BepInEx necessaire a `BepInEx.Unity.IL2CPP`. La compilation CI reference aussi `UnityEngine.Modules`, qui fournit les assemblies UnityEngine au compilateur sans les embarquer dans le zip final.

Construire un zip de release :

```powershell
.\scripts\Build-Release.ps1 -Version 0.1.0
```

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

Le mod ne patche pas `resources.assets`. Il intercepte le texte des `TextAsset` Unity nommes `Dialogs`, `UIElements`, `QuestPoints`, `GlossaryTerms`, `Feats` et `SheetInfo`, puis renvoie les fichiers localises depuis `BepInEx/plugins/EsotericEbbFrench/translations/`.

Voir `docs/ARCHITECTURE.md` pour les details.

## Publier

Le code du mod est sous licence MIT. Les traductions sont separees juridiquement : lire `LEGAL.md` avant une publication publique.

La checklist de release est dans `docs/PUBLISHING.md`. Le workflow `.github/workflows/release.yml` compile le mod et attache automatiquement le zip d'installation quand une GitHub Release est publiee.

Pour initialiser le depot quand Git est disponible :

```powershell
git init
git add .
git commit -m "Initial French translation mod"
```
