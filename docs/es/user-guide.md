# Guía del usuario de Luna Player

Luna Player es un reproductor de audio y video para Windows pensado para usarse con el teclado. Combina la reproducción de medios locales, transmisiones de red, reproducción y descarga de YouTube, grabación de audio, administración de archivos, marcadores, anuncios por voz y atajos locales y de todo el sistema configurables.

Esta guía describe la versión actual de Luna Player. Los nombres de menús y los atajos que aparecen a continuación usan la interfaz en español y la configuración de atajos predeterminada.

[TOC]

## 1. Antes de comenzar

### Requisitos del sistema

Luna Player requiere Windows 10 de 64 bits versión 1809 o posterior. Capturar el sonido de un programa específico requiere Windows 10 versión 2004 o posterior. Las demás funciones de reproducción y grabación siguen disponibles en versiones compatibles más antiguas.

### Ediciones instalada y portátil

Las versiones de Luna Player están disponibles como instalador y como archivo ZIP portátil.

- Para instalar Luna Player, ejecute el instalador de la versión y siga sus páginas. El instalador puede crear accesos directos en el menú Inicio y en el escritorio, y puede registrar los tipos de medios compatibles.
- Para usar la edición portátil, extraiga el archivo ZIP completo en una carpeta y ejecute `LunaPlayer.exe`. Mantenga juntos todos los archivos y carpetas extraídos.

Las dos ediciones usan la misma carpeta de configuración del usuario. El actualizador integrado reconoce qué edición se está ejecutando y descarga el instalador o el archivo portátil correspondiente.

### Inicio de Luna Player

Puede iniciar Luna Player directamente, abrir con él un archivo compatible desde Windows o pasarle archivos y carpetas desde la línea de comandos. Luna se ejecuta como instancia única: al abrir más medios mientras ya está en ejecución, esos medios se envían a la ventana existente.

Si **Recordar la posición del último archivo** está habilitado, al iniciar Luna sin otro archivo se restaura el último archivo local y su última posición de reproducción, siempre que ese archivo siga disponible.

## 2. Interfaz y accesibilidad

### Ventana principal

La ventana principal contiene una barra de menús y cinco botones:

- **Anterior** pasa al elemento anterior.
- **Retroceder** retrocede la cantidad de desplazamiento seleccionada.
- **Reproducir** o **Pausar** cambia el estado de reproducción.
- **Avanzar** avanza la cantidad de desplazamiento seleccionada.
- **Siguiente** pasa al elemento siguiente.

Todos los comandos también están disponibles desde un menú o un atajo. Los elementos que no se pueden aplicar a los medios actuales aparecen deshabilitados. Por ejemplo, las propiedades del archivo están disponibles para un archivo local pero no para una transmisión web, y el menú **Opciones de video** solo se habilita para un video de YouTube activo.

### Navegación con el teclado

Use la navegación estándar de Windows en toda la aplicación:

- Presione <kbd>Alt</kbd> para acceder a la barra de menús y luego use las teclas de flecha y <kbd>Intro</kbd>.
- Presione <kbd>Tab</kbd> y <kbd>Mayús</kbd>+<kbd>Tab</kbd> para moverse entre los controles de los cuadros de diálogo.
- Use las teclas de flecha para desplazarse por listas y opciones.
- Presione <kbd>Intro</kbd> para activar el elemento seleccionado donde un cuadro de diálogo lo admita.
- Presione <kbd>Esc</kbd> para cerrar la mayoría de los cuadros de diálogo sin aplicar ningún cambio.
- Presione <kbd>F1</kbd> en la ventana principal para abrir esta guía.

En un control de Preferencias, <kbd>F1</kbd> tiene otra función: Luna lee en voz alta la ayuda contextual de ese control. Si el foco está en el árbol de categorías, explica cómo moverse entre las páginas de configuración.

### Anuncios por voz

Luna informa por medio de un lector de pantalla compatible el estado de la reproducción, el tiempo, el volumen, la navegación, las operaciones con archivos y otros cambios. Hay dos niveles de detalle disponibles:

- **Principiante** usa mensajes completos y explicativos.
- **Avanzado** usa confirmaciones más breves.

Presione <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>V</kbd> para cambiar el nivel de detalle de inmediato, o elíjalo en la página **General** de Preferencias. Habilite **Leer el nombre del archivo al navegar (Anterior/Siguiente)** si desea que Luna anuncie cada elemento al que se llega con Anterior o Siguiente.

### Controles multimedia de Windows

Luna se integra con la superposición multimedia de Windows y con las teclas multimedia de hardware compatibles. La superposición puede mostrar el título actual y la línea de tiempo. Los botones de reproducir, pausar, anterior, siguiente, retroceder y avance rápido invocan los comandos correspondientes de Luna.

## 3. Inicio rápido

Para reproducir un archivo local:

1. Presione <kbd>Ctrl</kbd>+<kbd>O</kbd>.
2. Elija un archivo multimedia o una lista de reproducción M3U/M3U8.
3. Presione <kbd>Espacio</kbd> para pausar o reanudar.
4. Presione <kbd>Flecha izquierda</kbd> o <kbd>Flecha derecha</kbd> para avanzar o retroceder.
5. Presione <kbd>Flecha arriba</kbd> o <kbd>Flecha abajo</kbd> para cambiar el volumen.

Para reproducir todos los archivos compatibles de una carpeta, presione <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>O</kbd> y elija la carpeta. Use <kbd>Tab</kbd> y <kbd>Mayús</kbd>+<kbd>Tab</kbd> para moverse entre los elementos cargados.

Para reproducir un video de YouTube, presione <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>Y</kbd> e ingrese su dirección. Para buscar en cambio, presione <kbd>Ctrl</kbd>+<kbd>Y</kbd>.

## 4. Apertura de medios

### Apertura de un archivo

Elija **Archivo > Abrir archivo...** o presione <kbd>Ctrl</kbd>+<kbd>O</kbd>. El cuadro de diálogo acepta archivos de audio, video, M3U y M3U8 compatibles.

La opción **Qué desea abrir con los archivos?** controla lo que sucede cuando se abre un archivo multimedia desde Windows o desde el cuadro de diálogo de archivos:

- **Abrir solo el archivo** carga únicamente el archivo seleccionado.
- **Abrir el archivo y los archivos de la carpeta principal** carga los archivos compatibles de la misma carpeta y selecciona el archivo solicitado.
- **Abrir el archivo con los archivos de la carpeta principal y las subcarpetas** analiza la carpeta y todas las carpetas inferiores, carga todos los archivos multimedia compatibles que encuentre y selecciona el archivo solicitado.

Un análisis recursivo muestra un cuadro de diálogo de progreso cancelable e informa cuántos archivos multimedia ha encontrado. Los archivos se ordenan según el orden natural de Windows.

Cuando se envían varios archivos juntos a Luna y está seleccionada la opción **Abrir solo el archivo**, Luna carga todos esos archivos seleccionados explícitamente en lugar de descartar todos menos uno.

### Apertura de una carpeta

Elija **Archivo > Abrir carpeta...** o presione <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>O</kbd>.

Con cualquiera de los dos primeros modos de apertura de archivos, Luna carga los medios compatibles que están directamente dentro de la carpeta elegida. Con el modo de subcarpetas, realiza un análisis recursivo. Las carpetas inaccesibles y del sistema se omiten durante un análisis recursivo.

### Apertura de una transmisión de red

Elija **Archivo > Abrir enlace...** o presione <kbd>Ctrl</kbd>+<kbd>L</kbd>. Ingrese una dirección `http` o `https`.

Use este comando para transmisiones multimedia directas y listas de reproducción M3U o M3U8 remotas. Use **Abrir enlace de YouTube...** para direcciones de videos y listas de reproducción de YouTube.

### Apertura de listas de reproducción

Luna admite listas de reproducción M3U y M3U8 locales y remotas. Las entradas relativas de una lista de reproducción local se resuelven a partir de la carpeta de la lista; las entradas relativas de una lista de reproducción de red se resuelven a partir de su dirección.

Un manifiesto HLS M3U8 se trata como una sola transmisión y se pasa al motor de reproducción. No se expande en una lista de segmentos de medios. Las demás listas de reproducción se convierten en una lista de archivos abiertos que contiene sus rutas locales reproducibles y sus enlaces web.

### Apertura de medios desde el portapapeles

Copie un archivo o una carpeta en el Explorador de archivos, vuelva a Luna y presione <kbd>Ctrl</kbd>+<kbd>V</kbd>. Luna abre la primera ruta existente del portapapeles según el modo de apertura de archivos configurado.

### Asociaciones de archivos

El instalador puede registrar Luna para los tipos de archivo compatibles. También puede usar **Preferencias > General > Registrar extensiones de archivo** o **Anular el registro de extensiones de archivo**.

El registro agrega Luna a la lista de aplicaciones de Windows para los medios compatibles. Es posible que Windows todavía le exija seleccionar Luna en **Configuración > Aplicaciones > Aplicaciones predeterminadas** para que se convierta en el reproductor predeterminado.

## 5. Controles de reproducción

### Reproducir y pausar

Presione <kbd>Espacio</kbd> o <kbd>Intro</kbd>, elija **Reproductor > Reproducir/Pausar**, o use el botón Reproducir/Pausar de la ventana principal.

Si un elemento terminado o descargado se puede volver a abrir, Reproducir/Pausar lo vuelve a cargar. En el nivel Principiante, Luna anuncia Reproducir o Pausar; el nivel Avanzado evita estos mensajes de estado más largos.

### Avanzar y retroceder

Presione <kbd>Flecha izquierda</kbd> o <kbd>Flecha derecha</kbd> para moverse un paso de desplazamiento. Hay movimientos mayores disponibles sin cambiar el paso seleccionado:

- <kbd>Mayús</kbd>+<kbd>Flecha izquierda</kbd> o <kbd>Mayús</kbd>+<kbd>Flecha derecha</kbd> se mueve dos pasos.
- <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>Flecha izquierda</kbd> o <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>Flecha derecha</kbd> se mueve cuatro pasos.
- <kbd>Inicio</kbd> se mueve al principio.
- <kbd>Fin</kbd> se mueve al final.

La cantidad de desplazamiento predeterminada es de 5 segundos. Elija otro valor en **Reproductor > Cantidad de desplazamiento**, o presione <kbd>Mayús</kbd> más un número:

| Atajo | Cantidad de desplazamiento |
| --- | --- |
| <kbd>Mayús</kbd>+<kbd>1</kbd> | 1 segundo |
| <kbd>Mayús</kbd>+<kbd>2</kbd> | 5 segundos |
| <kbd>Mayús</kbd>+<kbd>3</kbd> | 10 segundos |
| <kbd>Mayús</kbd>+<kbd>4</kbd> | 20 segundos |
| <kbd>Mayús</kbd>+<kbd>5</kbd> | 30 segundos |
| <kbd>Mayús</kbd>+<kbd>6</kbd> | 1 minuto |
| <kbd>Mayús</kbd>+<kbd>7</kbd> | 2 minutos |
| <kbd>Mayús</kbd>+<kbd>8</kbd> | 3 minutos |
| <kbd>Mayús</kbd>+<kbd>9</kbd> | 5 minutos |
| <kbd>Mayús</kbd>+<kbd>0</kbd> | 10 minutos |
| <kbd>Mayús</kbd>+<kbd>-</kbd> | Valor personalizado de las Preferencias de audio |

La cantidad seleccionada se guarda de inmediato. Cambiar **Valor de desplazamiento personalizado (segundos)** no selecciona automáticamente el valor personalizado; selecciónelo desde el menú o con <kbd>Mayús</kbd>+<kbd>-</kbd> cuando desee usarlo.

### Ir a un tiempo

Elija **Reproductor > Ir al tiempo...** o presione <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>G</kbd>. Ingrese las horas, los minutos y los segundos que sean pertinentes para la duración actual. Luna impide que la posición seleccionada supere el final del archivo.

### Saltar por porcentaje

Elija **Reproductor > Saltar al porcentaje** o use los atajos de porcentaje predeterminados:

- <kbd>Ctrl</kbd>+<kbd>1</kbd> hasta <kbd>Ctrl</kbd>+<kbd>9</kbd> saltan del 10% al 90%.
- <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>1</kbd> hasta <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>9</kbd> saltan del 15% al 95%.
- <kbd>Ctrl</kbd>+<kbd>0</kbd> o <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>0</kbd> salta al 100%.

Los saltos por porcentaje requieren una duración conocida y, por lo tanto, no están disponibles en algunas transmisiones en vivo.

### Obtención de información de reproducción

Luna puede leer en voz alta información de reproducción sin mover el foco:

- <kbd>E</kbd> lee el tiempo transcurrido.
- <kbd>R</kbd> lee el tiempo restante.
- <kbd>T</kbd> lee la duración total.
- <kbd>P</kbd> lee el porcentaje actual.
- <kbd>V</kbd> lee el volumen actual.
- <kbd>S</kbd> lee la velocidad actual.
- <kbd>Mayús</kbd>+<kbd>P</kbd> lee el ajuste de tono.
- <kbd>B</kbd> lee el valor del balance izquierdo/derecho.

### Nombre de archivo, ruta y título del medio

Presione <kbd>F</kbd> repetidamente para obtener información progresivamente más detallada sobre el elemento actual:

1. La primera pulsación lee el nombre del archivo, el nombre de la transmisión o el título del video de YouTube.
2. Una segunda pulsación rápida lee la ruta local completa o la dirección de la transmisión.
3. Una tercera pulsación rápida copia esa ruta o dirección al portapapeles.

Si hace una pausa de más de un breve instante, la secuencia vuelve a comenzar con el nombre mostrado.

Presione <kbd>I</kbd> para leer el título almacenado en los metadatos del medio. Es independiente del nombre del archivo; un archivo local puede no tener un título incrustado o puede tener un título distinto de su nombre.

### Volumen

Presione <kbd>Flecha arriba</kbd> o <kbd>Flecha abajo</kbd> para cambiar el volumen según el paso de volumen configurado. El paso predeterminado es de 5 puntos porcentuales.

- <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>Flecha arriba</kbd> establece el volumen en el máximo de Luna.
- <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>Flecha abajo</kbd> establece el volumen en 5%.
- <kbd>V</kbd> lee el volumen actual.

Luna permite la amplificación por encima del 100%, hasta el 2000%. Una amplificación alta puede aumentar el ruido de fondo y la distorsión de la fuente. La normalización dinámica y el limitador pueden ayudar a contener los picos, pero comience con un nivel moderado para proteger su audición y sus equipos.

### Velocidad de reproducción

Presione <kbd>Ctrl</kbd>+<kbd>Flecha arriba</kbd> o <kbd>Ctrl</kbd>+<kbd>Flecha abajo</kbd> para cambiar la velocidad de reproducción. Presione <kbd>Alt</kbd>+<kbd>Y</kbd> para volver a 1x. La velocidad está limitada de 0.5x a 4x, y el paso se puede configurar en la página de Preferencias de audio.

### Tono

Los cambios de tono no modifican la velocidad de reproducción.

- <kbd>Mayús</kbd>+<kbd>Flecha arriba</kbd> sube el tono.
- <kbd>Mayús</kbd>+<kbd>Flecha abajo</kbd> baja el tono.
- <kbd>Alt</kbd>+<kbd>Mayús</kbd>+<kbd>P</kbd> restablece el tono original.
- <kbd>Mayús</kbd>+<kbd>P</kbd> lee el ajuste actual en semitonos.

El tono está limitado a 12 semitonos por encima o por debajo del original. El paso se puede configurar de 0.001 a 12 semitonos.

### Balance estéreo

- <kbd>Ctrl</kbd>+<kbd>Flecha izquierda</kbd> mueve el sonido hacia la izquierda.
- <kbd>Ctrl</kbd>+<kbd>Flecha derecha</kbd> mueve el sonido hacia la derecha.
- <kbd>B</kbd> lee el porcentaje de balance actual.

Cero es el centro, los valores negativos son izquierda y los valores positivos son derecha. El paso de balance se puede configurar de 1 a 100 puntos porcentuales.

### Elección de un dispositivo de salida

Elija **Reproductor > Tarjetas de sonido...** o presione <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>A</kbd>. Seleccione un dispositivo de salida de audio de Windows y presione Aceptar. El dispositivo seleccionado se guarda de inmediato.

## 6. Trabajo con varios archivos

### Anterior, siguiente, primero y último

- <kbd>Mayús</kbd>+<kbd>Tab</kbd> o <kbd>Re Pág</kbd> reproduce el elemento anterior.
- <kbd>Tab</kbd> o <kbd>Av Pág</kbd> reproduce el elemento siguiente.
- <kbd>Ctrl</kbd>+<kbd>Inicio</kbd> reproduce el primer elemento.
- <kbd>Ctrl</kbd>+<kbd>Fin</kbd> reproduce el último elemento.

Si **Volver al principio con varios archivos** está habilitado, Siguiente en el último elemento vuelve al primero y Anterior en el primero vuelve al último. El avance automático sigue la misma regla.

### Ir a un número de archivo

Elija **Reproductor > Ir al archivo...** o presione <kbd>Ctrl</kbd>+<kbd>G</kbd>. Ingrese un número desde 1 hasta la cantidad de elementos cargados.

### Cuadro de diálogo Archivos abiertos

Elija **Archivo > Archivos abiertos...** o presione <kbd>F2</kbd>. El elemento actual aparece seleccionado en la lista.

- Elija un elemento y active **Saltar al seleccionado**, o presione <kbd>Intro</kbd> sobre él, para reproducirlo.
- Active **Información de la lista de reproducción** para calcular la cantidad de archivos, el tamaño total, la duración total, el tiempo transcurrido en la lista de reproducción y el tiempo restante. El análisis es cancelable y el resultado se abre en una ventana de texto de solo lectura.

La lista está diseñada para seguir respondiendo incluso con una cantidad muy grande de archivos cargados.

### Reproducción aleatoria

Elija **Reproductor > Reproducción aleatoria** o presione <kbd>Ctrl</kbd>+<kbd>Z</kbd>. Al habilitar la reproducción aleatoria se genera un orden de reproducción al azar manteniendo seleccionado el elemento actual.

Mientras la reproducción aleatoria está activada, Anterior, Siguiente, Primer archivo, Último archivo, Ir al archivo y el cuadro de diálogo Archivos abiertos operan sobre el orden aleatorio. Al desactivarla se restablece el orden normal de la lista conservando el elemento actual.

### Repetición y comportamiento al final del archivo

Elija **Reproductor > Repetir archivo** o presione <kbd>Ctrl</kbd>+<kbd>R</kbd> para repetir el elemento actual. Esta opción de tiempo de ejecución tiene prioridad mientras está habilitada.

La página de Preferencias de audio también controla la acción normal al final de un archivo:

- **Avanzar al siguiente archivo** inicia el elemento siguiente.
- **Repetir el archivo** inicia de nuevo el mismo elemento.
- **No hacer nada** deja la reproducción al final.

### Recordar posiciones

Dos opciones cumplen propósitos diferentes:

- **Recordar la posición del último archivo** restaura el archivo local activo y el tiempo de reproducción la próxima vez que se inicie Luna.
- **Guardar la posición actual de cada archivo** recuerda una posición independiente para cada elemento local y vuelve a ella cuando se revisita ese elemento.

Si ambas están deshabilitadas, los archivos normalmente comienzan desde el principio.

## 7. Bucles A-B

Un bucle A-B repite una parte del elemento actual.

1. Desplácese hasta el comienzo deseado y presione <kbd>[</kbd>. Esto establece el punto A.
2. Desplácese hasta una posición posterior y presione <kbd>]</kbd>. Esto establece el punto B y comienza el bucle de A a B.
3. Presione <kbd>Retroceso</kbd> para borrar el bucle y continuar desde el punto B.

Mientras un bucle está activo, el desplazamiento normal queda limitado al rango seleccionado. <kbd>Inicio</kbd> va al punto A y <kbd>Fin</kbd> va al punto B. El punto B debe ser posterior al punto A.

La selección se aplica al elemento actual y se borra cuando la reproducción pasa a otro elemento.

## 8. Eliminación de silencios y procesamiento de audio

### Activación o desactivación de la eliminación de silencios

Elija **Reproductor > Habilitar el filtro de eliminación de silencios** o presione <kbd>Ctrl</kbd>+<kbd>M</kbd>. Luna guarda de inmediato el estado de activado/desactivado.

La eliminación de silencios acorta las secciones silenciosas mientras se reproducen los medios. No modifica el archivo original. Los resultados dependen en gran medida de la fuente: un umbral agresivo puede tratar el habla o la música suaves como silencio.

### Configuración básica de silencios

Abra **Preferencias > Eliminación de silencios**.

- **Duración mínima del silencio (segundos)** controla cuánto debe durar una sección silenciosa para poder acortarse. Aumentarla conserva más pausas cortas.
- **Umbral de silencio** es el nivel, en decibeles, por debajo del cual el audio se considera silencio. Un valor menos negativo, como -20, trata el audio más fuerte como silencio; un valor más negativo, como -50, limita la eliminación al audio más silencioso.

Los valores predeterminados son 0.5 segundos y -30 dB.

### Configuración avanzada de silencios

Habilite **Mostrar configuración avanzada** para configurar con mayor precisión el filtro de eliminación de silencios subyacente:

- **Partes silenciosas iniciales a recortar**: 0 conserva el comienzo; 1 elimina el silencio inicial hasta encontrar un sonido sostenido. Los valores más altos continúan a través de más secciones de sonido.
- **Sonido requerido antes de que termine el recorte inicial**: cuánto tiempo debe permanecer el audio por encima del umbral antes de que Luna lo conserve.
- **Partes silenciosas a recortar después de que comienza el audio**: -1 acorta todas las pausas posteriores que cumplan los requisitos, 0 conserva el silencio posterior y un valor positivo limita la cantidad que se acorta.
- **Duración mínima del silencio interno**: la pausa mínima que cumple los requisitos después de que el sonido ha comenzado.
- **Pausa a conservar después del silencio recortado**: retiene una parte de cada pausa para que las palabras no se junten.
- **Tamaño de la ventana de detección**: el intervalo usado en cada medición de volumen. Las ventanas más grandes son más estables; las más pequeñas reaccionan más rápido.
- **Modo de detección**: Pico reacciona a la muestra más fuerte y a los sonidos breves; RMS usa la energía promedio para una detección más suave.

Haga cambios pequeños y pruébelos con material representativo. El filtro afecta la reproducción de inmediato después de aceptar las Preferencias.

### Normalización, limitación y mono

La página de Preferencias de audio contiene dos filtros adicionales:

- **Habilitar normalización dinámica y limitador** uniforma el volumen cambiante y contiene los picos para reducir la saturación.
- **Reproducir el audio en mono** combina los canales izquierdo y derecho en una única salida mono.

Ninguna de las dos opciones modifica el archivo de origen.

### Ecualizador

El ecualizador realza y atenúa rangos de frecuencia individuales. Elija **Reproductor > Ecualizador** y seleccione una entrada de la lista. El cambio surte efecto de inmediato y se recuerda entre sesiones.

La lista comienza con **Desactivado**, seguido de seis ajustes preestablecidos para problemas de escucha comunes:

- **Aumento de graves** y **Reducción de graves** suben o bajan los graves. El efecto es más fuerte por debajo de 100 Hz, sigue siendo considerable a 170 Hz y se desvanece a los 600 Hz, por lo que abarca el rango que ocupan un bombo y un bajo eléctrico sin engrosar las voces.
- **Aumento de agudos** sube todo desde aproximadamente 4 kHz hacia arriba.
- **Auriculares** añade peso a los graves, suaviza el rango de 3.5 kHz donde los auriculares suenan estridentes y abre la parte alta.
- **Voz** elimina el retumbo y el sonido encajonado y realza el rango de presencia, que es lo que hace que las consonantes se distingan. Es adecuado para audiolibros, podcasts y conferencias más que para música.
- **Sonoridad** sube ambos extremos y deja intacto el centro, para escuchar a volumen bajo, donde el oído es menos sensible a ellos.

Luego vienen los ajustes preestablecidos por género: Clásica, Club, Dance, En vivo, Pop, Reggae, Rock, Ska, Suave, Rock suave y Techno. **Personalizado** es el último. No cambia nada hasta que usted lo edite, y es el punto de partida para sus propios ajustes preestablecidos.

Un ajuste preestablecido que realza un rango hace que el sonido sea más fuerte y no solo diferente, y ningún rango se atenúa para hacer que el cambio parezca mayor. Por lo tanto, el material que ya se masterizó cerca del límite puede saturarse. **Habilitar normalización dinámica y limitador** en la página de Preferencias de audio evita esto y está activado de forma predeterminada; si lo desactiva, baje el volumen en su lugar.

**Desactivado** aplana todas las bandas en lugar de quitar el ecualizador. El ajuste preestablecido que usó por última vez se recuerda mientras el ecualizador está desactivado, por lo que al seleccionarlo de nuevo se vuelve a él.

### Edición de un ajuste preestablecido

Elija **Reproductor > Ecualizador > Editar el ajuste preestablecido actual** para abrir el ajuste al que está configurado el ecualizador. Todos los ajustes preestablecidos se pueden editar, incluidos los que suministra Luna.

El cuadro de diálogo contiene un grupo por banda: un filtro shelf de graves, quince bandas de 25 Hz a 16 kHz y un filtro shelf de agudos. Cada grupo contiene tres campos.

- **Frecuencia** es el centro de la banda, o el punto donde gira un filtro shelf.
- **Ganancia** es cuánto se sube o se baja la banda, en decibeles, de -20 a 20.
- **Q** es el ancho de la banda, de 0.1 a 10. Los valores más grandes son más estrechos.

Mientras el ajuste preestablecido que se edita es el que está en uso, cada cambio se aplica a medida que escribe, de modo que puede juzgarlo de oído antes de conservarlo. **Guardar** conserva el resultado; **Cancelar** restablece el ajuste preestablecido tal como estaba.

**Ajuste preestablecido base** completa todas las bandas a partir de otro ajuste preestablecido. Úselo para empezar desde algo cercano a lo que desea. **Restablecer valores predeterminados** devuelve las bandas a la forma en que se suministró el ajuste preestablecido elegido allí.

Al editar un ajuste preestablecido que suministra Luna, solo se registran los valores que usted cambió, de modo que las bandas que deje intactas siguen a Luna y las mejoras posteriores de ese ajuste preestablecido siguen llegándole. Al restablecer, se descartan esos cambios. El nombre de un ajuste preestablecido suministrado no se puede cambiar.

### Administración de ajustes preestablecidos

Elija **Reproductor > Ecualizador > Administrar ajustes preestablecidos** para ver la lista completa.

- **Nuevo** crea un ajuste preestablecido, a partir del ajuste base que elija en el editor.
- **Editar** abre el ajuste preestablecido seleccionado.
- **Eliminar** lo quita después de una confirmación.
- **Cerrar** sale de la lista.

Editar y Eliminar se aplican a sus propios ajustes preestablecidos y están ocultos mientras está seleccionado uno que suministra Luna; use **Editar el ajuste preestablecido actual** para esos. Para copiar un ajuste preestablecido, cree uno nuevo y elija el original como base.

Los ajustes preestablecidos que cree aparecen en el menú Ecualizador junto con los demás y se almacenan en `equalizer.json`, junto al archivo de configuración.

## 9. Administración de archivos locales

Los comandos de cambio de nombre, eliminación, carpeta contenedora y propiedades de Windows solo funcionan con archivos locales. No están disponibles para transmisiones de red ni para medios de YouTube.

### Mostrar el archivo en Windows

- Presione <kbd>Ctrl</kbd>+<kbd>F</kbd> o elija **Archivo > Abrir carpeta contenedora** para abrir el Explorador de archivos con el archivo actual seleccionado.
- Presione <kbd>Alt</kbd>+<kbd>Intro</kbd> o elija **Archivo > Propiedades del archivo...** para abrir el cuadro de diálogo de propiedades de Windows.

### Cambio de nombre de un archivo

Elija **Editar > Cambiar nombre...** o presione <kbd>Mayús</kbd>+<kbd>F2</kbd>. Escriba el nuevo nombre del archivo. Si omite la extensión, Luna conserva la extensión actual. Luna rechaza un nombre vacío o un nombre que ya se use en esa carpeta.

### Eliminación de un archivo

Elija **Editar > Eliminar** o presione <kbd>Mayús</kbd>+<kbd>Supr</kbd>. Confirme el aviso para eliminar de forma permanente el archivo actual del disco y quitarlo de la lista cargada. Luna no mueve el archivo a la Papelera de reciclaje.

Eliminar no es lo mismo que **Cerrar archivo**. Cerrar quita un elemento solo de Luna; eliminar quita el archivo local del disco.

### Copia de un archivo

Presione <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>C</kbd> para colocar el elemento actual en el portapapeles de archivos de Windows. Péguelo en el Explorador de archivos o en otro programa que acepte archivos copiados.

### Cierre de archivos

- Presione <kbd>Ctrl</kbd>+<kbd>W</kbd> para quitar el elemento actual de Luna sin eliminarlo.
- Presione <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>W</kbd> para quitar todos los elementos cargados.

## 10. Archivos marcados y operaciones por lotes

Las marcas le permiten reunir archivos locales mientras navega y luego operar sobre ellos en conjunto.

- Presione <kbd>Ctrl</kbd>+<kbd>K</kbd> para marcar o desmarcar el archivo actual.
- Presione <kbd>Ctrl</kbd>+<kbd>A</kbd> para marcar todos los archivos cargados o desmarcarlos todos cuando ya estén todos marcados.
- Presione <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>K</kbd> para borrar todas las marcas.
- Presione <kbd>K</kbd> para escuchar la cantidad de archivos marcados.

Cuando hay al menos un archivo marcado, el menú **Acciones para archivos marcados** pasa a estar disponible:

- **Copiar a la carpeta...** copia todos los archivos marcados a una carpeta elegida.
- **Mover a la carpeta...** los mueve y quita de Luna las entradas movidas correctamente.
- **Copiar al portapapeles** los coloca en el portapapeles de archivos de Windows.
- **Eliminar** solicita confirmación, elimina de forma permanente los archivos del disco sin usar la Papelera de reciclaje y quita de Luna las entradas procesadas correctamente.

Copiar y mover usan un cuadro de diálogo de progreso cancelable. Luna informa éxito total, éxito parcial, cancelación o error. Si algunos archivos fallan, los archivos procesados correctamente permanecen procesados.

## 11. Marcadores

Los marcadores guardan posiciones de reproducción con nombre para los archivos locales. Se almacenan por archivo y no están disponibles para transmisiones ni videos de YouTube.

### Agregar un marcador

1. Reproduzca o desplácese hasta la posición deseada.
2. Elija **Marcadores > Agregar un marcador nuevo** o presione <kbd>Mayús</kbd>+<kbd>M</kbd>.
3. Acepte el nombre generado basado en el tiempo o escriba un nombre descriptivo.

Los marcadores se ordenan por posición de reproducción. Los primeros diez en ese orden ocupan las ranuras expuestas por los atajos directos.

### Saltar a un marcador numerado

Presione <kbd>Alt</kbd>+<kbd>1</kbd> hasta <kbd>Alt</kbd>+<kbd>9</kbd> para las ranuras de la 1 a la 9. Presione <kbd>Alt</kbd>+<kbd>0</kbd> para la ranura 10. Luna anuncia cuando la ranura solicitada está vacía.

### Administración de marcadores

Elija **Marcadores > Administrar marcadores** o presione <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>M</kbd>. El cuadro de diálogo enumera los nombres y las posiciones de los marcadores del archivo actual.

- **Saltar** mueve la reproducción al marcador seleccionado.
- **Editar** cambia su nombre.
- **Eliminar** lo quita después de una confirmación.
- Al activar un marcador en la lista se salta directamente a él.

Use la página de Preferencias Copia de seguridad y restauración para exportar o importar toda la colección de marcadores.

## 12. YouTube

Las funciones de YouTube requieren una conexión a internet. La disponibilidad puede cambiar cuando YouTube modifica su servicio. Luna incluye un resolvedor integrado y opcionalmente puede usar yt-dlp.

### Apertura de un enlace de YouTube

Elija **Archivo > Abrir enlace de YouTube...** o presione <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>Y</kbd>. Ingrese la dirección de un video o de una lista de reproducción. Este comando no acepta direcciones de canales.

Algunas direcciones de YouTube identifican tanto un video como una lista de reproducción. Luna puede preguntar si desea reproducir el video o abrir la lista de reproducción, o seguir el comportamiento fijo seleccionado en las Preferencias de YouTube.

### Búsqueda en YouTube

Elija **Archivo > Buscar en YouTube...** o presione <kbd>Ctrl</kbd>+<kbd>Y</kbd>. Ingrese el texto de búsqueda y luego desplácese por la lista de resultados.

- Presione <kbd>Intro</kbd> sobre un resultado seleccionado o active **Reproducir** para reproducirlo.
- Active **Descargar** para guardarlo.
- Presione <kbd>Ctrl</kbd>+<kbd>C</kbd> para copiar el enlace del resultado seleccionado.
- Presione <kbd>Ctrl</kbd>+<kbd>B</kbd> para abrirlo en el navegador predeterminado.
- Presione <kbd>Ctrl</kbd>+<kbd>N</kbd> para ir a los resultados del canal de quien lo subió.
- Abra el menú contextual para las mismas acciones de copiar, navegador, canal y descarga.

Cuando la selección llega al final de la página de resultados actual, Luna solicita más resultados automáticamente hasta que YouTube ya no tenga más para devolver.

Las ventanas de resultados de listas de reproducción y canales funcionan de la misma manera. Siguiente avanza por la sesión de resultados de YouTube activa, obteniendo el siguiente video cuando es necesario. Presionar <kbd>Esc</kbd> en la ventana principal después de que comienza la reproducción puede volver a la lista de resultados relacionada cuando esa sesión sigue disponible.

### Reproducción solo de audio y calidad

Abra **Preferencias > YouTube**.

- **Reproducir videos solo como audio** solicita únicamente el sonido, lo que reduce el ancho de banda y a menudo inicia más rápido.
- **Calidad de video** elige Baja, Media o Mejor cuando el video está habilitado.

Las mismas opciones se usan al descargar un video. El modo solo audio guarda el sonido sin imagen; de lo contrario, Luna descarga con la calidad de video seleccionada.

### Opciones de video

Mientras un video de YouTube está activo, el menú **Opciones de video** ofrece:

- **Descargar...** o <kbd>Ctrl</kbd>+<kbd>D</kbd>: elija una carpeta y guarde el video actual con progreso y cancelación.
- **Descripción del video...** o <kbd>Alt</kbd>+<kbd>D</kbd>: obtiene el título y la descripción de quien lo subió y los muestra en una ventana de texto de solo lectura.
- **Copiar enlace del video**: copia la dirección estable de visualización de YouTube en lugar de la dirección temporal de la transmisión multimedia.

Una descarga repetida no sobrescribe un archivo existente con el mismo nombre; Luna elige un nombre numerado sin usar.

### Videos y transmisiones favoritos

Los favoritos guardan enlaces, mientras que los marcadores guardan posiciones dentro de archivos locales. Abra los favoritos con **Archivo > Videos favoritos...** o <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>F</kbd>.

El cuadro de diálogo Favoritos puede abrir, agregar, editar o quitar entradas. Cada entrada tiene un nombre, un enlace y un tipo:

- **Video** requiere un enlace de video de YouTube.
- **Lista de reproducción** requiere un enlace de lista de reproducción de YouTube.
- **Enlace combinado** requiere una dirección que contenga identificadores de video y de lista de reproducción, y abre su video.
- **Transmisión genérica** acepta cualquier dirección de transmisión `http` o `https`.

Luna valida el formato de la dirección cuando se guarda una entrada. Comprueba si el contenido remoto sigue disponible solo cuando usted lo abre.

### Resolvedor integrado y yt-dlp

Luna normalmente usa su resolvedor de YouTube integrado. Si este no puede abrir un video, habilite **Usar yt-dlp para resolver las transmisiones** en la página de Preferencias de YouTube. Luna ofrece descargar el componente requerido si falta.

yt-dlp siempre es necesario para la ruta del resolvedor yt-dlp, pero el resolvedor integrado y otras funciones de YouTube pueden funcionar sin él. La página de componentes ofrece tres canales de actualización:

- **Stable** cambia con menos frecuencia y se recomienda para el uso normal.
- **Nightly** sigue las compilaciones nocturnas de yt-dlp.
- **Master** sigue el código fuente más reciente y puede ser menos confiable.

Use **Descargar componentes de YouTube** en Preferencias para instalar los ejecutables de yt-dlp y Deno que falten. Use **Ayuda > Actualizaciones > Actualizar componentes de YouTube** para actualizar un yt-dlp instalado. Habilite **Buscar actualizaciones de yt-dlp al iniciar** para una comprobación silenciosa en segundo plano que solo muestra un aviso cuando hay una actualización disponible.

## 13. Grabación de audio

Luna puede grabar micrófonos y otras entradas, todo lo que se reproduce a través de un dispositivo de salida, o el audio de un programa en particular. Se pueden mezclar varias fuentes en una sola grabación con niveles de volumen independientes.

### Grabación rápida con el micrófono predeterminado

Si no se han creado fuentes en la sesión actual, presione <kbd>F9</kbd> para grabar el dispositivo de entrada predeterminado de Windows usando las Preferencias de grabación guardadas. Presione <kbd>F7</kbd> para pausar o reanudar, y <kbd>F8</kbd> para detener.

Un tono ascendente confirma que la grabación está comenzando. Un tono descendente confirma que se ha detenido. El inicio y la detención correctos no agregan un mensaje hablado sobre esos tonos.

### Apertura de la interfaz de grabación

Elija **Grabación > Abrir la interfaz de grabación...** o presione <kbd>Alt</kbd>+<kbd>R</kbd>. La ventana tiene una lista de fuentes, controles de fuente, configuración de salida y controles de Iniciar/Pausar.

Cerrar esta ventana no detiene una grabación activa. La grabación pertenece a la aplicación y continúa hasta que use Detener, <kbd>F8</kbd>, o salga de Luna.

### Agregar fuentes

Active **Agregar** y elija:

- **Dispositivo de entrada...** para un micrófono, una entrada de línea u otro dispositivo de captura de Windows.
- **Salida del sistema...** para todo lo que se reproduce a través de un dispositivo de altavoces o auriculares seleccionado.
- **Programa...** para el audio de un solo programa. Esto requiere Windows 10 versión 2004 o posterior.

Dé a la fuente un nombre descriptivo, elija el dispositivo o el programa y establezca su volumen. Una fuente de dispositivo puede seguir el dispositivo predeterminado actual de Windows en lugar de un dispositivo con nombre.

Para una fuente de programa, **Capturar todo excepto esta aplicación** invierte la selección y captura en cambio el audio de otras aplicaciones. Solo aparecen en la lista los programas con una sesión de audio de Windows; inicie audio en el programa de destino si falta.

Use **Editar...** para cambiar la fuente seleccionada y **Quitar...** para eliminarla de la sesión. El control deslizante de volumen de la fuente cambia su nivel en la mezcla final.

Las fuentes son solo de sesión. Permanecen cuando se cierra y se vuelve a abrir la ventana de grabación durante la misma sesión de Luna, pero no se restauran después de que Luna se cierra porque los dispositivos y los identificadores de proceso pueden dejar de representar las mismas fuentes.

### Formato y destino de la grabación

Elija el formato, la frecuencia de muestreo, la cantidad de canales, la calidad donde corresponda y la carpeta de destino:

- **WAV** no tiene compresión y normalmente produce el archivo más grande.
- **FLAC** no tiene pérdida y normalmente es más pequeño que WAV.
- **MP3** y **AAC** tienen pérdida y usan la tasa de bits seleccionada para equilibrar tamaño y calidad.
- **Mono** suele ser suficiente para un micrófono o para voz.
- **Estéreo** conserva canales izquierdo y derecho separados para música y salida del sistema.

Las frecuencias de muestreo, las cantidades de canales y las tasas de bits disponibles se obtienen de los codificadores instalados con Windows. Por lo tanto, las opciones cambian según el formato seleccionado y pueden variar según el sistema. Luna asigna a cada grabación un nombre con marca de tiempo para evitar reemplazar un archivo anterior.

Los cambios realizados dentro de la interfaz de grabación se aplican a su sesión. Para cambiar los valores predeterminados usados después del próximo inicio o por los atajos directos, use **Preferencias > Grabación**.

### Inicio, pausa y detención

La ventana de grabación requiere al menos una fuente configurada para que **Iniciar** pase a estar disponible. Una vez que comienza la grabación, los controles de fuente y de salida se bloquean para que el formato de grabación y la mezcla se mantengan coherentes.

- El botón **Iniciar** pasa a ser **Detener** durante la grabación.
- El botón **Pausar** pasa a ser **Reanudar** mientras está en pausa.
- <kbd>F9</kbd>, <kbd>F7</kbd> y <kbd>F8</kbd> funcionan desde la ventana principal de Luna.
- Los atajos globales correspondientes funcionan incluso cuando otra aplicación tiene el foco.

Si solo se pueden abrir algunas fuentes, Luna comienza con las fuentes disponibles y enumera las que fallaron. Si no se puede abrir ninguna, la grabación no comienza.

Elija **Grabación > Abrir carpeta de grabaciones** para abrir el destino activo. La carpeta predeterminada es `Documents\Luna Player\Recordings`.

## 14. Referencia de Preferencias

Abra las Preferencias con **Archivo > Preferencias...** o <kbd>Ctrl</kbd>+<kbd>P</kbd>. Seleccione una página en el árbol de categorías. Presione Aceptar para validar y aplicar todas las páginas, o Cancelar para descartar los cambios no aceptados.

Presione <kbd>F1</kbd> mientras una opción tiene el foco para escuchar una explicación detallada de esa opción.

### General

| Opción | Finalidad |
| --- | --- |
| Idioma | Usa el idioma de visualización de Windows o una traducción incluida. Reinicie Luna después de cambiarlo. |
| Recordar la posición del último archivo | Restaura el último archivo local activo y su posición en el próximo inicio. |
| Leer el nombre del archivo al navegar | Anuncia el elemento al que se llega con Anterior o Siguiente. |
| Buscar actualizaciones de la aplicación al iniciar | Comprueba en silencio después del inicio y solo muestra un aviso cuando hay una versión más nueva lista. |
| Guardar la configuración al cerrar | Guarda los cambios de la sesión, como el volumen y la velocidad, cuando Luna se cierra. Los cambios de Preferencias aceptados explícitamente se siguen guardando de inmediato. |
| Nivel de detalle | Elige los anuncios completos del nivel Principiante o los anuncios más breves del nivel Avanzado. |
| Qué desea abrir con los archivos? | Controla si un archivo abierto trae consigo ningún archivo vecino, su carpeta, o su carpeta y subcarpetas. |
| Registrar/Anular el registro de extensiones de archivo | Agrega o quita los registros de medios compatibles de Luna para el usuario actual de Windows. |

### Copia de seguridad y restauración

| Comando | Finalidad |
| --- | --- |
| Exportar configuración | Guarda las preferencias aplicadas actualmente en un archivo JSON sin mover la copia de trabajo. |
| Importar configuración | Carga un archivo JSON de configuración válido y reemplaza los valores mostrados en Preferencias. |
| Exportar marcadores | Guarda todos los marcadores en un archivo JSON. |
| Importar marcadores | Reemplaza la colección de marcadores actual por una colección exportada válida. |
| Restablecer configuración | Restaura todas las preferencias y los atajos a sus valores predeterminados después de una confirmación. |
| Abrir la carpeta de configuración del usuario | Abre el directorio de configuración de Luna en el Explorador de archivos. |

La importación reemplaza la colección correspondiente; no combina entradas. Guarde los archivos exportados en un lugar separado de la carpeta de configuración de Luna si están destinados a ser copias de seguridad.

### Audio

| Opción | Finalidad |
| --- | --- |
| Valor de desplazamiento personalizado | Cantidad de segundos usada por la cantidad de desplazamiento Personalizada; se aceptan decimales positivos como 2.5. |
| Paso de velocidad | Cantidad que suma o resta cada comando de velocidad. |
| Paso de tono | Cambio de tono por pulsación, de 0.001 a 12 semitonos. |
| Paso de volumen | Cambio de volumen por pulsación, de 1 a 20 puntos porcentuales. |
| Paso de balance | Movimiento izquierda/derecha por pulsación, de 1 a 100 puntos porcentuales. |
| Qué sucede cuando termina un archivo? | Avanza, repite o se detiene al final. |
| Volver al principio con varios archivos | Conecta el final y el comienzo de una lista de varios elementos. |
| Guardar la posición actual de cada archivo | Mantiene una posición de reanudación independiente para cada elemento local. |
| Habilitar normalización dinámica y limitador | Uniforma el volumen y contiene los picos. |
| Reproducir el audio en mono | Mezcla los canales izquierdo y derecho en mono. |

### Eliminación de silencios

Consulte [Eliminación de silencios y procesamiento de audio](#8-eliminacion-de-silencios-y-procesamiento-de-audio) para ver los controles básicos y avanzados.

### YouTube

| Opción | Finalidad |
| --- | --- |
| Reproducir videos solo como audio | Solicita el sonido sin la imagen. |
| Calidad de video | Elige Baja, Media o Mejor para la reproducción y las descargas de video. |
| Cantidad de resultados de búsqueda | Establece el objetivo inicial de 5 a 100; se cargan más resultados al llegar al final. |
| Comportamiento del enlace de video+lista de reproducción | Pregunta cada vez, siempre reproduce el video o siempre abre la lista de reproducción. |
| Usar yt-dlp para resolver las transmisiones | Usa el componente yt-dlp descargado en lugar del resolvedor integrado de Luna. |
| Canal de actualización de yt-dlp | Elige Stable, Nightly o Master. |
| Buscar actualizaciones de yt-dlp al iniciar | Realiza una comprobación silenciosa del componente después del inicio. |
| Descargar componentes de YouTube | Instala o actualiza yt-dlp y las herramientas de apoyo. |

### Grabación

Esta página establece los valores predeterminados usados por los atajos de grabación directa y alimenta la interfaz de grabación al comienzo de cada sesión de Luna. Contiene el formato de audio, la frecuencia de muestreo, los canales, la calidad de audio para los formatos comprimidos y la carpeta de grabaciones.

### Atajos de teclado

Esta página enumera todas las acciones que se pueden ejecutar mientras la ventana principal de Luna tiene el foco. Cada acción puede tener un atajo principal; algunas acciones también tienen una ranura secundaria predefinida.

1. Seleccione una acción.
2. Active **Editar atajo principal** o, donde esté disponible, **Editar atajo secundario**.
3. Presione la combinación deseada. Presione <kbd>Esc</kbd> para cancelar la captura.
4. Presione Aceptar en Preferencias para aplicar los cambios.

Luna rechaza una combinación ya asignada a otra acción del mismo conjunto de atajos. Los atajos locales pueden usar teclas imprimibles, teclas de navegación, teclas de función de F1 a F24 y los modificadores Ctrl, Mayús o Alt. Use **Restablecer valores predeterminados** para restaurar toda la página de atajos locales después de una confirmación.

### Atajos globales

Los atajos globales funcionan mientras otra aplicación tiene el foco. Se limitan intencionalmente a comandos de reproducción, navegación, volumen y grabación que resultan útiles fuera de Luna.

Seleccione una acción, active **Editar atajo** y presione la combinación deseada. Los atajos globales también pueden usar la tecla de Windows. Windows puede rechazar un atajo ya reservado por el sistema operativo o por otra aplicación; Luna informa los atajos que no pudo registrar.

Los atajos globales son independientes de los atajos locales. Cambiar uno no cambia el otro.

## 15. Actualizaciones de la aplicación

### Comprobación manual

Elija **Ayuda > Actualizaciones > Buscar actualizaciones de la aplicación**. Aparece un cuadro de diálogo de progreso cancelable mientras Luna consulta la información de la versión.

Si hay una versión más nueva lista, Luna muestra las versiones instalada y disponible y una lista desplazable de cambios. Elija **Actualizar** para descargarla o **Más tarde** para dejar sin cambios la versión actual.

Luna verifica que el instalador o el archivo portátil real exista antes de ofrecer la actualización. Si la información de la versión se publicó antes de que su paquete terminara de subirse, una comprobación manual le pide que lo intente de nuevo más tarde en lugar de presentar una descarga que fallará.

### Comprobaciones al iniciar

Habilite **Buscar actualizaciones de la aplicación al iniciar** para una comprobación silenciosa en segundo plano. No muestra ninguna ventana de progreso ni mensaje de que no hay actualizaciones; solo aparece un aviso cuando hay un paquete compatible más nuevo disponible.

### Instalación de una actualización descargada

La ventana de descarga muestra el tamaño total, el tamaño descargado, el porcentaje y un botón Cancelar. Después de una descarga correcta, Luna inicia su pequeño asistente de actualización y se cierra:

- Una copia instalada inicia el instalador de la actualización. Su progreso de instalación permanece visible y Luna se abre cuando termina la instalación.
- Una copia portátil reemplaza los archivos extraídos de la aplicación y abre el reproductor actualizado.

No inicie otro proceso de Luna mientras se aplica la actualización.

## 16. Ayuda e información de versiones

- **Ayuda > Guía del usuario** o <kbd>F1</kbd> abre la guía instalada que coincide con el idioma de la interfaz de Luna. Si no hay instalada una guía en el idioma regional o base correspondiente, Luna abre la guía en inglés.
- **Ayuda > Acerca de** muestra una descripción, la versión, la información de derechos de autor y un enlace al sitio web del proyecto.
- **Ayuda > Notas de la versión** abre la página de versiones de GitHub para la versión instalada en su navegador predeterminado.

La guía del usuario se instala como HTML local y no requiere una conexión a internet. Las notas de la versión y el sitio web del proyecto sí la requieren.

## 17. Configuración y datos del usuario

Luna almacena los datos del usuario en `%APPDATA%\Luna Player`:

| Archivo | Contenido |
| --- | --- |
| `settings.json` | Preferencias y personalizaciones de atajos |
| `bookmarks.json` | Marcadores con nombre de archivos locales |
| `positions.json` | Posiciones de reproducción por archivo |
| `favorites.json` | Enlaces favoritos de YouTube y de transmisiones |

Use **Preferencias > Copia de seguridad y restauración > Abrir la carpeta de configuración del usuario** para abrir la carpeta exacta en lugar de escribir la ruta.

No edite estos archivos mientras Luna se está ejecutando. Use los controles de exportación e importación de la configuración y los marcadores siempre que sea posible.

Si `settings.json` no se puede leer o contiene valores no válidos, Luna se inicia con los valores predeterminados, muestra el motivo y protege el archivo existente para que no se sobrescriba. Importe una copia de seguridad de configuración válida o use **Restablecer configuración** para reemplazarlo deliberadamente. Los archivos de marcadores y favoritos no válidos también se informan en lugar de reemplazarse silenciosamente.

## 18. Referencia completa de atajos predeterminados

Estos son los valores de fábrica. Es posible que sus asignaciones actuales difieran después de la personalización. La lista autorizada para su instalación está en **Preferencias > Atajos de teclado** y **Atajos globales**.

### Archivos, información y Preferencias

| Acción | Atajo principal | Atajo secundario |
| --- | --- | --- |
| Abrir archivos | <kbd>Ctrl</kbd>+<kbd>O</kbd> | — |
| Abrir un enlace de transmisión de red | <kbd>Ctrl</kbd>+<kbd>L</kbd> | — |
| Abrir todos los archivos de una carpeta | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>O</kbd> | — |
| Mostrar el archivo actual en el Explorador de archivos | <kbd>Ctrl</kbd>+<kbd>F</kbd> | — |
| Propiedades del archivo de Windows | <kbd>Alt</kbd>+<kbd>Intro</kbd> | — |
| Cuadro de diálogo Archivos abiertos | <kbd>F2</kbd> | — |
| Cerrar el archivo actual | <kbd>Ctrl</kbd>+<kbd>W</kbd> | — |
| Cerrar todos los archivos | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>W</kbd> | — |
| Preferencias | <kbd>Ctrl</kbd>+<kbd>P</kbd> | — |
| Leer la información del archivo actual | <kbd>F</kbd> | — |
| Leer el título del medio actual | <kbd>I</kbd> | — |
| Salir de Luna | Ninguno | — |

### Navegación por la lista de reproducción y modos

| Acción | Atajo principal | Atajo secundario |
| --- | --- | --- |
| Archivo anterior | <kbd>Mayús</kbd>+<kbd>Tab</kbd> | <kbd>Re Pág</kbd> |
| Archivo siguiente | <kbd>Tab</kbd> | <kbd>Av Pág</kbd> |
| Primer archivo | <kbd>Ctrl</kbd>+<kbd>Inicio</kbd> | — |
| Ir al número de archivo | <kbd>Ctrl</kbd>+<kbd>G</kbd> | — |
| Último archivo | <kbd>Ctrl</kbd>+<kbd>Fin</kbd> | — |
| Activar o desactivar la reproducción aleatoria | <kbd>Ctrl</kbd>+<kbd>Z</kbd> | — |
| Activar o desactivar la repetición del archivo | <kbd>Ctrl</kbd>+<kbd>R</kbd> | — |

### Reproducción y desplazamiento

| Acción | Atajo principal | Atajo secundario |
| --- | --- | --- |
| Reproducir o pausar | <kbd>Espacio</kbd> | <kbd>Intro</kbd> |
| Retroceder un paso | <kbd>Flecha izquierda</kbd> | — |
| Avanzar un paso | <kbd>Flecha derecha</kbd> | — |
| Retroceder dos pasos | <kbd>Mayús</kbd>+<kbd>Flecha izquierda</kbd> | — |
| Avanzar dos pasos | <kbd>Mayús</kbd>+<kbd>Flecha derecha</kbd> | — |
| Retroceder cuatro pasos | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>Flecha izquierda</kbd> | — |
| Avanzar cuatro pasos | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>Flecha derecha</kbd> | — |
| Principio | <kbd>Inicio</kbd> | — |
| Final | <kbd>Fin</kbd> | — |
| Ir al tiempo | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>G</kbd> | — |
| Elegir tarjeta de sonido | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>A</kbd> | — |

### Selección de la cantidad de desplazamiento

| Acción | Atajo |
| --- | --- |
| Seleccionar paso de 1 segundo | <kbd>Mayús</kbd>+<kbd>1</kbd> |
| Seleccionar paso de 5 segundos | <kbd>Mayús</kbd>+<kbd>2</kbd> |
| Seleccionar paso de 10 segundos | <kbd>Mayús</kbd>+<kbd>3</kbd> |
| Seleccionar paso de 20 segundos | <kbd>Mayús</kbd>+<kbd>4</kbd> |
| Seleccionar paso de 30 segundos | <kbd>Mayús</kbd>+<kbd>5</kbd> |
| Seleccionar paso de 1 minuto | <kbd>Mayús</kbd>+<kbd>6</kbd> |
| Seleccionar paso de 2 minutos | <kbd>Mayús</kbd>+<kbd>7</kbd> |
| Seleccionar paso de 3 minutos | <kbd>Mayús</kbd>+<kbd>8</kbd> |
| Seleccionar paso de 5 minutos | <kbd>Mayús</kbd>+<kbd>9</kbd> |
| Seleccionar paso de 10 minutos | <kbd>Mayús</kbd>+<kbd>0</kbd> |
| Seleccionar paso personalizado | <kbd>Mayús</kbd>+<kbd>-</kbd> |

### Saltos por porcentaje

| Posición | Atajo | Posición | Atajo |
| --- | --- | --- | --- |
| 10% | <kbd>Ctrl</kbd>+<kbd>1</kbd> | 15% | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>1</kbd> |
| 20% | <kbd>Ctrl</kbd>+<kbd>2</kbd> | 25% | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>2</kbd> |
| 30% | <kbd>Ctrl</kbd>+<kbd>3</kbd> | 35% | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>3</kbd> |
| 40% | <kbd>Ctrl</kbd>+<kbd>4</kbd> | 45% | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>4</kbd> |
| 50% | <kbd>Ctrl</kbd>+<kbd>5</kbd> | 55% | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>5</kbd> |
| 60% | <kbd>Ctrl</kbd>+<kbd>6</kbd> | 65% | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>6</kbd> |
| 70% | <kbd>Ctrl</kbd>+<kbd>7</kbd> | 75% | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>7</kbd> |
| 80% | <kbd>Ctrl</kbd>+<kbd>8</kbd> | 85% | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>8</kbd> |
| 90% | <kbd>Ctrl</kbd>+<kbd>9</kbd> | 95% | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>9</kbd> |
| 100% | <kbd>Ctrl</kbd>+<kbd>0</kbd> | 100% alternativo | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>0</kbd> |

### Volumen y estado hablado

| Acción | Atajo |
| --- | --- |
| Subir el volumen | <kbd>Flecha arriba</kbd> |
| Bajar el volumen | <kbd>Flecha abajo</kbd> |
| Establecer el volumen al máximo | <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>Flecha arriba</kbd> |
| Establecer el volumen al mínimo | <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>Flecha abajo</kbd> |
| Leer el volumen | <kbd>V</kbd> |
| Leer el tiempo transcurrido | <kbd>E</kbd> |
| Leer el tiempo restante | <kbd>R</kbd> |
| Leer la duración total | <kbd>T</kbd> |
| Leer el porcentaje de posición | <kbd>P</kbd> |
| Leer la velocidad de reproducción | <kbd>S</kbd> |
| Cambiar entre el nivel de detalle Principiante/Avanzado | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>V</kbd> |

### Velocidad, tono, balance, bucles y filtros

| Acción | Atajo |
| --- | --- |
| Aumentar la velocidad de reproducción | <kbd>Ctrl</kbd>+<kbd>Flecha arriba</kbd> |
| Disminuir la velocidad de reproducción | <kbd>Ctrl</kbd>+<kbd>Flecha abajo</kbd> |
| Restablecer la velocidad de reproducción | <kbd>Alt</kbd>+<kbd>Y</kbd> |
| Subir el tono | <kbd>Mayús</kbd>+<kbd>Flecha arriba</kbd> |
| Bajar el tono | <kbd>Mayús</kbd>+<kbd>Flecha abajo</kbd> |
| Restablecer el tono | <kbd>Alt</kbd>+<kbd>Mayús</kbd>+<kbd>P</kbd> |
| Leer el tono | <kbd>Mayús</kbd>+<kbd>P</kbd> |
| Balance a la izquierda | <kbd>Ctrl</kbd>+<kbd>Flecha izquierda</kbd> |
| Balance a la derecha | <kbd>Ctrl</kbd>+<kbd>Flecha derecha</kbd> |
| Leer el balance | <kbd>B</kbd> |
| Activar o desactivar la eliminación de silencios | <kbd>Ctrl</kbd>+<kbd>M</kbd> |
| Establecer el inicio del bucle A-B | <kbd>[</kbd> |
| Establecer el final del bucle A-B | <kbd>]</kbd> |
| Borrar el bucle A-B | <kbd>Retroceso</kbd> |

### Edición y archivos marcados

| Acción | Atajo |
| --- | --- |
| Cambiar el nombre del archivo actual | <kbd>Mayús</kbd>+<kbd>F2</kbd> |
| Eliminar el archivo actual | <kbd>Mayús</kbd>+<kbd>Supr</kbd> |
| Copiar el archivo actual al portapapeles | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>C</kbd> |
| Pegar/abrir un archivo o carpeta desde el portapapeles | <kbd>Ctrl</kbd>+<kbd>V</kbd> |
| Marcar o desmarcar el archivo actual | <kbd>Ctrl</kbd>+<kbd>K</kbd> |
| Marcar o desmarcar todos los archivos | <kbd>Ctrl</kbd>+<kbd>A</kbd> |
| Borrar todas las marcas | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>K</kbd> |
| Leer la cantidad de archivos marcados | <kbd>K</kbd> |
| Copiar los archivos marcados a una carpeta | Ninguno |
| Mover los archivos marcados a una carpeta | Ninguno |
| Copiar los archivos marcados al portapapeles | Ninguno |
| Eliminar los archivos marcados | Ninguno |

### Marcadores

| Acción | Atajo |
| --- | --- |
| Agregar marcador | <kbd>Mayús</kbd>+<kbd>M</kbd> |
| Administrar marcadores | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>M</kbd> |
| Saltar a las ranuras de marcador 1–9 | <kbd>Alt</kbd>+<kbd>1</kbd> hasta <kbd>Alt</kbd>+<kbd>9</kbd> |
| Saltar a la ranura de marcador 10 | <kbd>Alt</kbd>+<kbd>0</kbd> |

### YouTube, grabación, actualizaciones y ayuda

| Acción | Atajo |
| --- | --- |
| Abrir enlace de YouTube | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>Y</kbd> |
| Buscar en YouTube | <kbd>Ctrl</kbd>+<kbd>Y</kbd> |
| Videos favoritos | <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>F</kbd> |
| Descargar el video actual de YouTube | <kbd>Ctrl</kbd>+<kbd>D</kbd> |
| Mostrar la descripción del video actual | <kbd>Alt</kbd>+<kbd>D</kbd> |
| Copiar el enlace del video actual | Ninguno |
| Abrir la interfaz de grabación | <kbd>Alt</kbd>+<kbd>R</kbd> |
| Iniciar la grabación | <kbd>F9</kbd> |
| Pausar o reanudar la grabación | <kbd>F7</kbd> |
| Detener la grabación | <kbd>F8</kbd> |
| Abrir la carpeta de grabaciones | Ninguno |
| Abrir la guía del usuario | <kbd>F1</kbd> |
| Acerca de Luna | Ninguno |
| Abrir las notas de la versión | Ninguno |
| Buscar actualizaciones de la aplicación | Ninguno |
| Actualizar componentes de YouTube | Ninguno |

### Atajos globales predeterminados

Estos comandos funcionan en todo el sistema mientras Luna se está ejecutando, incluso si otro programa tiene el foco.

| Acción | Atajo global |
| --- | --- |
| Reproducir o pausar | <kbd>Win</kbd>+<kbd>Alt</kbd>+<kbd>Espacio</kbd> |
| Retroceder un paso | <kbd>Win</kbd>+<kbd>Alt</kbd>+<kbd>Flecha izquierda</kbd> |
| Avanzar un paso | <kbd>Win</kbd>+<kbd>Alt</kbd>+<kbd>Flecha derecha</kbd> |
| Subir el volumen | <kbd>Win</kbd>+<kbd>Alt</kbd>+<kbd>Flecha arriba</kbd> |
| Bajar el volumen | <kbd>Win</kbd>+<kbd>Alt</kbd>+<kbd>Flecha abajo</kbd> |
| Archivo anterior | <kbd>Win</kbd>+<kbd>Alt</kbd>+<kbd>Re Pág</kbd> |
| Archivo siguiente | <kbd>Win</kbd>+<kbd>Alt</kbd>+<kbd>Av Pág</kbd> |
| Iniciar la grabación | <kbd>Win</kbd>+<kbd>Alt</kbd>+<kbd>F9</kbd> |
| Pausar o reanudar la grabación | <kbd>Win</kbd>+<kbd>Alt</kbd>+<kbd>F7</kbd> |
| Detener la grabación | <kbd>Win</kbd>+<kbd>Alt</kbd>+<kbd>F8</kbd> |

## 19. Formatos compatibles

Luna filtra la selección de archivos y carpetas usando las siguientes extensiones. La reproducción la gestiona mpv, por lo que otros formatos admitidos por ese motor también pueden reproducirse cuando se abren directamente.

**Audio:** AAC, AC-3, AIFF, ALAC, APE, AU, DTS, E-AC-3, FLAC, M4A, MKA, MP1, MP2, MP3, MPC, OGA, OGG, OGM, Opus, TAK, TrueHD (`.thd`), TTA, WAV, WMA y WavPack.

**Video:** 3G2, 3GP, AVI, FLV, IVF, M2TS, M4V, MJ2, MKV, MOV, MP4, MPEG, MPG, MXF, OGV, RMVB, TS, WebM, WMV y Y4M.

**Listas de reproducción:** M3U y M3U8, incluidas las transmisiones HLS.

## 20. Solución de problemas

### Un archivo o una carpeta abre menos elementos de los esperados

Revise **Preferencias > General > Qué desea abrir con los archivos?** El valor predeterminado abre solo el archivo seleccionado. Los comandos de carpeta incluyen solo las extensiones reconocidas como medios. Las carpetas inaccesibles y las carpetas del sistema se omiten durante los análisis recursivos.

### Una transmisión en vivo no tiene duración ni porcentaje

Las transmisiones en vivo a menudo no publican una duración fija. Ir al tiempo, los saltos por porcentaje, el tiempo restante y el desplazamiento hasta el final requieren una duración conocida y pueden no estar disponibles.

### Un video de YouTube no se abre

Pruebe estos pasos en orden:

1. Confirme que la dirección se abre en un navegador y que nombra un video o una lista de reproducción en lugar de un canal.
2. Elija **Ayuda > Actualizaciones > Actualizar componentes de YouTube**.
3. Habilite **Usar yt-dlp para resolver las transmisiones** en las Preferencias de YouTube.
4. Pruebe primero el canal Stable; use Nightly o Master solo si Stable no puede manejar un cambio reciente del servicio.

### La grabación no encuentra un programa

La captura de programas requiere Windows 10 versión 2004 o posterior. El programa también debe tener una sesión de audio de Windows activa. Inicie la reproducción en ese programa, vuelva a abrir el cuadro de diálogo de fuentes y elíjalo en la lista actualizada.

### La grabación comienza sin una de sus fuentes

Luna puede continuar cuando se abre al menos una fuente. Lea la advertencia que enumera las fuentes que fallaron y luego compruebe si sus dispositivos están conectados, habilitados y disponibles. Detenga la grabación antes de editar la lista de fuentes.

### Un atajo global no funciona

Abra **Preferencias > Atajos globales** y aplique de nuevo la asignación. Windows no permite que dos aplicaciones registren la misma combinación global. Elija otra combinación si Luna informa que uno o más atajos no se pudieron registrar.

### La configuración no se guarda después de un error de inicio

Luna protege un `settings.json` no válido o ilegible en lugar de sobrescribirlo. Abra Preferencias e importe una copia de seguridad de configuración válida o elija **Copia de seguridad y restauración > Restablecer configuración**. Si necesita el original para un diagnóstico, cópielo primero desde la carpeta de configuración del usuario.

### Los marcadores o los favoritos informan datos no válidos

Un archivo de almacenamiento no válido se conserva en lugar de reemplazarse silenciosamente. Abra la carpeta de configuración del usuario, haga una copia del archivo JSON afectado y restaure una copia de seguridad válida conocida. Los marcadores se pueden restaurar a través de Preferencias. Los favoritos requieren actualmente restaurar el propio archivo `favorites.json` mientras Luna está cerrado.

### La guía del usuario no se abre

La guía instalada debería estar en `docs\<código-de-idioma>\user-guide.html` junto al ejecutable. Vuelva a extraer el archivo portátil completo o repare/reinstale la aplicación si falta la carpeta `docs`. Luna recurre del idioma regional a su idioma base y luego al inglés.

## 21. Editor y licencias

Luna Player es publicado por **Diamond Star**.

Copyright © 2026 Diamond Star.

El código fuente original de Luna Player está licenciado bajo la Licencia Apache, versión 2.0. El enlace de mpv traducido en `src/Mpv.cs` está licenciado bajo la Licencia Pública General Reducida de GNU, versión 2.1 o posterior.

Luna Player también usa componentes de terceros con licencias independientes, incluidos mpv, FFmpeg, wxWidgets, Prism, NAudio y PyYt. Cada componente se mantiene bajo su propia licencia. Consulte `NOTICE.txt`, instalado junto con Luna Player, para ver las declaraciones de derechos de autor, las versiones de los componentes, los nombres de las licencias y las ubicaciones del código fuente. Los textos de licencia pertinentes se instalan en la carpeta `licenses`.

El resumen de licencias de esta guía se ofrece por conveniencia. `LICENSE.txt`, `NOTICE.txt` y los archivos de la carpeta `licenses` contienen los términos y las atribuciones oficiales. En el repositorio del código fuente, los archivos correspondientes de nivel superior se llaman `LICENSE` y `NOTICE`.

## 22. Contacto y soporte

Luna Player es desarrollado y publicado por Diamond Star. Puede usar los siguientes canales:

- **Correo electrónico:** [ramymaherali55@gmail.com](mailto:ramymaherali55@gmail.com)
- **Telegram:** [Contactar a Diamond Star en Telegram](https://t.me/diamondStar35)
- **Informes de errores:** [Problemas de Luna Player en GitHub](https://github.com/diamondStar35/luna_player/issues)
- **Código fuente y versiones:** [Luna Player en GitHub](https://github.com/diamondStar35/luna_player)

Para un informe de errores, incluya la versión de Luna que se muestra en **Ayuda > Acerca de**, su versión de Windows, lo que esperaba, lo que ocurrió y los pasos más breves que reproducen el problema. Incluya el texto exacto de cualquier mensaje de error. Elimine los nombres de archivo privados, las direcciones web, los datos de cuentas y otra información personal de los registros o las capturas de pantalla antes de compartirlos públicamente.
