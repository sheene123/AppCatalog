terraform {
  required_version = ">= 1.9.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # Backend distant recommandé en équipe (état partagé + verrouillage).
  # Laissé en commentaire : par défaut l'état est local (fichier terraform.tfstate).
  # backend "azurerm" {
  #   resource_group_name  = "tfstate-rg"
  #   storage_account_name = "tfstateappcatalog"
  #   container_name       = "tfstate"
  #   key                  = "appcatalog.tfstate"
  # }
}
