# Architecture

## Objectif

Fournir une traduction francaise complete de *Esoteric Ebb* sous forme de mod BepInEx, sans modifier les assets Unity originaux du jeu.

## Strategie retenue

Le jeu stocke ses textes globaux dans des `TextAsset` Unity :

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

## Pourquoi pas un patch `resources.assets` ?

Un patch binaire est possible, mais moins reutilisable :

- il depend fortement de la version exacte du jeu ;
- il remplace des fichiers Steam ;
- il est plus difficile a desinstaller ;
- il complique les mises a jour.

Le mod BepInEx garde une installation reversible : supprimer le dossier `BepInEx/plugins/EsotericEbbFrench` suffit a desactiver la traduction.
