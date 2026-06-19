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
