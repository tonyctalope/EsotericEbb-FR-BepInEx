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

Il patche ensuite `TextAsset.text`. Quand le jeu demande le texte d'un asset connu, le mod renvoie le contenu localise. Cela couvre les references serialisees et les chargements `Resources.Load` classiques.

Un patch secondaire sur `Resources.Load` permet aussi de fournir un `TextAsset` de remplacement si le jeu tente de charger directement l'un de ces noms et que l'asset original n'est pas resolu.

## Profils

### `fr-columns`

Profil propre. Il conserve les colonnes francaises natives :

- `Dialogs`: colonne `FR`
- autres tables : colonne `FRENCH`

Ce profil est ideal si le jeu expose une option francaise, ou si un patch futur force explicitement la langue `FR`.

### `german-slot`

Profil compatible par defaut. Il conserve les colonnes francaises mais copie aussi :

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
