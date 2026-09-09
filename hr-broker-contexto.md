# Contexto del sistema: HR Broker

> **Instrucción para el agente — leer primero.**
>
> Todo lo descrito en este documento **ya está construido y funcionando** en HR Broker.
>
> 1. **No le preguntes al usuario nada que este documento ya responda.** El usuario es un
>    analista o usuario funcional; probablemente no sepa responder preguntas técnicas y, si
>    intenta hacerlo, dará respuestas que contradicen lo que el sistema ya tiene.
> 2. Cuando un requerimiento dependa de una capacidad ya existente (permisos, trazabilidad,
>    adjuntos, exportes, notificaciones, etc.), **asúmela como resuelta** y decláratela como
>    **precondición** o **supuesto** dentro del caso de uso. No la conviertas en pregunta ni
>    en requerimiento nuevo.
> 3. Usa el vocabulario de este documento. El sistema está en español y el negocio es
>    **corretaje y reaseguro de seguros**.
> 4. Concentra tus preguntas en lo que **solo el usuario puede saber**: la regla de negocio
>    nueva, los datos nuevos, quién la ejecuta, cuándo, y cómo se valida que quedó bien.
>
> Si dudas entre preguntar o asumir: **si aparece en este documento, se asume**.

---

## 1. Qué es HR Broker

HR Broker es el sistema interno de operación de una correduría de seguros y reaseguros.
Lo usan las áreas comerciales, técnicas, de operaciones, de cartera y contables para
gestionar el ciclo completo de un negocio de seguros: desde que se estructura y se coloca
con reaseguradores, pasando por su autorización, facturación y recaudo, hasta su cierre
contable y el reporte a la contabilidad corporativa.

Es una aplicación web interna. **No es un portal de clientes ni de asegurados**: todos sus
usuarios son empleados de la correduría con un rol asignado.

Características generales del sistema:

- Idioma: **español** (pantallas, mensajes, documentos generados).
- Opera en **varios países**; el comportamiento de moneda, zona horaria y algunos
  parámetros cambia según el país configurado.
- Maneja **múltiples monedas** con tasas de cambio administradas por fecha.
- Es un sistema **transaccional con trazabilidad**: casi toda operación relevante queda
  registrada con usuario, rol y fecha.

---

## 2. Capacidades transversales que YA existen

Esta es la sección más importante. Cada punto describe algo que el sistema **ya resuelve
de forma estándar** para cualquier módulo o funcionalidad nueva.

### 2.1 Acceso y autenticación

**Ya existe:** ingreso con usuario y contraseña, segundo factor de autenticación (por
aplicación autenticadora o por correo), recuperación y restablecimiento de contraseña,
expiración de contraseña, y cierre de sesión automático por vencimiento.

**No preguntes:** cómo se autentica el usuario, si se requiere doble factor, cómo se
recupera la contraseña, cuánto dura la sesión, ni cómo se maneja el olvido de contraseña.

### 2.2 Permisos y control de acceso

**Ya existe:** un esquema de **permisos por rol**. Cada acción del sistema (consultar,
crear, modificar, autorizar, anular, exportar, eliminar) está protegida por un permiso, y
los permisos se agrupan en roles. Un usuario puede tener uno o varios roles y elige con
cuál trabaja al ingresar. Existen pantallas de administración para crear permisos,
asignarlos a roles y asignar roles a usuarios.

Si un usuario no tiene el permiso, **no ve la opción** (el botón o la pantalla no se
muestran) y además el sistema bloquea la operación por detrás.

**No preguntes:** cómo debe funcionar el sistema de permisos, cómo se validan, dónde se
configuran, si se necesita un permiso nuevo, ni cómo se oculta un botón.

**Sí pregunta:** **qué roles** participan en el requerimiento y **qué puede hacer cada
uno** en términos de negocio (por ejemplo: "el Director autoriza, el Ejecutivo de Cuenta
solo consulta").

### 2.3 Menú y navegación

**Ya existe:** el menú lateral se arma automáticamente según el rol del usuario. Al crear
una pantalla nueva se le asigna una opción de menú y se habilita para los roles que
corresponda; el usuario que no tiene acceso simplemente no la ve.

**No preguntes:** dónde debe ubicarse la opción en el menú, cómo se controla la
visibilidad del menú, ni si hay que crear un menú nuevo.

### 2.4 Trazabilidad y bitácora

**Ya existe:** dos niveles de trazabilidad.

- **Trazabilidad automática:** los registros guardan quién los creó, quién los modificó y
  cuándo. Los cambios de estado de un negocio quedan registrados con usuario, rol, fecha y
  observación.
- **Bitácora de comentarios:** un mecanismo genérico y reutilizable donde el usuario puede
  registrar un comentario sobre un registro, con o sin archivos adjuntos según se
  configure, y consultar todo el historial. Habilitar la bitácora para un nuevo tipo de
  registro es **configuración, no desarrollo**.

**No preguntes:** cómo se audita, si se guarda quién hizo el cambio, cómo se consulta el
historial, ni si se pueden adjuntar soportes al comentario.

**Sí pregunta:** si el requerimiento necesita que el usuario **deje comentarios o
justificaciones** sobre el registro, y si esos comentarios deben exigir soporte adjunto.

### 2.5 Archivos adjuntos

**Ya existe:** carga, almacenamiento, consulta, descarga y desactivación de archivos
adjuntos, asociados a negocios, siniestros, recibos de caja, aplicaciones, licencias
internacionales, coordenadas bancarias y comentarios de bitácora. Los archivos quedan
protegidos y sólo accesibles desde el sistema.

**No preguntes:** cómo se guardan los archivos, dónde se almacenan, cómo se descargan, ni
cómo se controla el acceso a ellos.

**Sí pregunta:** **qué documentos** debe adjuntar el usuario, si son obligatorios y en qué
momento del proceso.

### 2.6 Exportes y documentos generados

**Ya existe:** generación de archivos **Excel**, **PDF** y **CSV** desde múltiples
pantallas (reportes, cierres, interfaces contables, hojas de datos de negocio, recibos de
caja, órdenes de pago). También existe **carga masiva desde Excel** con validación previa
para negocios, números de factura, recibos de caja y otros procesos.

**No preguntes:** en qué formato se exporta, cómo se genera el Excel o el PDF, ni cómo se
implementa la carga masiva.

**Sí pregunta:** **qué columnas o campos** debe contener el archivo y **qué validaciones**
aplican a la carga.

### 2.7 Notificaciones por correo

**Ya existe:** envío de correos desde el sistema (por ejemplo, alertas de vencimiento de
licencias internacionales y comunicaciones del flujo de autorización).

**No preguntes:** cómo se envían los correos ni cómo se configura el servidor de correo.

**Sí pregunta:** **a quién** se debe notificar, **cuándo** y **qué debe decir** el mensaje.

### 2.8 Mensajes de error y validaciones al usuario

**Ya existe:** un manejo centralizado de errores. Cuando una regla de negocio no se
cumple, el sistema muestra al usuario un mensaje claro y detiene la operación; los errores
técnicos no se le muestran al usuario. Los formularios validan campos obligatorios y
formatos antes de enviar.

**No preguntes:** cómo se muestran los errores, si se usa un modal o una notificación, ni
cómo se manejan los campos obligatorios.

**Sí pregunta:** **qué regla de negocio** debe impedir la operación y **qué mensaje** debe
entender el usuario.

### 2.9 Moneda, tasas de cambio y cálculos

**Ya existe:** manejo multimoneda con administración de tasas de cambio por fecha,
conversión automática en negocios, cartera y reportes, y cálculo de primas, comisiones,
conceptos y cuotas con precisión decimal (sin errores de redondeo).

**No preguntes:** cómo se manejan las monedas, de dónde sale la tasa de cambio, ni cómo se
redondean los valores.

**Sí pregunta:** **qué fórmula** aplica al cálculo nuevo y **sobre qué base** se calcula.

### 2.10 Consultas, filtros y listados

**Ya existe:** un patrón estándar de pantalla de listado con tabla, filtros por múltiples
criterios, paginación, ordenamiento y exportación a Excel. Todas las pantallas de consulta
del sistema siguen ese patrón.

**No preguntes:** cómo se pagina, si hay buscador, ni cómo se ordenan las columnas.

**Sí pregunta:** **por qué criterios** necesita filtrar el usuario y **qué columnas**
necesita ver.

### 2.11 Administración de maestros

**Ya existe:** un patrón estándar de mantenimiento de tablas maestras (consultar, crear,
editar, activar/desactivar) usado en toda la sección de Parametrización. Los maestros no
se eliminan físicamente: se **desactivan**.

**No preguntes:** cómo se crean o editan los maestros, ni si se pueden borrar.

### 2.12 Activación de funcionalidades

**Ya existe:** un mecanismo para activar o desactivar funcionalidades sin desplegar código,
usado para liberar cambios de forma controlada por ambiente.

**No preguntes:** cómo se activa una funcionalidad nueva ni cómo se hace el despliegue
progresivo.

---

## 3. Roles del sistema

Estos son los roles funcionales que participan en los flujos. El sistema permite crear
otros, pero estos son los que estructuran el proceso principal.

| Rol | Qué hace |
|---|---|
| **Ejecutivo de Cuenta** | Estructura y registra el negocio, carga la información técnica y lo envía a revisión. |
| **Director** | Revisa y autoriza los negocios de su equipo; puede devolverlos con observación. |
| **CFO** | Autorización financiera para negocios que superan ciertos umbrales o condiciones. |
| **Líder de Operaciones** | Coordina el área de operaciones y distribuye los negocios autorizados. |
| **Operaciones** | Ejecuta la operación del negocio ya autorizado: emisión, facturación, cargue. |
| **Siniestros** | Registra y gestiona los siniestros y sus pagos. |
| **Cartera** | Registra el recaudo, aplica el dinero a los negocios, gestiona recibos de caja, aplicaciones y órdenes de pago, y hace seguimiento a la cartera pendiente. |
| **Contabilidad** | Ejecuta los cierres contables del período, genera y valida las interfaces hacia el sistema contable corporativo, y administra la equivalencia con el plan de cuentas. |
| **Administrador** | Administra parametrización, permisos, roles y usuarios. |

**No preguntes** cómo se crean roles o cómo se asignan usuarios a roles.
**Sí pregunta** cuál de estos roles ejecuta cada paso del requerimiento.

---

## 4. Módulos del sistema

### 4.1 Negocios

**Qué hace.** Es el núcleo del sistema. Permite estructurar un negocio de seguro o
reaseguro completo: datos generales, vigencias, cliente/cedente, ramo y subramo,
intermediarios, bienes asegurados y sus coberturas, deducibles, sublímites, cláusulas,
primas, comisiones, conceptos adicionales y la forma de pago en cuotas. Sobre esa base se
construye la **colocación**: cómo se reparte el riesgo entre la retención propia y los
reaseguradores.

**Conceptos que maneja.**
- **Negocio**: la operación completa (póliza o contrato).
- **Endoso**: modificación posterior de un negocio ya emitido (prórroga, cambio,
  movimiento o anulación).
- **Ítem y cobertura**: los bienes o riesgos asegurados y qué ampara cada uno.
- **Deducible y sublímite**: límites y descuentos aplicables por cobertura o ítem.
- **Prima, comisión y concepto**: los valores económicos del negocio.
- **Cuota**: la forma en que se fracciona el pago de la prima.
- **Colocación proporcional**: reparto porcentual del riesgo entre participantes.
- **Colocación no proporcional por capas**: reparto por tramos de exceso de pérdida, cada
  capa con sus propios participantes, conceptos y deducibles.
- **Facultativo**: colocación individual de un riesgo con reaseguradores.
- **Renovación**: creación de un negocio nuevo a partir de uno próximo a vencer.

**Acciones disponibles.** Crear, modificar, consultar (individual y por listados
filtrados), registrar endosos, cargar masivamente desde Excel, generar la hoja de datos en
PDF, adjuntar soportes, consultar el historial de cambios, gestionar renovaciones y asignar
o reasignar el negocio a otros usuarios.

**Ya resuelto — no preguntar.** Estructura de datos del negocio; cómo se calculan primas,
comisiones y conceptos; cómo se reparte la colocación; cómo se adjuntan documentos; cómo se
consulta el historial; cómo se filtra el listado; cómo se exporta a Excel; cómo se genera
la hoja de datos.

**Sí preguntar.** Qué campo o dato nuevo se necesita; qué regla de validación nueva aplica;
qué comportamiento cambia y bajo qué condición; a qué tipo de negocio o ramo aplica.

---

### 4.2 Autorizaciones (flujo de aprobación del negocio)

**Qué hace.** Gobierna el ciclo de vida del negocio mediante estados y una cadena de
aprobación por roles. El negocio no pasa a operación hasta estar autorizado.

**Flujo estándar.**
1. El **Ejecutivo de Cuenta** crea el negocio y lo **envía** a revisión.
2. El **Director** revisa y autoriza. Concentra toda la autorización comercial y de cuenta:
   no hay niveles intermedios por encima ni por debajo de él en ese frente.
3. Cuando el tipo de negocio, el monto o las condiciones lo exigen, se suma la autorización
   del **CFO** como aprobación financiera.
4. Cualquier autorizador puede **devolver** el negocio con una observación; vuelve al
   Ejecutivo de Cuenta para corrección y reenvío.
5. Autorizado el negocio, pasa a **Operaciones**.
6. En cualquier punto anterior a su finalización, un negocio puede ser **anulado** con
   justificación.

**Ya resuelto — no preguntar.** Cómo se implementa el flujo de estados; cómo se registra
quién autorizó y cuándo; cómo se notifica y se devuelve con observación; cómo se consultan
los negocios pendientes por autorizar; cómo se mide la gestión de autorizaciones.

**Sí preguntar.** Qué roles deben autorizar en este caso; en qué orden; bajo qué condición
o umbral se dispara cada autorización; qué pasa si se rechaza; si el requerimiento agrega
un estado nuevo al flujo.

---

### 4.3 Operaciones

**Qué hace.** Es la etapa posterior a la autorización. El área de operaciones toma el
negocio autorizado, verifica la información, la completa con los datos de emisión y lo
marca como operado. Incluye la distribución de negocios entre los analistas del área.

**Acciones disponibles.** Consultar la bandeja de negocios autorizados pendientes,
completar la operación del negocio, asignar y reasignar negocios entre usuarios del área,
y procesar cargues masivos de negocios de canal retail.

**Ya resuelto — no preguntar.** Cómo se asignan los negocios a los analistas; cómo se marca
un negocio como operado; cómo se consulta la bandeja de trabajo.

**Sí preguntar.** Qué validación adicional debe hacer Operaciones; qué dato debe
completarse en esta etapa; qué debe impedir que el negocio avance.

---

### 4.4 Facturación

**Qué hace.** Asigna el número de factura al negocio operado y ejecuta el cierre de
facturación, que es el proceso que consolida los negocios facturables de un período y los
deja listos para la contabilidad.

**Acciones disponibles.** Registrar el número de factura de forma individual, cargar
números de factura de forma masiva desde Excel, actualizar un número ya asignado, ejecutar
el cierre de facturación y **reprocesar** los movimientos que fallaron en un cierre
(mecanismo de reintento con su propio seguimiento).

**Ya resuelto — no preguntar.** Cómo se asigna y almacena el número de factura; cómo se
carga masivamente; cómo funciona el cierre de facturación; cómo se reprocesan los
movimientos fallidos.

**Sí preguntar.** Qué regla determina si un negocio es facturable; qué debe pasar cuando la
facturación falla; qué información adicional debe llevar la factura.

---

### 4.5 Cartera

**Qué hace.** Gestiona el recaudo: registrar el dinero que entra, identificar a quién
corresponde, aplicarlo contra los negocios y las primas pendientes, y gestionar los pagos
salientes a terceros (reaseguradores, intermediarios, proveedores).

**Conceptos que maneja.**
- **Recibo de caja**: el ingreso de dinero registrado, con su remitente, banco, moneda y
  soportes.
- **Aplicación (APL)**: la asignación de ese dinero a uno o varios negocios, cuotas o
  conceptos. Un recibo puede aplicarse parcialmente y en varios momentos.
- **Concepto de aplicación**: la naturaleza de lo que se está aplicando.
- **Remitente y alias de remitente**: quién consigna, y las variantes de nombre con que
  aparece en los extractos bancarios, para poder identificarlo automáticamente.
- **Cuenta bancaria**: las cuentas propias donde entra el dinero.
- **Coordenada bancaria de tercero**: los datos bancarios de un tercero al que se le va a
  pagar, con sus soportes documentales y su historial de cambios.
- **Orden de pago**: la instrucción de pago a un tercero.
- **Transferencia por lote**: el archivo que se entrega al banco para pagar varias órdenes
  en una sola operación.
- **Comprobante (voucher)**: el respaldo contable del movimiento.

**Acciones disponibles.** Registrar y modificar recibos de caja (individual y masivo),
comentar y gestionar recibos, anular recibos y aplicaciones con justificación, crear y
consultar aplicaciones, aplicar primas, adjuntar soportes, administrar cuentas bancarias,
alias de remitentes y coordenadas bancarias de terceros, generar y consultar órdenes de
pago, generar los archivos de transferencia para el banco, y emitir reportes y PDF de
recibos y aplicaciones.

**Ya resuelto — no preguntar.** Cómo se registra un recibo; cómo se aplica el dinero; cómo
se anula con justificación; cómo se adjuntan soportes; cómo se identifica al remitente;
cómo se generan las órdenes de pago y los archivos bancarios; cómo se consulta y exporta.

**Sí preguntar.** Qué regla nueva aplica al recaudo o a la aplicación; qué validación debe
impedir aplicar; qué información adicional se necesita del remitente o del tercero; qué
debe pasar con los saldos.

---

### 4.6 Siniestros

**Qué hace.** Registra y hace seguimiento a los siniestros reportados sobre los negocios
vigentes, y a los pagos derivados de ellos.

**Conceptos que maneja.**
- **Siniestro**: el evento reportado, asociado a un negocio.
- **Tipo de proceso de siniestro**: el tratamiento que recibe según la naturaleza del
  riesgo (los formularios cambian según sea un siniestro de vida o de daños/propiedad).
- **Pago de siniestro**: los desembolsos asociados.

**Acciones disponibles.** Buscar el negocio afectado, registrar el siniestro con el
formulario correspondiente a su tipo, adjuntar soportes, registrar y consultar pagos,
consultar el listado filtrado de siniestros y exportar el reporte.

**Ya resuelto — no preguntar.** Cómo se asocia el siniestro al negocio; cómo se adjuntan
soportes; cómo se consultan y exportan.

**Sí preguntar.** Qué campos requiere el tipo de siniestro; qué estados debe recorrer; qué
reglas condicionan el pago; quién autoriza.

---

### 4.7 Cierres

**Qué hace.** Consolida periódicamente la información para entregarla a la contabilidad y
para congelar los datos del período. Un período cerrado no admite movimientos nuevos.

**Tipos de cierre que existen.**
- **Cierre de producción**: consolida los negocios emitidos del período.
- **Cierre contable de producción**: genera la información contable de la producción.
- **Cierre contable de cartera**: consolida el recaudo del período.
- **Cierre de recibos de caja**: congela los recibos incluidos en el período.
- **Cierre de aplicaciones (APL)**: congela las aplicaciones del período.
- **Cierre contable de órdenes de pago**: consolida los pagos del período.
- **Cierre de facturación**: descrito en el módulo de Facturación.

**Acciones disponibles.** Crear y ejecutar el cierre, consultar los cierres anteriores y su
detalle, consultar lo que quedó pendiente por incluir, eliminar o reversar un cierre cuando
el rol lo permite, y generar los archivos e informes del cierre en Excel, PDF o CSV.

**Ya resuelto — no preguntar.** Cómo se ejecuta un cierre; cómo se consulta el histórico;
cómo se generan los archivos del cierre; cómo se reversa.

**Sí preguntar.** Qué debe incluir o excluir el cierre nuevo; con qué periodicidad; qué
regla determina qué queda pendiente; quién puede reversarlo.

---

### 4.8 Parametrización

**Qué hace.** Administra toda la información maestra que alimenta a los demás módulos. Es
la sección que permite que el negocio configure el sistema sin desarrollo.

**Maestros que ya existen.**
- **Técnicos del seguro**: ramo, subramo, cobertura, cobertura base, tipo de cobertura,
  cláusula, cláusula de garantía, deducible y tipo de deducible, sublímite, modalidad, tipo
  de ítem, uso de ítem, unidad, factor de tasa, tipo de endoso y motivo de endoso.
- **Terceros**: cedente, asegurado, intermediario, reasegurador, tercero, contraparte
  ("cobrar a"), corredor, tipo de persona, tipo de documento, tipo de nacionalidad.
- **Comerciales y organizativos**: línea de negocio, línea de negocio por usuario, tipo de
  negocio, estado de negocio, proyecto, director, ejecutivo, subrokeraje, clasificación
  global, país.
- **Financieros**: moneda, tasa de cambio por fecha, cuenta bancaria, concepto de
  aplicación, plan de cuentas contable.
- **Regulatorios**: licencia internacional, estados de licencia y sus comentarios.
- **Administración del sistema**: menús, menús por rol, permisos, permisos por rol, roles,
  usuarios y personas, parámetros generales, casos de bitácora, reportes habilitados.

**Ya resuelto — no preguntar.** Cómo se administra un maestro; cómo se activa o desactiva;
si existe pantalla de mantenimiento; cómo se consulta.

**Sí preguntar.** Si el requerimiento necesita un **maestro nuevo** o un **campo nuevo** en
uno existente, y qué reglas gobiernan su uso.

---

### 4.9 Interfaces contables

**Qué hace.** Comunica a HR Broker con el sistema contable corporativo. Genera los archivos
de salida con los movimientos de producción, cartera y pagos, y recibe y valida archivos de
entrada provenientes del sistema contable.

**Acciones disponibles.** Generar y descargar las interfaces de producción, de cartera y de
aplicaciones; cargar archivos de interfaz de entrada; validar el archivo antes de
procesarlo; consultar el histórico de interfaces cargadas y generadas; y administrar la
equivalencia entre los conceptos del sistema y el plan de cuentas contable.

**Ya resuelto — no preguntar.** Cómo se genera el archivo; en qué formato se entrega; cómo
se carga y valida; cómo se consulta el histórico.

**Sí preguntar.** Qué movimiento nuevo debe reflejarse en la interfaz y contra qué cuenta
contable.

---

### 4.10 Reportes

**Qué hace.** Entrega información consolidada para gestión y control. Todos los reportes
siguen el mismo patrón: filtros, consulta en pantalla y exportación a Excel.

**Reportes que ya existen.** Producción; producción por rol y usuario; cartera; cartera y
producción combinada; primas; estado de primas; valor asegurado; subrokeraje; aplicaciones;
recibos de caja; tasas de cambio; asegurados; cedentes; intermediarios; reaseguradores;
matriz de usuarios y roles; métricas de autorización; sinergia retail.

**Ya resuelto — no preguntar.** Cómo se filtra, se consulta o se exporta un reporte; si se
puede bajar a Excel; cómo se controla quién lo ve.

**Sí preguntar.** Qué columnas debe tener el reporte nuevo; con qué filtros; qué cálculo o
agrupación necesita; quién debe poder verlo.

---

## 5. Preguntas que NO debes hacer

| Si ibas a preguntar… | Asume esto y sigue |
|---|---|
| ¿Cómo deben funcionar los permisos? | Ya existen permisos por rol; solo identifica **qué roles** participan. |
| ¿Quién puede acceder a esta pantalla? | Se define asignando el permiso al rol. Pregunta solo el rol funcional. |
| ¿Cómo se autentica el usuario? | Usuario y contraseña con segundo factor. Ya existe. |
| ¿Dónde va la opción en el menú? | El menú se configura por rol. Ya existe. |
| ¿Se debe registrar quién hizo el cambio? | Sí, siempre. Ya existe trazabilidad automática. |
| ¿Cómo se consulta el historial de cambios? | Ya existe bitácora e historial de estados. |
| ¿Se pueden adjuntar archivos? | Sí. Ya existe. Pregunta solo **qué documentos** y si son obligatorios. |
| ¿En qué formato se exporta? | Excel, PDF o CSV según el caso. Ya existe. Pregunta las **columnas**. |
| ¿Se puede cargar información masivamente? | Sí, desde Excel con validación. Ya existe. |
| ¿Cómo se muestran los errores al usuario? | Mensaje de negocio claro que detiene la operación. Ya existe. |
| ¿Cómo se manejan varias monedas? | Multimoneda con tasa por fecha. Ya existe. |
| ¿La tabla tiene filtros, paginación y orden? | Sí, es el patrón estándar. Pregunta solo **por qué criterios** filtrar. |
| ¿Cómo se envían las notificaciones? | Por correo. Ya existe. Pregunta **a quién, cuándo y qué dice**. |
| ¿Se puede eliminar el registro? | Los maestros se **desactivan**, no se borran. Ya existe. |
| ¿Cómo se despliega o se activa la funcionalidad? | Existe activación por configuración. No es parte del requerimiento. |
| ¿Qué tecnología / base de datos / arquitectura se usa? | No es competencia del usuario funcional. No preguntar. |
| ¿Cómo se integra con el sistema contable? | Ya existen interfaces de entrada y salida. Pregunta solo **qué movimiento** nuevo se refleja. |

---

## 6. Preguntas que SÍ debes hacer

Estas son las únicas que el usuario puede responder y sin las cuales el caso de uso queda
incompleto:

1. **Objetivo de negocio.** ¿Qué problema resuelve? ¿Qué pasa hoy sin esto?
2. **Actor.** ¿Qué rol ejecuta la acción? ¿Alguien más participa o aprueba?
3. **Módulo afectado.** ¿En qué parte del sistema ocurre? (usa los nombres de la sección 4)
4. **Disparador.** ¿Cuándo se ejecuta? ¿Manual, por período, por un evento previo?
5. **Datos.** ¿Qué información nueva se necesita capturar o mostrar?
6. **Reglas de negocio.** ¿Qué debe validarse? ¿Qué debe impedir que la operación continúe?
7. **Casos alternos.** ¿Qué pasa si falla, si se rechaza, si el dato no existe?
8. **Efecto sobre lo existente.** ¿Cambia algo que hoy funciona de otra forma?
9. **Volumen y frecuencia.** ¿Cuántos registros? ¿Con qué periodicidad?
10. **Criterio de aceptación.** ¿Cómo sabrá el usuario que quedó bien hecho?

---

## 7. Glosario del negocio

| Término | Significado |
|---|---|
| **Negocio** | La operación de seguro o reaseguro completa que administra el sistema. |
| **Ramo / Subramo** | Clasificación del tipo de seguro (daños, vida, transporte, etc.) y su subdivisión. |
| **Cedente** | La aseguradora o entidad que cede el riesgo al reaseguro. |
| **Asegurado** | La persona o empresa titular del riesgo cubierto. |
| **Intermediario** | El corredor o agente que participa comercialmente en el negocio. |
| **Reasegurador** | La entidad que asume una parte del riesgo. |
| **Corredor / Subrokeraje** | Intermediación adicional en la colocación del riesgo. |
| **Colocación** | El reparto del riesgo entre la retención propia y los participantes. |
| **Proporcional** | Reparto por porcentaje del riesgo total. |
| **No proporcional / Capa** | Reparto por tramos de exceso de pérdida; cada tramo es una capa. |
| **Facultativo** | Colocación individual y negociada de un riesgo específico. |
| **Ítem** | Cada bien o riesgo asegurado dentro del negocio. |
| **Cobertura** | Lo que ampara la póliza sobre un ítem. |
| **Deducible** | Monto o porcentaje que asume el asegurado antes de la indemnización. |
| **Sublímite** | Tope máximo de indemnización para una cobertura específica. |
| **Cláusula** | Condición particular pactada en el contrato. |
| **Prima** | El valor que paga el asegurado por la cobertura. |
| **Comisión** | La remuneración del corredor y demás participantes. |
| **Concepto** | Valores adicionales asociados al negocio (gastos, impuestos, recargos). |
| **Cuota** | Fracción en que se divide el pago de la prima. |
| **Endoso** | Modificación de un negocio ya emitido: prórroga, cambio, movimiento o anulación. |
| **Renovación** | Nuevo negocio generado a partir de uno que está por vencer. |
| **Recibo de caja** | Registro del dinero recibido. |
| **Aplicación (APL)** | Asignación del dinero recibido a los negocios y conceptos que corresponde. |
| **Remitente** | Quien realiza la consignación o el pago. |
| **Coordenada bancaria** | Los datos bancarios de un tercero para poder pagarle. |
| **Orden de pago** | Instrucción de pago a un tercero. |
| **Comprobante / Voucher** | Respaldo contable del movimiento. |
| **Cierre** | Consolidación y congelamiento de un período. |
| **Interfaz contable** | Archivo de intercambio con el sistema contable corporativo. |
| **Siniestro** | El evento reportado que activa la cobertura. |
| **Licencia internacional** | Autorización regulatoria para operar riesgos en el exterior, con vigencia y seguimiento. |
| **Bitácora** | Registro de comentarios y soportes sobre un registro del sistema. |
| **Parametrización** | La configuración de información maestra que alimenta los módulos. |

---

## 8. Cómo usar este contexto al generar el caso de uso

1. Identifica el **módulo** de la sección 4 al que pertenece el requerimiento.
2. Lee su bloque **"Ya resuelto — no preguntar"** y descarta esas preguntas.
3. Haz únicamente las preguntas de la **sección 6** más las del bloque **"Sí preguntar"**
   del módulo.
4. Al redactar el caso de uso, incluye como **precondiciones** las capacidades existentes
   que aplican (usuario autenticado con rol y permiso, trazabilidad activa, adjuntos
   disponibles, etc.) en lugar de describirlas como funcionalidad nueva.
5. Usa el vocabulario del **glosario**.
6. Si el requerimiento contradice algo de este documento, **señálalo explícitamente** al
   usuario: significa que se está pidiendo cambiar un comportamiento existente, y eso debe
   quedar declarado en el caso de uso.
