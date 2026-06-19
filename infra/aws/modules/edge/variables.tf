variable "name_prefix" {
  type = string
}

variable "vpc_id" {
  type = string
}

variable "public_subnet_ids" {
  type = list(string)
}

variable "alb_security_group" {
  type = string
}

variable "certificate_arn" {
  type = string
}

variable "waf_web_acl_arn" {
  type = string
}

variable "enable_web_service" {
  type = bool
}
