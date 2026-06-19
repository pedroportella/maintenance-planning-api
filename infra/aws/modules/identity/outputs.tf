output "execution_role_arn" {
  value = aws_iam_role.execution.arn
}

output "api_task_role_arn" {
  value = aws_iam_role.api_task.arn
}

output "web_task_role_arn" {
  value = aws_iam_role.web_task.arn
}

output "migration_task_role_arn" {
  value = aws_iam_role.migration_task.arn
}

output "worker_task_role_arn" {
  value = aws_iam_role.worker_task.arn
}

output "simulator_task_role_arn" {
  value = aws_iam_role.simulator_task.arn
}

output "scheduler_role_arn" {
  value = aws_iam_role.scheduler.arn
}
