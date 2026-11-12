# Gestión de Dependencias

Las dependencias del proyecto se clasifican en:

- Producción
- Desarrollo
- Pruebas
- Transitivas

## Reglas

Las dependencias deberán mantenerse siempre en versiones estables oficialmente soportadas.

No se actualizarán paquetes únicamente porque exista una versión más reciente.

Antes de actualizar cualquier dependencia deberá verificarse:

- Compatibilidad con la versión de .NET utilizada.
- Compatibilidad con el resto de paquetes del proyecto.
- Impacto sobre la arquitectura.
- Impacto sobre las pruebas.

## Dependencias Transitivas

Las dependencias transitivas no deberán sobrescribirse manualmente salvo que exista una justificación técnica documentada.

Se consideran causas válidas:

- Vulnerabilidad crítica sin mitigación.
- Recomendación oficial de Microsoft.
- Corrección documentada mediante un ADR.
- Aprobación del equipo de arquitectura.

En caso contrario se esperará una actualización oficial del paquete principal.

## Versiones Mayores

Las actualizaciones de versión mayor (Major Version) deberán realizarse únicamente durante un Sprint de Mantenimiento.

Toda actualización mayor deberá incluir:

- Compilación completa.
- Ejecución de pruebas.
- Actualización del CHANGELOG.
- Registro en la Bitácora.
- Registro de ADR cuando aplique.

## Versiones Menores y de Parche

Las actualizaciones menores (Minor) y de parche (Patch) podrán aplicarse cuando:

- Corrijan errores.
- Corrijan vulnerabilidades.
- No rompan compatibilidad.
- Superen la compilación y las pruebas del proyecto.

## Auditoría de Dependencias

Periódicamente deberán ejecutarse los siguientes comandos:

```bash
dotnet list package --vulnerable

dotnet list package --include-transitive

dotnet list package --outdated
```

Los resultados deberán analizarse antes de realizar cualquier actualización.


## Segunda Regla de Oro

Nunca modificar una dependencia oficial de Microsoft únicamente para eliminar una advertencia.

Primero deberá verificarse:

- Si la vulnerabilidad es directa o transitiva.
- Si existe una actualización oficial.
- Si Microsoft recomienda una mitigación.
- Si el cambio mantiene la compatibilidad del proyecto.

La estabilidad del proyecto tiene prioridad sobre la eliminación de advertencias aisladas.