output "api_database_password_secret_arn" {
  value = aws_secretsmanager_secret.this["api_database_password"].arn
}

output "migration_database_password_secret_arn" {
  value = aws_secretsmanager_secret.this["migration_database_password"].arn
}

output "worker_database_password_secret_arn" {
  value = aws_secretsmanager_secret.this["worker_database_password"].arn
}

output "workload_secret_arns" {
  value = [
    for secret in aws_secretsmanager_secret.this : secret.arn
  ]
}
