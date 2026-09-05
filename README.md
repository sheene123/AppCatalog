# AppCatalog

Référentiel des applications d'un SI, modélisé comme un **graphe** : chaque application
est un nœud (nom, propriétaire, stack, criticité, dernier déploiement), et les
**dépendances entre applications** sont des relations. On peut alors répondre à des
questions qu'un stockage tabulaire fait mal, comme « quelles applications sont
impactées si celle-ci tombe ? » (parcours transitif du graphe).

Construit en **développement assisté par IA** sur une stack .NET / cloud. L'intérêt du
projet n'est pas le CRUD mais la **méthode d'industrialisation** : tests comme contrat,
CI comme barrière, IaC, sécurité par défaut. Voir [`JOURNAL.md`](JOURNAL.md) et
[`.github/copilot-instructions.md`](.github/copilot-instructions.md).

## Architecture

```
   Internet
      │  (north-south)
      ▼
  ┌───────────────┐   LoadBalancer (seul point exposé)
  │  Web (Blazor) │
  └───────────────┘
      │  http://appcatalog-api        (east-west, interne)
      ▼
  ┌───────────────┐   ClusterIP (pas d'IP publique)
  │  API (.NET)   │
  └───────────────┘
      │  bolt://neo4j:7687            (secret via Azure Key Vault)
      ▼
  ┌───────────────┐   StatefulSet + volume persistant
  │    Neo4j      │
  └───────────────┘
```

Cloisonnement réseau strict (NetworkPolicies) : le public ne joint que le frontend,
le frontend ne joint que l'API, l'API seule joint Neo4j.

## Stack

| Élément | Choix |
|---|---|
| Frontend | Blazor Server (ASP.NET Core 8, C#) |
| API | ASP.NET Core 8 (controllers) |
| Base de données | **Neo4j 5** (graphe), driver officiel + Cypher |
| Doc API | Swagger / OpenAPI |
| Tests | xUnit + `WebApplicationFactory` + **Testcontainers** (vrai Neo4j éphémère) |
| Conteneurs | Docker multi-étapes (2 images) |
| Infra | Terraform : AKS (Calico) + ACR + **Azure Key Vault** |
| Secrets | **Key Vault + CSI Secret Store** (aucun mot de passe dans Git ni dans l'image) |
| CI | format, build, tests, audit de vulnérabilités |
| CD | build ACR + déploiement AKS en OIDC (sans secret) |

## Endpoints de l'API

| Méthode | Route | Rôle |
|---|---|---|
| GET/POST | `/api/applications` | Liste / création |
| GET/PUT/DELETE | `/api/applications/{id}` | Détail / mise à jour / suppression |
| POST | `/api/applications/{id}/dependencies` | Déclarer une dépendance (`{ "targetId": "..." }`) |
| GET | `/api/applications/{id}/dependencies` | Applications dont {id} dépend |
| GET | `/api/applications/{id}/impact` | **Applications impactées si {id} tombe** (transitif) |
| GET | `/health` | Disponibilité (dont connexion Neo4j) |

## Lancer en local

Neo4j (Docker), puis l'API et le frontend :

```bash
docker run -d --name neo4j -p 7687:7687 -e NEO4J_AUTH=neo4j/motdepasse123 neo4j:5.24

# API (fenêtre 1)
Neo4j__Uri=bolt://localhost:7687 Neo4j__User=neo4j Neo4j__Password=motdepasse123 \
  dotnet run --project src/AppCatalog.Api        # http://localhost:5080/swagger

# Frontend (fenêtre 2)
ApiBaseUrl=http://localhost:5080 dotnet run --project src/AppCatalog.Web
```

## Tests

```bash
dotnet test
```

Les tests démarrent un **vrai Neo4j** (Testcontainers, Docker requis) et vérifient le
contrat HTTP de bout en bout : CRUD, validation, et le graphe (dépendance + impact
transitif).

## Déploiement Azure (AKS + Terraform + CI/CD)

> ⚠️ **Coût** : AKS facture ses nœuds. Faire `terraform destroy` après la démo.

```bash
az login
export ARM_SUBSCRIPTION_ID="<votre-subscription>"

# 1. Infra : RG + ACR + AKS (Calico) + Key Vault (+ mot de passe Neo4j généré)
terraform -chdir=infra/terraform init
terraform -chdir=infra/terraform apply

# 2. Câbler GitHub -> Azure (OIDC, sans secret)
GITHUB_REPO="sheene123/AppCatalog" ./scripts/bootstrap-azure-oidc.sh

# 3. Variables GitHub (Settings > Secrets and variables > Actions > Variables) :
#    AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_SUBSCRIPTION_ID,
#    ACR_NAME, ACR_LOGIN_SERVER, AKS_RESOURCE_GROUP, AKS_CLUSTER_NAME,
#    KEYVAULT_NAME, KV_IDENTITY_CLIENT_ID   (ces deux-là via `terraform output`)

# 4. Pousser sur main -> la CD build les 2 images et déploie. L'IP publique du
#    frontend s'affiche en fin de job.

# Puis, pour arrêter les frais :
terraform -chdir=infra/terraform destroy
```

Le déploiement provisionne Neo4j (StatefulSet + volume), l'API et le frontend, applique
les NetworkPolicies et monte le mot de passe Neo4j depuis Key Vault via le CSI Secret
Store (synchronisé en Secret Kubernetes, jamais en clair dans Git).

### HTTPS (Ingress + Let's Encrypt)

Le frontend est exposé en **HTTPS** derrière un Ingress nginx, avec un certificat
**Let's Encrypt** automatique (cert-manager) sur un FQDN Azure (`*.cloudapp.azure.com`,
sans IP). Tous les Services applicatifs sont en `ClusterIP` : l'Ingress est le seul point
d'entrée public.

Prérequis (une fois sur le cluster) :

```bash
# 1. Contrôleur Ingress + label DNS Azure (donne un FQDN sans IP)
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.11.3/deploy/static/provider/cloud/deploy.yaml
kubectl annotate service ingress-nginx-controller -n ingress-nginx \
  service.beta.kubernetes.io/azure-dns-label-name=appcatalog-<suffixe> --overwrite

# 2. cert-manager (émission/renouvellement automatique des certificats)
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.16.2/cert-manager.yaml

# 3. Émetteur Let's Encrypt (adapter le FQDN dans k8s/ingress.yaml)
kubectl apply -f k8s/clusterissuer.yaml
```

L'Ingress (`k8s/ingress.yaml`), la NetworkPolicy du challenge ACME
(`k8s/networkpolicy-acme.yaml`) et le passage du frontend en `ClusterIP` sont ensuite
gérés par la kustomization. Le certificat est émis en ~1 min et HTTP est redirigé (308)
vers HTTPS.

## Sécurité

- **Secrets** : mot de passe Neo4j généré par Terraform, stocké dans Azure Key Vault,
  monté dans les pods via le CSI Secret Store (identité managée). Rien en clair dans le
  code, l'image ou Git.
- **Réseau** : NetworkPolicies (Calico) en zero-trust — deny par défaut, puis
  web→api et api→neo4j uniquement.
- **Conteneurs** : `runAsNonRoot`, système de fichiers en lecture seule, capacités Linux
  supprimées, quotas CPU/mémoire, `seccompProfile: RuntimeDefault`.
- **Registre** : ACR sans mot de passe admin ; AKS tire les images par identité managée
  (rôle AcrPull).
- **Transport** : HTTPS via Ingress + certificat Let's Encrypt, redirection forcée
  HTTP→HTTPS (HSTS), en-têtes de sécurité (CSP, nosniff, anti-clickjacking). Aucun
  Service applicatif n'a d'IP publique (tout en ClusterIP derrière l'Ingress).
- **API** : validation des entrées, requêtes Cypher paramétrées (anti-injection),
  limitation de débit (429).
- **CI/CD** : audit des dépendances vulnérables (barrière), déploiement en OIDC sans
  secret stocké.

## Structure

```
AppCatalog/
├── src/
│   ├── AppCatalog.Api/       # API : Domain, Data (Neo4j + Cypher), Contracts, Controllers
│   └── AppCatalog.Web/       # Frontend Blazor (client API typé + pages)
├── tests/AppCatalog.Api.Tests/   # tests d'intégration (Testcontainers Neo4j)
├── infra/terraform/          # AKS + ACR + Key Vault (+ CSI, Calico)
├── k8s/                      # Neo4j, API, Web, NetworkPolicies, SecretProviderClass, HPA
├── scripts/bootstrap-azure-oidc.sh
├── Dockerfile / Dockerfile.web
├── .github/workflows/        # ci.yml, cd.yml
├── .github/copilot-instructions.md
└── JOURNAL.md
```

## Périmètre

Pas d'authentification applicative (hors périmètre volontaire). La discipline de
périmètre est un choix assumé ; les extensions envisagées sont dans [`JOURNAL.md`](JOURNAL.md).
