variable "name_prefix" {
  type = string
}

variable "aws_region" {
  type = string
}

variable "ecr_repository_urls" {
  type = map(string)
}

variable "api_image_digest" {
  type = string
}

variable "web_image_digest" {
  type = string
}

variable "private_subnet_ids" {
  type = list(string)
}

variable "api_security_group_id" {
  type = string
}

variable "web_security_group_id" {
  type = string
}

variable "api_target_group_arn" {
  type = string
}

variable "web_target_group_arn" {
  type = string
}

variable "execution_role_arn" {
  type = string
}

variable "api_task_role_arn" {
  type = string
}

variable "web_task_role_arn" {
  type = string
}

variable "log_group_names" {
  type = map(string)
}

variable "api_database_secret_arn" {
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

variable "work_queue_url" {
  type = string
}

variable "work_dlq_url" {
  type = string
}

variable "api_database_username" {
  type = string
}

variable "api_desired_count" {
  type = number
}

variable "web_desired_count" {
  type = number
}

variable "enable_web_service" {
  type = bool
}

variable "web_data_mode" {
  type = string
}

variable "web_backend_api_url" {
  type = string
}
