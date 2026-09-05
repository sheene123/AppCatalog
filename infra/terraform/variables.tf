variable "subscription_id" {
  description = "ID de l'abonnement Azure. Peut aussi être fourni via ARM_SUBSCRIPTION_ID."
  type        = string
  default     = null
}

variable "prefix" {
  description = "Préfixe commun aux noms de ressources."
  type        = string
  default     = "appcatalog"
}

variable "location" {
  description = "Région Azure."
  type        = string
  default     = "francecentral"
}

variable "node_count" {
  description = "Nombre de nœuds du pool système AKS (1 suffit pour la démo — FinOps)."
  type        = number
  default     = 1
}

variable "node_size" {
  description = "Taille des VM du pool AKS. D2as_v7 = 2 vCPU / 8 Go (famille autorisée par l'abonnement)."
  type        = string
  default     = "Standard_D2as_v7"
}

variable "tags" {
  description = "Étiquettes appliquées à toutes les ressources (suivi des coûts)."
  type        = map(string)
  default = {
    project = "appcatalog"
    env     = "demo"
    owner   = "carld"
  }
}
