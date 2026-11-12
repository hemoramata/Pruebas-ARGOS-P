# Aplicar una política separada por institución. Sustituir el prefijo de tenant.
path "transit/encrypt/argos-tenant-REPLACE" {
  capabilities = ["update"]
}

path "transit/decrypt/argos-tenant-REPLACE" {
  capabilities = ["update"]
}

path "transit/rewrap/argos-tenant-REPLACE" {
  capabilities = ["update"]
}
