output "log_group_names" {
  value = {
    for name, log_group in aws_cloudwatch_log_group.workload : name => log_group.name
  }
}

output "log_group_arns" {
  value = {
    for name, log_group in aws_cloudwatch_log_group.workload : name => log_group.arn
  }
}
