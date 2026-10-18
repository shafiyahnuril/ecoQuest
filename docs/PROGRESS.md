# Progress Tracker: EcoQuest (Berdasarkan PRD v1.1)

Dokumen ini memuat status pengembangan keseluruhan (keseluruhan *workspace*) proyek **EcoQuest** yang diukur berdasarkan dokumen acuan **PRD_EcoQuest_FINAL_v1.1.md**.

---

## 📊 Ringkasan Status Tahapan (Milestone)
*(Sesuai Bagian 11 PRD - Timeline & Milestone)*

| Tahap | Fokus Utama | Status | Catatan |
|---|---|---|---|
| **1. Fondasi** | Setup proyek WPF, Struktur Layer (UI-Service-Repo), Setup Supabase/PostgreSQL | 🟡 **In Progress (Sebagian Besar Selesai)** | Proyek WPF terbuat. Skema DB PostgreSQL (Final v1.1) sudah final. *Class* `Models` dan koneksi via `SupabaseService` sudah dibuat, namun struktur *Repository* & *ViewModel* (MVVM) belum terpasang penuh. |
| **2. Autentikasi & Data Referensi** | Register/Login (BCrypt), Seeding Kategori & Tipe Aktivitas | 🟡 **In Progress** | Data referensi (seed) dan relasinya sudah di-*deploy* di DB SQL. Logika autentikasi dan hashing `BCrypt` di C# belum diimplementasikan. UI halaman Login belum dibuat. |
| **3. Fitur Inti (Evening Review)** | Form dinamis 6 aktivitas, validasi 1x/hari, Kalkulasi CO2 via *Strategy Pattern* | 🟡 **In Progress** | Kerangka *Strategy Pattern* (`IImpactCalculator`, `CountImpactCalculator`, dll.) sudah selesai dan formula poin di-*update* sesuai v1.1. Form antarmuka pengguna belum ada. |
| **4. Edukasi & Cuaca** | Integrasi API AccuWeather, `WeatherCache`, Fallback UI, Insight Edukatif | 🔴 **To Do** | Model `WeatherCache` sudah siap. Integrasi *service* HTTP/AccuWeather belum dibuat. |
| **5. Dashboard & Riwayat** | Grafik Tren, Breakdown Kategori, Tooltip, Filter Riwayat | 🔴 **To Do** | Belum ada pengerjaan di UI. |
| **6. Elemen Opsional** | Sistem Poin/Level, Pencapaian, Foto, Ekspor CSV | 🟡 **In Progress (Database siap)** | Tabel Badge/UserBadge dan logika Model `Level` untuk sentinel limit poin (*MaxPoints = 2147483647*) sudah siap di backend. Fitur foto `PhotoPath` sudah ditambahkan pada Model Log. Implementasi di *logic/UI* masih *To Do*. |

---

## 🛠️ Detail Komponen Sistem Saat Ini

### ✅ Selesai / Terimplementasi
- **Database (PostgreSQL / Supabase)**: 
  - `ecoquest_schema_final.sql` dengan skema v1.1 sepenuhnya.
  - Implementasi *constraint* (seperti keunikan email/username, *sentinel value* di `Level`, dsb.).
  - *Seed data* resmi untuk 4 `ActivityCategory` dan 6 `ActivityType` (termasuk nilai `CO2Coefficient` dan `pointFactor`).
- **Data Models (C#)**:
  - `User.cs` & `UserModel.cs` (termasuk `AvatarUrl`).
  - `ActivityLog.cs` (lengkap dengan `StartTime`, `Note`, `PhotoPath` opsional).
  - `ActivityType.cs`, `ActivityCategory.cs`.
  - `Level.cs`, `Badge.cs`, `UserBadge.cs`.
  - `WeatherCache.cs`.
- **Core Services (C#)**:
  - `SupabaseService.cs` untuk inisialisasi koneksi SDK.
  - Skema *Strategy Pattern* untuk perhitungan skor: `IImpactCalculator`, `CountImpactCalculator`, `DistanceImpactCalculator`, `DurationImpactCalculator` yang menggunakan rumus final v1.1: `Math.Round(co2SavedKg * 20 * pointFactor)`.

### 🔄 Sedang Dikerjakan / Belum Selesai (Next Steps)
Berdasarkan pembagian tugas pada PRD dan *Requirement* (FR-1.1 hingga FR-7.3):

1. **Front-End / UI (WPF)**
   - Perlu membuat struktur *Views* dan *ViewModels* (MVVM).
   - *Screen* yang harus dibuat: Register, Login, Dashboard Utama, Form Evening Review, dan Riwayat.
2. **Backend Authentication Layer**
   - Mengimplementasikan `BCrypt.Net` untuk *hashing* password (sesuai FR-1.1 dan Keamanan PRD).
   - Membuat *Service/Repository* khusus akun pengguna (`AuthService`).
3. **Repository Pattern untuk ActivityLog**
   - Logika untuk mengecek "*Apakah User sudah submit hari ini?*" (FR-2.1) sebelum merender form Evening Review.
   - Pengecekan limit (misal `plausibleMaxPerDay`).
4. **Third-party Services**
   - Membuat *Service* API untuk AccuWeather (FR-6.1) yang berinteraksi dengan `WeatherCache`.

---
*Dokumen ini dibuat otomatis pada branch `DatabasePreparation` untuk merepresentasikan jejak penyelesaian target utama produk EcoQuest.*
