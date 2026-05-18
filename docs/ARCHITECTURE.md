# Architecture

## Objectif

Fournir une traduction francaise complete de *Esoteric Ebb* avec une solution reversible :

- BepInEx pour les tables de localisation, l'UI et les textes charges a l'execution ;
- un patcher statique pour les dialogues Ink compiles dans les assets Unity locaux du joueur.

## Strategie BepInEx

Le jeu stocke une partie de ses textes globaux dans des `TextAsset` Unity :

- `Dialogs`
- `UIElements`
- `QuestPoints`
- `GlossaryTerms`
- `Feats`
- `SheetInfo`

Le mod charge des fichiers `.txt` externes portant les memes noms depuis :

```text
BepInEx/plugins/EsotericEbbFrench/translations/<profile>/
```

Il indexe aussi les lignes CSV en memoire, puis patche `LocalizationManager.ParseCSV`, `LocalizationManager.CheckLanguage` et `LocalizationManager.CheckDialogLanguage`. Quand le jeu parse ses tables, le mod remplace l'entree CSV par le profil francais ; quand le jeu demande ensuite le texte d'un ID localise, le mod peut aussi renvoyer directement la traduction francaise. Un patch `TextAsset.text` reste present comme filet de securite pour les chemins qui lisent encore le `TextAsset` brut.

Des patchs runtime supplementaires remplacent les textes deja presents dans les scenes, les composants TextMeshPro et certains appels de dialogue. Cette couche reste volontairement conservative pour limiter les crashs IL2CPP.

## Strategie statique Ink

Certains dialogues et choix ne passent pas par les tables CSV au moment de l'affichage. Ils sont compiles dans des `TextAsset` Ink individuels presents dans `sharedassets*.assets`, avec des marqueurs internes du type `LOC_1`, `LOC_2`, etc.

Le tool `tools/StaticInkPatcher` :

- lit les fichiers `sharedassets*.assets` avec `AssetsTools.NET` ;
- detecte les `TextAsset` Ink contenant `inkVersion` ;
- associe le nom de story et le marqueur `LOC_x` a la ligne correspondante de `assets/translations/fr-columns/Dialogs.txt` ;
- remplace la chaine Ink affichee par la traduction francaise ;
- sauvegarde les assets originaux dans `EsotericEbb-FR-StaticBackup/` avant toute ecriture.

Le depot ne contient pas et ne publie pas d'assets Unity modifies. Le patch s'applique uniquement sur l'installation locale du joueur et peut etre restaure via `Restore-Original-Assets.ps1`.

## Profils

### `fr-columns`

Profil propre. Il conserve les colonnes francaises natives :

- `Dialogs`: colonne `FR`
- autres tables : colonne `FRENCH`

Ce profil est ideal si le jeu expose une option francaise, ou si un patch futur force explicitement la langue `FR`.

### `english-slot`

Profil par defaut. Il conserve les colonnes francaises mais copie aussi :

- `Dialogs.FR` vers `Dialogs.EN`
- `FRENCH` vers `ENGLISH`

Il permet a la traduction de fonctionner meme quand le jeu reste sur sa langue anglaise par defaut ou ne propose pas encore de menu de langue.

### `german-slot`

Profil compatible alternatif. Il conserve les colonnes francaises mais copie aussi :

- `Dialogs.FR` vers `Dialogs.DE`
- `FRENCH` vers `GERMAN`

Il permet aux joueurs de choisir l'emplacement allemand dans le jeu pour afficher le francais, sans patcher le binaire ni les assets originaux.

## Pourquoi garder BepInEx en plus ?

Le patch statique regle les dialogues Ink en jeu, mais BepInEx reste utile :

- il charge les tables UI/quetes/glossaire/feats/sheet info ;
- il couvre les textes crees dynamiquement a l'execution ;
- il permet de corriger des termes ou labels sans repatcher tous les assets ;
- il garde une couche de log utile pour debugger les futures versions.

Pour desinstaller completement : restaurer les assets statiques, puis supprimer `BepInEx/plugins/EsotericEbbFrench`.
