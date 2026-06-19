module "network" {
  source = "./modules/network"

  name_prefix              = local.name_prefix
  aws_region               = var.aws_region
  vpc_cidr                 = var.vpc_cidr
  public_subnet_cidrs      = var.public_subnet_cidrs
  private_subnet_cidrs     = var.private_subnet_cidrs
  database_subnet_cidrs    = var.database_subnet_cidrs
  allowed_http_cidr_blocks = var.allowed_http_cidr_blocks
  enable_nat_gateway       = var.enable_nat_gateway
  enable_vpc_endpoints     = var.enable_vpc_endpoints
}

module "ecr" {
  source = "./modules/ecr"

  repositories    = local.ecr_repositories
  encryption_type = var.ecr_encryption_type
  kms_key_arn     = var.ecr_kms_key_arn
}

module "edge" {
  source = "./modules/edge"

  name_prefix        = local.name_prefix
  vpc_id             = module.network.vpc_id
  public_subnet_ids  = module.network.public_subnet_ids
  alb_security_group = module.network.security_group_ids.alb
  certificate_arn    = var.certificate_arn
  waf_web_acl_arn    = var.waf_web_acl_arn
  enable_web_service = var.enable_web_service
}

module "secrets" {
  source = "./modules/secrets"

  name_prefix = local.name_prefix
  kms_key_arn = var.secrets_kms_key_arn
}

module "messaging" {
  source = "./modules/messaging"

  name_prefix = local.name_prefix
}

module "observability" {
  source = "./modules/observability"

  name_prefix        = local.name_prefix
  log_retention_days = var.log_retention_days
  kms_key_arn        = var.logs_kms_key_arn
  workload_names     = ["api", "web", "worker", "migration", "simulator"]
}

module "database" {
  source = "./modules/database"

  name_prefix              = local.name_prefix
  subnet_ids               = module.network.database_subnet_ids
  security_group_ids       = [module.network.security_group_ids.database]
  engine                   = var.database_engine
  engine_version           = var.database_engine_version
  instance_class           = var.database_instance_class
  allocated_storage_gb     = var.database_allocated_storage_gb
  max_allocated_storage_gb = var.database_max_allocated_storage_gb
  master_username          = var.database_master_username
  backup_retention_days    = var.database_backup_retention_days
  deletion_protection      = var.database_deletion_protection
  skip_final_snapshot      = var.database_skip_final_snapshot
  storage_kms_key_arn      = var.database_storage_kms_key_arn
}

module "identity" {
  source = "./modules/identity"

  name_prefix         = local.name_prefix
  secret_arns         = module.secrets.workload_secret_arns
  event_bus_arn       = module.messaging.event_bus_arn
  work_queue_arn      = module.messaging.work_queue_arn
  work_dlq_arn        = module.messaging.work_dlq_arn
  secrets_kms_key_arn = var.secrets_kms_key_arn
}

module "app" {
  source = "./modules/app"

  name_prefix             = local.name_prefix
  aws_region              = var.aws_region
  ecr_repository_urls     = module.ecr.repository_urls
  api_image_digest        = var.api_image_digest
  web_image_digest        = var.web_image_digest
  private_subnet_ids      = module.network.private_subnet_ids
  api_security_group_id   = module.network.security_group_ids.api
  web_security_group_id   = module.network.security_group_ids.web
  api_target_group_arn    = module.edge.api_target_group_arn
  web_target_group_arn    = module.edge.web_target_group_arn
  execution_role_arn      = module.identity.execution_role_arn
  api_task_role_arn       = module.identity.api_task_role_arn
  web_task_role_arn       = module.identity.web_task_role_arn
  log_group_names         = module.observability.log_group_names
  api_database_secret_arn = module.secrets.api_database_password_secret_arn
  database_address        = module.database.address
  database_port           = module.database.port
  database_name           = var.database_name
  work_queue_url          = module.messaging.work_queue_url
  work_queue_arn          = module.messaging.work_queue_arn
  work_dlq_url            = module.messaging.work_dlq_url
  work_dlq_arn            = module.messaging.work_dlq_arn
  api_database_username   = var.api_database_username
  api_desired_count       = var.api_desired_count
  web_desired_count       = var.web_desired_count
  enable_web_service      = var.enable_web_service
  web_data_mode           = var.web_data_mode
  web_backend_api_url     = var.web_backend_api_url

  depends_on = [module.edge]
}

module "worker" {
  source = "./modules/worker"

  name_prefix                   = local.name_prefix
  aws_region                    = var.aws_region
  cluster_arn                   = module.app.cluster_arn
  cluster_name                  = module.app.cluster_name
  ecr_repository_urls           = module.ecr.repository_urls
  worker_image_digest           = var.worker_image_digest
  migration_image_digest        = var.migration_image_digest
  simulator_image_digest        = var.simulator_image_digest
  private_subnet_ids            = module.network.private_subnet_ids
  worker_security_group_id      = module.network.security_group_ids.worker
  migration_security_group_id   = module.network.security_group_ids.migration
  simulator_security_group_id   = module.network.security_group_ids.simulator
  execution_role_arn            = module.identity.execution_role_arn
  worker_task_role_arn          = module.identity.worker_task_role_arn
  migration_task_role_arn       = module.identity.migration_task_role_arn
  simulator_task_role_arn       = module.identity.simulator_task_role_arn
  scheduler_role_arn            = module.identity.scheduler_role_arn
  log_group_names               = module.observability.log_group_names
  migration_database_secret_arn = module.secrets.migration_database_password_secret_arn
  worker_database_secret_arn    = module.secrets.worker_database_password_secret_arn
  database_address              = module.database.address
  database_port                 = module.database.port
  database_name                 = var.database_name
  migration_database_username   = var.migration_database_username
  worker_database_username      = var.worker_database_username
  enable_worker_service         = var.enable_worker_service
  enable_simulator_schedule     = var.enable_simulator_schedule
  simulator_schedule_expression = var.simulator_schedule_expression
  event_bus_name                = module.messaging.event_bus_name
  work_queue_url                = module.messaging.work_queue_url
  work_dlq_url                  = module.messaging.work_dlq_url

  depends_on = [module.app]
}

module "budget" {
  source = "./modules/budget"

  name_prefix           = local.name_prefix
  monthly_budget_amount = var.monthly_budget_amount
  alert_emails          = var.budget_alert_emails
}
