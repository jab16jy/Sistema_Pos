# Project Start Notes

## Source of truth

The initial structure comes from:

- `md/Exploracion/requerimientos.md`
- `md/fase_disenio_pos_md/01_diseno_integracion_tecnologica.md`
- `md/fase_disenio_pos_md/02_diseno_arquitectura_capas.md`
- `md/fase_disenio_pos_md/03_diseno_modelo_dominio.md`
- `md/fase_disenio_pos_md/04_diseno_modelo_datos_sqlite.md`
- `md/fase_disenio_pos_md/09_diseno_protocolo_pruebas.md`

## Decisions applied

- The solution follows four production projects: Domain, Application, Infrastructure, Presentation.
- Tests are split by architectural layer: Domain tests and Application tests.
- Package versions are centralized in `Directory.Packages.props`.
- WPF targets `net9.0-windows` and enables `UseWPF`.
- `EnableWindowsTargeting` is enabled so non-Windows machines can restore/build Windows-targeted projects when the .NET SDK is installed.
- `.atl/` is ignored because it is an agent registry/cache, not project source.

## Environment status

The current machine is EndeavourOS/Arch and has .NET SDK `9.0.118` installed through `pacman` package `dotnet-sdk-9.0`.

The following has been verified:

- `dotnet --info` works.
- `dotnet restore POS.sln` works.
- `dotnet build src/POS.Infrastructure/POS.Infrastructure.csproj --no-restore` works.
- Domain and Application tests pass.

## Known limitation

The Arch source-built SDK does not include `Microsoft.NET.Sdk.WindowsDesktop`, so `POS.Presentation` fails on Linux with MSB4019 for missing `Microsoft.NET.Sdk.WindowsDesktop.targets`. This is an environment/platform limitation for WPF, not a Domain/Application/Infrastructure failure.

Build and run the WPF app on Windows with the .NET Desktop SDK/Visual Studio. Use Linux for the inner layers and tests.

---

## Documentos relacionados

[[requerimientos]] · [[01_diseno_integracion_tecnologica]] · [[02_diseno_arquitectura_capas]] · [[03_diseno_modelo_dominio]] · [[04_diseno_modelo_datos_sqlite]] · [[09_diseno_protocolo_pruebas]] · [[UI_STRATEGY]]
