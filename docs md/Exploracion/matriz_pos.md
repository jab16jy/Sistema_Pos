## PROMPT PARA NOTEBOOKLM — MATRIZ
| TÍTULO | OBJETIVO GENERAL | OBJETIVO ESPECÍFICO | FASES | ACTIVIDADES | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 | 19 | 20 | 21 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Desarrollo de un sistema POS local de escritorio para pequeños comercios, implementado con tecnologías .NET modernas bajo principios de arquitectura limpia. | Desarrollar un sistema POS local de escritorio para pequeños comercios utilizando tecnologías .NET 9, WPF, MVVM y Clean Architecture, garantizando un rendimiento óptimo, operación offline y escalabilidad. | Analizar las necesidades funcionales, tecnológicas y de contexto para fundamentar el diseño del sistema POS. | FASE 1 — EXPLORACIÓN | Revisión de antecedentes: sistemas POS existentes (open source y comerciales), análisis comparativo de funcionalidades. | X |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |
|   |   |   |   | Levantamiento inicial de requerimientos con usuarios objetivo (tenderos, pequeños comerciantes). | X | X |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |
|   |   |   |   | Análisis de restricciones tecnológicas: compatibilidad WPF + .NET 9, limitaciones de hardware en pequeños comercios. |   | X |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |
|   |   |   |   | Evaluación de SQLite como motor de base de datos local vs. alternativas (LiteDB, SQL Server LocalDB). |   |   | X |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |
|   |   |   |   | Investigación de librerías para impresión térmica ESC/POS en .NET. |   |   | X |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |
|   |   |   |   | Definición formal del stack tecnológico y justificación de cada elección. |   |   |   | X |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |
|   |   |   |   | Entregable: Informe de exploración tecnológica y contextual. |   |   |   | X |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |
|   |   | Diseñar la arquitectura, modelo de datos y flujos funcionales del sistema POS bajo Clean Architecture y MVVM. | FASE 2 — DISEÑO | Especificación formal de requerimientos funcionales y no funcionales (documento SRS). |   |   |   |   | X |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |
|   |   |   |   | Diseño del modelo de dominio: entidades, value objects, reglas de negocio. |   |   |   |   | X | X |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |
|   |   |   |   | Diseño del modelo entidad-relación (ER) para la base de datos SQLite. |   |   |   |   |   | X |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |
|   |   |   |   | Diseño de la arquitectura en capas (Domain / Application / Infrastructure / Presentation). |   |   |   |   |   |   | X |   |   |   |   |   |   |   |   |   |   |   |   |   |   |
|   |   |   |   | Diseño de los casos de uso principales con diagramas UML (diagrama de casos de uso, secuencia). |   |   |   |   |   |   | X | X |   |   |   |   |   |   |   |   |   |   |   |   |   |
|   |   |   |   | Diseño de wireframes y mockups de la interfaz WPF (flujo de caja, inventario, reportes). |   |   |   |   |   |   |   | X |   |   |   |   |   |   |   |   |   |   |   |   |   |
|   |   |   |   | Definición del protocolo de pruebas: pruebas unitarias, de integración y funcionales. |   |   |   |   |   |   |   |   | X |   |   |   |   |   |   |   |   |   |   |   |   |
|   |   |   |   | Entregable: Documento de diseño del sistema (SRS + diagramas + mockups). |   |   |   |   |   |   |   |   | X |   |   |   |   |   |   |   |   |   |   |   |   |
|   |   | Implementar el sistema POS completo con todas las capas de Clean Architecture, incluyendo UI WPF, lógica de negocio, persistencia y servicios de impresión. | FASE 3 — DESARROLLO | Configuración del proyecto: solución .NET 9, proyectos por capa, inyección de dependencias, EF Core + SQLite. |   |   |   |   |   |   |   |   |   | X |   |   |   |   |   |   |   |   |   |   |   |
|   |   |   |   | Implementación de la capa Domain: entidades, interfaces de repositorio, excepciones de dominio. |   |   |   |   |   |   |   |   |   | X | X |   |   |   |   |   |   |   |   |   |   |
|   |   |   |   | Implementación de la capa Application: casos de uso de ventas, inventario, usuarios y reportes. |   |   |   |   |   |   |   |   |   |   | X | X |   |   |   |   |   |   |   |   |   |
|   |   |   |   | Implementación de la capa Infrastructure: AppDbContext, migraciones EF Core, repositorios concretos. |   |   |   |   |   |   |   |   |   |   |   | X | X |   |   |   |   |   |   |   |   |
|   |   |   |   | Desarrollo del módulo de autenticación y gestión de usuarios (roles, hash de contraseñas). |   |   |   |   |   |   |   |   |   |   |   |   | X |   |   |   |   |   |   |   |   |
|   |   |   |   | Desarrollo del módulo de inventario y gestión de productos (CRUD, alertas de stock). |   |   |   |   |   |   |   |   |   |   |   |   |   | X |   |   |   |   |   |   |   |
|   |   |   |   | Desarrollo del módulo de punto de venta (caja): carrito, métodos de pago, anulaciones. |   |   |   |   |   |   |   |   |   |   |   |   |   | X | X |   |   |   |   |   |   |
|   |   |   |   | Desarrollo del módulo de clientes, proveedores y compras. |   |   |   |   |   |   |   |   |   |   |   |   |   |   | X |   |   |   |   |   |   |
|   |   |   |   | Implementación del servicio de impresión ESC/POS para recibos térmicos. |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   | X |   |   |   |   |   |
|   |   |   |   | Desarrollo de la interfaz WPF (Views + ViewModels MVVM): todas las pantallas. |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   | X | X |   |   |   |   |
|   |   |   |   | Implementación del módulo de reportes (PDF, Excel, gráficos). |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   | X |   |   |   |   |
|   |   |   |   | Entregable: Prototipo funcional completo con todas las capas integradas. |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   | X |   |   |   |   |
|   |   | Validar el funcionamiento del sistema mediante pruebas formales, corregir errores y consolidar el producto final listo para despliegue. | FASE 4 — REFINAMIENTO Y PRUEBAS | Ejecución de pruebas unitarias sobre casos de uso (capa Application) con xUnit o NUnit. |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   | X |   |   |   |
|   |   |   |   | Pruebas de integración: EF Core + SQLite, flujo completo de venta. |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   | X | X |   |   |
|   |   |   |   | Pruebas funcionales del flujo de caja con escenarios reales (pago mixto, anulación, crédito). |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   | X |   |   |
|   |   |   |   | Evaluación de rendimiento: tiempo de arranque, velocidad de búsqueda con catálogo grande. |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   | X |   |
|   |   |   |   | Corrección de errores detectados y ajuste de parámetros de configuración. |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   | X |   |
|   |   |   |   | Validación de la impresión térmica con distintos modelos de impresora. |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   | X |
|   |   |   |   | Generación del instalador y prueba de instalación en equipo limpio. |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   | X |
|   |   |   |   | Entregable: Informe de pruebas, métricas de calidad y sistema listo para producción. |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   |   | X |

---

## Documentos relacionados

[[requerimientos]] · [[modulos]] · [[00_indice_fase_diseno_pos]]
