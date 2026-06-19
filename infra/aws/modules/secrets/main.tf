locals {
  secret_names = {
    api_database_password       = "${var.name_prefix}/api/database-password"
    migration_database_password = "${var.name_prefix}/migration/database-password"
    worker_database_password    = "${var.name_prefix}/worker/database-password"
  }
}

resource "aws_secretsmanager_secret" "this" {
  for_each = local.secret_names

  name                    = each.value
  description             = "Review runtime secret placeholder for ${replace(each.key, "_", " ")}."
  kms_key_id              = var.kms_key_arn
  recovery_window_in_days = 7
}
