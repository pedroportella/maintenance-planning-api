locals {
  api_image = "${var.ecr_repository_urls.api}@${var.api_image_digest}"
  web_image = "${var.ecr_repository_urls.web}@${var.web_image_digest}"

  api_environment = [
    {
      name  = "ASPNETCORE_URLS"
      value = "http://+:8080"
    },
    {
      name  = "DOTNET_EnableDiagnostics"
      value = "0"
    },
    {
      name  = "MaintenancePlanning__Database__Enabled"
      value = "true"
    },
    {
      name  = "MaintenancePlanning__Database__Server"
      value = "${var.database_address},${var.database_port}"
    },
    {
      name  = "MaintenancePlanning__Database__Database"
      value = var.database_name
    },
    {
      name  = "MaintenancePlanning__Database__User"
      value = var.api_database_username
    },
    {
      name  = "MaintenancePlanning__Database__Encrypt"
      value = "true"
    },
    {
      name  = "MaintenancePlanning__Database__TrustServerCertificate"
      value = "false"
    },
    {
      name  = "MaintenancePlanning__Eventing__Enabled"
      value = "true"
    },
    {
      name  = "MaintenancePlanning__Eventing__QueueUrl"
      value = var.work_queue_url
    },
    {
      name  = "MaintenancePlanning__Eventing__DeadLetterQueueUrl"
      value = var.work_dlq_url
    },
    {
      name  = "MaintenancePlanning__Eventing__Region"
      value = var.aws_region
    }
  ]

  web_environment = concat(
    [
      {
        name  = "PORT"
        value = "8080"
      },
      {
        name  = "HOSTNAME"
        value = "0.0.0.0"
      },
      {
        name  = "MAINTENANCE_PLANNING_WEB_DATA_MODE"
        value = var.web_data_mode
      }
    ],
    var.web_backend_api_url == null ? [] : [
      {
        name  = "MAINTENANCE_PLANNING_API_URL"
        value = var.web_backend_api_url
      }
    ]
  )
}

resource "aws_ecs_cluster" "this" {
  name = "${var.name_prefix}-cluster"

  setting {
    name  = "containerInsights"
    value = "enabled"
  }
}

resource "aws_ecs_task_definition" "api" {
  family                   = "${var.name_prefix}-api"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "512"
  memory                   = "1024"
  execution_role_arn       = var.execution_role_arn
  task_role_arn            = var.api_task_role_arn

  runtime_platform {
    operating_system_family = "LINUX"
    cpu_architecture        = "X86_64"
  }

  volume {
    name = "api-tmp"
  }

  container_definitions = jsonencode([
    {
      name                   = "api"
      image                  = local.api_image
      essential              = true
      user                   = "1654"
      readonlyRootFilesystem = true
      linuxParameters = {
        initProcessEnabled = true
        capabilities = {
          drop = ["ALL"]
        }
      }
      portMappings = [
        {
          containerPort = 8080
          hostPort      = 8080
          protocol      = "tcp"
        }
      ]
      mountPoints = [
        {
          sourceVolume  = "api-tmp"
          containerPath = "/tmp"
          readOnly      = false
        }
      ]
      environment = local.api_environment
      secrets = [
        {
          name      = "MaintenancePlanning__Database__Password"
          valueFrom = var.api_database_secret_arn
        }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = var.log_group_names.api
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = "api"
        }
      }
    }
  ])
}

resource "aws_ecs_service" "api" {
  name            = "${var.name_prefix}-api"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.api.arn
  desired_count   = var.api_desired_count
  launch_type     = "FARGATE"

  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  network_configuration {
    subnets          = var.private_subnet_ids
    security_groups  = [var.api_security_group_id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = var.api_target_group_arn
    container_name   = "api"
    container_port   = 8080
  }
}

resource "aws_ecs_task_definition" "web" {
  count = var.enable_web_service ? 1 : 0

  family                   = "${var.name_prefix}-web"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "512"
  memory                   = "1024"
  execution_role_arn       = var.execution_role_arn
  task_role_arn            = var.web_task_role_arn

  runtime_platform {
    operating_system_family = "LINUX"
    cpu_architecture        = "X86_64"
  }

  volume {
    name = "web-tmp"
  }

  container_definitions = jsonencode([
    {
      name                   = "web"
      image                  = local.web_image
      essential              = true
      user                   = "10001"
      readonlyRootFilesystem = true
      linuxParameters = {
        initProcessEnabled = true
        capabilities = {
          drop = ["ALL"]
        }
      }
      portMappings = [
        {
          containerPort = 8080
          hostPort      = 8080
          protocol      = "tcp"
        }
      ]
      mountPoints = [
        {
          sourceVolume  = "web-tmp"
          containerPath = "/tmp"
          readOnly      = false
        }
      ]
      environment = local.web_environment
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = var.log_group_names.web
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = "web"
        }
      }
    }
  ])
}

resource "aws_ecs_service" "web" {
  count = var.enable_web_service ? 1 : 0

  name            = "${var.name_prefix}-web"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.web[0].arn
  desired_count   = var.web_desired_count
  launch_type     = "FARGATE"

  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  network_configuration {
    subnets          = var.private_subnet_ids
    security_groups  = [var.web_security_group_id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = var.web_target_group_arn
    container_name   = "web"
    container_port   = 8080
  }
}
