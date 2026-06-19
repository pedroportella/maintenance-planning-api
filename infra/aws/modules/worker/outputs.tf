output "worker_task_definition_arn" {
  value = aws_ecs_task_definition.worker.arn
}

output "migration_task_definition_arn" {
  value = aws_ecs_task_definition.migration.arn
}

output "simulator_task_definition_arn" {
  value = aws_ecs_task_definition.simulator.arn
}
