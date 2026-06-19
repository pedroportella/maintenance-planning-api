output "load_balancer_dns_name" {
  value = aws_lb.this.dns_name
}

output "load_balancer_arn" {
  value = aws_lb.this.arn
}

output "api_target_group_arn" {
  value = aws_lb_target_group.api.arn
}

output "web_target_group_arn" {
  value = var.enable_web_service ? aws_lb_target_group.web[0].arn : null
}
