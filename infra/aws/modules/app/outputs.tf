output "cluster_arn" {
  value = aws_ecs_cluster.this.arn
}

output "cluster_name" {
  value = aws_ecs_cluster.this.name
}

output "api_task_definition_arn" {
  value = aws_ecs_task_definition.api.arn
}

output "web_task_definition_arn" {
  value = var.enable_web_service ? aws_ecs_task_definition.web[0].arn : null
}
