output "resource_group_name" {
  description = "Nom du groupe de ressources."
  value       = azurerm_resource_group.main.name
}

output "acr_name" {
  description = "Nom du registre ACR."
  value       = azurerm_container_registry.acr.name
}

output "acr_login_server" {
  description = "URL du registre (ex. appcatalogacrxxxx.azurecr.io)."
  value       = azurerm_container_registry.acr.login_server
}

output "aks_name" {
  description = "Nom du cluster AKS."
  value       = azurerm_kubernetes_cluster.aks.name
}

output "get_credentials_command" {
  description = "Commande pour configurer kubectl sur le cluster."
  value       = "az aks get-credentials --resource-group ${azurerm_resource_group.main.name} --name ${azurerm_kubernetes_cluster.aks.name}"
}
