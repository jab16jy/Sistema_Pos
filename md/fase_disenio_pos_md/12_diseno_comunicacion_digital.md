# 12 — Diseño del Módulo de Comunicación Digital

> **Enfoque didáctico del sistema:** todas las pantallas y flujos se diseñan para que una persona sin formación técnica pueda aprender a usar el POS en poco tiempo. El sistema debe guiar al usuario paso a paso, usar lenguaje cotidiano, mostrar ayudas visuales, prevenir errores y permitir operar las funciones frecuentes con teclado o escáner.

## Propósito del módulo

El módulo de Comunicación Digital permite enviar facturas y cotizaciones al cliente por correo electrónico o por WhatsApp directamente desde el POS. Su objetivo es reducir el tiempo y los pasos necesarios para que el cliente reciba su documento, sin que el operador deba salir del sistema.

El módulo opera de forma completamente local para el envío por correo (SMTP nativo), y delega el envío por WhatsApp a un script externo configurable. No existe integración directa con la API de Meta; esta decisión es intencional y se explica en la sección correspondiente.

---

## Arquitectura de comunicación

El módulo sigue el patrón de puertos y adaptadores (Hexagonal Architecture). La capa de aplicación define interfaces (puertos) que la capa de infraestructura implementa. Esto permite intercambiar el proveedor de envío sin modificar la lógica de negocio.

```text
Application Layer (puertos)
├── IPdfService          → Genera el PDF del documento
├── IEmailService        → Envía el PDF por correo electrónico
└── IWhatsAppService     → Entrega el PDF al script de WhatsApp

Infrastructure Layer (adaptadores)
├── QuestPdfService      → Implementa IPdfService con QuestPDF (Community License)
├── SmtpEmailService     → Implementa IEmailService con System.Net.Mail (SMTP nativo)
└── ScriptWhatsAppService → Implementa IWhatsAppService con Process.Start
```

### Interfaces de puerto

```csharp
// Application/Ports/IPdfService.cs
public interface IPdfService
{
    Task<byte[]> GenerateInvoicePdfAsync(Guid invoiceId);
    Task<byte[]> GenerateQuotePdfAsync(Guid quoteId);
}

// Application/Ports/IEmailService.cs
public interface IEmailService
{
    Task<bool> SendDocumentAsync(string recipientEmail, string subject, string body, byte[] pdfAttachment, string attachmentName);
}

// Application/Ports/IWhatsAppService.cs
public interface IWhatsAppService
{
    Task<bool> SendDocumentAsync(string recipientPhone, string pdfTempPath, string message);
}
```

---

## Canal 1 — Correo electrónico (SMTP)

### Tecnología

`System.Net.Mail` (incluido en .NET 9, sin dependencias externas).

### Configuración

Los parámetros SMTP se almacenan en la tabla `SYSTEM_CONFIGURATION` con las siguientes claves:

| Clave | Descripción | Ejemplo |
|---|---|---|
| `smtp_host` | Servidor SMTP | `smtp.gmail.com` |
| `smtp_port` | Puerto del servidor | `587` |
| `smtp_user` | Correo de envío | `negocio@gmail.com` |
| `smtp_password` | Contraseña o contraseña de aplicación | `****` |
| `smtp_use_ssl` | Activar TLS/SSL | `true` |
| `smtp_sender_name` | Nombre visible del remitente | `Tienda El Sol` |

### Implementación (adaptador)

```csharp
// Infrastructure/Communication/SmtpEmailService.cs
public sealed class SmtpEmailService : IEmailService
{
    private readonly ISystemConfigurationRepository _config;

    public async Task<bool> SendDocumentAsync(
        string recipientEmail,
        string subject,
        string body,
        byte[] pdfAttachment,
        string attachmentName)
    {
        // 1. Lee configuración SMTP desde SystemConfiguration
        // 2. Crea SmtpClient con host, port y credenciales
        // 3. Construye MailMessage con PDF adjunto
        // 4. Envía el mensaje
        // 5. Retorna true si el envío fue exitoso; false + captura excepción si falló
    }
}
```

### Plantillas de mensaje

Los asuntos y cuerpos de correo predeterminados se almacenan en `SYSTEM_CONFIGURATION`:

| Clave | Valor por defecto |
|---|---|
| `email_invoice_subject` | `Tu factura de {NegocioNombre}` |
| `email_invoice_body` | `Hola {ClienteNombre}, adjuntamos tu factura {NumeroFactura}.` |
| `email_quote_subject` | `Cotización {NumeroCotizacion} de {NegocioNombre}` |
| `email_quote_body` | `Hola {ClienteNombre}, adjuntamos la cotización solicitada. Vigente hasta {FechaVigencia}.` |

---

## Canal 2 — WhatsApp (script externo)

### Decisión de diseño: sin integración con Meta API

La integración directa con la API oficial de WhatsApp Business (Meta) no se implementa en esta versión por las siguientes razones:

- Requiere aprobación de Meta, número de teléfono dedicado y cuenta de empresa verificada.
- Implica costos por mensaje y tiempos de revisión imprevisibles.
- Restringe el formato y el contenido de los mensajes mediante plantillas pre-aprobadas.
- Añade una dependencia de internet que contradice el principio de operación local del POS.

En su lugar, el sistema delega el envío a un **script externo intercambiable**. El operador configura la ruta del script; el POS solo lo ejecuta y registra el resultado.

### Configuración

| Clave en SystemConfiguration | Descripción | Ejemplo |
|---|---|---|
| `whatsapp_script_path` | Ruta absoluta al script de envío | `/opt/pos/scripts/send-whatsapp.sh` |
| `whatsapp_message_invoice` | Plantilla de mensaje para facturas | `Hola {nombre}, te enviamos tu factura.` |
| `whatsapp_message_quote` | Plantilla de mensaje para cotizaciones | `Hola {nombre}, aquí está la cotización que pediste.` |

### Template de script de referencia

El archivo `scripts/send-whatsapp.sh` provee una estructura de referencia para el operador:

```bash
#!/bin/bash
# send-whatsapp.sh — Script puente de WhatsApp
# Argumentos:
#   $1 → Número de teléfono destino (formato internacional: 573001234567)
#   $2 → Ruta absoluta al archivo PDF a enviar
#   $3 → Mensaje de texto a incluir
#
# El operador reemplaza la lógica de envío con la solución que corresponda
# a su entorno (whatsapp-web.js, wppconnect, otro cliente local, etc.).
#
# Retorna código de salida 0 en éxito, distinto de 0 en fallo.

PHONE="$1"
PDF_PATH="$2"
MESSAGE="$3"

# Ejemplo con whatsapp-web.js mediante CLI hipotético:
# node /opt/wa-sender/index.js --phone "$PHONE" --file "$PDF_PATH" --msg "$MESSAGE"

echo "Enviando a $PHONE el archivo $PDF_PATH..."
exit 0   # Reemplazar con llamada real
```

### Implementación (adaptador)

```csharp
// Infrastructure/Communication/ScriptWhatsAppService.cs
public sealed class ScriptWhatsAppService : IWhatsAppService
{
    private readonly ISystemConfigurationRepository _config;

    public async Task<bool> SendDocumentAsync(
        string recipientPhone,
        string pdfTempPath,
        string message)
    {
        // 1. Lee la ruta del script desde SystemConfiguration (whatsapp_script_path)
        // 2. Construye ProcessStartInfo con los argumentos: teléfono, ruta PDF, mensaje
        // 3. Ejecuta el proceso y espera su finalización
        // 4. Retorna true si ExitCode == 0; false en caso contrario
        // 5. Captura excepciones si el script no existe o no puede ejecutarse
    }
}
```

---

## Generación de PDF

### Tecnología

**QuestPDF** (Community License — uso libre para proyectos con ingresos anuales menores a USD 1 M).

### Implementación

```csharp
// Infrastructure/Pdf/QuestPdfService.cs
public sealed class QuestPdfService : IPdfService
{
    private readonly ISaleRepository _saleRepository;
    private readonly IQuoteRepository _quoteRepository;

    public async Task<byte[]> GenerateInvoicePdfAsync(Guid invoiceId)
    {
        // 1. Carga la venta y sus ítems
        // 2. Construye el documento QuestPDF con encabezado del negocio, tabla de ítems y totales
        // 3. Retorna el PDF como array de bytes
    }

    public async Task<byte[]> GenerateQuotePdfAsync(Guid quoteId)
    {
        // 1. Carga la cotización y sus ítems
        // 2. Incluye número de cotización, vigencia y datos del cliente
        // 3. Retorna el PDF como array de bytes
    }
}
```

---

## Auditoría — DocumentSendLog

Cada intento de envío, exitoso o fallido, genera un registro en `DOCUMENT_SEND_LOGS`. Este registro es inmutable: no se edita ni elimina.

### Entidad

Ver definición completa en `03_diseno_modelo_dominio.md` (entidad `DocumentSendLog`).

### Tabla SQLite

Ver definición completa en `04_diseno_modelo_datos_sqlite.md` (tabla `DOCUMENT_SEND_LOGS`).

### Interfaz de repositorio

```csharp
public interface IDocumentSendLogRepository
{
    Task AddAsync(DocumentSendLog log);
    Task<IReadOnlyList<DocumentSendLog>> GetByDocumentAsync(DocumentType type, Guid documentId);
}
```

---

## Casos de uso de aplicación

### SendDocumentUseCase

```csharp
// Application/UseCases/Communication/SendDocumentUseCase.cs
public sealed class SendDocumentUseCase
{
    // Dependencias: IPdfService, IEmailService, IWhatsAppService, IDocumentSendLogRepository
    // Entrada: SendDocumentRequest (documentType, documentId, channel, recipientContact)
    // Flujo:
    //   1. Valida el contacto de destino (email válido o teléfono no vacío)
    //   2. Genera el PDF con IPdfService
    //   3. Envía mediante IEmailService o IWhatsAppService según el canal
    //   4. Registra el resultado en DocumentSendLog
    // Salida: SendDocumentResult (success, errorMessage)
}
```

### ResendDocumentUseCase

```csharp
// Application/UseCases/Communication/ResendDocumentUseCase.cs
public sealed class ResendDocumentUseCase
{
    // Dependencias: IDocumentSendLogRepository, SendDocumentUseCase
    // Entrada: ResendDocumentRequest (originalLogId)
    // Flujo:
    //   1. Recupera el log original por ID
    //   2. Ejecuta SendDocumentUseCase con los mismos parámetros
    //   3. Registra el nuevo intento en DocumentSendLog (no sobreescribe el original)
    // Salida: SendDocumentResult
}
```

---

## Casos de uso relacionados

Los siguientes casos de uso del documento `07_diseno_casos_uso.md` cubren este módulo:

- **Caso de uso 11** — Enviar documento por correo electrónico
- **Caso de uso 12** — Enviar documento por WhatsApp

---

## Integración con otros módulos

| Módulo relacionado | Tipo de integración | Descripción |
|---|---|---|
| **Facturas** | Lectura | Proporciona los datos de la venta para generar el PDF de factura. |
| **Cotizaciones** | Lectura | Proporciona los datos de la propuesta para generar el PDF de cotización. |
| **SystemConfiguration** | Lectura | Provee la configuración SMTP y la ruta del script de WhatsApp. |

---

## Diseño didáctico del módulo

| Situación | Respuesta del sistema |
|---|---|
| Correo enviado con éxito | "El documento fue enviado al correo {correo}." |
| Fallo en el envío por correo | "No se pudo enviar el correo. Verifica la configuración SMTP en Ajustes." |
| Script de WhatsApp no configurado | "El envío por WhatsApp no está configurado. Ingresa la ruta del script en Ajustes." |
| Script ejecutado sin errores | "El documento fue entregado al servicio de WhatsApp." |
| Script retornó error | "El script de WhatsApp reportó un error. Revisa el registro de envíos para más detalles." |
| Contacto vacío o inválido | "El cliente no tiene {correo/teléfono} registrado. Actualiza sus datos antes de enviar." |

## Resultado esperado

Al finalizar la implementación de este módulo, el operador podrá enviar facturas y cotizaciones por correo electrónico y WhatsApp desde el POS sin salir de la aplicación. Cada envío quedará registrado para auditoría y el operador podrá reenviar documentos desde el historial en cualquier momento.
