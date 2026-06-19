output "event_bus_arn" {
  value = aws_cloudwatch_event_bus.this.arn
}

output "event_bus_name" {
  value = aws_cloudwatch_event_bus.this.name
}

output "work_queue_arn" {
  value = aws_sqs_queue.work.arn
}

output "work_queue_url" {
  value = aws_sqs_queue.work.url
}

output "work_dlq_arn" {
  value = aws_sqs_queue.dlq.arn
}

output "work_dlq_url" {
  value = aws_sqs_queue.dlq.url
}

output "event_rule_arn" {
  value = aws_cloudwatch_event_rule.maintenance_events.arn
}
