variable "name_prefix" {
  type = string
}

variable "event_sources" {
  type    = list(string)
  default = ["maintenance-data-simulator"]
}

variable "event_detail_types" {
  type    = list(string)
  default = ["MaintenanceEvent"]
}
