#

## CONTEXTO DEL PROYECTO

**Título del proyecto:**
Desarrollo de un sistema POS local de escritorio para pequeños comercios, implementado con tecnologías .NET modernas bajo principios de arquitectura limpia.

**Stack tecnológico:**
- Lenguaje: C# (.NET 9)
- UI Framework: WPF (Windows Presentation Foundation)
- Patrón de presentación: MVVM (Model-View-ViewModel)
- Arquitectura: Clean Architecture (Domain, Application, Infrastructure, Presentation)
- ORM: Entity Framework Core 9
- Base de datos: SQLite (local, sin servidor)
- Reportes: FastReport / Crystal Reports / RDLC
- Impresión: ESC/POS para impresoras térmicas

---

## ANÁLISIS DE REQUERIMIENTOS DEL SISTEMA POS

### REQUERIMIENTOS FUNCIONALES

#### RF-01 — Módulo de Autenticación y Usuarios
- RF-01.1: El sistema debe permitir inicio de sesión con usuario y contraseña.
- RF-01.2: Debe existir al menos dos roles: Administrador y Cajero.
- RF-01.3: El administrador puede crear, editar y desactivar usuarios.
- RF-01.4: Las contraseñas deben almacenarse con hash (BCrypt o PBKDF2).
- RF-01.5: El sistema debe registrar log de sesiones (usuario, fecha, hora de entrada/salida).

#### RF-02 — Módulo de Inventario / Productos
- RF-02.1: Registrar productos con: código, nombre, descripción, categoría, precio de compra, precio de venta, stock actual, stock mínimo.
- RF-02.2: Soporte para código de barras (EAN-13, Code 128).
- RF-02.3: Clasificación de productos por categorías y subcategorías.
- RF-02.4: Gestión de unidades de medida (unidad, caja, kilogramo, litro).
- RF-02.5: Alerta automática cuando el stock cae por debajo del mínimo configurado.
- RF-02.6: Historial de movimientos de inventario (entradas, salidas, ajustes).
- RF-02.7: Importación masiva de productos desde archivo CSV o Excel.

#### RF-03 — Módulo de Punto de Venta (Caja)
- RF-03.1: Interfaz de venta con búsqueda de productos por nombre o código de barras.
- RF-03.2: Soporte para lector de código de barras USB/HID.
- RF-03.3: Carrito de compra con posibilidad de modificar cantidad, aplicar descuentos por ítem.
- RF-03.4: Descuentos globales sobre el total de la venta (porcentaje o monto fijo).
- RF-03.5: Múltiples métodos de pago: efectivo, tarjeta, transferencia, mixto.
- RF-03.6: Cálculo automático del cambio (vuelto) para pagos en efectivo.
- RF-03.7: Anulación de ventas (con autorización de administrador).
- RF-03.8: Generación e impresión del recibo/factura en impresora térmica ESC/POS.
- RF-03.9: Posibilidad de dejar ventas en espera (hold) y retomar.
- RF-03.10: Soporte para ventas a crédito con registro de deuda por cliente.

#### RF-04 — Módulo de Clientes
- RF-04.1: Registro de clientes: nombre, identificación, teléfono, correo, dirección.
- RF-04.2: Historial de compras por cliente.
- RF-04.3: Gestión de saldo a crédito y abonos de deuda.
- RF-04.4: Búsqueda y filtro de clientes por nombre o identificación.

#### RF-05 — Módulo de Proveedores y Compras
- RF-05.1: Registro de proveedores: nombre, NIT/RUT, teléfono, correo, productos que suministra.
- RF-05.2: Registro de órdenes de compra y entradas de mercancía.
- RF-05.3: Actualización automática del inventario al registrar una entrada de compra.
- RF-05.4: Historial de compras por proveedor.

#### RF-06 — Módulo de Reportes y Cierre de Caja
- RF-06.1: Reporte de ventas del día (resumen y detalle).
- RF-06.2: Reporte de ventas por rango de fechas, categoría, usuario o producto.
- RF-06.3: Cierre de caja diario con cuadre de efectivo vs. sistema.
- RF-06.4: Reporte de inventario: stock actual, productos bajo mínimo, movimientos.
- RF-06.5: Reporte de cuentas por cobrar (clientes con saldo pendiente).
- RF-06.6: Exportación de reportes a PDF y Excel.
- RF-06.7: Gráficos de ventas por día, semana y mes (integrado en la UI WPF).

#### RF-07 — Módulo de Configuración del Sistema
- RF-07.1: Configuración de datos del negocio (nombre, NIT, dirección, logo para recibos).
- RF-07.2: Configuración de impresora (puerto, modelo, ancho del papel).
- RF-07.3: Configuración de porcentaje de impuesto (IVA u otro).
- RF-07.4: Copia de seguridad manual de la base de datos SQLite.
- RF-07.5: Restauración de copia de seguridad.
- RF-07.6: Configuración de moneda y formato regional.

---

### REQUERIMIENTOS NO FUNCIONALES

#### RNF-01 — Rendimiento
- El sistema debe responder a cualquier operación de caja en menos de 1 segundo.
- Las consultas de inventario con hasta 10,000 productos deben completarse en menos de 2 segundos.
- El arranque de la aplicación no debe superar los 5 segundos en hardware estándar.

#### RNF-02 — Usabilidad
- La interfaz debe ser intuitiva para usuarios sin formación técnica.
- Debe ser operable completamente con teclado (atajos de teclado para acciones frecuentes).
- Resolución mínima soportada: 1280 x 720 px.
- El diseño visual debe seguir los principios de Fluent Design (WPF moderno).

#### RNF-03 — Disponibilidad y Confiabilidad
- El sistema opera 100% offline, sin dependencia de internet.
- Ante un fallo inesperado, los datos de la venta en curso no deben perderse (mecanismo de recuperación).
- Las transacciones de venta deben ser atómicas (EF Core con DbTransaction).

#### RNF-04 — Seguridad
- Los datos de usuarios deben almacenarse con hash + salt.
- Las acciones críticas (anulación, descuentos altos, eliminación) deben requerir confirmación de contraseña de administrador.
- La base de datos SQLite debe estar cifrada con SQLCipher o similar.

#### RNF-05 — Mantenibilidad (Clean Architecture)
- El código debe estar separado en capas: Domain, Application, Infrastructure, Presentation.
- Toda la lógica de negocio debe residir en la capa Domain y Application, sin dependencia de WPF.
- El sistema debe incluir pruebas unitarias para los casos de uso (Use Cases) de la capa Application.
- Se debe utilizar inyección de dependencias (Microsoft.Extensions.DependencyInjection).

#### RNF-06 — Portabilidad
- El instalador debe ser compatible con Windows 10 y Windows 11 (x64).
- La distribución debe hacerse mediante un instalador NSIS o MSI o publicación ClickOnce.

---

## ESTRUCTURA DE CAPAS — CLEAN ARCHITECTURE

```
POS.sln
│
├── src/
│   ├── POS.Domain/                  ← Entidades, Value Objects, Interfaces de repos
│   │   ├── Entities/
│   │   │   ├── Product.cs
│   │   │   ├── Sale.cs
│   │   │   ├── SaleItem.cs
│   │   │   ├── Customer.cs
│   │   │   ├── Supplier.cs
│   │   │   ├── User.cs
│   │   │   └── InventoryMovement.cs
│   │   ├── Enums/
│   │   ├── Interfaces/
│   │   │   ├── IProductRepository.cs
│   │   │   ├── ISaleRepository.cs
│   │   │   └── IUnitOfWork.cs
│   │   └── Exceptions/
│   │
│   ├── POS.Application/             ← Casos de uso, DTOs, Interfaces de servicios
│   │   ├── UseCases/
│   │   │   ├── Sales/
│   │   │   │   ├── CreateSaleUseCase.cs
│   │   │   │   └── CancelSaleUseCase.cs
│   │   │   ├── Products/
│   │   │   │   ├── CreateProductUseCase.cs
│   │   │   │   └── UpdateStockUseCase.cs
│   │   │   └── Reports/
│   │   │       └── GenerateDailyReportUseCase.cs
│   │   ├── DTOs/
│   │   └── Interfaces/
│   │       ├── IPrintService.cs
│   │       └── IReportService.cs
│   │
│   ├── POS.Infrastructure/          ← EF Core, SQLite, Repositorios, Servicios externos
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Migrations/
│   │   │   └── Repositories/
│   │   ├── Services/
│   │   │   ├── PrintService.cs      ← ESC/POS
│   │   │   └── ReportService.cs     ← PDF/Excel
│   │   └── DependencyInjection.cs
│   │
│   └── POS.Presentation/            ← WPF, ViewModels, Views, Commands
│       ├── Views/
│       │   ├── LoginView.xaml
│       │   ├── MainShellView.xaml
│       │   ├── SaleView.xaml
│       │   ├── InventoryView.xaml
│       │   └── ReportsView.xaml
│       ├── ViewModels/
│       │   ├── LoginViewModel.cs
│       │   ├── SaleViewModel.cs
│       │   ├── InventoryViewModel.cs
│       │   └── ReportsViewModel.cs
│       ├── Commands/
│       └── App.xaml.cs
│
└── tests/
    ├── POS.Domain.Tests/
    └── POS.Application.Tests/
```

---

## FASES, OBJETIVOS ESPECÍFICOS Y ACTIVIDADES DEL PROYECTO

### FASE 1 — EXPLORACIÓN (Semanas 1–4)
**Objetivo específico:** Analizar las necesidades funcionales, tecnológicas y de contexto para fundamentar el diseño del sistema POS.

**Actividades:**
1. Revisión de antecedentes: sistemas POS existentes (open source y comerciales), análisis comparativo de funcionalidades.
2. Levantamiento inicial de requerimientos con usuarios objetivo (tenderos, pequeños comerciantes).
3. Análisis de restricciones tecnológicas: compatibilidad WPF + .NET 9, limitaciones de hardware en pequeños comercios.
4. Evaluación de SQLite como motor de base de datos local vs. alternativas (LiteDB, SQL Server LocalDB).
5. Investigación de librerías para impresión térmica ESC/POS en .NET.
6. Definición formal del stack tecnológico y justificación de cada elección.
7. **Entregable:** Informe de exploración tecnológica y contextual.

---

### FASE 2 — DISEÑO (Semanas 5–9)
**Objetivo específico:** Diseñar la arquitectura, modelo de datos y flujos funcionales del sistema POS bajo Clean Architecture y MVVM.

**Actividades:**
1. Especificación formal de requerimientos funcionales y no funcionales (documento SRS).
2. Diseño del modelo de dominio: entidades, value objects, reglas de negocio.
3. Diseño del modelo entidad-relación (ER) para la base de datos SQLite.
4. Diseño de la arquitectura en capas (Domain / Application / Infrastructure / Presentation).
5. Diseño de los casos de uso principales con diagramas UML (diagrama de casos de uso, secuencia).
6. Diseño de wireframes y mockups de la interfaz WPF (flujo de caja, inventario, reportes).
7. Definición del protocolo de pruebas: pruebas unitarias, de integración y funcionales.
8. **Entregable:** Documento de diseño del sistema (SRS + diagramas + mockups).

---

### FASE 3 — DESARROLLO (Semanas 10–17)
**Objetivo específico:** Implementar el sistema POS completo con todas las capas de Clean Architecture, incluyendo UI WPF, lógica de negocio, persistencia y servicios de impresión.

**Actividades:**
1. Configuración del proyecto: solución .NET 9, proyectos por capa, inyección de dependencias, EF Core + SQLite.
2. Implementación de la capa Domain: entidades, interfaces de repositorio, excepciones de dominio.
3. Implementación de la capa Application: casos de uso de ventas, inventario, usuarios y reportes.
4. Implementación de la capa Infrastructure: AppDbContext, migraciones EF Core, repositorios concretos.
5. Desarrollo del módulo de autenticación y gestión de usuarios (roles, hash de contraseñas).
6. Desarrollo del módulo de inventario y gestión de productos (CRUD, alertas de stock).
7. Desarrollo del módulo de punto de venta (caja): carrito, métodos de pago, anulaciones.
8. Desarrollo del módulo de clientes, proveedores y compras.
9. Implementación del servicio de impresión ESC/POS para recibos térmicos.
10. Desarrollo de la interfaz WPF (Views + ViewModels MVVM): todas las pantallas.
11. Implementación del módulo de reportes (PDF, Excel, gráficos).
12. **Entregable:** Prototipo funcional completo con todas las capas integradas.

---

### FASE 4 — REFINAMIENTO Y PRUEBAS (Semanas 18–21)
**Objetivo específico:** Validar el funcionamiento del sistema mediante pruebas formales, corregir errores y consolidar el producto final listo para despliegue.

**Actividades:**
1. Ejecución de pruebas unitarias sobre casos de uso (capa Application) con xUnit o NUnit.
2. Pruebas de integración: EF Core + SQLite, flujo completo de venta.
3. Pruebas funcionales del flujo de caja con escenarios reales (pago mixto, anulación, crédito).
4. Evaluación de rendimiento: tiempo de arranque, velocidad de búsqueda con catálogo grande.
5. Corrección de errores detectados y ajuste de parámetros de configuración.
6. Validación de la impresión térmica con distintos modelos de impresora.
7. Generación del instalador y prueba de instalación en equipo limpio.
8. **Entregable:** Informe de pruebas, métricas de calidad y sistema listo para producción.

---

