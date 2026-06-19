**SISTEMA POS LOCAL**

Especificación de Módulos

*Documento de requerimientos funcionales orientado al cliente*

Versión 1.0 · 2025

# **Introducción**

Este documento presenta la especificación funcional de los módulos que conforman el Sistema POS Local, una solución de punto de venta diseñada para pequeños y medianos comercios. Su objetivo es brindar al cliente una descripción clara y comprensible de cada módulo, sus funcionalidades y el valor que aporta al negocio.

El sistema está concebido para operar de manera local, sin dependencia de internet, garantizando disponibilidad continua durante la operación del negocio. Cada módulo se diseña con foco en la facilidad de uso, permitiendo que cualquier persona del equipo pueda operar el sistema con una curva de aprendizaje mínima.

# **Resumen de módulos**

El sistema se compone de 11 módulos funcionales, cada uno responsable de una área específica de la operación del negocio:

| **#** | **Módulo** | **Propósito principal** |
| --- | --- | --- |
| **01** | **Módulo de Caja** | Cobro de productos con múltiples métodos de pago y cálculo de cambio. |
| **02** | **Módulo de Productos** | Registro y gestión del catálogo de productos con existencias y categorías. |
| **03** | **Módulo de Inventario** | Consulta del valor y cantidades del inventario filtrado por categoría. |
| **04** | **Módulo de Clientes** | Directorio de clientes con sistema de puntos de fidelización. |
| **05** | **Módulo de Puntos** | Configuración de reglas, niveles y beneficios del programa de puntos. |
| **06** | **Módulo de Proveedores** | Directorio de proveedores con información comercial completa. |
| **07** | **Módulo de Pagos (Compras)** | Registro de compras a proveedores y control del gasto diario en mercancía. |
| **08** | **Módulo de Informe de Ventas** | Análisis de ventas por fecha y tipo de pago con cuadre de caja. |
| **09** | **Módulo de Facturas** | Historial de facturas con opción de reimpresión y gestión de devoluciones. |
| **10** | **Módulo de Utilidades** | Cálculo de utilidad bruta por producto y por período de tiempo. |
| **11** | **Módulo de Código de Barras** | Generación e impresión de etiquetas con código de barras para productos. |

# **Descripción detallada de módulos**

## **01. Módulo de Caja**

Interfaz principal de cobro para el cajero. Gestiona el proceso completo de una venta, desde la selección de productos hasta el cierre del pago, incluyendo el cálculo del cambio y la selección del medio de pago.

|  |  |
| --- | --- |
| **01** | **Módulo de Caja** |
| *Interfaz principal de cobro para el cajero. Gestiona el proceso completo de una venta, desde la selección de productos hasta el cierre del pago, incluyendo el cálculo del cambio y la selección del medio de pago.* | |
| **Funcionalidades del módulo** | |
| **●** Búsqueda de productos por nombre mediante campo de texto con sugerencias en tiempo real. | |
| **●** Lectura de productos mediante escáner de código de barras conectado por USB. | |
| **●** Selección de cantidad de unidades por producto antes o después de agregarlo al carrito. | |
| **●** Visualización del carrito con nombre del producto, precio unitario, cantidad y subtotal. | |
| **●** Cálculo automático del total de la venta sumando todos los productos del carrito. | |
| **●** Campo para ingresar el valor del billete o monto recibido, con cálculo instantáneo del cambio (vuelto) a devolver. | |
| **●** Selección del tipo de pago: efectivo, tarjeta débito, tarjeta crédito o transferencia electrónica. | |
| **●** En pagos por transferencia: selección del banco/plataforma (Nequi, Bancolombia, Daviplata). | |
| **●** Posibilidad de asociar la venta a un cliente registrado para acumulación de puntos. | |
| **●** Generación e impresión del recibo de venta al finalizar el cobro. | |
| **Nota:** El sistema identifica automáticamente el tipo de pago para el cuadre de caja: el efectivo se contabiliza como dinero físico en caja, mientras que los pagos digitales se registran como ingresos pero no incrementan el efectivo en caja. | |

## **02. Módulo de Productos**

Administración del catálogo de productos del negocio. Permite registrar, editar y organizar los productos con toda la información comercial necesaria para la operación del punto de venta.

|  |  |
| --- | --- |
| **02** | **Módulo de Productos** |
| *Administración del catálogo de productos del negocio. Permite registrar, editar y organizar los productos con toda la información comercial necesaria para la operación del punto de venta.* | |
| **Funcionalidades del módulo** | |
| **●** Creación de productos con los campos: nombre, código de barras (opcional), precio de costo, precio de venta, cantidad en existencia y categoría. | |
| **●** Edición y eliminación de productos existentes. | |
| **●** Asociación de cada producto a una categoría para su organización y filtrado. | |
| **●** Soporte para lectura directa del código de barras con escáner al momento de registrar el producto. | |
| **●** Visualización del listado completo de productos con filtro por nombre y categoría. | |
| **●** Control de existencias: el sistema descuenta automáticamente del inventario cada vez que se realiza una venta. | |
| **●** Configuración de stock mínimo para generación de alertas cuando el producto esté por agotarse. | |

## **03. Módulo de Inventario**

Vista consolidada del estado del inventario. Ofrece al administrador una visión global del capital invertido en productos y las cantidades disponibles, con opciones de filtrado para un análisis detallado.

|  |  |
| --- | --- |
| **03** | **Módulo de Inventario** |
| *Vista consolidada del estado del inventario. Ofrece al administrador una visión global del capital invertido en productos y las cantidades disponibles, con opciones de filtrado para un análisis detallado.* | |
| **Funcionalidades del módulo** | |
| **●** Visualización del valor total del inventario en costo (precio de costo × existencias de todos los productos). | |
| **●** Resumen de cantidades generales: total de productos registrados, total de unidades en existencia. | |
| **●** Filtrado del inventario por categoría para analizar segmentos específicos del catálogo. | |
| **●** Búsqueda y consulta de la existencia actual de un producto específico. | |
| **●** Indicador visual de productos con stock bajo el mínimo configurado. | |
| **●** Vista de los últimos movimientos de inventario (entradas por compra, salidas por venta, ajustes manuales). | |

## **04. Módulo de Clientes**

Gestión del directorio de clientes del negocio. Permite registrar su información de contacto e integrar el sistema de puntos de fidelización para recompensar la lealtad de compra.

|  |  |
| --- | --- |
| **04** | **Módulo de Clientes** |
| *Gestión del directorio de clientes del negocio. Permite registrar su información de contacto e integrar el sistema de puntos de fidelización para recompensar la lealtad de compra.* | |
| **Funcionalidades del módulo** | |
| **●** Creación de clientes con los campos: nombre completo, número de cédula (CC), correo electrónico y datos adicionales de contacto. | |
| **●** Edición y consulta del perfil de cada cliente. | |
| **●** Visualización del saldo de puntos acumulados por el cliente en tiempo real. | |
| **●** Historial de compras asociadas al cliente. | |
| **●** Búsqueda de clientes por nombre o número de cédula. | |
| **●** Redención de puntos al momento del pago en caja (descuento sobre el total de la venta). | |
| **Nota:** La acumulación de puntos se configura en el Módulo de Puntos. El cajero puede buscar al cliente por cédula al inicio de la venta para asociar la compra y sumar puntos automáticamente. | |

## **05. Módulo de Puntos**

Configuración del programa de fidelización. Permite al administrador definir las reglas de acumulación y redención de puntos, así como establecer niveles de membresía con beneficios diferenciados.

|  |  |
| --- | --- |
| **05** | **Módulo de Puntos** |
| *Configuración del programa de fidelización. Permite al administrador definir las reglas de acumulación y redención de puntos, así como establecer niveles de membresía con beneficios diferenciados.* | |
| **Funcionalidades del módulo** | |
| **●** Configuración de la tasa de acumulación de puntos (ejemplo: 1 punto por cada $1.000 en compras). | |
| **●** Definición de niveles o categorías de membresía (ejemplo: Básico, Plata, Oro, Platino) con sus umbrales de puntos. | |
| **●** Asignación de beneficios por nivel: porcentaje de descuento, multiplicador de puntos u otros privilegios. | |
| **●** Configuración del valor de redención: cuántos puntos equivalen a un descuento en pesos. | |
| **●** Activación o desactivación del programa de puntos de forma general. | |
| **●** Reglas de vencimiento de puntos (opcional): definir si los puntos expiran tras un período de inactividad. | |

## **06. Módulo de Proveedores**

Directorio de proveedores del negocio. Centraliza la información de cada proveedor para facilitar la gestión de compras y el seguimiento de relaciones comerciales.

|  |  |
| --- | --- |
| **06** | **Módulo de Proveedores** |
| *Directorio de proveedores del negocio. Centraliza la información de cada proveedor para facilitar la gestión de compras y el seguimiento de relaciones comerciales.* | |
| **Funcionalidades del módulo** | |
| **●** Registro de proveedores con los campos: razón social, NIT, dirección, correo electrónico y teléfono. | |
| **●** Edición y consulta de la ficha de cada proveedor. | |
| **●** Listado de compras realizadas a cada proveedor. | |
| **●** Búsqueda de proveedores por nombre o NIT. | |
| **●** Vinculación de los productos que suministra cada proveedor. | |

## **07. Módulo de Pagos (Compras)**

Registro de las compras realizadas a proveedores. Permite controlar el gasto diario en adquisición de mercancía, asociar cada compra a su proveedor y actualizar el inventario automáticamente.

|  |  |
| --- | --- |
| **07** | **Módulo de Pagos (Compras)** |
| *Registro de las compras realizadas a proveedores. Permite controlar el gasto diario en adquisición de mercancía, asociar cada compra a su proveedor y actualizar el inventario automáticamente.* | |
| **Funcionalidades del módulo** | |
| **●** Registro de una nueva compra seleccionando el proveedor desde el directorio de proveedores. | |
| **●** Ingreso de los productos adquiridos con cantidad comprada y precio total pagado por cada ítem. | |
| **●** Cálculo del total de la factura de compra. | |
| **●** Actualización automática del inventario al registrar una entrada de mercancía. | |
| **●** Resumen de cuánto se ha pagado en compras en el día actual. | |
| **●** Historial de compras por proveedor y por rango de fechas. | |
| **●** Posibilidad de ingresar el número de factura del proveedor como referencia. | |

## **08. Módulo de Informe de Ventas**

Panel de análisis de ventas para la toma de decisiones. Consolida la información de ingresos con filtros flexibles por fecha y tipo de pago, diferenciando claramente el efectivo disponible en caja del total facturado.

|  |  |
| --- | --- |
| **08** | **Módulo de Informe de Ventas** |
| *Panel de análisis de ventas para la toma de decisiones. Consolida la información de ingresos con filtros flexibles por fecha y tipo de pago, diferenciando claramente el efectivo disponible en caja del total facturado.* | |
| **Funcionalidades del módulo** | |
| **●** Resumen de ventas del día actual: número de transacciones, total facturado y total en caja. | |
| **●** Filtro por rango de fechas mediante selector de calendario para análisis de períodos específicos. | |
| **●** Separación del total de ventas por tipo de pago: efectivo (contado como dinero en caja) vs. pagos digitales (tarjeta, transferencia — contabilizados como ingreso pero no como efectivo físico). | |
| **●** Vista detallada de los montos recibidos en cada medio de pago: efectivo, débito, crédito, Nequi, Bancolombia, Daviplata. | |
| **●** Listado de productos vendidos en el período con la cantidad de unidades y el total generado por cada uno. | |
| **●** Identificación de los productos más vendidos en el período seleccionado. | |
| **●** Resumen del cuadre de caja: efectivo esperado en caja según el sistema vs. efectivo declarado. | |

## **09. Módulo de Facturas**

Registro histórico de todas las ventas realizadas. Permite consultar, reimprimir y gestionar devoluciones sobre cualquier factura generada por el sistema.

|  |  |
| --- | --- |
| **09** | **Módulo de Facturas** |
| *Registro histórico de todas las ventas realizadas. Permite consultar, reimprimir y gestionar devoluciones sobre cualquier factura generada por el sistema.* | |
| **Funcionalidades del módulo** | |
| **●** Listado completo de todas las facturas generadas, con número, fecha, cliente (si aplica), total y método de pago. | |
| **●** Búsqueda de facturas por fecha mediante selector de calendario. | |
| **●** Búsqueda de facturas por número de factura o por nombre del cliente. | |
| **●** Visualización del detalle completo de cualquier factura: productos, cantidades, precios y total. | |
| **●** Reimpresión de cualquier factura seleccionada en impresora térmica o en papel normal. | |
| **●** Gestión de devoluciones: permite anular total o parcialmente una venta y revertir el movimiento de inventario correspondiente. | |
| **●** Registro del motivo de la devolución para trazabilidad. | |
| **Nota:** Las devoluciones requieren autorización del usuario administrador para garantizar el control del proceso. | |

## **10. Módulo de Utilidades**

Análisis de rentabilidad del negocio. Calcula la utilidad generada por las ventas comparando el precio de venta con el precio de costo, con filtros por día o por rango de fechas.

|  |  |
| --- | --- |
| **10** | **Módulo de Utilidades** |
| *Análisis de rentabilidad del negocio. Calcula la utilidad generada por las ventas comparando el precio de venta con el precio de costo, con filtros por día o por rango de fechas.* | |
| **Funcionalidades del módulo** | |
| **●** Visualización de la utilidad bruta total generada en el día actual. | |
| **●** Filtro por día específico o por rango de fechas mediante calendario. | |
| **●** Desglose de utilidad por producto: precio de costo, precio de venta, unidades vendidas, utilidad individual y porcentaje de margen. | |
| **●** Utilidad total del período seleccionado: suma de las utilidades de todos los productos vendidos. | |
| **●** Identificación de los productos que generan mayor utilidad en el período. | |
| **●** Comparativo de utilidad entre períodos (semana actual vs. semana anterior, mes actual vs. mes anterior). | |
| **Nota:** La utilidad se calcula como: (Precio de venta − Precio de costo) × Unidades vendidas. El sistema usa los precios de costo registrados en el catálogo de productos. | |

## **11. Módulo de Código de Barras**

Herramienta de configuración y generación de códigos de barras para los productos del catálogo. Útil cuando los productos no tienen código de barras propio o se desea estandarizar el etiquetado interno.

|  |  |
| --- | --- |
| **11** | **Módulo de Código de Barras** |
| *Herramienta de configuración y generación de códigos de barras para los productos del catálogo. Útil cuando los productos no tienen código de barras propio o se desea estandarizar el etiquetado interno.* | |
| **Funcionalidades del módulo** | |
| **●** Generación de códigos de barras para productos que no cuentan con uno asignado por el fabricante. | |
| **●** Configuración del formato del código de barras (EAN-13, Code 128, QR, entre otros). | |
| **●** Vista previa del código de barras generado antes de imprimirlo. | |
| **●** Impresión de etiquetas con el código de barras y el nombre del producto. | |
| **●** Configuración del diseño de la etiqueta: tamaño, incluir precio, incluir nombre. | |
| **●** Impresión masiva de etiquetas para múltiples productos seleccionados. | |
| **Nota:** Este módulo es especialmente útil para productos a granel o de producción propia que no tienen código de barras de fábrica. El código generado es compatible con el escáner del Módulo de Caja. | |
