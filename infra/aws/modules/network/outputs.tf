output "vpc_id" {
  value = aws_vpc.this.id
}

output "public_subnet_ids" {
  value = aws_subnet.public[*].id
}

output "private_subnet_ids" {
  value = aws_subnet.private[*].id
}

output "database_subnet_ids" {
  value = aws_subnet.database[*].id
}

output "security_group_ids" {
  value = {
    alb       = aws_security_group.alb.id
    api       = aws_security_group.api.id
    web       = aws_security_group.web.id
    worker    = aws_security_group.worker.id
    migration = aws_security_group.migration.id
    simulator = aws_security_group.simulator.id
    database  = aws_security_group.database.id
  }
}
