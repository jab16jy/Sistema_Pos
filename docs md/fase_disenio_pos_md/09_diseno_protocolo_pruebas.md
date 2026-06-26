# 09 — Diseño del Protocolo de Pruebas

> **Enfoque didáctico del sistema:** todas las pantallas y flujos se diseñan para que una persona sin formación técnica pueda aprender a usar el POS en poco tiempo. El sistema debe guiar al usuario paso a paso, usar lenguaje cotidiano, mostrar ayudas visuales, prevenir errores y permitir operar las funciones frecuentes con teclado o escáner.

## Objetivo

Definir cómo se validará que el sistema POS funcione correctamente, sea rápido, confiable y fácil de aprender para usuarios sin formación técnica.

## Tipos de pruebas

```text
1. Pruebas unitarias
2. Pruebas de integración
3. Pruebas funcionales
4. Pruebas de usabilidad didáctica
5. Pruebas de rendimiento
6. Pruebas de seguridad
7. Pruebas de instalación
```

## 1. Pruebas unitarias

| Prueba | Resultado esperado |
|---|---|
| Calcular total de venta | El total coincide con productos, cantidades y descuentos. |
| Calcular cambio | El cambio es correcto cuando se paga en efectivo. |
| Validar stock suficiente | El sistema impide vender más de lo disponible. |
| Aplicar descuento | El descuento se calcula correctamente. |
| Acumular puntos | El cliente recibe los puntos esperados. |
| Calcular utilidad | Utilidad = precio venta - costo × cantidad. |

## 2. Pruebas de integración

| Escenario | Validación |
|---|---|
| Venta completa | Guarda venta, ítems, pago y descuenta inventario. |
| Compra a proveedor | Guarda compra y aumenta stock. |
| Reporte diario | Consulta ventas reales desde SQLite. |
| Factura | Permite consultar e imprimir venta registrada. |
| Backup | Copia correctamente la base de datos. |

## 3. Pruebas funcionales

| Flujo | Caso |
|---|---|
| Caja | Venta en efectivo con cambio. |
| Caja | Venta con transferencia. |
| Caja | Pago mixto. |
| Caja | Venta en espera y retomar venta. |
| Facturas | Reimpresión de factura. |
| Devoluciones | Anulación con autorización de administrador. |
| Inventario | Alerta de bajo stock. |
| Reportes | Exportación a PDF y Excel. |

## 4. Pruebas de usabilidad didáctica

Una persona nueva debe poder realizar una venta básica en poco tiempo siguiendo las instrucciones visibles del sistema.

| Prueba | Indicador |
|---|---|
| Usuario encuentra Caja | Lo logra sin ayuda externa. |
| Usuario agrega producto | Entiende el campo de búsqueda o escáner. |
| Usuario cobra en efectivo | Comprende monto recibido y cambio. |
| Usuario imprime recibo | Identifica cuándo la venta finalizó. |
| Usuario corrige error | Entiende mensajes como stock insuficiente. |

## 5. Pruebas de rendimiento

| Prueba | Meta |
|---|---|
| Abrir aplicación | Menos de 5 segundos. |
| Buscar producto en caja | Menos de 1 segundo. |
| Consultar inventario de 10.000 productos | Menos de 2 segundos. |
| Finalizar venta | Menos de 1 segundo en operación normal. |

## 6. Pruebas de seguridad

| Prueba | Resultado esperado |
|---|---|
| Contraseña guardada | Debe estar protegida con hash. |
| Usuario cajero intenta anular | El sistema solicita administrador. |
| Base de datos protegida | Debe aplicar cifrado o mecanismo equivalente. |
| Sesión registrada | Debe guardar entrada y salida del usuario. |

## Formato sugerido de registro de pruebas

```text
ID de prueba:
Módulo:
Escenario:
Pasos ejecutados:
Resultado esperado:
Resultado obtenido:
Estado: Aprobado / Fallido
Observaciones:
```

## Resultado esperado

El protocolo de pruebas permitirá validar no solo que el sistema funcione, sino que sea realmente fácil de usar y aprender por cajeros y administradores de pequeños comercios.

---

## Documentos relacionados

[[00_indice_fase_diseno_pos]] · [[04_diseno_modelo_datos_sqlite]] · [[05_diseno_flujos_funcionales]] · [[07_diseno_casos_uso]]
