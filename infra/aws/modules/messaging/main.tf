resource "aws_cloudwatch_event_bus" "this" {
  name = "${var.name_prefix}-events"
}

resource "aws_sqs_queue" "dlq" {
  name                      = "${var.name_prefix}-work-dlq"
  message_retention_seconds = 1209600
}

resource "aws_sqs_queue" "work" {
  name                       = "${var.name_prefix}-work"
  visibility_timeout_seconds = 60
  message_retention_seconds  = 345600

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.dlq.arn
    maxReceiveCount     = 5
  })
}

resource "aws_cloudwatch_event_rule" "maintenance_events" {
  name           = "${var.name_prefix}-maintenance-events"
  event_bus_name = aws_cloudwatch_event_bus.this.name

  event_pattern = jsonencode({
    source        = var.event_sources
    "detail-type" = var.event_detail_types
    detail = {
      schemaVersion = ["1.0"]
    }
  })
}

resource "aws_cloudwatch_event_target" "work_queue" {
  rule           = aws_cloudwatch_event_rule.maintenance_events.name
  event_bus_name = aws_cloudwatch_event_bus.this.name
  target_id      = "work-queue"
  arn            = aws_sqs_queue.work.arn
}

data "aws_iam_policy_document" "work_queue" {
  statement {
    actions   = ["sqs:SendMessage"]
    resources = [aws_sqs_queue.work.arn]

    principals {
      type        = "Service"
      identifiers = ["events.amazonaws.com"]
    }

    condition {
      test     = "ArnEquals"
      variable = "aws:SourceArn"
      values   = [aws_cloudwatch_event_rule.maintenance_events.arn]
    }
  }
}

resource "aws_sqs_queue_policy" "work_queue" {
  queue_url = aws_sqs_queue.work.url
  policy    = data.aws_iam_policy_document.work_queue.json
}
