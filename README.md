# JobTrack

Aplicacion web para organizar postulaciones laborales, documentos enviados, comunicaciones, entrevistas y recordatorios de seguimiento.

## Requisitos

- .NET 10 SDK.
- SQL Server LocalDB instalado, o una instancia de SQL Server accesible.

La configuracion local usa la base `JobTrackDb` en `(localdb)\MSSQLLocalDB`. Al iniciar la API, Entity Framework Core aplica las migraciones pendientes automaticamente.

Para instalar LocalDB en Windows, descarga el medio de SQL Server Express, selecciona la caracteristica LocalDB y verifica la instancia desde PowerShell:

```powershell
sqllocaldb info MSSQLLocalDB
```

Consulta la [guia oficial de instalacion de SQL Server Express LocalDB](https://learn.microsoft.com/es-es/sql/database-engine/configure-windows/sql-server-express-localdb).

## Ejecutar en Windows

Desde la carpeta del proyecto:

```powershell
dotnet run --project .\backend\JobTrack.API\JobTrack.API.csproj
```

Abri en el navegador la direccion HTTPS que informa `dotnet run`. La API sirve tambien la interfaz web y crea o actualiza el esquema de la base al iniciar.

Si SQL Server usa otra instancia o requiere credenciales, configura la cadena localmente con User Secrets para no guardarla en Git:

```powershell
dotnet user-secrets init --project .\backend\JobTrack.API\JobTrack.API.csproj
dotnet user-secrets set "ConnectionStrings:JobTrack" "<cadena-de-conexion>" --project .\backend\JobTrack.API\JobTrack.API.csproj
```

La variable de entorno `ConnectionStrings__JobTrack` tambien puede sobrescribir la cadena de conexion.

## Base de datos

- Migraciones EF Core: `backend/JobTrack.API/Data/Migrations`.
- Script SQL idempotente para instalacion manual: `backend/JobTrack.API/Data/Migrations/JobTrack.sql`.
- No se versionan archivos de base de datos, documentos cargados ni credenciales locales.

## Estado y alcance

Esta version esta pensada para uso local y un unico perfil. Todavia no incorpora autenticacion; no publiques la API en Internet ni la uses como servicio multiusuario sin agregar autenticacion y autorizacion.

El buscador inteligente de oportunidades pertenece a una etapa futura. La aplicacion actual registra y organiza postulaciones manuales.
