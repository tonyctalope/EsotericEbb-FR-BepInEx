# Contribuer

## Traduction

- Garder les noms propres et termes de lore coherents avec `docs/ETAT_LOCALISATION_FR.md` et le glossaire source.
- Preserver les balises du jeu : `<b>`, `<i>`, `<shake>`, variables, IDs `LOC_...`, ponctuation technique et guillemets CSV.
- Eviter les reformulations qui changent un indice, une condition de quete ou un ton de personnage.

## Assets

Les fichiers sources de release sont dans :

```text
assets/translations/fr-columns/
assets/translations/german-slot/
```

Le profil `fr-columns` conserve les colonnes francaises propres. Le profil `german-slot` copie le francais dans les colonnes allemandes pour les versions du jeu qui ne proposent pas encore explicitement le francais.

Apres modification, regenerer le manifeste :

```powershell
.\scripts\Refresh-Translations.ps1 -SourcePath "chemin\vers\localized_textassets"
```

## Verification

Avant une release :

```powershell
dotnet build .\src\EsotericEbbFrench\EsotericEbbFrench.csproj -c Release
.\scripts\Build-Release.ps1 -Version 0.1.0
```

Puis tester en jeu :

- menu principal ;
- nouvelle partie ;
- debut `LL_Intro` ;
- journal et quetes ;
- accents, retours ligne et balises de style.
