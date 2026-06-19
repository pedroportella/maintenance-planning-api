resource "aws_cloudwatch_log_group" "workload" {
  for_each = toset(var.workload_names)

  name              = "/ecs/${var.name_prefix}/${each.value}"
  retention_in_days = var.log_retention_days
  kms_key_id        = var.kms_key_arn
}
