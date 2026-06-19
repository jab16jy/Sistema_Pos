# 10 — Diseño del Entregable Final de la Fase

> **Enfoque didáctico del sistema:** todas las pantallas y flujos se diseñan para que una persona sin formación técnica pueda aprender a usar el POS en poco tiempo. El sistema debe guiar al usuario paso a paso, usar lenguaje cotidiano, mostrar ayudas visuales, prevenir errores y permitir operar las funciones frecuentes con teclado o escáner.

## Objetivo

Definir cómo debe quedar organizado el documento final de la **Fase 2 — Diseño** para que sirva como guía clara antes de iniciar la programación del sistema POS.

## Nombre sugerido del entregable

```text
Documento de Diseño del Sistema POS Local
```

## Estructura recomendada

```text
1. Portada
2. Introducción
3. Objetivo de la fase de diseño
4. Alcance del sistema
5. Requerimientos considerados
6. Diseño de integración tecnológica
7. Diseño de arquitectura en capas
8. Diseño del modelo de dominio
9. Diseño del modelo entidad-relación
10. Diseño de casos de uso
11. Diseño de flujos funcionales
12. Wireframes y mockups preliminares
13. Diseño de integración de módulos
14. Diseño del protocolo de pruebas
15. Consideraciones didácticas y de usabilidad
16. Conclusiones
17. Anexos
```

## Introducción sugerida

```text
El presente documento describe la fase de diseño del Sistema POS Local, una aplicación de escritorio orientada a pequeños comercios. El diseño busca establecer una base técnica y funcional que permita iniciar el desarrollo del sistema de forma ordenada, integrando tecnologías .NET modernas, arquitectura limpia, base de datos local, reportes e impresión térmica.

Además de los aspectos técnicos, el diseño prioriza la facilidad de uso. El sistema debe ser didáctico, intuitivo y adecuado para usuarios sin formación técnica, permitiendo que cajeros y administradores aprendan a operar las funciones principales en poco tiempo.
```

## Objetivo de diseño sugerido

```text
Diseñar la arquitectura, modelo de datos, flujos funcionales e interfaz preliminar del Sistema POS Local bajo Clean Architecture y MVVM, integrando WPF, .NET 9, EF Core, SQLite, servicios de reportes e impresión ESC/POS, con un enfoque didáctico que facilite el aprendizaje rápido por parte del usuario final.
```

## Alcance del diseño

- Autenticación y usuarios.
- Caja y ventas.
- Productos e inventario.
- Clientes y puntos.
- Proveedores y compras.
- Facturas y devoluciones.
- Reportes y cierre de caja.
- Utilidades.
- Código de barras.
- Configuración, backup e impresión.

## Consideraciones didácticas obligatorias

| Elemento | Criterio |
|---|---|
| Textos | Deben ser cortos, claros y orientados a la acción. |
| Botones | Deben usar verbos: “Guardar”, “Cobrar”, “Imprimir”, “Cancelar”. |
| Errores | Deben explicar qué pasó y cómo solucionarlo. |
| Pantallas | Deben mostrar solo lo necesario para la tarea. |
| Flujos | Deben guiar al usuario paso a paso. |
| Atajos | Deben ser visibles en las funciones frecuentes. |
| Confirmaciones | Deben explicar la consecuencia antes de ejecutar acciones críticas. |

## Matriz de trazabilidad sugerida

| Requerimiento | Diseño relacionado | Módulo |
|---|---|---|
| Inicio de sesión | LoginView + AuthenticateUserUseCase | Usuarios |
| Registro de productos | ProductsView + CreateProductUseCase | Productos |
| Venta en caja | SaleView + CreateSaleUseCase | Caja |
| Descuento de inventario | InventoryMovement + ProductRepository | Inventario |
| Impresión de recibo | IPrintService + PrintService | Facturas/Caja |
| Reportes | IReportService + ReportsView | Reportes |
| Cierre de caja | CloseCashRegisterUseCase | Caja/Reportes |
| Backup | BackupService | Configuración |

## Checklist para validar el entregable

```text
[ ] Se explica cómo se integran las tecnologías.
[ ] Se incluye arquitectura en capas.
[ ] Se describen entidades del dominio.
[ ] Se propone modelo de base de datos.
[ ] Se definen casos de uso principales.
[ ] Se describen flujos funcionales.
[ ] Se incluyen wireframes preliminares.
[ ] Se explica integración entre módulos.
[ ] Se define protocolo de pruebas.
[ ] Se incluye enfoque didáctico y facilidad de uso.
```

## Conclusión sugerida

```text
La fase de diseño permite transformar los requerimientos del Sistema POS Local en una guía técnica y funcional para el desarrollo. Mediante la separación en capas, el uso de MVVM, la definición del modelo de datos, los flujos de trabajo y los wireframes, se establece una base sólida para construir un sistema mantenible, confiable y fácil de usar.

El enfoque didáctico garantiza que el sistema no solo cumpla técnicamente con sus funciones, sino que también pueda ser aprendido rápidamente por cajeros y administradores de pequeños comercios. Esto reduce errores operativos, facilita la adopción del sistema y mejora la experiencia del usuario final.
```

## Resultado esperado

Este documento final debe funcionar como la guía oficial para iniciar la fase de desarrollo, evitando improvisaciones y asegurando que todo el equipo comprenda cómo se construirá el sistema.
