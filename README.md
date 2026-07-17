# 💸 Finanzas IA

Aplicación web de finanzas personales con dashboard inteligente, análisis financiero con recomendaciones, presupuesto mensual, reportes y captura de gastos por WhatsApp. Instalable como aplicación de escritorio/móvil (PWA).

## ✨ Funcionalidades

- **Dashboard**: balance neto, ingresos vs egresos, gastos por categoría (gráfico de dona), salud financiera y recomendaciones IA.
- **Movimientos**: alta, edición y baja de ingresos/egresos, exportación a CSV y datos de demostración.
- **Categorías**: gestión de categorías de gastos e ingresos.
- **Presupuesto**: presupuesto mensual configurable con estado (en control / alerta / excedido).
- **Reporte mensual**: resumen del mes con puntaje, categorías principales e insights.
- **Configuración**: nombre de usuario, moneda, presupuesto y día fiscal (persistidos en el navegador).
- **Modo oscuro** con persistencia.
- **PWA**: instalable como app, con accesos directos y soporte offline básico.
- **WhatsApp**: webhook que interpreta mensajes como "compré café 2500" y los registra como movimientos.

## 🏗️ Arquitectura

| Proyecto | Descripción |
|---|---|
| `FinanzasIA.Core` | Entidades y enums de dominio |
| `FinanzasIA.Application` | DTOs y servicios de aplicación |
| `FinanzasIA.Infrastructure` | Acceso a datos (EF Core, PostgreSQL) |
| `FinanzasIA.Api` | API REST (puerto **5027**) |
| `FinanzasIA.Backoffice` | Frontend Blazor Server (puerto **5244**) |

Stack: **.NET 10**, Blazor Server (InteractiveServer), EF Core, PostgreSQL, Bootstrap.

## 🚀 Cómo ejecutarla

### Requisitos

- .NET 10 SDK
- PostgreSQL (local en `localhost:5432`, base `finanzasia`, usuario/clave `postgres`)

### Pasos

1. Clonar el repositorio.
2. Verificar la cadena de conexión en `FinanzasIA.Api/appsettings.json` (la base `FinanzasIA_App` se crea y migra automáticamente al iniciar la API).
3. Iniciar la **API** (siempre primero):

   ```powershell
   dotnet run --project FinanzasIA.Api --launch-profile http
   ```

4. Iniciar el **Backoffice** en otra terminal:

   ```powershell
   dotnet run --project FinanzasIA.Backoffice --launch-profile http
   ```

5. Abrir `http://localhost:5244` en el navegador, o en modo aplicación:

   ```powershell
   Start-Process msedge -ArgumentList "--app=http://localhost:5244"
   ```

> 💡 En Visual Studio: clic derecho en la solución → *Configurar proyectos de inicio* → *Varios proyectos de inicio* y marcar `FinanzasIA.Api` y `FinanzasIA.Backoffice`.

## 📱 Integración con WhatsApp

El webhook (`api/whatsapp`) usa la WhatsApp Cloud API de Meta. Configurar en `FinanzasIA.Api/appsettings.json` (o mejor, en *user secrets*):

```json
"WhatsApp": {
  "VerifyToken": "tu-token-de-verificacion",
  "AccessToken": "tu-access-token",
  "PhoneNumberId": "tu-phone-number-id"
}
```

Mensajes soportados:

- `compré / gasté / pagué <descripción> <monto>` → registra un egreso.
- `cobré / ingreso <descripción> <monto>` → registra un ingreso.
- `resumen` / `analisis` → devuelve el análisis financiero.
- `ayuda` → lista de comandos.

Sin credenciales configuradas, el webhook procesa mensajes igualmente (modo de prueba local, sin enviar respuestas).

## 🔒 Notas de seguridad

- No commitear `AccessToken` ni `PhoneNumberId`: usar `dotnet user-secrets` o variables de entorno.
- Las preferencias de usuario se guardan en `localStorage` del navegador (no hay autenticación aún; multi-usuario es un trabajo futuro).

## Deploy en Railway

1. Crear cuenta en https://railway.app con GitHub y crear un proyecto nuevo.
2. Agregar un servicio **PostgreSQL** (Railway genera la variable DATABASE_URL / credenciales).
3. Agregar dos servicios desde el repo de GitHub:
   - **API**: Root Directory = FinanzasIA, Dockerfile Path = FinanzasIA.Api/Dockerfile
   - **Backoffice**: Root Directory = FinanzasIA, Dockerfile Path = FinanzasIA.Backoffice/Dockerfile
4. Variables de entorno de la **API**:
   - ConnectionStrings__DefaultConnection = Host=<host>;Port=<port>;Database=<db>;Username=<user>;Password=<pass>
   - Api__Key = una clave secreta larga (generala con: [guid]::NewGuid())
   - WhatsApp__VerifyToken / WhatsApp__AccessToken / WhatsApp__PhoneNumberId (si se usa WhatsApp)
5. Variables de entorno del **Backoffice**:
   - ConnectionStrings__DefaultConnection = igual que la API
   - Api__BaseUrl = URL publica de la API (ej: https://finanzasia-api.up.railway.app/)
   - Api__Key = la misma clave que la API
6. En cada servicio, Settings > Networking > Generate Domain para obtener la URL publica HTTPS.
7. Con la URL HTTPS del Backoffice, generar el APK en https://www.pwabuilder.com (pegar la URL, elegir Android y descargar el paquete).

Notas:
- Las migraciones se aplican automaticamente al iniciar cada servicio.
- La API exige el header X-Api-Key en todos los endpoints excepto /health y /api/whatsapp (que valida su propio token).
