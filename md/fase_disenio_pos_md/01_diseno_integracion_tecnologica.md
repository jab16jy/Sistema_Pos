# 01 — Diseño de Integración Tecnológica

> **Enfoque didáctico del sistema:** todas las pantallas y flujos se diseñan para que una persona sin formación técnica pueda aprender a usar el POS en poco tiempo. El sistema debe guiar al usuario paso a paso, usar lenguaje cotidiano, mostrar ayudas visuales, prevenir errores y permitir operar las funciones frecuentes con teclado o escáner.

## Objetivo

Definir cómo se integran las tecnologías seleccionadas para construir un **sistema POS local de escritorio**, moderno, mantenible, offline y fácil de aprender.

## Tecnologías integradas

| Tecnología | Uso dentro del sistema | Explicación sencilla |
|---|---|---|
| C# / .NET 9 | Lenguaje y plataforma principal | Permite crear la lógica del programa y organizar el proyecto. |
| WPF | Interfaz gráfica de escritorio | Muestra las pantallas que verá el cajero o administrador. |
| MVVM | Patrón de presentación | Separa la pantalla de la lógica para mantener el código ordenado. |
| Clean Architecture | Organización por capas | Divide el sistema en responsabilidades claras. |
| EF Core 9 | ORM | Permite guardar y consultar datos sin escribir todo el SQL manualmente. |
| SQLite | Base de datos local | Guarda la información en el equipo, sin depender de internet. |
| ESC/POS | Impresión térmica | Permite imprimir recibos en impresoras de punto de venta. |
| ReportService | Reportes PDF/Excel | Genera informes de ventas, inventario, caja y utilidades. |

## Integración general

```text
Usuario
  ↓
Pantallas WPF
  ↓
ViewModels MVVM
  ↓
Casos de uso Application
  ↓
Reglas de negocio Domain
  ↓
Repositorios Infrastructure
  ↓
EF Core + SQLite
  ↓
Servicios externos: impresión, reportes, backup
```

## Explicación del flujo

Cuando el usuario realiza una acción, por ejemplo **finalizar una venta**, no se guarda directamente desde la pantalla. El recorrido correcto es:

1. El cajero presiona **Finalizar venta**.
2. La vista WPF envía la acción al `SaleViewModel`.
3. El ViewModel llama al caso de uso `CreateSaleUseCase`.
4. El caso de uso valida reglas de negocio: stock, total, pagos, descuentos.
5. Los repositorios guardan la venta en SQLite mediante EF Core.
6. Se actualiza el inventario.
7. Se imprime el recibo usando el servicio ESC/POS.
8. El sistema muestra un mensaje claro: **“Venta guardada e impresa correctamente”**.

## Principios didácticos aplicados

| Principio | Aplicación en el diseño |
|---|---|
| Claridad | Los botones deben decir exactamente qué hacen: “Cobrar”, “Imprimir”, “Buscar producto”. |
| Flujo paso a paso | En caja se sigue el orden: buscar producto → agregar → cobrar → imprimir. |
| Ayuda visible | Cada pantalla debe mostrar una frase guía, por ejemplo: “Escanea el producto o escribe su nombre”. |
| Feedback inmediato | Después de cada acción debe aparecer una confirmación visual. |
| Reducción de carga mental | Las opciones avanzadas se ocultan o agrupan para no confundir al cajero. |

## Diseño preliminar de navegación tecnológica

```text
LoginView
  ↓
MainShellView
  ├── Caja
  ├── Productos
  ├── Inventario
  ├── Clientes
  ├── Proveedores
  ├── Compras
  ├── Facturas
  ├── Reportes
  ├── Utilidades
  ├── Código de Barras
  └── Configuración
```

## Reglas de integración

- La interfaz WPF no debe consultar directamente la base de datos.
- Los ViewModels no deben contener reglas complejas de negocio.
- La lógica de negocio debe estar en Domain y Application.
- Infrastructure debe encargarse de SQLite, EF Core, impresión, reportes y backup.
- Los servicios externos deben ser llamados mediante interfaces.
- Toda acción crítica debe devolver mensajes claros para el usuario.

## Resultado esperado

Este diseño permite comenzar el desarrollo sabiendo cómo se conectarán todas las tecnologías. Además, garantiza que el sistema sea comprensible para el usuario final y mantenible para el equipo desarrollador.
