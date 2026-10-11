# Third-party notices

DipsyPaint is MIT-licensed (ADR-0007). It ships with the components below, each under its own licence.

## SixLabors.ImageSharp

- **Use:** all image encoding and decoding (ADR-0008).
- **Licence:** [Six Labors Split License 1.0](https://github.com/SixLabors/ImageSharp/blob/main/LICENSE). DipsyPaint
  uses ImageSharp under the **Apache License 2.0**, which the Split License grants to software released under an
  open-source licence. A closed-source fork does not qualify and needs a
  [Six Labors commercial licence](https://sixlabors.com/pricing/), unless it meets another Apache-2.0 criterion
  of the Split License, such as annual gross revenue under 1M USD.
- **Build note:** ImageSharp 4 checks for a Six Labors licence key when building in Release. Supply it through the
  `SixLaborsLicenseKey` MSBuild property or environment variable (CI reads the `SIXLABORS_LICENSE_KEY` secret), or a
  `sixlabors.lic` file, which is git-ignored. Debug builds work without a key.
- Copyright © Six Labors.

## Avalonia

- **Use:** the shell's UI framework, including the colour wheel and slider of Edit colours (Avalonia.Controls.ColorPicker).
- **Licence:** [MIT](https://github.com/AvaloniaUI/Avalonia/blob/master/licence.md). Copyright © .NET Foundation and Contributors.

## SkiaSharp

- **Use:** rendering, through Avalonia.
- **Licence:** [MIT](https://github.com/mono/SkiaSharp/blob/main/LICENSE.md). Copyright © .NET Foundation and Contributors.
  SkiaSharp bundles Google's Skia, under its [BSD 3-Clause licence](https://skia.org/docs/dev/contrib/license/).

## Velopack

- **Use:** installers, portable packages and automatic updates from GitHub Releases (ADR-0015). Packages also contain
  Velopack's updater program (`Update.exe`, `UpdateMac` or `UpdateNix`), under the same licence.
- **Licence:** [MIT](https://github.com/velopack/velopack/blob/master/LICENSE). Copyright © 2021 Caelan Sayler,
  Copyright © 2024 Velopack Ltd.

## Fluent UI System Icons

- **Use:** tool and command icons (ADR-0012).
- **Licence:** [MIT](https://github.com/microsoft/fluentui-system-icons/blob/main/LICENSE). Copyright © Microsoft Corporation.
