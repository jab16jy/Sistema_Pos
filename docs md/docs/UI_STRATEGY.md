# UI Strategy — WPF

## Decision

WPF es la UI definitiva del proyecto. El desarrollo se realiza en Windows con el .NET Desktop SDK / Visual Studio.

## Architecture

```text
POS.Presentation (WPF) -> POS.Presentation.Shared -> POS.Application -> POS.Domain
POS.Infrastructure -> POS.Application/POS.Domain contracts
```

Rules:

- WPF views son UI only — sin lógica de negocio.
- ViewModels compartidos viven en `POS.Presentation.Shared`: estado de presentación, comandos, mensajes al operador.
- Reglas de negocio en Domain/Application.
- Persistencia, impresión, reportes y backup en Infrastructure.
- WPF no consulta SQLite directamente.

## Current status

La shell de navegación WPF está implementada con las vistas principales: Caja, Clientes, Proveedores, Compras, Cotizaciones, Cuentas por cobrar, Reportes y Envío de documentos.

Correr en Windows:

```bash
dotnet run --project src/POS.Presentation/POS.Presentation.csproj
```

Validar lógica de presentación compartida:

```bash
dotnet test tests/POS.Presentation.Shared.Tests/POS.Presentation.Shared.Tests.csproj
```

---

## Documentos relacionados

[[PROJECT_START]] · [[01_diseno_integracion_tecnologica]] · [[02_diseno_arquitectura_capas]]
