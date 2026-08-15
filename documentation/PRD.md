# PRD — Wallpaper Scheduler

## 1. Problem Statement

Windows gak punya cara native buat ganti wallpaper berdasarkan jadwal yang fleksibel. Fitur "Slideshow" bawaan cuma bisa: pilih folder, interval tetap, urutan random/sequential. Gak ada konsep "hari tertentu", "jam tertentu", atau "tanggal tertentu pakai wallpaper khusus".

Buat orang yang suka personalisasi desktop dengan detail (misal wallpaper pagi vs malam beda, atau wallpaper spesial pas hari kemerdekaan), satu-satunya cara sekarang adalah ganti manual — yang gampang lupa dan repetitif.

## 2. Goals

- User bisa bikin jadwal wallpaper mingguan dengan multi time-slot per hari, jam bebas diatur.
- User bisa override jadwal itu untuk tanggal tertentu (one-time) atau tanggal berulang tiap bulan.
- Wallpaper berganti otomatis tepat waktu tanpa perlu app dibuka/terlihat.
- App ringan — idle di background, gak ada polling berat, gak ganggu performa sistem.
- App auto-start pas Windows nyala, langsung minimize ke tray.
- UI terasa "native Windows 11" — konsisten sama Settings/Clock/PowerToys, bukan kayak app pihak ketiga yang asing.

## 3. Non-Goals (di luar scope versi awal)

- Wallpaper per-monitor berbeda (user sudah confirm: sama semua monitor dulu)
- Sinkronisasi cloud / multi-device
- Sumber wallpaper online (API gambar, Bing wallpaper, dsb) — hanya dari file lokal user
- Dukungan Windows lawas (di bawah Windows 11) — WinUI 3 target-nya Windows 11 (bisa jalan di Win10 versi tertentu tapi gak jadi prioritas)
- Video wallpaper / live wallpaper
- Publish ke Microsoft Store (untuk sekarang; app didesain portable/unpackaged)

## 4. Target User & Use Case

**User**: pengguna Windows personal, teknikal, suka kontrol detail atas desktop-nya.

**Use case utama**:
- "Aku mau tiap hari kerja (Senin-Jumat) pagi pakai wallpaper motivational, sore pakai wallpaper calm."
- "Weekend wallpaper-nya beda, lebih santai."
- "Tanggal 17 Agustus otomatis ganti wallpaper merah-putih, tanpa aku harus inget-inget ganti manual."
- "Tanggal ulang tahun aku, sekali doang, pakai wallpaper spesial."

## 5. Fitur (Functional Overview)

| # | Fitur | Prioritas |
|---|-------|-----------|
| 1 | Weekly schedule editor (7 hari, multi slot per hari) | Must Have |
| 2 | Import wallpaper (via slot picker & default), preview thumbnail | Must Have |
| 3 | Background scheduler engine (auto-apply wallpaper sesuai jadwal) | Must Have |
| 4 | System tray icon + menu (buka app, pause schedule, exit) | Must Have |
| 5 | Auto-start saat Windows boot | Must Have |
| 6 | Monthly recurring override (by tanggal), kalender view | Must Have |
| 7 | Specific date override (one-time), kalender view | Must Have |
| 8 | Style wallpaper per slot (Fill/Fit/Stretch/Tile/Center/Span/**Custom**) | Should Have |
| 9 | Transisi fade antar wallpaper (native + frame crossfade) | Should Have |
| 10 | Notifikasi ringan pas wallpaper berganti (opsional, bisa dimatikan) | Could Have |
| 11 | Export/import konfigurasi jadwal (backup/restore) | Could Have |
| 12 | Dark/Light theme mengikuti sistem | Must Have |
| 13 | Manual "apply now" / preview wallpaper tertentu tanpa nunggu jadwal | Should Have |

## 6. User Flow (ringkas)

1. User install & buka app pertama kali → halaman **Overview** menampilkan wallpaper saat ini, default wallpaper, dan ringkasan jadwal.
2. User buka **Weekly** (atau Monthly/Dates), klik **Add Wallpaper** → pilih gambar → langsung jadi slot.
3. User buka **Weekly**, pilih hari, atur jam mulai-selesai tiap slot, dan set style wallpaper (bisa **Custom** = pilih area crop).
4. (Opsional) User buka **Monthly** atau **Dates** untuk override berulang/sekali, lewat kalender.
5. User aktifkan **Auto-start** di Settings.
6. User minimize app → app hilang ke tray, scheduler jalan di background.
7. Wallpaper otomatis berganti sesuai jadwal, kapan pun, tanpa app perlu dibuka.
8. User bisa klik kanan tray icon buat quick actions (buka app, pause, exit).

## 7. Success Criteria

- Wallpaper berganti tepat waktu (toleransi delay < beberapa detik dari waktu terjadwal).
- Idle resource usage: mendekati 0% CPU, RAM kecil (target di bawah ~50MB saat idle di tray — angka indikatif, divalidasi pas implementasi).
- App survive restart Windows, sleep/resume, tanpa kehilangan jadwal atau gagal apply wallpaper yang seharusnya aktif.
- UI kerasa natural buat orang yang biasa pakai Settings/PowerToys — gak perlu belajar ulang pola interaksi.

## 8. Future Considerations (bukan sekarang, tapi dicatat)

- Per-monitor wallpaper berbeda
- Wallpaper dari online source
- Widget kalender yang lebih visual buat lihat jadwal sebulan penuh (saat ini kalender sudah ada di Monthly/Dates, tapi belum menampilkan jadwal waktu per slot di atasnya)
