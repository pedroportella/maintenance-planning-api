variable "name_prefix" {
  type = string
}

variable "aws_region" {
  type = string
}

variable "vpc_cidr" {
  type = string
}

variable "public_subnet_cidrs" {
  type = list(string)
}

variable "private_subnet_cidrs" {
  type = list(string)
}

variable "database_subnet_cidrs" {
  type = list(string)
}

variable "allowed_http_cidr_blocks" {
  type = list(string)
}

variable "enable_nat_gateway" {
  type = bool
}

variable "enable_vpc_endpoints" {
  type = bool
}
