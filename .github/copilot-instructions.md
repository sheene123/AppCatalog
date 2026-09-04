# Instructions projet pour l'assistant IA — AppCatalog

Ce fichier encode les règles que **tout code généré par IA** (Copilot, Claude,
Cursor…) doit respecter dans ce dépôt. C'est le cœur de la démarche : à l'échelle
d'une direction, on ne se demande pas « est-ce que l'IA sait coder », mais
« comment garantir que ce qu'elle produit reste cohérent, relisable et vérifiable ».

## Contexte

API REST de référencement des applications d'un SI. Stack imposée :
ASP.NET Core 8 (C#), EF Core + SQLite, xUnit, Docker, GitHub Actions.

## Règles d'architecture

- **Ne jamais exposer l'entité `Application` directement** : passer par les DTO de
  `Contracts/` (requête et réponse séparées). L'entité reste interne au stockage.
- **Le controller ne contient pas de logique métier complexe** : il orchestre
  (validation, appel EF, choix du code HTTP). Si une règle grossit, l'extraire.
- **Toujours retourner des codes HTTP explicites** via `ActionResult<T>` :
  200/201/204 en succès, 400/404 en erreur, jamais d'exception nue.
- **Les erreurs suivent ProblemDetails** (RFC 7807), déjà branché dans `Program.cs`.

## Règles de qualité

- **Toute nouvelle fonctionnalité vient avec son test d'intégration** dans
  `tests/`. Les tests sont le contrat, pas une option.
- **Aucun `async` sans `CancellationToken`** propagé jusqu'à EF Core.
- **Requêtes de lecture en `AsNoTracking()`**.
- **Respecter `.editorconfig`** : le formatage est vérifié en CI
  (`dotnet format --verify-no-changes`).
- **Interdiction du code obsolète** : pas de `.NET Framework`, `Newtonsoft.Json`
  (on utilise `System.Text.Json`), ni d'API EF6. On cible EF Core 8.

## Règles de sécurité

- **Jamais de secret en dur** ni dans le code ni dans `appsettings.json` versionné :
  configuration par variables d'environnement.
- **Ne jamais faire confiance aux entrées** : valider via DataAnnotations sur les DTO.
- **Une dépendance vulnérable fait échouer la CI** (`dotnet list package --vulnerable`).

## Ce que l'IA ne doit pas faire

- Inventer des méthodes EF Core ou ASP.NET qui n'existent pas : en cas de doute,
  vérifier la signature réelle avant de proposer.
- Ajouter des dépendances (NuGet) sans justification explicite.
- Écrire un test qui « passe » sans rien vérifier (assertion absente ou triviale).

> Règle d'or humaine : **toute ligne qu'on ne sait pas expliquer est supprimée.**
