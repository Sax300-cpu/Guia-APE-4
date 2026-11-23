# LogiMarket 360

Aplicacion de escritorio de ventas y logistica: carrito, cobro simulado, despacho, entrega y devolucion de pedidos, con
persistencia real en SQLite. Implementa los 23 patrones GoF dentro de una Clean Architecture.

**Asignatura:** Patrones de Software (APE04) - Universidad Tecnica de Ambato, Carrera de Software, ciclo julio-diciembre 2026.
**Autor:** Velasco Huayamave Kevin Andres (trabajo individual).

## Requisitos
- Windows 10/11 (la interfaz es Windows Forms).
- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) (`global.json` fija 8.0.424 con avance a la ultima 8.0.x). Comprobar con `dotnet --version`.
- No hace falta instalar SQLite ni un servidor de base de datos: el archivo se crea solo.
- Opcional: Visual Studio 2022 17.8 o superior para abrir `LogiMarket360.sln`.

## Estructura
| Proyecto | Responsabilidad | Referencias |
|---|---|---|
| `src/LogiMarket.Domain` | Entidades, reglas de negocio, estados, politicas y composite del catalogo | ninguna |
| `src/LogiMarket.Application` | Casos de uso, puertos, validacion, comandos, eventos, autorizacion | Domain |
| `src/LogiMarket.Infrastructure` | EF Core + SQLite, repositorios, Unit of Work, pasarelas, hash de contrasenas | Application |
| `src/LogiMarket.WinForms` | Interfaz (12 pantallas) y raiz de composicion con DI | Application, Infrastructure |
| `tests/LogiMarket.Tests` | Pruebas xUnit de dominio, aplicacion, integracion y arquitectura | Domain, Application, Infrastructure |

La documentacion de evidencia esta en `docs/` (matrices de 38 pruebas y 23 patrones, riesgos de diseno, logs y capturas).

## Compilar
```powershell
dotnet restore LogiMarket360.sln
dotnet build LogiMarket360.sln -c Release
```
Debe terminar con 0 advertencias y 0 errores.

## Base de datos: migraciones y datos iniciales
No hay que ejecutar nada a mano. Al iniciar, la aplicacion aplica las migraciones de EF Core y carga los datos iniciales
de forma idempotente (relanzar no duplica nada):

- 5 productos (P01 a P05) con stock 8, 5, 20, 4 y 30, y 2 kits (`KIT_OFICINA`, `KIT_TELETRABAJO`).
- Clientes de ejemplo: Ana (VIP), Luis (NORMAL) y Carmen (NORMAL; el descuento mayorista de 15% se aplica al comprar 10 o mas unidades de un mismo producto).
- 4 usuarios con contrasena almacenada con PBKDF2 (nunca en texto plano).

El archivo se crea en `%LOCALAPPDATA%\LogiMarket360\logimarket.db`. Los comprobantes TXT se guardan en
`%LOCALAPPDATA%\LogiMarket360\comprobantes`.

Para generar o inspeccionar migraciones (opcional; la herramienta se restaura desde `.config/dotnet-tools.json`):
```powershell
dotnet tool restore
dotnet ef migrations list --project src/LogiMarket.Infrastructure --startup-project src/LogiMarket.Infrastructure
```

## Ejecutar
```powershell
dotnet run --project src\LogiMarket.WinForms -c Release
```
Para usar otro archivo de base de datos: `dotnet run --project src\LogiMarket.WinForms -c Release -- --db C:\ruta\mi.db`.

### Cuentas de demostracion
| Usuario | Contrasena | Rol | Puede |
|---|---|---|---|
| `admin` | `Admin123*` | Admin | Todo, incluida la gestion del catalogo y la demostracion controlada |
| `ventas` | `Ventas123*` | Ventas | Clientes, carrito, cobro, devoluciones y consultas |
| `bodega` | `Bodega123*` | Bodega | Despacho, entrega y consultas |
| `consulta` | `Consulta123*` | Consulta | Consultas y auditoria (solo lectura) |

Son credenciales de demostracion para un entorno local; no se usan en ningun servicio externo.

### Flujo de prueba rapido
1. Entrar como `ventas`, abrir **Carrito**, cliente `Ana`, entrega `Estandar`, producto `KIT_TELETRABAJO` x1: total **USD 59.00**.
2. En **Cobro**, cobrar el pedido (pago simulado). El stock pasa a P01=7, P02=4, P03=19.
3. Entrar como `bodega`, **Despacho**: despachar (guia `STD-...`) y marcar entregado.
4. Entrar como `ventas`, **Devoluciones**: motivo `Defecto`; reembolso simulado de **USD 54.00** sin tarifa de envio.

## Pruebas automatizadas
```powershell
dotnet test LogiMarket360.sln -c Release
```
Ejecuta las pruebas xUnit (mas de 100, que incluyen los casos P01 a P38 en el nombre del metodo, concurrencia sobre SQLite y reglas de arquitectura).
Para generar el TRX y regenerar las matrices de evidencia:
```powershell
dotnet test LogiMarket360.sln -c Release --logger "trx;LogFileName=resultados.trx" --results-directory docs/evidencias/trx
powershell -File tools\generar-matriz-pruebas.ps1
```
Verificacion de la interfaz con botones reales (usa una base temporal, no toca la tuya): `powershell -File tools\ejecutar-autoprueba.ps1`.

## Reiniciar la base de datos de forma segura
Cierra la aplicacion y borra solo el archivo de LogiMarket (no hay otros datos en esa carpeta de la que dependa nada mas):
```powershell
Remove-Item "$env:LOCALAPPDATA\LogiMarket360\logimarket.db*" -Force
```
Al volver a abrir la aplicacion se recrea la base con los datos iniciales. Las pruebas automatizadas usan bases temporales
propias y nunca modifican este archivo.

## Notas de diseno
- Dinero como `decimal` con redondeo `AwayFromZero`, persistido en centavos; peso en gramos.
- Transacciones reales con Unit of Work, concurrencia optimista (`Version`), clave de idempotencia unica y Outbox procesado despues de confirmar.
- La autorizacion se aplica en Application (matriz de permisos por rol); la interfaz solo oculta o deshabilita lo no permitido.
