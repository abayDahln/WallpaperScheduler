# Design — Wallpaper Scheduler

Dokumen ini turunan lebih detail dari `architecture.md`, fokus ke data model konkret, algoritma resolusi jadwal, dan flow-flow penting.

## 1. Data Model / Config Schema

File: `%LOCALAPPDATA%\WallpaperSchedule\config.json`

```json
{
  "version": 1,
  "settings": {
    "autoStart": true,
    "closeButtonMinimizesToTray": true,
    "notifyOnWallpaperChange": false,
    "defaultWallpaperId": "wp_001",
    "themeOverride": "system",
    "wallpaperStyle": "Fill"
  },
  "wallpaperLibrary": [
    {
      "id": "wp_001",
      "fileName": "a1b2c3.jpg",
      "label": "Sunrise Morning",
      "addedAt": "2026-08-01T09:00:00+07:00",
      "cropLeft": 0,
      "cropTop": 0,
      "cropWidth": 1,
      "cropHeight": 1
    }
  ],
  "weeklySchedule": {
    "monday": [
      { "start": "00:00", "end": "12:00", "wallpaperId": "wp_001", "wallpaperStyle": "Fill" },
      { "start": "12:00", "end": "24:00", "wallpaperId": "wp_002", "wallpaperStyle": "" }
    ],
    "tuesday": [],
    "wednesday": [],
    "thursday": [],
    "friday": [],
    "saturday": [],
    "sunday": []
  },
  "monthlyOverrides": [
    {
      "id": "mo_001",
      "dayOfMonth": 17,
      "label": "Tanggal Kemerdekaan tiap bulan",
      "slots": [
        { "start": "00:00", "end": "24:00", "wallpaperId": "wp_010", "wallpaperStyle": "Custom" }
      ]
    }
  ],
  "dateOverrides": [
    {
      "id": "do_001",
      "date": "2026-12-25",
      "label": "Natal",
      "slots": [
        { "start": "00:00", "end": "24:00", "wallpaperId": "wp_011", "wallpaperStyle": "" }
      ]
    }
  ]
}
```

Catatan schema:
- `id` di tiap entity pakai string pendek unik (GUID pendek), dipakai buat referensi (`wallpaperId`) dan buat keperluan edit/hapus dari UI.
- `start`/`end` format `"HH:mm"`, 24 jam. `"24:00"` valid dipakai sebagai representasi "sampai akhir hari" (biar gak ambigu sama `"00:00"` di awal hari berikutnya).
- `wallpaperStyle` di tiap slot: Fill/Fit/Stretch/Tile/Center/Span/Custom; kosong (`""`) = ikuti `settings.wallpaperStyle` (global).
- `cropLeft/Top/Width/Height` di `WallpaperItem` (ternormalisasi 0–1) dipakai saat style Custom. Default `0,0,1,1` = seluruh gambar.
- `fileName` di library menyimpan **nama file yang disalin** ke folder lokal `%LOCALAPPDATA%\WallpaperSchedule\Wallpapers\` (saat import, file disalin dengan nama acak biar tidak bentrok). `label` menyimpan nama file asli tanpa ekstensi.

## 2. Algoritma Resolusi Jadwal (`ScheduleResolver`)

Fungsi inti: `ResolveActiveWallpaper(now, ref lastApplied) → (wallpaperId?, style?)` — mengembalikan id wallpaper **dan** style slot yang menang.

```
1. today = now.Date
   currentTime = now.TimeOfDay

2. cari dateOverride di config.dateOverrides dimana date == today
   KALAU ketemu:
       slot = CariSlotYangCover(dateOverride.slots, currentTime)
       KALAU slot ketemu → return slot.wallpaperId
       KALAU slot gak ketemu (gap) → lanjut ke FallbackGap(dateOverride.slots, currentTime)
       → return hasilnya, STOP (override tanggal spesifik selalu final, gak turun ke monthly/weekly)

3. cari monthlyOverride di config.monthlyOverrides dimana dayOfMonth == today.Day
   KALAU ketemu:
       slot = CariSlotYangCover(monthlyOverride.slots, currentTime)
       KALAU slot ketemu → return slot.wallpaperId
       KALAU gap → return FallbackGap(monthlyOverride.slots, currentTime)
       → STOP

4. dayName = today.DayOfWeek (mapped ke "monday".."sunday")
   slots = config.weeklySchedule[dayName]
   slot = CariSlotYangCover(slots, currentTime)
   KALAU slot ketemu → return slot.wallpaperId
   KALAU gap → return FallbackGap(slots, currentTime)

5. KALAU semua di atas gak menghasilkan apa-apa (hari itu emang kosong sama sekali,
   dan gak ada slot sebelumnya buat fallback) → return config.settings.defaultWallpaperId
```

`CariSlotYangCover(slots, time)`: return slot dimana `slot.start <= time < slot.end`.

`FallbackGap(slots, time)`: cari slot dengan `end` terbesar yang `end <= time` (slot terakhir yang "sudah lewat" hari itu) → pakai wallpaper-nya. Kalau gak ada juga (gap-nya di awal hari, sebelum slot pertama mulai) → jatuh ke wallpaper yang terakhir kali benar-benar ter-apply (disimpan terpisah sebagai "last applied state", bukan bagian config jadwal) sebagai fallback paling akhir sebelum jatuh ke `defaultWallpaperId`.

> Style yang dikembalikan = `WallpaperStyle` dari slot yang menang; kalau kosong atau fallback
> ke default, pakai `settings.wallpaperStyle` (global).

> Ini bagian yang paling penting buat di-unit-test terpisah dari efek samping (apply ke Windows beneran) — persis kayak disebut di `architecture.md` section 10.

## 3. Menghitung Waktu Event Berikutnya (`GetNextEventTime`)

Dipakai scheduler buat nentuin kapan timer berikutnya harus fire (lihat `architecture.md` section 4).

```
1. Resolve jadwal yang SEDANG aktif sekarang (pakai ResolveActiveWallpaper di atas,
   tapi versi yang juga return "slot yang lagi dipakai" bukan cuma wallpaper id-nya)

2. candidateTimes = []

3. KALAU ada slot aktif → tambahkan slot.end (dikonversi ke DateTime hari ini,
   atau besok kalau end == "24:00") ke candidateTimes

4. Tambahkan "besok jam 00:00" ke candidateTimes
   (karena override tanggal/bulan bisa berubah begitu tanggal berganti,
   walau kelihatannya lagi di tengah slot yang "masih lama")

5. nextEventTime = MIN(candidateTimes)

6. return nextEventTime
```

Timer di-set ke `nextEventTime - now`. Pas fire, ulangi dari langkah 1 (resolve ulang, apply kalau beda, hitung next event lagi).

## 4. Flow: Apply Wallpaper

```mermaid
sequenceDiagram
    participant Timer as Scheduler Timer
    participant Resolver as ScheduleResolver
    participant Config as ConfigService
    participant Wallpaper as WallpaperApplyService
    participant Frame as WallpaperFrameService
    participant Win32 as Windows API

    Timer->>Resolver: ResolveActiveWallpaper(now)
    Resolver->>Config: baca weeklySchedule / overrides
    Config-->>Resolver: data jadwal
    Resolver-->>Timer: (wallpaperId, style)

    alt wallpaperId/style beda dari yang lagi aktif (atau force)
        Timer->>Wallpaper: Apply(wallpaperId, style)
        Wallpaper->>Config: cari file path dari wallpaperId
        alt style == Custom
            Wallpaper->>Wallpaper: CropHelper.GenerateCustom → {id}_custom.bmp (resolusi layar utama)
        end
        Wallpaper->>Win32: IDesktopWallpaper / SystemParametersInfo(SPI_SETDESKWALLPAPER)
        Wallpaper-->>Timer: sukses, update "last applied state" (id + style)
        Timer->>Frame: ShowWallpaper(path, style) → crossfade ±500ms (WorkerW frame)
        else file hilang
            Wallpaper-->>Timer: gagal
            Timer->>Timer: tampilkan notifikasi tray, skip
        end
    end

    Timer->>Timer: GetNextEventTime() → reset timer
```

Catatan: perubahan slot yang sedang aktif (dari UI) memanggil `ForceReevaluate(fresh, force: true)`
sehingga wallpaper/style aktif langsung di-apply ulang tanpa menunggu event berikutnya.

## 5. Flow: Import Wallpaper

1. User pilih file gambar (FileOpenPicker, bisa multi-select).
2. Untuk tiap file: **salin** file ke folder `%LOCALAPPDATA%\WallpaperSchedule\Wallpapers\` dengan nama acak (`{guid}{ext}`), buat entry baru di `wallpaperLibrary` dengan `id` unik, `fileName` = nama file hasil salinan, `label` diambil dari nama file asli tanpa ekstensi.
3. Buat/update cache file thumbnail di `%LOCALAPPDATA%\WallpaperSchedule\Thumbs\{id}.jpg` untuk representasi visual di UI.
4. Simpan config (atomic write).
5. Refresh UI.

## 6. Flow: Startup Aplikasi

```
1. App start, cek startup argument (--tray atau normal)
2. Load config.json (kalau gak ada / corrupt → buat config default baru,
   backup file lama kalau corrupt biar gak hilang data user)
3. Inisialisasi SchedulerEngine dengan config yang sudah di-load
4. SchedulerEngine langsung ResolveActiveWallpaper(now) dan apply
   (jaga-jaga wallpaper sistem beda dari yang seharusnya, misal abis restart
   atau abis diubah manual sama user di luar app)
5. SchedulerEngine hitung next event, set timer
6. KALAU startup argument == --tray → langsung hide window utama, cuma tray icon yang muncul
   KALAU normal → tampilkan window utama seperti biasa
7. Subscribe SystemEvents.PowerModeChanged buat handle resume-from-sleep
```

## 7. Edge Cases yang Perlu Ditangani

| Case | Handling |
|------|----------|
| Time slot overlap dalam satu hari saat user edit | Validasi di UI, tolak save, tampilkan pesan error di slot yang konflik |
| Wallpaper dihapus dari library tapi masih dipakai di jadwal | Warning saat mau hapus: "dipakai di N jadwal, tetap hapus?" — kalau ya, slot yang pakai wallpaper itu jadi kosong/perlu di-assign ulang |
| Config file corrupt / gagal parse saat startup | Backup file corrupt (`config.json.bak`), buat config default baru, tampilkan notifikasi ke user |
| Komputer mati total pas lagi di tengah time slot (bukan sleep, listrik mati) | Startup flow (section 6, langkah 4) otomatis re-apply wallpaper yang seharusnya aktif, jadi self-healing |
| User ganti wallpaper manual dari luar app (klik kanan desktop) | App gak proaktif "melawan" — cuma bakal re-apply pas event berikutnya sesuai jadwal. Ini keputusan desain: app gak polling terus buat "menjaga" wallpaper (bakal makan resource), cukup pastikan benar tiap kali event scheduler fire |
| Slot dengan durasi sangat pendek (misal 1 menit) | Secara teknis didukung, gak ada batasan minimum durasi. Bukan use case utama tapi gak perlu di-block |
| Tanggal 31 dipilih buat monthly override, tapi bulan berjalan cuma 30/28/29 hari | Override itu simply gak pernah match di bulan yang gak punya tanggal itu — behavior alami, gak perlu handling khusus |

## 8. Tray Menu — Detail

```
┌─────────────────────────┐
│ Wallpaper Scheduler        │  ← header, non-clickable / app name
├─────────────────────────┤
│ Buka Aplikasi              │
│ ─────────────────────── │
│ ⏸ Pause Schedule          │  ← toggle text jadi "▶ Resume Schedule" kalau lagi paused
│ ─────────────────────── │
│ Keluar                     │
└─────────────────────────┘
```

- Double-click icon tray = shortcut buat "Buka Aplikasi".
- Icon tray idealnya kasih indikasi visual state (aktif vs paused) — bisa pakai dua versi icon berbeda atau badge kecil.
