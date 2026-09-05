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

## Étape 5 — Passage à une base graphe (Neo4j) + frontend + sécurité

Évolution majeure : ajout d'un frontend Blazor, remplacement de SQLite par **Neo4j**
(les dépendances entre applications deviennent des relations), déploiement des trois
services sur AKS avec cloisonnement réseau et secrets dans Azure Key Vault.

Deux vraies erreurs rencontrées, toutes deux **de configuration/environnement** — pas
de logique métier —, ce qui est représentatif du travail réel avec l'IA.

### Erreur n°3 : tests qui pendent 7 minutes (piège Testcontainers / Neo4j)

**Symptôme :** les 8 tests d'intégration échouaient après ~7 minutes, sur des erreurs
de socket « Connection refused » vers Neo4j.

**Cause :** Testcontainers expose Neo4j via `GetConnectionString()` au schéma
`neo4j://` (routing). En mode routing, le serveur **annonce son adresse interne au
conteneur**, injoignable depuis l'hôte → le driver boucle sur des retries.

**Rattrapé par :** l'exécution réelle des tests (le message d'erreur exact désignait
l'adresse annoncée). Un test qui ne tourne pas est un signal, pas un détail.

**Correctif :** forcer le schéma `bolt://` (connexion directe, sans routing) dans la
configuration de test.

### Erreur n°4 : l'override de config de test ignoré (minimal hosting)

**Symptôme :** même après le correctif, l'API se connectait toujours à
`bolt://localhost:7687` (valeur par défaut d'`appsettings.json`) au lieu du port
mappé par Testcontainers.

**Cause :** `Program.cs` lisait la configuration **au moment du builder**, donc avant
que la factory de test (`WebApplicationFactory`) n'injecte sa propre configuration.
Piège classique du modèle *minimal hosting*.

**Rattrapé par :** le test, encore — la valeur `localhost:7687` dans l'erreur trahissait
que l'override n'était pas pris en compte.

**Correctif :** créer le driver Neo4j **paresseusement** via le `ServiceProvider`
(lecture de la config finale à la résolution), au lieu de le lire tôt dans le builder.

**Leçon :** avec l'IA, la majorité des frictions ne viennent pas d'un mauvais algorithme
mais de l'**intégration** : ordre d'initialisation, environnement, réseau. Le harnais
de tests reste le meilleur détecteur.

### Sécurité mise en place

- Mot de passe Neo4j **généré par Terraform**, stocké dans **Azure Key Vault**, monté
  dans les pods via le **CSI Secret Store** (identité managée) — jamais en clair.
- **NetworkPolicies** (Calico) en zero-trust : deny par défaut, puis web→api et
  api→neo4j uniquement.
- Conteneurs durcis (non-root, FS lecture seule, capacités supprimées, quotas).
- Nouvelle CVE transitive (SSH.NET, via Testcontainers) attrapée par l'audit et épinglée.

---

## État final vérifié

- `dotnet format --verify-no-changes` : format conforme au `.editorconfig`.
- `dotnet build -c Release` : 0 warning, 0 erreur.
- `dotnet test` : **8/8** tests d'intégration au vert (contre un vrai Neo4j via Testcontainers),
  dont le test de graphe (dépendance + impact transitif).
- `dotnet list package --vulnerable` : **aucun paquet vulnérable**.
- Manifestes Kubernetes : **12/12 valides** (kubeconform). Terraform : `validate` OK.
- Test de fumée conteneurisé : la chaîne **Web → API → Neo4j** fonctionne dans un réseau
  Docker (comme le réseau interne du cluster), le frontend rend les données du graphe.

## Ce que je mettrais en place ensuite (non fait, assumé)

- Visualisation du graphe de dépendances dans le frontend.
- Authentification réelle (aujourd'hui : aucune, volontairement hors périmètre).
- TLS Bolt entre l'API et Neo4j ; sauvegardes du volume Neo4j.
- *Central Package Management* pour centraliser les versions et l'audit.
