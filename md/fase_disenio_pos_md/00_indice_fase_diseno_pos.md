# Índice — Fase de Diseño del Sistema POS Local

> **Enfoque didáctico del sistema:** todas las pantallas y flujos se diseñan para que una persona sin formación técnica pueda aprender a usar el POS en poco tiempo. El sistema debe guiar al usuario paso a paso, usar lenguaje cotidiano, mostrar ayudas visuales, prevenir errores y permitir operar las funciones frecuentes con teclado o escáner.

## Propósito

Este paquete contiene los documentos Markdown de la **Fase 2 — Diseño** del Sistema POS Local. Cada archivo explica una parte del diseño preliminar del programa: arquitectura, modelo de datos, flujos funcionales, pantallas, casos de uso, integración de módulos, pruebas y entregables.

## Documentos incluidos

| Archivo | Contenido |
|---|---|
| `01_diseno_integracion_tecnologica.md` | Explica cómo se integran WPF, MVVM, Clean Architecture, EF Core, SQLite, reportes e impresión ESC/POS. |
| `02_diseno_arquitectura_capas.md` | Define la separación en capas: Domain, Application, Infrastructure y Presentation. |
| `03_diseno_modelo_dominio.md` | Presenta entidades, value objects y reglas principales del negocio. |
| `04_diseno_modelo_datos_sqlite.md` | Propone el modelo entidad-relación preliminar para SQLite. |
| `05_diseno_flujos_funcionales.md` | Describe los flujos principales: venta, compra, cierre, devolución y respaldo. |
| `06_diseno_wireframes_interfaz.md` | Incluye wireframes textuales de las pantallas principales con enfoque didáctico. |
| `07_diseno_casos_uso.md` | Define casos de uso principales y actores del sistema. |
| `08_diseno_integracion_modulos.md` | Explica cómo se conectan los 11 módulos funcionales del POS. |
| `09_diseno_protocolo_pruebas.md` | Define pruebas unitarias, integración, funcionales, usabilidad y rendimiento. |
| `10_diseno_entregable_final.md` | Resume el documento de diseño final que se debe entregar en la fase. |

## Principios didácticos transversales

1. **Aprendizaje rápido:** cada módulo debe poder entenderse por observación, con textos claros y botones evidentes.
2. **Lenguaje del negocio:** usar palabras como “Vender”, “Cobrar”, “Cambio”, “Producto”, “Factura”, “Cliente”, “Devolución”, en lugar de términos técnicos.
3. **Flujo guiado:** el sistema debe mostrar qué paso sigue, especialmente en caja, compras, cierre y devoluciones.
4. **Prevención de errores:** antes de anular, eliminar, descontar o cerrar caja, el sistema debe pedir confirmación clara.
5. **Retroalimentación inmediata:** cada acción debe responder visualmente: producto agregado, stock bajo, venta guardada, recibo impreso.
6. **Operación con teclado:** las tareas frecuentes deben tener atajos visibles para acelerar el aprendizaje.
7. **Ayuda contextual:** cada pantalla debe incluir textos cortos de apoyo, tooltips o mensajes como “Escanea o escribe el producto”.

## Resultado esperado de la fase

Al finalizar esta fase, el equipo debe tener una guía clara para iniciar el desarrollo: qué se va a construir, cómo se conectarán las tecnologías, cómo se guardarán los datos, qué pantallas existirán, cómo se moverá el usuario dentro del sistema y cómo se probará que el POS sea fácil, rápido y confiable.
