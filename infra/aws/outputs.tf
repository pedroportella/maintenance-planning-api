output "load_balancer_dns_name" {
  description = "Review load balancer DNS name."
  value       = module.edge.load_balancer_dns_name
}

output "api_repository_url" {
  description = "API ECR repository URL."
  value       = module.ecr.repository_urls.api
}

output "web_repository_url" {
  description = "Web ECR repository URL."
  value       = module.ecr.repository_urls.web
}

output "simulator_repository_url" {
  description = "Simulator ECR repository URL."
  value       = module.ecr.repository_urls.simulator
}

output "worker_repository_url" {
  description = "Worker ECR repository URL."
  value       = module.ecr.repository_urls.worker
}

output "migration_repository_url" {
  description = "Migration-runner ECR repository URL."
  value       = module.ecr.repository_urls.migration
}

output "database_endpoint" {
  description = "Review database endpoint."
  value       = module.database.endpoint
  sensitive   = true
}

output "migration_task_definition_arn" {
  description = "Migration task definition ARN for release orchestration."
  value       = module.worker.migration_task_definition_arn
}

output "simulator_task_definition_arn" {
  description = "Simulator task definition ARN."
  value       = module.worker.simulator_task_definition_arn
}
