variable "name_prefix" {
  type = string
}

variable "aws_region" {
  type = string
}

variable "cluster_arn" {
  type = string
}

variable "cluster_name" {
  type = string
}

variable "ecr_repository_urls" {
  type = map(string)
}

variable "worker_image_digest" {
  type = string
}

variable "migration_image_digest" {
  type = string
}

variable "simulator_image_digest" {
  type = string
}

variable "private_subnet_ids" {
  type = list(string)
}

variable "worker_security_group_id" {
  type = string
}

variable "migration_security_group_id" {
  type = string
}

variable "simulator_security_group_id" {
  type = string
}

variable "execution_role_arn" {
  type = string
}

variable "worker_task_role_arn" {
  type = string
}

variable "migration_task_role_arn" {
  type = string
}

variable "simulator_task_role_arn" {
  type = string
}

variable "scheduler_role_arn" {
  type = string
}

variable "log_group_names" {
  type = map(string)
}

variable "migration_database_secret_arn" {
  type = string
}

variable "simulator_api_token_secret_arn" {
  type = string
}

variable "database_address" {
  type = string
}

variable "database_port" {
  type = number
}

variable "database_name" {
  type = string
}

variable "migration_database_username" {
  type = string
}

variable "enable_worker_service" {
  type = bool
}

variable "enable_simulator_schedule" {
  type = bool
}

variable "simulator_schedule_expression" {
  type = string
}

variable "simulator_api_url" {
  type = string
}

variable "event_bus_name" {
  type = string
}

variable "work_queue_url" {
  type = string
}
