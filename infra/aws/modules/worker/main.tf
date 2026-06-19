locals {
  worker_image    = "${var.ecr_repository_urls.worker}@${var.worker_image_digest}"
  migration_image = "${var.ecr_repository_urls.migration}@${var.migration_image_digest}"
  simulator_image = "${var.ecr_repository_urls.simulator}@${var.simulator_image_digest}"

  migration_environment = [
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
      value = var.migration_database_username
    },
    {
      name  = "MaintenancePlanning__Database__Encrypt"
      value = "true"
    },
    {
      name  = "MaintenancePlanning__Database__TrustServerCertificate"
      value = "false"
    }
  ]

  worker_environment = [
    {
      name  = "DOTNET_EnableDiagnostics"
      value = "0"
    },
    {
      name  = "MAINTENANCE_PLANNING_WORK_QUEUE_URL"
      value = var.work_queue_url
    },
    {
      name  = "MAINTENANCE_PLANNING_EVENT_BUS_NAME"
      value = var.event_bus_name
    }
  ]

  simulator_environment = concat(
    [
      {
        name  = "SIMULATOR_EVENT_BUS_NAME"
        value = var.event_bus_name
      }
    ],
    var.simulator_api_url == null ? [] : [
      {
        name  = "SIMULATOR_API_URL"
        value = var.simulator_api_url
      }
    ]
  )
}

resource "aws_ecs_task_definition" "worker" {
  family                   = "${var.name_prefix}-worker"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "512"
  memory                   = "1024"
  execution_role_arn       = var.execution_role_arn
  task_role_arn            = var.worker_task_role_arn

  runtime_platform {
    operating_system_family = "LINUX"
    cpu_architecture        = "X86_64"
  }

  volume {
    name = "worker-tmp"
  }

  container_definitions = jsonencode([
    {
      name                   = "worker"
      image                  = local.worker_image
      essential              = true
      user                   = "1654"
      readonlyRootFilesystem = true
      linuxParameters = {
        initProcessEnabled = true
        capabilities = {
          drop = ["ALL"]
        }
      }
      mountPoints = [
        {
          sourceVolume  = "worker-tmp"
          containerPath = "/tmp"
          readOnly      = false
        }
      ]
      environment = local.worker_environment
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = var.log_group_names.worker
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = "worker"
        }
      }
    }
  ])
}

resource "aws_ecs_service" "worker" {
  count = var.enable_worker_service ? 1 : 0

  name            = "${var.name_prefix}-worker"
  cluster         = var.cluster_name
  task_definition = aws_ecs_task_definition.worker.arn
  desired_count   = 1
  launch_type     = "FARGATE"

  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  network_configuration {
    subnets          = var.private_subnet_ids
    security_groups  = [var.worker_security_group_id]
    assign_public_ip = false
  }
}

resource "aws_ecs_task_definition" "migration" {
  family                   = "${var.name_prefix}-migration"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "512"
  memory                   = "1024"
  execution_role_arn       = var.execution_role_arn
  task_role_arn            = var.migration_task_role_arn

  runtime_platform {
    operating_system_family = "LINUX"
    cpu_architecture        = "X86_64"
  }

  volume {
    name = "migration-tmp"
  }

  container_definitions = jsonencode([
    {
      name                   = "migration"
      image                  = local.migration_image
      essential              = true
      user                   = "1654"
      readonlyRootFilesystem = true
      linuxParameters = {
        initProcessEnabled = true
        capabilities = {
          drop = ["ALL"]
        }
      }
      mountPoints = [
        {
          sourceVolume  = "migration-tmp"
          containerPath = "/tmp"
          readOnly      = false
        }
      ]
      environment = local.migration_environment
      secrets = [
        {
          name      = "MaintenancePlanning__Database__Password"
          valueFrom = var.migration_database_secret_arn
        }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = var.log_group_names.migration
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = "migration"
        }
      }
    }
  ])
}

resource "aws_ecs_task_definition" "simulator" {
  family                   = "${var.name_prefix}-simulator"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "256"
  memory                   = "512"
  execution_role_arn       = var.execution_role_arn
  task_role_arn            = var.simulator_task_role_arn

  runtime_platform {
    operating_system_family = "LINUX"
    cpu_architecture        = "X86_64"
  }

  volume {
    name = "simulator-tmp"
  }

  container_definitions = jsonencode([
    {
      name                   = "simulator"
      image                  = local.simulator_image
      essential              = true
      user                   = "10001"
      readonlyRootFilesystem = true
      linuxParameters = {
        initProcessEnabled = true
        capabilities = {
          drop = ["ALL"]
        }
      }
      command = ["feed", "--scenario", "baseline-week"]
      mountPoints = [
        {
          sourceVolume  = "simulator-tmp"
          containerPath = "/tmp"
          readOnly      = false
        }
      ]
      environment = local.simulator_environment
      secrets = [
        {
          name      = "SIMULATOR_API_TOKEN"
          valueFrom = var.simulator_api_token_secret_arn
        }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = var.log_group_names.simulator
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = "simulator"
        }
      }
    }
  ])
}

resource "aws_scheduler_schedule" "simulator" {
  count = var.enable_simulator_schedule ? 1 : 0

  name                = "${var.name_prefix}-simulator"
  schedule_expression = var.simulator_schedule_expression

  flexible_time_window {
    mode = "OFF"
  }

  target {
    arn      = var.cluster_arn
    role_arn = var.scheduler_role_arn

    ecs_parameters {
      task_definition_arn = aws_ecs_task_definition.simulator.arn
      launch_type         = "FARGATE"
      platform_version    = "LATEST"

      network_configuration {
        subnets          = var.private_subnet_ids
        security_groups  = [var.simulator_security_group_id]
        assign_public_ip = false
      }
    }
  }
}
