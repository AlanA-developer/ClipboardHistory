# 📋 Advanced Clipboard History for Windows 11 (Fluent Edition)

Un gestor de historial de portapapeles de próxima generación, diseñado para integrarse perfectamente con la estética de Windows 11. Construido con **.NET 10**, **WPF** y **WPF-UI**.

![Windows 11](https://img.shields.io/badge/Windows-11-blue?style=for-the-badge&logo=windows)
![.NET 10](https://img.shields.io/badge/.NET-10.0-purple?style=for-the-badge&logo=dotnet)

## ✨ Características Premium

- **Experiencia Fluent**: Ventana `FluentWindow` con material **Mica** y esquinas redondeadas nativas.
- **Organización por Pestañas**:
  - 📝 **Texto**: Historial completo de fragmentos de texto.
  - 🖼️ **Imágenes**: Captura automática de imágenes del portapapeles con rejilla de miniaturas y previsualización.
  - 📌 **Fijados**: Acceso rápido a tus elementos favoritos y más usados.
- **Captura de Imágenes**: Guarda automáticamente capturas de pantalla e imágenes copiadas como PNG en SQLite (BLOB).
- **Rejilla de Miniaturas**: Las imágenes se muestran en un `WrapPanel` con tarjetas de 190px que incluyen resolución, tamaño y hora.
- **Rendimiento Optimizado**: Las miniaturas se generan a 200px máx. usando `DecodePixelWidth`, sin cargar la imagen completa en memoria.
- **Acceso Instantáneo**: Invoca el panel con `Ctrl + Shift + V` desde cualquier aplicación.
- **Búsqueda Integrada**: Filtrado en tiempo real mediante una barra de búsqueda estilizada.
- **Persistencia Robusta**: Base de datos SQLite local para mantener tu historial seguro.

## 🛠️ Stack Técnico

- **UI Library:** [WPF-UI](https://github.com/lepoco/wpfui) (v4.2.1)
- **Database:** Microsoft.Data.Sqlite + EF Core 10.0.7
- **Win32 API:** P/Invoke para monitoreo de portapapeles y hotkeys globales.

## 📂 Estructura del Proyecto

```
ClipboardHistory/
├── Models/
│   ├── ClipboardItem.cs        # Modelo de texto (Id, Content, Timestamp, IsPinned)
│   └── ClipboardImage.cs       # Modelo de imagen (Id, ImageData BLOB, Width, Height, FileSizeBytes, Timestamp, IsPinned)
├── ViewModels/
│   └── ClipboardImageViewModel.cs  # ViewModel con thumbnail BitmapSource pre-generado
├── Data/
│   └── ClipboardDbContext.cs   # EF Core context con ClipboardItems + ClipboardImages
├── Services/
│   └── Win32Api.cs             # P/Invoke: ClipboardFormatListener, RegisterHotKey
├── App.xaml / App.xaml.cs      # Theme Dark + ControlsDictionary + ApplicationThemeManager
├── MainWindow.xaml             # FluentWindow + Mica + TabControl + WrapPanel de imágenes
└── MainWindow.xaml.cs          # Clipboard monitor, image capture, thumbnail generation
```

## 🚀 Instalación y Ejecución

### Prerrequisitos
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows 10/11 (Recomendado Windows 11 para efectos Mica)

### Compilación y Ejecución
```bash
cd ClipboardHistory
dotnet build -c Release
dotnet run -c Release
```

### Generar Ejecutable (.exe)
El ejecutable final se genera en la carpeta `./publish`:
```bash
dotnet build -c Release -o ./publish
```

## ⌨️ Atajos de Teclado

| Acción | Atajo |
|--------|--------|
| **Abrir Historial** | `Ctrl + Shift + V` |
| **Cerrar/Ocultar** | `Esc` o clic fuera de la ventana |
| **Copiar Elemento** | Clic en la tarjeta del historial |

## 🖼️ Pestaña de Imágenes

La pestaña de imágenes captura automáticamente cualquier imagen copiada al portapapeles:

- **Captura automática**: `Clipboard.ContainsImage()` + `Clipboard.GetImage()`
- **Almacenamiento**: PNG → `byte[]` → SQLite BLOB
- **Miniaturas**: Generadas con `DecodePixelWidth=200` para rendimiento
- **Detección de duplicados**: Compara dimensiones + tamaño en bytes
- **Acciones**: Copiar imagen original, Anclar, Eliminar

---
Desarrollado con ❤️ para la comunidad de Windows.
