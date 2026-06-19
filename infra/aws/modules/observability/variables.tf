variable "name_prefix" {
  type = string
}

variable "log_retention_days" {
  type = number
}

variable "kms_key_arn" {
  type = string
}

variable "workload_names" {
  type = list(string)
}
