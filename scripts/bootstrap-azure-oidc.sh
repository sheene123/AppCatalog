#!/usr/bin/env bash
# Provisionne l'authentification OIDC entre GitHub Actions et Azure (sans secret).
#
# Crée une application Entra ID, une « federated credential » liée à ton dépôt,
# et attribue le rôle Contributor sur le groupe de ressources. Affiche ensuite les
# variables à déclarer côté GitHub (Settings > Secrets and variables > Actions > Variables).
#
# Prérequis : az CLI connecté (az login) et l'infra Terraform déjà appliquée.
# Usage :
#   GITHUB_REPO="sheene123/AppCatalog" ./scripts/bootstrap-azure-oidc.sh
set -euo pipefail

: "${GITHUB_REPO:?Définis GITHUB_REPO=\"owner/repo\" (ex. sheene123/AppCatalog)}"

# Récupère les noms depuis les sorties Terraform (exécuté depuis la racine du repo).
TF_DIR="infra/terraform"
RG=$(terraform -chdir="$TF_DIR" output -raw resource_group_name)
ACR_NAME=$(terraform -chdir="$TF_DIR" output -raw acr_name)
ACR_LOGIN=$(terraform -chdir="$TF_DIR" output -raw acr_login_server)
AKS_NAME=$(terraform -chdir="$TF_DIR" output -raw aks_name)
KEYVAULT_NAME=$(terraform -chdir="$TF_DIR" output -raw key_vault_name)
KV_IDENTITY_CLIENT_ID=$(terraform -chdir="$TF_DIR" output -raw kv_identity_client_id)

SUB_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)
APP_NAME="gh-appcatalog-oidc"

echo "→ Création de l'application Entra ID ($APP_NAME)..."
APP_ID=$(az ad app create --display-name "$APP_NAME" --query appId -o tsv)
# Crée le service principal associé (idempotent).
az ad sp create --id "$APP_ID" >/dev/null 2>&1 || true

echo "→ Federated credential pour $GITHUB_REPO (branche main)..."
az ad app federated-credential create --id "$APP_ID" --parameters "{
  \"name\": \"gh-main\",
  \"issuer\": \"https://token.actions.githubusercontent.com\",
  \"subject\": \"repo:${GITHUB_REPO}:ref:refs/heads/main\",
  \"audiences\": [\"api://AzureADTokenExchange\"]
}" >/dev/null

# GitHub présente aussi un sujet « immuable » incluant les IDs numériques
# (protection anti-renommage) : repo:<owner>@<ownerId>/<repo>@<repoId>:ref:...
# On crée le credential correspondant pour couvrir les deux formats.
OWNER_ID=$(gh api "repos/${GITHUB_REPO}" --jq '.owner.id' 2>/dev/null || echo "")
REPO_ID=$(gh api "repos/${GITHUB_REPO}" --jq '.id' 2>/dev/null || echo "")
OWNER="${GITHUB_REPO%%/*}"; REPO="${GITHUB_REPO##*/}"
if [ -n "$OWNER_ID" ] && [ -n "$REPO_ID" ]; then
  echo "→ Federated credential immuable (IDs ${OWNER_ID}/${REPO_ID})..."
  az ad app federated-credential create --id "$APP_ID" --parameters "{
    \"name\": \"gh-main-immutable\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:${OWNER}@${OWNER_ID}/${REPO}@${REPO_ID}:ref:refs/heads/main\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }" >/dev/null || true
fi

echo "→ Attribution du rôle Contributor sur le groupe $RG..."
# (Least-privilege possible ensuite : AcrPush sur l'ACR + Cluster Admin sur l'AKS.)
az role assignment create \
  --assignee "$APP_ID" \
  --role "Contributor" \
  --scope "/subscriptions/${SUB_ID}/resourceGroups/${RG}" >/dev/null

cat <<EOF

✅ Terminé. Déclare ces VARIABLES dans GitHub
   (Settings > Secrets and variables > Actions > onglet « Variables ») :

   AZURE_CLIENT_ID        = ${APP_ID}
   AZURE_TENANT_ID        = ${TENANT_ID}
   AZURE_SUBSCRIPTION_ID  = ${SUB_ID}
   ACR_NAME               = ${ACR_NAME}
   ACR_LOGIN_SERVER       = ${ACR_LOGIN}
   AKS_RESOURCE_GROUP     = ${RG}
   AKS_CLUSTER_NAME       = ${AKS_NAME}
   KEYVAULT_NAME          = ${KEYVAULT_NAME}
   KV_IDENTITY_CLIENT_ID  = ${KV_IDENTITY_CLIENT_ID}

   Aucun secret : l'auth passe par OIDC. Pousse sur main -> la CD déploie.
EOF
