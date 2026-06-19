variable "repositories" {
  type = map(string)
}

variable "encryption_type" {
  type = string
}

variable "kms_key_arn" {
  type = string
}
