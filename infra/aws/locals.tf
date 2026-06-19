locals {
  name_prefix = "${var.project}-${var.environment}"

  common_tags = {
    project      = var.project
    owner        = var.owner
    environment  = var.environment
    managed_by   = "terraform"
    data_profile = "synthetic"
  }

  ecr_repositories = {
    api       = "${local.name_prefix}-api"
    web       = "${local.name_prefix}-web"
    simulator = "${local.name_prefix}-simulator"
    worker    = "${local.name_prefix}-worker"
    migration = "${local.name_prefix}-migration-runner"
  }
}
