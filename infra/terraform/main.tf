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

  tags = var.tags
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
