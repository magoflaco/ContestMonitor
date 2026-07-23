# Contest Monitor

Aplicación de escritorio para Windows (.NET 8 / WPF) que vigila la página de
concursos de la Red de Programación Competitiva y envía **notificaciones push
(toasts de Windows)** cuando aparece un concurso nuevo.

Reescribe y mejora la lógica del script original `comprobar.py`.

## Qué hace

- Comprueba el sitio **dos veces al día** (horas configurables, por defecto 09:00 y 21:00).
- Detecta concursos **nuevos** comparando contra un estado persistente (`seen_contests.json`).
- Envía una notificación push y la **repite hasta 4 veces** (cada 30 min por defecto)
  mientras el usuario no la vea. Se considera "vista" al hacer clic en el toast,
  abrir/enfocar la ventana o pulsar **Mark as seen**.
- Interfaz oscura y minimalista, iconos vectoriales dibujados (sin emojis).

## Mejora sobre el script original

El script Python tomaba la primera `<table>` después del encabezado
*Upcoming contests*. Si esa sección está **vacía** (sin tabla), tomaría por error
la tabla de *Past contests* y marcaría todos los concursos pasados como nuevos.

Aquí el parser (`Services/ContestScraper.cs`) **acota** la búsqueda: solo acepta la
tabla si aparece **entre** el encabezado *Upcoming contests* y el de *Past contests*.
La estructura real confirmada es una tabla con columnas
**Name / Start date / Registrations end date**.

## Estructura del proyecto

```
ContestMonitor/
├─ ContestMonitor.csproj        Proyecto WPF .NET 8 + dependencias
├─ app.manifest                 DPI awareness + compat Win10/11
├─ App.xaml / App.xaml.cs       Composition root (cablea servicios y VM)
├─ Models/
│  └─ Contest.cs                Modelo de un concurso
├─ Configuration/
│  └─ AppSettings.cs            Ajustes persistentes (%AppData%\ContestMonitor)
├─ Services/
│  ├─ ContestScraper.cs         Descarga + parseo HTML (HtmlAgilityPack)
│  ├─ SeenStore.cs              Persistencia de concursos ya vistos
│  ├─ ToastNotifier.cs          Notificaciones push con reintentos (hasta 4)
│  ├─ MonitorService.cs         Núcleo DRY: scrapea → diff → notifica
│  └─ SchedulerService.cs       Programación de las 2 comprobaciones diarias
├─ ViewModels/
│  ├─ ObservableObject.cs       Base INotifyPropertyChanged
│  ├─ RelayCommand.cs           ICommand con soporte async
│  ├─ ContestRow.cs             Fila de concurso para la UI
│  └─ MainViewModel.cs          Estado y comandos de la ventana
├─ Views/
│  ├─ MainWindow.xaml(.cs)      Ventana principal
│  └─ Converters.cs             Convertidores de binding
└─ Themes/
   └─ Theme.xaml                Paleta, estilos e iconos vectoriales
```

## Requisitos

- **.NET 8 SDK** (Windows). Descarga: https://dotnet.microsoft.com/download/dotnet/8.0
- Windows 10 (1809+) u 11 para los toasts.

> Este equipo no tiene el SDK instalado; instálalo antes de compilar.

## Compilar y ejecutar

```powershell
cd C:\Users\ba-lab1-pc17\Desktop\ContestMonitor

# Restaurar dependencias y compilar
dotnet build -c Release

# Ejecutar
dotnet run -c Release

# O generar un ejecutable autocontenido (no requiere .NET instalado)
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -o .\publish
```

El ejecutable quedará en `publish\ContestMonitor.exe`.

## Configuración

Se guarda en `%AppData%\ContestMonitor\settings.json`:

| Campo                    | Descripción                                  | Por defecto        |
|--------------------------|----------------------------------------------|--------------------|
| `ContestsUrl`            | URL a vigilar                                | página de contests |
| `CheckTimes`             | Horas de comprobación (24h)                  | `["09:00","21:00"]`|
| `CheckOnStartup`         | Comprobar al arrancar                        | `true`             |
| `RepeatIntervalMinutes`  | Minutos entre reintentos de notificación     | `30`               |
| `MaxNotificationRepeats` | Nº total de veces que se muestra el aviso    | `4`                |
| `StartMinimized`         | Arrancar minimizado                          | `false`            |

Las horas también se editan desde la propia ventana (campo *Daily check times*).

## Notas

- Las notificaciones se registran automáticamente la primera vez que se muestran.
- Para que el monitor funcione de forma continua, deja la app abierta (puede quedar
  minimizada). Para que arranque con Windows, crea un acceso directo a
  `ContestMonitor.exe` en `shell:startup`.
