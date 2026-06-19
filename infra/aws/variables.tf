variable "aws_region" {
  description = "AWS region for the review environment."
  type        = string
  default     = "ap-southeast-2"
}

variable "project" {
  description = "Project tag and naming prefix."
  type        = string
  default     = "maintenance-planning-prototype"

  validation {
    condition     = can(regex("^[a-z0-9-]+$", var.project))
    error_message = "project must use lowercase letters, numbers and hyphens."
  }
}

variable "owner" {
  description = "Owner tag for cost and teardown review."
  type        = string
  default     = "pedro"
}

variable "environment" {
  description = "Environment tag and naming suffix."
  type        = string
  default     = "review"

  validation {
    condition     = can(regex("^[a-z0-9-]+$", var.environment))
    error_message = "environment must use lowercase letters, numbers and hyphens."
  }
}

variable "vpc_cidr" {
  description = "CIDR block for the review VPC."
  type        = string
  default     = "10.42.0.0/16"

  validation {
    condition     = can(cidrhost(var.vpc_cidr, 0))
    error_message = "vpc_cidr must be a valid CIDR block."
  }
}

variable "public_subnet_cidrs" {
  description = "CIDR blocks for public load-balancer subnets."
  type        = list(string)
  default     = ["10.42.0.0/24", "10.42.1.0/24"]
}

variable "private_subnet_cidrs" {
  description = "CIDR blocks for private service subnets."
  type        = list(string)
  default     = ["10.42.10.0/24", "10.42.11.0/24"]
}

variable "database_subnet_cidrs" {
  description = "CIDR blocks for isolated database subnets."
  type        = list(string)
  default     = ["10.42.20.0/24", "10.42.21.0/24"]
}

variable "allowed_http_cidr_blocks" {
  description = "CIDR blocks allowed to reach the public review load balancer."
  type        = list(string)
  default     = ["0.0.0.0/0"]
}

variable "enable_nat_gateway" {
  description = "Whether to add a NAT gateway for private service egress."
  type        = bool
  default     = false
}

variable "enable_vpc_endpoints" {
  description = "Whether to add private service endpoints for registry, logs and secrets access."
  type        = bool
  default     = false
}

variable "certificate_arn" {
  description = "Optional ACM certificate ARN for an HTTPS listener."
  type        = string
  default     = null
}

variable "waf_web_acl_arn" {
  description = "Optional WAF web ACL ARN to associate with the review load balancer."
  type        = string
  default     = null
}

variable "ecr_encryption_type" {
  description = "ECR encryption type."
  type        = string
  default     = "AES256"

  validation {
    condition     = contains(["AES256", "KMS"], var.ecr_encryption_type)
    error_message = "ecr_encryption_type must be AES256 or KMS."
  }
}

variable "ecr_kms_key_arn" {
  description = "Optional KMS key ARN when ECR encryption type is KMS."
  type        = string
  default     = null
}

variable "secrets_kms_key_arn" {
  description = "Optional KMS key ARN for Secrets Manager secrets."
  type        = string
  default     = null
}

variable "logs_kms_key_arn" {
  description = "Optional KMS key ARN for CloudWatch log groups."
  type        = string
  default     = null
}

variable "database_storage_kms_key_arn" {
  description = "Optional KMS key ARN for database storage."
  type        = string
  default     = null
}

variable "api_image_digest" {
  description = "Immutable API image digest, formatted as sha256:<64 lowercase hex characters>."
  type        = string

  validation {
    condition     = can(regex("^sha256:[a-f0-9]{64}$", var.api_image_digest))
    error_message = "api_image_digest must be a sha256 digest."
  }
}

variable "web_image_digest" {
  description = "Immutable web image digest, formatted as sha256:<64 lowercase hex characters>."
  type        = string

  validation {
    condition     = can(regex("^sha256:[a-f0-9]{64}$", var.web_image_digest))
    error_message = "web_image_digest must be a sha256 digest."
  }
}

variable "simulator_image_digest" {
  description = "Immutable simulator image digest, formatted as sha256:<64 lowercase hex characters>."
  type        = string

  validation {
    condition     = can(regex("^sha256:[a-f0-9]{64}$", var.simulator_image_digest))
    error_message = "simulator_image_digest must be a sha256 digest."
  }
}

variable "worker_image_digest" {
  description = "Immutable worker image digest, formatted as sha256:<64 lowercase hex characters>."
  type        = string

  validation {
    condition     = can(regex("^sha256:[a-f0-9]{64}$", var.worker_image_digest))
    error_message = "worker_image_digest must be a sha256 digest."
  }
}

variable "migration_image_digest" {
  description = "Immutable migration-runner image digest, formatted as sha256:<64 lowercase hex characters>."
  type        = string

  validation {
    condition     = can(regex("^sha256:[a-f0-9]{64}$", var.migration_image_digest))
    error_message = "migration_image_digest must be a sha256 digest."
  }
}

variable "api_desired_count" {
  description = "Desired API task count for the review service."
  type        = number
  default     = 1
}

variable "web_desired_count" {
  description = "Desired web task count for the review service."
  type        = number
  default     = 1
}

variable "enable_web_service" {
  description = "Whether to deploy the web review service."
  type        = bool
  default     = true
}

variable "enable_worker_service" {
  description = "Whether to run the event worker as a long-running service."
  type        = bool
  default     = false
}

variable "enable_simulator_schedule" {
  description = "Whether to schedule the simulator task."
  type        = bool
  default     = false
}

variable "simulator_schedule_expression" {
  description = "EventBridge Scheduler expression for the simulator task when scheduling is enabled."
  type        = string
  default     = "rate(1 day)"
}

variable "web_data_mode" {
  description = "Web runtime data mode. Use backend only when the API path and auth boundary are wired."
  type        = string
  default     = "mock"

  validation {
    condition     = contains(["mock", "backend"], var.web_data_mode)
    error_message = "web_data_mode must be mock or backend."
  }
}

variable "web_backend_api_url" {
  description = "Server-only API URL for the web service when backend mode is enabled."
  type        = string
  default     = null

  validation {
    condition     = var.web_backend_api_url == null || can(regex("^https?://", var.web_backend_api_url))
    error_message = "web_backend_api_url must be an absolute HTTP or HTTPS URL."
  }
}

variable "simulator_api_url" {
  description = "API URL used by optional simulator tasks."
  type        = string
  default     = null

  validation {
    condition     = var.simulator_api_url == null || can(regex("^https?://", var.simulator_api_url))
    error_message = "simulator_api_url must be an absolute HTTP or HTTPS URL."
  }
}

variable "database_engine" {
  description = "RDS engine for the review database."
  type        = string
  default     = "sqlserver-ex"
}

variable "database_engine_version" {
  description = "Optional database engine version."
  type        = string
  default     = null
}

variable "database_instance_class" {
  description = "Database instance class for review."
  type        = string
  default     = "db.t3.small"
}

variable "database_allocated_storage_gb" {
  description = "Initial database storage in GiB."
  type        = number
  default     = 20
}

variable "database_max_allocated_storage_gb" {
  description = "Maximum autoscaled database storage in GiB."
  type        = number
  default     = 100
}

variable "database_name" {
  description = "Application database name expected by the API and migration task."
  type        = string
  default     = "MaintenancePlanning"
}

variable "database_master_username" {
  description = "RDS master username. Password is managed by RDS."
  type        = string
  default     = "maintenanceadmin"
}

variable "api_database_username" {
  description = "Application database username used by the API task."
  type        = string
  default     = "maintenance_api"
}

variable "migration_database_username" {
  description = "Database username used by the migration task."
  type        = string
  default     = "maintenance_migrator"
}

variable "database_backup_retention_days" {
  description = "Review database backup retention in days."
  type        = number
  default     = 1
}

variable "database_deletion_protection" {
  description = "Whether database deletion protection is enabled."
  type        = bool
  default     = false
}

variable "database_skip_final_snapshot" {
  description = "Whether teardown skips a final database snapshot."
  type        = bool
  default     = true
}

variable "log_retention_days" {
  description = "CloudWatch log retention in days."
  type        = number
  default     = 14
}

variable "monthly_budget_amount" {
  description = "Monthly review budget amount in USD."
  type        = string
  default     = "35"
}

variable "budget_alert_emails" {
  description = "Email addresses for budget alerts. Leave empty only for validation before deployment."
  type        = list(string)
  default     = []
}
