# AWS And Terraform

Terraform will target Pedro's AWS review environment only.

Planned resources:

- remote state bootstrap;
- VPC and subnets;
- ALB/TLS/WAF where a domain is available;
- ECS/Fargate API and worker;
- RDS SQL Server;
- EventBridge, SQS and DLQ;
- Secrets Manager;
- CloudWatch logs, alarms and dashboards;
- budget alarm and resource tags.

Do not commit Terraform state, plans, account ids, ARNs, secrets or generated sensitive outputs.
