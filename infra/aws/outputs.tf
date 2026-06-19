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

output "cluster_name" {
  description = "ECS cluster name for release orchestration."
  value       = module.app.cluster_name
}

output "api_service_name" {
  description = "ECS API service name for release orchestration."
  value       = module.app.api_service_name
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

output "worker_task_definition_arn" {
  description = "Worker task definition ARN for optional event ingestion service runs."
  value       = module.worker.worker_task_definition_arn
}

output "event_bus_name" {
  description = "Event bus name for simulator event publishing."
  value       = module.messaging.event_bus_name
}

output "work_queue_url" {
  description = "SQS work queue URL used by the event ingestion worker."
  value       = module.messaging.work_queue_url
}

output "work_dlq_url" {
  description = "SQS dead-letter queue URL used for eventing posture."
  value       = module.messaging.work_dlq_url
}

output "migration_security_group_id" {
  description = "Security group id used by release-orchestrated migration tasks."
  value       = module.network.security_group_ids.migration
}

output "private_subnet_ids" {
  description = "Private subnet ids used by release-orchestrated migration tasks."
  value       = module.network.private_subnet_ids
}

output "simulator_task_definition_arn" {
  description = "Simulator task definition ARN."
  value       = module.worker.simulator_task_definition_arn
}
