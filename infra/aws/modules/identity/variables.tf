variable "name_prefix" {
  type = string
}

variable "secret_arns" {
  type = list(string)
}

variable "event_bus_arn" {
  type = string
}

variable "work_queue_arn" {
  type = string
}

variable "work_dlq_arn" {
  type = string
}

variable "secrets_kms_key_arn" {
  type = string
}
