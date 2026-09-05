# Suffixe aléatoire : le nom d'un registre ACR doit être unique dans tout Azure.
resource "random_string" "suffix" {
  length  = 6
  lower   = true
  upper   = false
  numeric = true
  special = false
}

resource "azurerm_resource_group" "main" {
  name     = "${var.prefix}-rg"
  location = var.location
  tags     = var.tags
}

# Registre de conteneurs privé : y stocke l'image Docker de l'API.
resource "azurerm_container_registry" "acr" {
  name                = "${var.prefix}acr${random_string.suffix.result}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "Basic" # le moins cher, suffisant pour une image
  admin_enabled       = false   # pas de mot de passe admin : on passe par l'identité managée
  tags                = var.tags
}

# Cluster Kubernetes managé.
resource "azurerm_kubernetes_cluster" "aks" {
  name                = "${var.prefix}-aks"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  dns_prefix          = "${var.prefix}-aks"

  # Control plane gratuit (on ne paie que les nœuds) — levier FinOps.
  sku_tier = "Free"

  default_node_pool {
    name       = "system"
    node_count = var.node_count
    vm_size    = var.node_size
  }

  # Identité managée : pas de secret à stocker pour l'authentification du cluster.
  identity {
    type = "SystemAssigned"
  }

  # Addon CSI Secret Store : monte les secrets Azure Key Vault dans les pods.
  # Crée une identité managée dédiée (secret_identity) utilisée par le driver.
  key_vault_secrets_provider {
    secret_rotation_enabled = true
  }

  # Calico applique réellement les NetworkPolicies (kubenet seul ne les applique pas).
  network_profile {
    network_plugin = "kubenet"
    network_policy = "calico"
  }

  tags = var.tags
}

# Contexte Azure courant (tenant, identité qui exécute Terraform).
data "azurerm_client_config" "current" {}

# Mot de passe Neo4j généré aléatoirement : jamais écrit dans le code ni dans Git.
resource "random_password" "neo4j" {
  length  = 24
  special = true
  # Caractères spéciaux sûrs pour Neo4j / URL / shell.
  override_special = "-_@#"
}

# Coffre-fort : stocke le mot de passe Neo4j. Autorisation par RBAC Azure.
resource "azurerm_key_vault" "main" {
  name                       = "${var.prefix}-kv-${random_string.suffix.result}"
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  rbac_authorization_enabled = true # droits gérés par RBAC, pas par access policies
  tags                       = var.tags
}

# Celui qui exécute Terraform doit pouvoir écrire le secret.
resource "azurerm_role_assignment" "kv_admin" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

# L'identité du driver CSI doit pouvoir lire le secret.
resource "azurerm_role_assignment" "kv_csi_reader" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_kubernetes_cluster.aks.key_vault_secrets_provider[0].secret_identity[0].object_id
}

# Le secret lui-même. depends_on : attend la propagation du rôle d'écriture.
resource "azurerm_key_vault_secret" "neo4j_password" {
  name         = "neo4j-password"
  value        = random_password.neo4j.result
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.kv_admin]
}

# Autorise le kubelet d'AKS à tirer les images de l'ACR, sans identifiant :
# rôle AcrPull attribué à l'identité managée du cluster. C'est le point clé qui
# évite tout mot de passe de registre dans Kubernetes.
resource "azurerm_role_assignment" "aks_acr_pull" {
  scope                            = azurerm_container_registry.acr.id
  role_definition_name             = "AcrPull"
  principal_id                     = azurerm_kubernetes_cluster.aks.kubelet_identity[0].object_id
  skip_service_principal_aad_check = true
}
