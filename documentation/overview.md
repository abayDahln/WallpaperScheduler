# Overview — Wallpaper Scheduler

## Apa ini?

Wallpaper Scheduler adalah aplikasi desktop Windows yang ganti wallpaper otomatis berdasarkan jadwal yang kamu atur sendiri. Bukan cuma "wallpaper beda tiap hari", tapi bisa lebih granular — dalam satu hari bisa ada beberapa wallpaper yang gantian sesuai jam (misal pagi beda, sore beda), dan bisa ada pengecualian untuk tanggal-tanggal tertentu.

Jalan di background terus, nongol di system tray, auto-start pas Windows nyala, dan didesain supaya seringan mungkin — gak ada alasan app kecil kayak gini makan CPU/RAM signifikan.

## Kenapa dibikin?

Ganti wallpaper manual itu ribet dan gampang lupa. Fitur "slideshow" bawaan Windows juga terbatas — cuma bisa random/berurutan dengan interval tetap, gak bisa diatur "hari Senin pagi pakai gambar A, siang pakai gambar B, tapi tiap tanggal 17 override jadi gambar C". Wallpaper Scheduler ngisi celah itu.

## Konsep Utama

Ada 3 lapis jadwal, dari yang paling general ke paling spesifik:

1. **Weekly Schedule (base/default)** — jadwal mingguan, tiap hari (Senin–Minggu) punya satu atau lebih *time slot*, tiap slot punya wallpaper sendiri. Ini yang selalu jadi fallback kalau gak ada override.
2. **Monthly Override** — jadwal berdasarkan tanggal yang berulang tiap bulan (misal "tanggal 17 tiap bulan pakai wallpaper merah-putih"). Override weekly schedule.
3. **Specific Date Override** — jadwal untuk tanggal spesifik, sekali doang, gak berulang (misal "25 Desember 2026"). Prioritas paling tinggi, override semua.

Dalam satu hari/tanggal, kamu bisa punya banyak time slot dengan jam mulai-selesai bebas (gak harus kelipatan 12 jam), masing-masing pakai wallpaper beda.

## Fitur Utama

- Jadwal wallpaper mingguan dengan multi time-slot per hari
- Override bulanan (recurring by day-of-month), dikelola lewat kalender bulanan
- Override tanggal spesifik (one-time), dikelola lewat kalender
- **Style wallpaper per slot** — Fill/Fit/Stretch/Tile/Center/Span, plus **Custom** (pilih area crop pada gambar, di-fit ke resolusi layar utama)
- Library wallpaper internal (import gambar, dikelola sama app)
- **Lapisan render hybrid** — wallpaper native + frame Win32 (WorkerW) dengan transisi crossfade
- Auto-update wallpaper saat ini ketika slot yang sedang aktif diubah (wallpaper/style)
- Background service yang efisien (event-driven, bukan polling)
- System tray icon dengan quick actions
- Auto-start saat Windows boot
- UI modern mengikuti bahasa desain Windows 11 (Fluent Design) — responsif, ukuran window minimum, nav pane auto-collapse

## Target Pengguna

Personal use — kamu sendiri, power user Windows yang suka atur-atur detail dan gak masalah sama app yang agak "engineer-y" dari sisi konfigurasi.

## Tech Stack

- **C# / .NET 8**
- **WinUI 3** (Windows App SDK), mode **unpackaged**
- **CommunityToolkit.Mvvm** untuk pola MVVM
- **H.NotifyIcon.WinUI** untuk system tray (WinUI 3 gak punya tray icon native)
- **System.Drawing.Common** untuk scaling gambar & crop custom
- Penyimpanan konfigurasi: file **JSON lokal**, bukan database — datanya kecil dan sederhana, gak butuh SQLite

## Status

Sudah ada implementasi: WinUI 3 + MVVM, scheduler engine event-driven, system tray,
hybrid render (native + frame crossfade), weekly schedule, monthly & date overrides,
per-slot wallpaper style (termasuk Custom crop), dan settings. Jalankan dengan
`dotnet run -p:Platform=x64`.

## Dokumen Lain

- `PRD.md` — kebutuhan produk & fitur secara fungsional
- `requirements.md` — requirement detail + asumsi yang perlu dikonfirmasi
- `architecture.md` — desain teknis & struktur sistem
- `design.md` — detail algoritma, data model, flow
- `style_guide.md` — panduan UI/UX mengikuti Fluent Design
