# Riesgos de diseno detectados, verificados y refactorizados (Actividad 7)

Cada riesgo se verifico en el codigo (`grep`, lectura o ejecucion) antes de tocarlo. Cada refactor tiene su propio commit y se comprobo la suite **antes** (linea base de 107 pruebas en verde) y **despues** (compilacion con 0 advertencias, pruebas en verde y `dotnet format --verify-no-changes`).

## A. Diez refactores con commit propio

| # | Tipo de riesgo | Riesgo verificado | Evidencia de verificacion | Commit | Pruebas antes -> despues |
|---|---|---|---|---|---|
| 1 | Duplicacion / comportamiento inconsistente | `CambiarCantidad` existia en `ServicioPedidos` y en `ServicioEdicionBorrador`; la copia de `ServicioPedidos` no registraba el historial del Memento, asi que ese cambio no se podia deshacer | `grep CambiarCantidadAsync`: dos servicios; solo la prueba P08 usaba el duplicado | `ed4e955` | 107 -> 107 |
| 2 | Cadenas magicas / acoplamiento entre capas | `"SIMULADA"`, `"LEGADA"`, `"EMAIL"`, `"SMS"` repetidas en Application, Infrastructure y WinForms (11 apariciones) | `grep` de los literales | `f3456e3` | 107 -> 107 (+ autoprueba de la UI) |
| 3 | Estado primitivo acoplado al esquema | El filtro del indice unico de pago aprobado usaba `'Aprobado'` escrito a mano | Lectura de `Configuraciones.cs`; `dotnet ef migrations has-pending-model-changes` sin cambios | `ed9e2e7` | 107 -> 107 |
| 4 | Duplicacion de invariantes | La regla "cantidad > 0" estaba copiada en 6 sitios del dominio con mensajes distintos | `grep "cantidad <= 0"`: 6 coincidencias | `dfcc269` | 107 -> 107 |
| 5 | Cadenas magicas | 12 codigos de rechazo (`NO_ENCONTRADO`, `ESTADO_INVALIDO`, ...) y el mensaje "El pedido no existe." repetidos en 5 servicios; `NO_ENCONTRADO` en 7 sitios | `grep` de los codigos | `8336379` | 107 -> 107 |
| 6 | Validaciones dispersas / duplicacion | Facade y `ServicioPedidos.ValidarAsync` repetian la carga del catalogo, el diccionario de productos y la construccion del contexto de la cadena. Efecto lateral: el Facade deja de depender del repositorio de catalogo | Lectura de ambos metodos (lineas casi identicas) | `7bcc5e3` | 107 -> 107 |
| 7 | Extensibilidad (OCP) | `SerializadorEventos` mantenia una lista manual de tipos: un evento nuevo podia olvidarse y fallar al reintentar el Outbox | Lectura de `Eventos.cs` | `d8d1642` | 107 -> 108 (nueva prueba de ida y vuelta de todos los eventos) |
| 8 | Estados primitivos | 8 comparaciones de estado de pedido por texto (`"Borrador"`, `"Pagado"`...) repartidas por las vistas | `grep` de los literales en WinForms | `0fa493c` | 108 -> 108 (+ autoprueba de la UI) |
| 9 | Validaciones dispersas | La regla "al menos una linea" estaba en tres capas (Builder, `Pedido.Validar`, eslabon de la cadena) con tres mensajes; el comprobante `Cantidad > 0` del eslabon era inalcanzable por la guardia de `PedidoDetalle` | Lectura de las tres comprobaciones y de `Restaurar`: un pedido no puede quedar sin lineas | `80baeab` | 108 -> 108 |
| 10 | Duplicacion en pruebas | `PagarAsync`/`EntregarAsync` de `PatronesAplicacionTests` eran copias de `Escenarios` | Comparacion de los cuerpos | `21b9e89` | 108 -> 108 |

Calidad adicional: `dotnet format --verify-no-changes` fallaba (orden de `using` y codificacion de la migracion generada); commit `6cb7cfb` aplica el formato y excluye el codigo generado.

## B. Otros riesgos corregidos durante la Fase 7 (sin commit "antes" separado)

Se detectaron y repararon antes de commitear; se declara asi para no aparentar un historial que no existe.

| Riesgo | Tratamiento |
|---|---|
| Combo de borradores desactualizado tras cancelar un pedido | `RefrescarAsync` tras cada operacion |
| Pantallas sin desregistrar del Mediator si la ventana se descarta sin `Close` | `FormPrincipal.Dispose` libera las vistas |
| Decimales con coma y punto mezclados | Cultura es-EC con separador decimal punto |
| `GroupBox` autoajustable recortaba botones | Altura fija en `Ui.Grupo` |
| `EliminarAsync` lanzaba una excepcion de EF sin traducir | Comprobacion previa `ExistenMovimientosAsync` |
| Excepcion nula en `ObtenerPorSkuAsync` con grafo parcial de kits | Carga del catalogo completo y filtrado |
| `YA_DEVUELTO` evaluado tarde en la plantilla de devolucion (P35 devolvia `NO_ELEGIBLE`) | Comprobacion al inicio de la plantilla |

## C. Riesgos aceptados (con justificacion)

| Riesgo | Justificacion |
|---|---|
| `ObservadorMetricas` y `ObservadorNotificacion` hacen `switch` por tipo de evento | Solo hay tres eventos y cada plantilla depende de su tipo; abstraerlo exige ampliar `IEventoNegocio` y seria sobrediseno |
| `ConfirmacionPedidoFacade` recibe 10 colaboradores | Es una fachada: orquestar colaboradores es su funcion |
| `PedidoResumenDto.Estado` es `string` | Es un DTO de presentacion; ahora sus valores salen de `EstadosPedidoTexto` (refactor 8) |

## D. Hallazgo de la revision final (patron sin participacion en un caso de uso)

Al auditar con `grep` donde se usa cada patron fuera de su definicion y de las pruebas, **Visitor** (`DesgloseFisicoVisitor`, `PesoLogisticoVisitor`) solo se ejecutaba en pruebas. La guia exige participacion efectiva en un caso de uso, asi que se corrigio: `ConsultasCatalogo.ListarAsync` calcula ahora el peso logistico y el desglose fisico por SKU final recorriendo cada producto o kit con ambos visitantes, y la pantalla de catalogo muestra la columna *Desglose fisico*. Commit `df68778`; suite 108 -> 108 (se ampliaron las aserciones de la prueba del catalogo), compilacion con 0 advertencias y autoprueba de la interfaz en OK.
Los otros 22 patrones se confirmaron en flujos reales (servicios, composicion de dependencias o pantallas).
