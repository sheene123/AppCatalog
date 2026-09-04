provider "azurerm" {
  features {}

  # L'ID d'abonnement peut venir d'ici ou de la variable d'environnement
  # ARM_SUBSCRIPTION_ID (recommandé pour ne rien committer).
  subscription_id = var.subscription_id
}

provider "random" {}
