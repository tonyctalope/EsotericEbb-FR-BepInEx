# Architecture

## Objectif

Fournir une traduction francaise complete de *Esoteric Ebb* avec une solution reversible :

- patch statique des tables de localisation dans `resources.assets` ;
- patch statique des dialogues Ink compiles dans `sharedassets*.assets` ;
- patch statique d'une liste blanche de textes de scene TextMeshPro ;
- backup/restauration des assets originaux.

## Strategie statique

Le jeu stocke ses textes globaux dans des `TextAsset` Unity :

- `Dialogs`
- `UIElements`
- `QuestPoints`
- `GlossaryTerms`
- `Feats`
- `SheetInfo`

Ces tables sont remplacees dans `resources.assets` par le profil `english-slot`, qui copie le francais dans les colonnes anglaises. Le jeu peut donc rester sur sa langue anglaise par defaut tout en affichant le francais.

Certains dialogues et choix ne passent pas par ces tables au moment de l'affichage. Ils sont compiles dans des `TextAsset` Ink individuels presents dans `sharedassets*.assets`, avec des marqueurs internes du type `LOC_1`, `LOC_2`, etc.

Le tool `tools/StaticInkPatcher` :

- lit les fichiers Unity avec `AssetsTools.NET` ;
- remplace les `TextAsset` de tables par les fichiers de `translations/english-slot/` ;
- detecte les `TextAsset` Ink contenant `inkVersion` ;
- associe le nom de story et le marqueur `LOC_x` a la ligne correspondante de `assets/translations/fr-columns/Dialogs.txt` ;
- remplace la chaine Ink affichee par la traduction francaise ;
- remplace les chaines serialisees des `MonoBehaviour` quand elles correspondent exactement a `translations/runtime_terms.csv` ;
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

## BepInEx

Le depot conserve du code BepInEx historique parce qu'il a servi au debug runtime et peut encore aider a diagnostiquer une future mise a jour du jeu. Il n'est pas inclus dans le zip joueur par defaut.

Pour desinstaller la traduction statique : lancer `Restore-Original-Assets.ps1`.
