# Riesgos de diseno detectados, verificados y su tratamiento

Cada fila nace de una verificacion en el codigo (`grep`, lectura o ejecucion), no de una suposicion.
Estado: **Corregido** (commit propio con pruebas antes/despues), **Corregido antes de commitear** (detectado durante el desarrollo; no existe commit "antes", y se declara asi) o **Aceptado** (se mantiene con justificacion).

| # | Riesgo (verificado) | Evidencia de la verificacion | Estado | Tratamiento |
|---|---|---|---|---|
| 1 | `CambiarCantidad` duplicado: `ServicioPedidos` lo editaba sin registrar el historial del Memento, por lo que un cambio por esa via no se podia deshacer | `grep CambiarCantidadAsync`: dos servicios; solo una prueba (P08) usaba el duplicado | Corregido | Commit `ed4e955`: se elimina el duplicado y P08 usa `ServicioEdicionBorrador`. 107/107 antes y despues |
| 2 | Cadenas magicas `"SIMULADA"`, `"LEGADA"`, `"EMAIL"`, `"SMS"` repetidas en Application, Infrastructure y WinForms | `grep` de los literales: 11 apariciones en 3 capas | Corregido | Commit `f3456e3`: constantes `ProveedoresPago` y `CanalesNotificacion`. Pruebas y autoprueba de la UI en verde |
| 3 | Filtro del indice unico de pago aprobado escrito a mano (`'Aprobado'`), acoplado al nombre del enum | Lectura de `Configuraciones.cs` | Corregido | Commit `ed9e2e7`: `nameof(EstadoPago.Aprobado)`. `dotnet ef migrations has-pending-model-changes`: sin cambios de modelo |
| 4 | `dotnet format --verify-no-changes` fallaba: orden de `using` y codificacion de la migracion generada | Ejecucion del comando | Corregido | Commit `6cb7cfb`: formato aplicado y codigo generado excluido en `.editorconfig`; la verificacion pasa |
| 5 | Combo de borradores desactualizado tras cancelar un pedido | Observado en la interfaz durante la Fase 7 | Corregido antes de commitear | `RefrescarAsync` tras cada operacion en las vistas |
| 6 | Pantallas sin desregistrar del Mediator si la ventana se descarta sin `Close` | Revision del ciclo de vida de `FormPrincipal` | Corregido antes de commitear | `FormPrincipal.Dispose` libera las vistas |
| 7 | Decimales con coma y punto mezclados en la interfaz | Observado al capturar importes | Corregido antes de commitear | Cultura propia es-EC con separador decimal punto |
| 8 | `GroupBox` autoajustable recortaba botones | Observado en la interfaz | Corregido antes de commitear | Altura fija en `Ui.Grupo` |
| 9 | `EliminarAsync` lanzaba una excepcion de EF sin traducir al borrar un producto con movimientos | Fallo real al ejecutar la prueba | Corregido antes de commitear | Comprobacion previa `ExistenMovimientosAsync` y mensaje de dominio |
| 10 | `ObservadorMetricas` y `ObservadorNotificacion` hacen `switch` por tipo de evento: un evento nuevo obliga a editar ambos, y el de metricas ignora en silencio los desconocidos | Lectura de `Notificaciones.cs` | Aceptado | Hay solo tres eventos y las plantillas dependen de cada uno. Cambiarlo exige ampliar `IEventoNegocio`; se evita el sobrediseno. Queda como mejora futura |
| 11 | `ConfirmacionPedidoFacade` recibe 10 dependencias por constructor | Lectura del constructor | Aceptado | Es una fachada: sus colaboradores son su razon de ser. Se mantiene la orquestacion en un solo lugar |
| 12 | `PedidoResumenDto.Estado` es `string` | Lectura de `ServiciosGestion.cs` | Aceptado | Es un DTO de presentacion; evita exponer el dominio a la interfaz |

Fallos reales detectados y reparados durante el desarrollo, sin commit "antes" separado: excepcion nula en `ObtenerPorSkuAsync` con un grafo parcial de kits (corregido cargando el catalogo completo) y `YA_DEVUELTO` evaluado tarde en la plantilla de devolucion (P35 devolvia `NO_ELEGIBLE`).
