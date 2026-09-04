# Journal d'industrialisation

But de ce document : montrer **la méthode**, pas seulement le résultat. Pour chaque
étape assistée par IA, ce qui a été demandé, ce que l'IA a produit, **ce qui était
faux, et le garde-fou qui l'a rattrapé.** Les erreurs sont le contenu le plus utile :
c'est exactement ce qu'un manager redoute à l'échelle de sa direction.

Fil directeur : **les tests sont le contrat, la CI est la barrière. On ne fait pas
confiance à la sortie de l'IA, on la vérifie automatiquement.**

Contexte : je n'avais jamais écrit de .NET avant ce projet. Objectif : produire une
API ASP.NET Core propre en développement assisté par IA, en gardant la maîtrise de
chaque ligne.

---

## Étape 1 — Squelette et modèle de données

**Demandé :** solution multi-projets (`src` + `tests`), entité `Application`,
DbContext EF Core, controller CRUD, DTO séparés.

**Produit :** structure correcte du premier coup. Choix retenus et compris :
- Controllers plutôt que Minimal API (pattern classique en entreprise, plus de
  concepts .NET à maîtriser).
- Enum `Criticality` stocké **en texte** en base (`HasConversion<string>`) pour la
  lisibilité et la robustesse au renommage.
- Seed via `HasData` avec des dates **constantes** — sinon chaque génération de
  migration détecte un faux changement.

**Garde-fou :** `dotnet build` → 0 warning. Point de départ sain.

---

## Étape 2 — Erreur de l'IA n°1 : namespace manquant (rattrapée par le compilateur)

**Symptôme :** à la compilation des tests,
`error CS1061: 'IWebHostBuilder' does not contain a definition for 'ConfigureTestServices'`.

**Cause :** le code utilisait `ConfigureTestServices` sans le `using
Microsoft.AspNetCore.TestHost;`. L'IA a écrit un appel plausible mais incomplet.

**Rattrapé par :** le **compilateur**, avant même les tests. C'est le garde-fou le
moins cher : le typage statique de C# refuse de compiler du code approximatif.

**Correctif :** ajout du `using` manquant. Une ligne.

**Leçon :** en environnement typé, une grande partie des « hallucinations » d'API
sont interceptées à la compilation. C'est un argument fort en faveur de C# pour du
code assisté par IA à grande échelle.

---

## Étape 3 — Erreur de l'IA n°2 : enums numériques (rattrapée par un test manuel)

**Symptôme :** au test de fumée, `POST /api/applications` avec
`"criticality": "Vital"` renvoyait **400 Bad Request**, et les réponses affichaient
`"criticality": 3` au lieu de `"Vital"`.

**Cause :** par défaut, `System.Text.Json` sérialise les enums en entiers et ne sait
pas lire leur nom. Le contrat produit par l'IA était techniquement valide mais peu
lisible et peu ergonomique.

**Rattrapé par :** un **test de fumée manuel** (curl sur l'API réelle). Les tests
d'intégration automatisés, eux, ne l'avaient pas vu : ils sérialisaient l'enum en
nombre des deux côtés, donc ils passaient « pour la mauvaise raison ». Le test
automatisé donnait une fausse assurance.

**Correctif :** ajout de `JsonStringEnumConverter` côté API. **Effet de bord** : il a
fallu aligner les options JSON **côté client de test** pour lire « Vital ». Un
changement de contrat se propage aux tests — c'est normal et voulu.

**Leçon :** un test qui passe ne prouve pas que le comportement est bon, seulement
qu'il est constant. Il faut vérifier le contrat réel, pas seulement la cohérence
interne. D'où l'ajout d'assertions explicites sur la valeur de l'enum.

---

## Étape 4 — La barrière CI attrape de vraies CVE (audit de dépendances)

**Symptôme :** `dotnet list package --vulnerable` a fait remonter **6 paquets
transitifs** classés « High » : `System.Text.Json 8.0.4`, `SQLitePCLRaw 2.1.6`,
`Microsoft.Extensions.Caching.Memory 8.0.0`, et deux vieux paquets `4.3.0` côté tests.

**Cause :** aucune erreur de l'IA ici — ce sont les versions tirées **transitivement**
par EF Core 8.0.8 et l'outillage de test. Personne ne les avait choisies
explicitement ; elles arrivent « par héritage » dans le graphe de dépendances.

**Rattrapé par :** l'étape d'audit de la **CI**, précisément conçue pour ça. C'est
le genre de faille qu'aucune relecture humaine ne voit à l'œil nu.

**Correctif :** montée d'EF Core en 8.0.10 et **épinglage explicite** des transitives
corrigées (`System.Text.Json 8.0.5`, `SQLitePCLRaw 2.1.13`, etc.). Il a fallu deux
itérations : la première remontée de SQLitePCLRaw (2.1.10) était encore vulnérable, la
CVE n'étant corrigée qu'à partir de 2.1.11.

**Leçon :** à l'échelle d'une direction, le risque n'est pas le code que l'IA écrit,
c'est ce que le projet **traîne sans le savoir**. Une barrière automatisée qui échoue
le build sur une CVE vaut mieux que la vigilance de quarante développeurs.

**Piste suivante :** passer en *Central Package Management* (`Directory.Packages.props`)
pour centraliser toutes les versions en un seul endroit auditable.

---

## État final vérifié

- `dotnet format --verify-no-changes` : format conforme au `.editorconfig`.
- `dotnet build -c Release` : 0 warning, 0 erreur.
- `dotnet test` : 7/7 tests d'intégration au vert.
- `dotnet list package --vulnerable` : **aucun paquet vulnérable**.
- Test de fumée manuel : CRUD complet OK, ProblemDetails sur entrée invalide,
  Swagger exposé, migration appliquée et base SQLite créée au démarrage.

## Ce que je mettrais en place ensuite (non fait, assumé)

- Pagination et filtres sur `GET /api/applications`.
- Authentification réelle (aujourd'hui : aucune, volontairement hors périmètre).
- Une évaluation de la qualité du code généré à l'échelle : métriques de couverture,
  analyse statique (`dotnet format` + analyseurs Roslyn en mode erreur).
