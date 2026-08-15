# Requirements — Wallpaper Scheduler

## 1. Asumsi yang Perlu Dikonfirmasi

Beberapa hal di brief awal ambigu, jadi aku ambil keputusan desain dengan asumsi berikut. Tolong dicek, kalau ada yang salah gampang diralat sebelum masuk implementasi:

1. **"Per bulan, tanggal tertentu"** ditafsirkan sebagai dua fitur terpisah: *monthly recurring override* (tanggal X tiap bulan, berulang selamanya) dan *specific date override* (tanggal tertentu, sekali doang, misal ulang tahun atau tanggal event tahun tertentu). Kalau maksudnya cuma salah satu, kabari — tapi menurutku dua-duanya sekalian lebih fleksibel dan gak nambah kompleksitas berarti.
2. **Time slot yang gak nutup 24 jam penuh** (misal cuma diisi 08:00-17:00, sisanya kosong) — fallback-nya pakai wallpaper dari slot terakhir yang berlaku sebelumnya (baik dari hari kemarin maupun slot sebelumnya di hari yang sama). Kalau belum ada slot sama sekali yang pernah aktif, pakai wallpaper default/fallback yang ditentukan user di Settings.
3. **Wallpaper hilang/dipindah dari disk** (walau harusnya gak kejadian karena di-copy ke folder app) — app tetap coba apply, kalau gagal, kasih notifikasi tray dan skip ke slot berikutnya, gak crash.
4. Target minimum OS: **Windows 11** (build 22000+). WinUI 3 secara teknis bisa jalan di Windows 10 versi tertentu, tapi gak jadi target resmi karena UI-nya didesain ngikut bahasa visual Windows 11.

## 2. Functional Requirements

### Wallpaper Import (per-slot picker)
- **FR-1**: User bisa import gambar (JPG, PNG, BMP) — langsung lewat tombol "Add Wallpaper" di slot, atau sebagai default wallpaper di Overview.
- **FR-2**: File yang di-import di-copy ke folder terkelola aplikasi (`%LOCALAPPDATA%\WallpaperSchedule\Wallpapers\`), bukan reference langsung ke lokasi asli.
- **FR-3**: Tiap slot menampilkan preview thumbnail dari wallpaper yang dipilih.
- **FR-4**: Wallpaper dihapus dengan menghapus slot yang memakainya (tidak ada layar library terpisah).

### Wallpaper Style (per slot)
- **FR-5a**: Tiap slot punya style sendiri (Fill/Fit/Stretch/Tile/Center/Span); kosong = ikuti setting global.
- **FR-5b**: Style **Custom** memakai area crop yang bisa dipilih user (drag kotak + resize) pada gambar, di-fit ke resolusi layar utama.

### Weekly Schedule
- **FR-6**: User bisa atur jadwal untuk 7 hari (Senin–Minggu) secara independen.
- **FR-7**: Tiap hari bisa punya 1 atau lebih time slot.
- **FR-8**: Tiap time slot punya jam mulai & jam selesai (format 24 jam, granularitas minimal 1 menit) dan satu wallpaper terpilih dari library.
- **FR-9**: Sistem validasi time slot dalam satu hari gak boleh overlap satu sama lain.
- **FR-10**: User bisa duplikat jadwal dari satu hari ke hari lain (biar gak input ulang manual, misal Senin-Jumat sama).

### Monthly & Date Override
- **FR-11**: User bisa bikin override berdasarkan tanggal-dalam-bulan (1-31) yang berulang tiap bulan, dengan time slot sendiri (struktur sama kayak weekly).
- **FR-12**: User bisa bikin override untuk tanggal spesifik (tanggal-bulan-tahun), berlaku sekali doang.
- **FR-13**: Kalau tanggal hari ini match dengan specific date override → override ini menang, weekly & monthly diabaikan hari itu.
- **FR-14**: Kalau gak ada specific date override tapi match monthly override → monthly menang atas weekly.
- **FR-15**: Kalau gak ada override sama sekali → pakai weekly schedule.

### Scheduler Engine
- **FR-16**: Sistem otomatis apply wallpaper yang sesuai begitu waktu masuk ke time slot terkait, tanpa perlu user buka aplikasi.
- **FR-17**: Sistem re-evaluate jadwal yang berlaku setiap melewati tengah malam (karena override tanggal bisa berubah).
- **FR-18**: Sistem re-apply wallpaper yang seharusnya aktif setelah komputer bangun dari sleep/hibernate (jaga-jaga ada perubahan waktu terlewat).
- **FR-19**: User bisa pause/resume scheduler dari tray menu tanpa harus close aplikasi atau uninstall jadwal.
- **FR-20**: User bisa trigger "apply sekarang" manual untuk preview wallpaper tertentu tanpa nunggu jadwal (opsional, gak override jadwal permanen).

### System Tray & Background
- **FR-21**: Aplikasi bisa di-minimize ke system tray (bukan taskbar), tetap jalan di background.
- **FR-22**: Tray icon punya context menu minimal: Buka Aplikasi, Pause/Resume Schedule, Exit.
- **FR-23**: Menutup window utama (klik X) default-nya minimize ke tray, bukan exit aplikasi (dengan opsi ini bisa diubah di Settings).
- **FR-24**: Aplikasi bisa auto-start saat Windows boot, langsung dalam kondisi minimized ke tray (gak nongolin window).
- **FR-25**: User bisa toggle on/off auto-start dari Settings.

### Settings
- **FR-26**: User bisa atur wallpaper default/fallback untuk kondisi gap (lihat asumsi #2).
- **FR-27**: User bisa toggle notifikasi tray pas wallpaper berganti.
- **FR-28**: Tema aplikasi ikut sistem (light/dark) secara default, dengan opsi override manual.

## 3. Non-Functional Requirements

- **NFR-1 (Efisiensi)**: Scheduler harus event-driven (hitung waktu event berikutnya, sleep sampai saat itu), bukan polling tiap detik/menit. Target CPU usage saat idle mendekati 0%.
- **NFR-2 (Resource)**: RAM usage aplikasi saat idle di tray ditarget serendah mungkin (indikatif < 50MB, divalidasi saat implementasi — WinUI 3 punya overhead runtime yang perlu diperhitungkan).
- **NFR-3 (Reliabilitas)**: Kalau aplikasi crash atau force-close, restart berikutnya harus langsung apply wallpaper yang seharusnya aktif saat itu (self-healing state, gak bergantung app selalu jalan mulus).
- **NFR-4 (Startup time)**: Auto-start ke tray gak boleh nunda boot time Windows secara berarti — startup ringan, lazy-load komponen UI yang berat.
- **NFR-5 (Portabilitas)**: Unpackaged, jalan tanpa instalasi installer wajib (portable exe + folder data), gak butuh admin privilege untuk instalasi maupun auto-start (pakai HKCU, bukan HKLM).
- **NFR-6 (Konsistensi visual)**: UI mengikuti Fluent Design System / WinUI 3 default styling, mendukung Mica backdrop, light/dark theme, dan accent color sistem.
- **NFR-7 (Data safety)**: Perubahan konfigurasi (jadwal, library) langsung tersimpan ke disk (gak ada "Save" manual yang wajib), tapi write dilakukan secara atomic (tulis ke temp file lalu rename) biar gak corrupt kalau app mati mendadak pas nulis.
- **NFR-8 (Toleransi waktu)**: Delay antara waktu terjadwal dan wallpaper benar-benar berganti idealnya di bawah beberapa detik.

## 4. Constraints

- Platform: Windows 11 saja.
- Tech stack: C# / .NET 8, WinUI 3 (Windows App SDK), unpackaged deployment.
- Tidak boleh butuh admin/elevated privilege untuk operasi normal (install, auto-start, ganti wallpaper).
- Tidak menggunakan Windows Service terpisah (lihat `architecture.md` untuk alasan teknis: session isolation).

## 5. Dependencies (indikatif, dikonfirmasi lagi saat setup project)

- `Microsoft.WindowsAppSDK` — runtime WinUI 3
- `CommunityToolkit.Mvvm` — MVVM helpers (ObservableObject, RelayCommand, dsb)
- `H.NotifyIcon.WinUI` — system tray icon (WinUI 3 gak native support ini)
- `System.Text.Json` — serialisasi config (built-in .NET, gak perlu paket tambahan)
