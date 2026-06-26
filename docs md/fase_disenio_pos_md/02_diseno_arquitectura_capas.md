# 02 — Diseño de Arquitectura en Capas

> **Enfoque didáctico del sistema:** todas las pantallas y flujos se diseñan para que una persona sin formación técnica pueda aprender a usar el POS en poco tiempo. El sistema debe guiar al usuario paso a paso, usar lenguaje cotidiano, mostrar ayudas visuales, prevenir errores y permitir operar las funciones frecuentes con teclado o escáner.

## Objetivo

Definir la estructura interna del sistema POS usando **Clean Architecture**, separando responsabilidades para que el programa sea fácil de mantener, probar y ampliar.

## Capas del sistema

```text
POS.Domain
POS.Application
POS.Infrastructure
POS.Presentation
POS.Tests
```

## 1. Capa Domain

La capa Domain representa el corazón del negocio. Aquí viven las entidades principales y las reglas más importantes.

```text
Entities/
  Product.cs
  Sale.cs
  SaleItem.cs
  Customer.cs
  Supplier.cs
  User.cs
  InventoryMovement.cs
  CashClosing.cs
  Purchase.cs
  Payment.cs

ValueObjects/
  Money.cs
  Barcode.cs
  Quantity.cs
  Email.cs

Enums/
  UserRole.cs
  PaymentMethod.cs
  SaleStatus.cs
  InventoryMovementType.cs

Exceptions/
  InsufficientStockException.cs
  UnauthorizedActionException.cs
```

### Reglas que deben vivir aquí

- No vender productos sin stock suficiente.
- No finalizar una venta vacía.
- No aceptar pagos menores al total.
- No permitir descuentos superiores al límite sin autorización.
- Registrar todo movimiento de inventario.
- Mantener trazabilidad de anulaciones y devoluciones.

## 2. Capa Application

La capa Application coordina los casos de uso. Responde a acciones como vender, crear producto, cerrar caja o generar reporte.

```text
Sales/
  CreateSaleUseCase.cs
  CancelSaleUseCase.cs
  HoldSaleUseCase.cs
  ResumeSaleUseCase.cs

Products/
  CreateProductUseCase.cs
  UpdateProductUseCase.cs
  SearchProductUseCase.cs

Inventory/
  AdjustStockUseCase.cs
  GetLowStockProductsUseCase.cs

Reports/
  GenerateDailySalesReportUseCase.cs
  GenerateInventoryReportUseCase.cs

Cash/
  CloseCashRegisterUseCase.cs
```

### Responsabilidades

- Recibir solicitudes desde los ViewModels.
- Validar reglas de aplicación.
- Coordinar repositorios.
- Iniciar transacciones cuando sea necesario.
- Devolver respuestas claras a la interfaz.

## 3. Capa Infrastructure

Esta capa implementa tecnologías concretas.

```text
Persistence/
  AppDbContext.cs
  Migrations/
  Repositories/

Services/
  PrintService.cs
  ReportService.cs
  BackupService.cs
  BarcodeService.cs
  PasswordHashService.cs

DependencyInjection.cs
```

## 4. Capa Presentation

Esta capa contiene la interfaz WPF y el patrón MVVM.

```text
Views/
  LoginView.xaml
  MainShellView.xaml
  SaleView.xaml
  ProductsView.xaml
  InventoryView.xaml
  ReportsView.xaml

ViewModels/
  LoginViewModel.cs
  SaleViewModel.cs
  ProductsViewModel.cs
  InventoryViewModel.cs
  ReportsViewModel.cs

Commands/
  RelayCommand.cs
```

## Enfoque didáctico por capa

| Capa | Aporte a la facilidad de uso |
|---|---|
| Presentation | Pantallas claras, botones grandes, textos de ayuda y navegación simple. |
| Application | Flujos ordenados que guían al usuario paso a paso. |
| Domain | Reglas que previenen errores y protegen la información. |
| Infrastructure | Guardado confiable, impresión automática y reportes fáciles de obtener. |

## Comunicación entre capas

```text
Presentation → Application → Domain
Application → Interfaces
Infrastructure → Implementa interfaces
Presentation nunca depende directamente de Infrastructure
Domain no depende de ninguna otra capa
```

## Resultado esperado

El diseño por capas permite iniciar el desarrollo sin mezclar responsabilidades. También facilita que el sistema sea didáctico, porque la interfaz puede enfocarse en guiar al usuario, mientras las reglas y validaciones quedan protegidas en capas internas.

---

## Documentos relacionados

[[00_indice_fase_diseno_pos]] · [[01_diseno_integracion_tecnologica]] · [[03_diseno_modelo_dominio]] · [[04_diseno_modelo_datos_sqlite]] · [[12_diseno_comunicacion_digital]]
