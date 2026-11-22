-- =====================================================================
-- EcoQuest Migration 002 — Weather Edge Security & Cache Write Lock
-- Mengunci tabel weathercache agar hanya Edge Function (service_role)
-- yang boleh menulis cache, serta membuat tabel weatherlocations
-- untuk cache locationKey AccuWeather di sisi server.
-- =====================================================================

begin;

-- 1. Tabel cache locationKey per kota (hanya diakses service_role / Edge Function)
create table if not exists weatherlocations (
    city        varchar(100) primary key,
    locationkey varchar(50)  not null,
    resolvedat  timestamptz  not null default now()
);

alter table weatherlocations enable row level security;
revoke all on weatherlocations from public, anon, authenticated;

-- 2. Cabut izin tulis weathercache dari client (anon & authenticated).
--    Client tetap boleh SELECT sesuai BR-07; Edge Function meng-upsert via service_role.
drop policy if exists weather_insert on weathercache;
drop policy if exists weather_update on weathercache;
drop policy if exists weather_read   on weathercache;
create policy weather_read on weathercache for select to anon, authenticated using (true);

revoke all on weathercache from public, anon, authenticated;
grant select on weathercache to anon, authenticated;
revoke all on sequence weathercache_cacheid_seq from public, anon, authenticated;

commit;

-- =====================================================================
-- VERIFIKASI MANUAL (jalankan terpisah di SQL Editor Supabase):
--   set role anon;
--   select * from weathercache limit 5;                       -- HARUS berhasil
--   insert into weathercache (city, fordate, forecastsummary)
--     values ('Yogyakarta', '2099-01-01', 'Palsu');           -- HARUS gagal: permission denied
--   select * from weatherlocations;                           -- HARUS gagal: permission denied
--   reset role;
-- =====================================================================
