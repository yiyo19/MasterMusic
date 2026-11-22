# Mediciones de rendimiento y disponibilidad de MasterMusic

Este procedimiento mide RNF-04 y RNF-05 de la ERS contra las dos APIs locales y sus bases de datos reales. Los resultados se generan al ejecutar la herramienta; guardar el archivo no acredita que los requisitos se hayan cumplido.

## Preparación

1. Iniciar MasterMusic.Api en HTTPS 7052 y MasterMusic.Ventas.Api en HTTPS 7267 con sus migraciones aplicadas y configuración del README. Los certificados de desarrollo deben ser de confianza para curl de Windows.
2. Mantener abiertas las dos ventanas de las APIs durante toda la ejecución. Evitar suspender el computador, reiniciar servicios o editar el catálogo durante la medición.
3. Abrir una tercera ventana de PowerShell en la raíz de MasterMusic. Se requieren Node.js, curl.exe de Windows y una cuenta Administrador válida. El JWT emitido debe tener al menos 35 minutos de vigencia; la configuración del proyecto utiliza 60 minutos.

La herramienta utiliza las variables Admin__Email y Admin__Password del proceso. Para cargar las ya guardadas en el perfil del usuario, sin mostrarlas:

```powershell
$env:Admin__Email = [Environment]::GetEnvironmentVariable('Admin__Email', 'User')
$env:Admin__Password = [Environment]::GetEnvironmentVariable('Admin__Password', 'User')
```

Si esas credenciales ya no coinciden con la cuenta existente, la ejecución se detiene antes de crear datos. En ese caso se pueden introducir las correctas solo para esta ventana:

```powershell
$env:Admin__Email = Read-Host 'Correo del Administrador'
$claveMedicion = Read-Host 'Contraseña del Administrador' -AsSecureString
$credencialMedicion = [System.Management.Automation.PSCredential]::new($env:Admin__Email, $claveMedicion)
$env:Admin__Password = $credencialMedicion.GetNetworkCredential().Password
```

No se deben agregar credenciales a los archivos de prueba ni al repositorio.

## Ejecución

```powershell
node .\Documentacion\Pruebas\Medir-RNF-MasterMusic.mjs
```

La herramienta valida el acceso de administrador y comprueba ambas consultas antes de preparar datos. Requiere entre 1 y 100 instrumentos activos iniciales, con categoría y marca existentes. Agrega los instrumentos que falten hasta llegar a exactamente 100, con stock cero y un prefijo único MM-RNF. No modifica el stock ni los demás campos de los instrumentos anteriores, ni crea pedidos.

El procedimiento realiza cinco consultas de calentamiento y luego cien GET consecutivos al catálogo. Cada observación registra estado HTTP, tiempo total de curl, cantidad recibida y validación básica del JSON. RNF-04 cumple si el catálogo tiene cien instrumentos y al menos 95 de las cien consultas responden correctamente en un máximo de 2000 ms. Los errores permanecen en la muestra.

Después consulta el catálogo y la lista administrativa de pedidos una vez por API cada treinta segundos, con sesenta consultas por API. La ventana dura treinta minutos completos: comienza con la primera ronda y se mantiene abierta hasta cumplir 1800 segundos. El programa registra los instantes y los retrasos de programación; un retraso mayor de cinco segundos impide acreditar la regularidad de esa ejecución.

Se compara la identidad y la hora de inicio de los procesos MasterMusic.Api y MasterMusic.Ventas.Api al comienzo y al final de la ventana. Si no se pueden identificar los procesos, el reporte mantiene ese punto como no verificable automáticamente. Una respuesta HTTP correcta por sí sola no prueba ausencia de reinicios.

## Resultados y limpieza

Cada ejecución crea una carpeta RNF con fecha UTC e identificador propio dentro de Documentacion/Evidencias/Cierre. Contiene:

- README.md con condiciones, resultados y límites de la medición.
- rendimiento.csv con cien observaciones, si se completó esa fase.
- disponibilidad.csv con sesenta observaciones por API, si se completó la ventana.
- resultado.json con la evaluación y la limpieza.
- estado-medicion.json con los identificadores necesarios para recuperar la limpieza.
- procesos-inicio.json y procesos-fin.json para comprobar continuidad.

La herramienta no guarda contraseñas, JWT, cuerpos de pedidos ni datos de clientes. Registra características técnicas del equipo, versiones disponibles e identificador del commit. Los tiempos pertenecen a consultas secuenciales sobre una sola máquina; no demuestran capacidad concurrente ni disponibilidad mensual.

Al terminar o detectar una interrupción, intenta eliminar exclusivamente sus instrumentos temporales. Cada eliminación vuelve a comprobar prefijo, marca de ejecución, stock cero y ausencia en el catálogo inicial. La API aplica además sus reglas de integridad. Si el instrumento cambió o tiene referencias, se conserva.

Si se cierra la ventana o no están disponibles las APIs al limpiar, reinícialas y usa la ruta de la carpeta que el programa imprimió:

```powershell
node .\Documentacion\Pruebas\Medir-RNF-MasterMusic.mjs --limpiar 'Documentacion\Evidencias\Cierre\RNF-CARPETA-DE-TU-EJECUCION'
```

El modo de limpieza no repite las mediciones ni convierte resultados fallidos en aprobados. La herramienta muestra CUMPLE o NO ACREDITADO por requisito y conserva los fallos para su análisis.

## Evidencia para el informe

Después de ejecutar, revisar el resumen y conservar la carpeta completa. Una ejecución interrumpida se presenta como tal; no se cambian sus filas ni porcentajes para aparentar aprobación. El informe técnico incorpora los resultados medidos junto con el alcance local y las condiciones de ejecución.
