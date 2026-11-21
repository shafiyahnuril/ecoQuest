-- =====================================================================
-- EcoQuest — Skema Database v1.2 (Supabase / PostgreSQL 15+)
-- File   : database/migrations/001_schema_v1_2.sql
-- Pemilik: Software Architect (review wajib oleh Backend Developer)
-- Sifat  : IDEMPOTEN — aman dijalankan pada database kosong MAUPUN pada
--          database yang sudah berisi skema v1.1 (ecoquest_schema_FINAL.sql).
-- Konvensi penamaan: seluruh tabel & kolom huruf kecil TANPA underscore
--          (mengikuti UserModel.cs yang sudah ter-deploy: "passwordhash",
--          "totalco2savedkg", dst.). Nama function, constraint & index BOLEH
--          memakai underscore (app_*, ux_*, ix_*, ck_*, uq_*).
-- PERINGATAN: BACKUP database (Supabase → Database → Backups) sebelum
--          menjalankan bagian "6. PEMBERSIHAN DATA UJI".
-- CATATAN : Jika nama tabel/kolom pada database yang sudah ter-deploy BERBEDA
--          dari skrip ini, JANGAN jalankan buta. Bandingkan dulu dengan
--          `\d+ <tabel>` / Table Editor, lalu sesuaikan skrip dan
--          docs/architect/Database-Schema.md (database yang ter-deploy menang,
--          dokumentasi mengikuti).
-- =====================================================================

begin;

-- ---------------------------------------------------------------------
-- 0. EKSTENSI
-- ---------------------------------------------------------------------
create extension if not exists pgcrypto;  -- gen_random_uuid()

-- ---------------------------------------------------------------------
-- 1. TABEL REFERENSI
-- ---------------------------------------------------------------------
create table if not exists activitycategories (
    categoryid    serial       primary key,
    categoryname  varchar(50)  not null,
    description   text
);
create unique index if not exists ux_activitycategories_name
    on activitycategories (categoryname);

create table if not exists activitytypes (
    activitytypeid      serial        primary key,
    typename            varchar(100)  not null,
    categoryid          int           not null references activitycategories(categoryid) on delete restrict,
    inputcontroltype    varchar(20)   not null,
    co2coefficient      numeric(8,4)  not null,
    pointfactor         numeric(4,2)  not null
);
-- Kolom tambahan v1.2 (aman untuk DB lama)
alter table activitytypes add column if not exists unitlabel           varchar(20)  not null default '';
alter table activitytypes add column if not exists plausiblemaxperday  numeric(8,2);
alter table activitytypes add column if not exists hardmaxperday       numeric(8,2) not null default 1;
alter table activitytypes add column if not exists description         text;
alter table activitytypes add column if not exists displayorder        smallint     not null default 0;
create unique index if not exists ux_activitytypes_name on activitytypes (typename);

alter table activitytypes drop constraint if exists ck_activitytypes_inputcontroltype;
alter table activitytypes add  constraint ck_activitytypes_inputcontroltype
    check (inputcontroltype in ('Numeric','TimeRange','Count','Toggle'));
alter table activitytypes drop constraint if exists ck_activitytypes_coefficient;
alter table activitytypes add  constraint ck_activitytypes_coefficient check (co2coefficient >= 0);
alter table activitytypes drop constraint if exists ck_activitytypes_pointfactor;
alter table activitytypes add  constraint ck_activitytypes_pointfactor check (pointfactor between 0.8 and 1.5);

create table if not exists levels (
    levelid    serial       primary key,
    levelname  varchar(50)  not null,
    minpoints  int          not null,
    maxpoints  int
);
create unique index if not exists ux_levels_name on levels (levelname);
-- v1.2: sentinel 2147483647 diganti NULL (sesuai ERD & class diagram: maxPoints int?) — ADR-009
alter table levels alter column maxpoints drop not null;
update levels set maxpoints = null where maxpoints = 2147483647;
alter table levels drop constraint if exists ck_levels_range;
alter table levels add  constraint ck_levels_range
    check (minpoints >= 0 and (maxpoints is null or maxpoints > minpoints));

create table if not exists badges (
    badgeid       serial        primary key,
    name          varchar(100)  not null,
    description   text,
    criteriatype  varchar(30)   not null
);
alter table badges add column if not exists criteriavalue numeric(12,2) not null default 1;
create unique index if not exists ux_badges_name on badges (name);
alter table badges drop constraint if exists ck_badges_criteriatype;
alter table badges add  constraint ck_badges_criteriatype
    check (criteriatype in ('ActivityCount','PointsThreshold','StreakDays','CO2SavedThreshold'));
alter table badges drop constraint if exists ck_badges_criteriavalue;
alter table badges add  constraint ck_badges_criteriavalue check (criteriavalue > 0);

-- ---------------------------------------------------------------------
-- 2. TABEL PENGGUNA & TRANSAKSI
-- ---------------------------------------------------------------------
create table if not exists users (
    userid           uuid          primary key default gen_random_uuid(),
    username         varchar(50)   not null,
    email            varchar(100)  not null,
    passwordhash     varchar(255)  not null,
    city             varchar(100)  not null,
    avatarurl        varchar(255),
    totalpoints      int           not null default 0,
    totalco2savedkg  numeric(12,4) not null default 0,
    currentlevelid   int           references levels(levelid),
    currentstreak    int           not null default 0,
    createdat        timestamptz   not null default now()
);
alter table users add column if not exists avatarurl varchar(255);
create unique index if not exists ux_users_username_lower on users (lower(username));
create unique index if not exists ux_users_email_lower    on users (lower(email));
alter table users drop constraint if exists ck_users_totals;
alter table users add  constraint ck_users_totals
    check (totalpoints >= 0 and totalco2savedkg >= 0 and currentstreak >= 0);
alter table users drop constraint if exists ck_users_passwordhash_bcrypt;
-- (constraint bcrypt ditambahkan kembali SETELAH pembersihan data uji, bagian 6)

-- BARU v1.2 — satu baris per pengguna per tanggal = penanda sesi Evening Review (ADR-004)
create table if not exists dailyreviews (
    reviewid           uuid           primary key default gen_random_uuid(),
    userid             uuid           not null references users(userid) on delete cascade,
    reviewdate         date           not null,
    totalco2savedkg    numeric(10,4)  not null default 0,
    totalpointsearned  int            not null default 0,
    submittedat        timestamptz    not null default now(),
    constraint uq_dailyreviews_user_date unique (userid, reviewdate),
    constraint ck_dailyreviews_totals check (totalco2savedkg >= 0 and totalpointsearned >= 0)
);
create index if not exists ix_dailyreviews_user_date on dailyreviews (userid, reviewdate desc);

create table if not exists activitylogs (
    logid           uuid           primary key default gen_random_uuid(),
    userid          uuid           not null references users(userid) on delete cascade,
    activitytypeid  int            not null references activitytypes(activitytypeid) on delete restrict,
    logdate         date           not null,
    quantity        numeric(10,4)  not null,
    starttime       time,
    endtime         time,
    pointsearned    int            not null default 0,
    co2savedkg      numeric(10,4)  not null,
    createdat       timestamptz    not null default now()
);
alter table activitylogs add column if not exists reviewid           uuid references dailyreviews(reviewid) on delete cascade;
alter table activitylogs add column if not exists note               text;
alter table activitylogs add column if not exists photopath          varchar(255);
alter table activitylogs add column if not exists co2coefficientused numeric(8,4);
alter table activitylogs add column if not exists pointfactorused    numeric(4,2);

-- Backfill data lama: buat dailyreviews dari log yang sudah ada
insert into dailyreviews (userid, reviewdate, totalco2savedkg, totalpointsearned, submittedat)
select l.userid, l.logdate, sum(l.co2savedkg), sum(l.pointsearned), min(l.createdat)
from activitylogs l
where l.reviewid is null
group by l.userid, l.logdate
on conflict (userid, reviewdate) do nothing;

update activitylogs l
set reviewid = r.reviewid
from dailyreviews r
where l.reviewid is null and r.userid = l.userid and r.reviewdate = l.logdate;

update activitylogs l
set co2coefficientused = t.co2coefficient, pointfactorused = t.pointfactor
from activitytypes t
where t.activitytypeid = l.activitytypeid
  and (l.co2coefficientused is null or l.pointfactorused is null);

alter table activitylogs alter column reviewid           set not null;
alter table activitylogs alter column co2coefficientused set not null;
alter table activitylogs alter column pointfactorused    set not null;

alter table activitylogs drop constraint if exists uq_activitylogs_user_type_date;
alter table activitylogs add  constraint uq_activitylogs_user_type_date unique (userid, activitytypeid, logdate);
alter table activitylogs drop constraint if exists ck_activitylogs_values;
alter table activitylogs add  constraint ck_activitylogs_values
    check (quantity >= 0 and co2savedkg >= 0 and pointsearned >= 0);
alter table activitylogs drop constraint if exists ck_activitylogs_timepair;
alter table activitylogs add  constraint ck_activitylogs_timepair
    check ((starttime is null) = (endtime is null));
alter table activitylogs drop constraint if exists ck_activitylogs_note_len;
alter table activitylogs add  constraint ck_activitylogs_note_len
    check (note is null or char_length(note) <= 500);
create index if not exists ix_activitylogs_user_date on activitylogs (userid, logdate desc);
create index if not exists ix_activitylogs_review    on activitylogs (reviewid);

create table if not exists userbadges (
    userid    uuid         not null references users(userid) on delete cascade,
    badgeid   int          not null references badges(badgeid) on delete cascade,
    earnedat  timestamptz  not null default now(),
    primary key (userid, badgeid)
);

create table if not exists weathercache (
    cacheid          serial        primary key,
    city             varchar(100)  not null,
    fordate          date          not null,
    forecastsummary  varchar(150)  not null,
    rawresponse      text,
    retrievedat      timestamptz   not null default now()
);
-- Unique pada kolom ASLI (bukan lower(city)) karena upsert PostgREST (on_conflict=city,fordate)
-- membutuhkan unique constraint pada kolom; kota sudah kanonik dari dropdown (ADR-012).
drop index if exists ux_weathercache_city_date;
alter table weathercache drop constraint if exists uq_weathercache_city_date;
alter table weathercache add  constraint uq_weathercache_city_date unique (city, fordate);

-- ---------------------------------------------------------------------
-- 3. SEED DATA (upsert — nilai CO2 & pointFactor mengikuti PRD v1.1 tabel 4.3;
--    plausiblemaxperday & hardmaxperday adalah USULAN v1.2, lihat ADR-005)
-- ---------------------------------------------------------------------
insert into activitycategories (categoryid, categoryname, description) values
 (1, 'Transportasi',      'Perjalanan rendah emisi sebagai pengganti kendaraan pribadi'),
 (2, 'Energi',            'Penghematan listrik rumah tangga'),
 (3, 'Sampah/Daur Ulang', 'Pengelolaan sampah untuk menekan emisi metana di TPA'),
 (4, 'Konsumsi',          'Mengurangi barang sekali pakai')
on conflict (categoryid) do update
  set categoryname = excluded.categoryname, description = excluded.description;
select setval(pg_get_serial_sequence('activitycategories','categoryid'),
              (select max(categoryid) from activitycategories));

insert into activitytypes (activitytypeid, typename, categoryid, inputcontroltype, unitlabel,
                           co2coefficient, pointfactor, plausiblemaxperday, hardmaxperday,
                           description, displayorder) values
 (1, 'Jalan Kaki / Bersepeda', 1, 'Numeric', 'km', 0.1500, 1.0, 30, 300,
     'Diasumsikan menggantikan perjalanan mobil pribadi sejauh jarak yang sama (0,15 kg CO2/km).', 1),
 (2, 'Transportasi Umum', 1, 'Numeric', 'km', 0.0900, 1.0, 150, 1000,
     'Selisih emisi mobil pribadi vs transportasi umum per km, nilai indikatif (0,09 kg CO2/km).', 2),
 (3, 'Mematikan Lampu Saat Tidak Dipakai', 2, 'TimeRange', 'jam', 0.0130, 0.8, 12, 24,
     'Asumsi 1 lampu sekitar 15 W dan faktor emisi grid PLN sekitar 0,85 kg CO2/kWh (0,013 kg CO2/jam).', 3),
 (4, 'Memilah Sampah Organik/Anorganik', 3, 'Count', 'kali', 0.3000, 1.2, 10, 50,
     'Estimasi pengurangan emisi metana dari sampah organik yang tidak berakhir di TPA campuran (0,3 kg CO2-eq/kejadian).', 4),
 (5, 'Membawa Tumbler Sendiri', 4, 'Toggle', 'kali', 0.1000, 1.0, null, 1,
     'Menghindari satu gelas/botol plastik sekali pakai (0,1 kg CO2, maksimal 1x/hari).', 5),
 (6, 'Membawa Kantong Belanja Sendiri', 4, 'Count', 'kali', 0.1000, 1.2, 5, 20,
     'Estimasi emisi produksi kantong plastik sekali pakai yang dihindari per kunjungan (0,1 kg CO2).', 6)
on conflict (activitytypeid) do update set
  typename = excluded.typename, categoryid = excluded.categoryid,
  inputcontroltype = excluded.inputcontroltype, unitlabel = excluded.unitlabel,
  co2coefficient = excluded.co2coefficient, pointfactor = excluded.pointfactor,
  plausiblemaxperday = excluded.plausiblemaxperday, hardmaxperday = excluded.hardmaxperday,
  description = excluded.description, displayorder = excluded.displayorder;
select setval(pg_get_serial_sequence('activitytypes','activitytypeid'),
              (select max(activitytypeid) from activitytypes));

alter table activitytypes drop constraint if exists ck_activitytypes_limits;
alter table activitytypes add  constraint ck_activitytypes_limits
    check (hardmaxperday > 0 and (plausiblemaxperday is null or plausiblemaxperday <= hardmaxperday));

insert into levels (levelid, levelname, minpoints, maxpoints) values
 (1, 'Eco Starter',     0,   99),
 (2, 'Eco Explorer',  100,  499),
 (3, 'Eco Warrior',   500, 1499),
 (4, 'Eco Champion', 1500, null)
on conflict (levelid) do update
  set levelname = excluded.levelname, minpoints = excluded.minpoints, maxpoints = excluded.maxpoints;
select setval(pg_get_serial_sequence('levels','levelid'), (select max(levelid) from levels));

insert into badges (badgeid, name, description, criteriatype, criteriavalue) values
 (1, 'Langkah Pertama',                'Mencatat aktivitas ramah lingkungan pertama kali.',             'ActivityCount',       1),
 (2, 'Pencatat Rajin',                 'Total 30 aktivitas tercatat.',                                   'ActivityCount',      30),
 (3, '3 Hari Beruntun',                'Mengisi Evening Review 3 hari berturut-turut.',                 'StreakDays',          3),
 (4, '7 Hari Refleksi Berturut-turut', 'Mengisi Evening Review 7 hari berturut-turut.',                 'StreakDays',          7),
 (5, '30 Hari Konsisten',              'Mengisi Evening Review 30 hari berturut-turut.',                'StreakDays',         30),
 (6, '1 kg CO2 Pertama',               'Akumulasi estimasi CO2 yang dihindari mencapai 1 kg.',          'CO2SavedThreshold',   1),
 (7, '10 kg CO2 Pertama',              'Akumulasi estimasi CO2 yang dihindari mencapai 10 kg.',         'CO2SavedThreshold',  10),
 (8, 'Eco 500 Poin',                   'Akumulasi poin indikatif mencapai 500 (tidak dapat ditukar).',  'PointsThreshold',   500)
on conflict (badgeid) do update set
  name = excluded.name, description = excluded.description,
  criteriatype = excluded.criteriatype, criteriavalue = excluded.criteriavalue;
select setval(pg_get_serial_sequence('badges','badgeid'), (select max(badgeid) from badges));

-- ---------------------------------------------------------------------
-- 4. ROW LEVEL SECURITY (ADR-003)
--    anon key tertanam di aplikasi desktop → tabel berisi data pribadi
--    TIDAK boleh diakses langsung. Semua akses data pribadi lewat function
--    SECURITY DEFINER pada bagian 5.
-- ---------------------------------------------------------------------
alter table activitycategories enable row level security;
alter table activitytypes      enable row level security;
alter table levels             enable row level security;
alter table badges             enable row level security;
alter table users              enable row level security;
alter table dailyreviews       enable row level security;
alter table activitylogs       enable row level security;
alter table userbadges         enable row level security;
alter table weathercache       enable row level security;

drop policy if exists ref_read_categories on activitycategories;
create policy ref_read_categories on activitycategories for select to anon, authenticated using (true);
drop policy if exists ref_read_types on activitytypes;
create policy ref_read_types      on activitytypes      for select to anon, authenticated using (true);
drop policy if exists ref_read_levels on levels;
create policy ref_read_levels     on levels             for select to anon, authenticated using (true);
drop policy if exists ref_read_badges on badges;
create policy ref_read_badges     on badges             for select to anon, authenticated using (true);

drop policy if exists weather_read   on weathercache;
drop policy if exists weather_insert on weathercache;
drop policy if exists weather_update on weathercache;
create policy weather_read   on weathercache for select to anon, authenticated using (true);
create policy weather_insert on weathercache for insert to anon, authenticated with check (true);
create policy weather_update on weathercache for update to anon, authenticated using (true) with check (true);

-- Grant eksplisit (Supabase biasanya sudah memberi default privileges ke anon;
-- baris ini membuat skrip tetap benar pada PostgreSQL biasa / proyek baru)
grant select on activitycategories, activitytypes, levels, badges to anon, authenticated;
grant select, insert, update on weathercache to anon, authenticated;
grant usage, select on sequence weathercache_cacheid_seq to anon, authenticated;

-- users, dailyreviews, activitylogs, userbadges: SENGAJA tanpa policy → deny-all untuk anon.
revoke all on users, dailyreviews, activitylogs, userbadges from anon, authenticated;

-- ---------------------------------------------------------------------
-- 5. FUNCTIONS (RPC) — dipanggil via Supabase client: client.Rpc("app_xxx", params)
--    Konvensi error: raise exception '<KODE>' using errcode = 'P0001'
--    Kode: USER_NOT_FOUND, USERNAME_TAKEN, EMAIL_TAKEN, REVIEW_ALREADY_SUBMITTED,
--          REVIEW_DATE_MISMATCH, VALIDATION_FAILED
--    Nama parameter (p_*) adalah KONTRAK — C# harus mengirim key yang persis sama.
-- ---------------------------------------------------------------------

-- 5.1 Cek ketersediaan username/email (validasi register — FR-1.2)
create or replace function app_check_availability(p_username text, p_email text)
returns json language sql stable security definer set search_path = public as $$
  select json_build_object(
    'usernametaken', exists (select 1 from users where lower(username) = lower(trim(p_username))),
    'emailtaken',    exists (select 1 from users where lower(email)    = lower(trim(p_email))));
$$;

-- 5.2 Register — hash BCrypt dibuat di C# (FR-1.1); DB hanya menyimpan
create or replace function app_register(p_username text, p_email text, p_passwordhash text, p_city text)
returns json language plpgsql security definer set search_path = public as $$
declare
  v_user       users%rowtype;
  v_constraint text;
begin
  if p_passwordhash is null or p_passwordhash not like '$2%' then
    raise exception 'VALIDATION_FAILED' using errcode = 'P0001';
  end if;
  begin
    insert into users (username, email, passwordhash, city, currentlevelid)
    values (trim(p_username), lower(trim(p_email)), p_passwordhash, trim(p_city),
            (select levelid from levels order by minpoints asc limit 1))
    returning * into v_user;
  exception when unique_violation then
    get stacked diagnostics v_constraint = constraint_name;
    if v_constraint like '%username%' then
      raise exception 'USERNAME_TAKEN' using errcode = 'P0001';
    else
      raise exception 'EMAIL_TAKEN' using errcode = 'P0001';
    end if;
  end;
  return json_build_object('userid', v_user.userid, 'username', v_user.username,
                           'email', v_user.email, 'city', v_user.city,
                           'avatarurl', v_user.avatarurl, 'createdat', v_user.createdat);
end $$;

-- 5.3 Record login (hash dibandingkan di C# dengan BCrypt.Verify — FR-1.3)
create or replace function app_get_login_record(p_identifier text)
returns table (userid uuid, username varchar, email varchar, passwordhash varchar, city varchar, avatarurl varchar)
language sql stable security definer set search_path = public as $$
  select u.userid, u.username, u.email, u.passwordhash, u.city, u.avatarurl
  from users u
  where lower(u.username) = lower(trim(p_identifier)) or lower(u.email) = lower(trim(p_identifier))
  limit 1;
$$;

-- 5.4 Profil + agregat (TANPA passwordhash)
create or replace function app_get_profile(p_user_id uuid)
returns json language sql stable security definer set search_path = public as $$
  select json_build_object(
    'userid', u.userid, 'username', u.username, 'email', u.email, 'city', u.city,
    'avatarurl', u.avatarurl, 'totalpoints', u.totalpoints, 'totalco2savedkg', u.totalco2savedkg,
    'currentlevelid', u.currentlevelid, 'currentstreak', u.currentstreak, 'createdat', u.createdat,
    'lastreviewdate',   (select max(r.reviewdate) from dailyreviews r where r.userid = u.userid),
    'totalreviewcount', (select count(*) from dailyreviews r where r.userid = u.userid),
    'totallogcount',    (select count(*) from activitylogs l where l.userid = u.userid))
  from users u where u.userid = p_user_id;
$$;

-- 5.5 Update profil (kota & avatar — FR-1.4)
create or replace function app_update_profile(p_user_id uuid, p_city text, p_avatarurl text)
returns void language plpgsql security definer set search_path = public as $$
begin
  update users set city = trim(p_city), avatarurl = nullif(p_avatarurl, '') where userid = p_user_id;
  if not found then raise exception 'USER_NOT_FOUND' using errcode = 'P0001'; end if;
end $$;

-- 5.6 Ganti password (verifikasi password lama dilakukan di C# — FR-1.4)
create or replace function app_update_password(p_user_id uuid, p_new_passwordhash text)
returns void language plpgsql security definer set search_path = public as $$
begin
  if p_new_passwordhash is null or p_new_passwordhash not like '$2%' then
    raise exception 'VALIDATION_FAILED' using errcode = 'P0001';
  end if;
  update users set passwordhash = p_new_passwordhash where userid = p_user_id;
  if not found then raise exception 'USER_NOT_FOUND' using errcode = 'P0001'; end if;
end $$;

-- 5.7 Review pada tanggal tertentu (NULL bila belum ada) — cek "sudah submit hari ini?" (FR-2.1)
create or replace function app_get_review_by_date(p_user_id uuid, p_date date)
returns json language sql stable security definer set search_path = public as $$
  select json_build_object(
    'reviewid', r.reviewid, 'reviewdate', r.reviewdate, 'totalco2savedkg', r.totalco2savedkg,
    'totalpointsearned', r.totalpointsearned, 'submittedat', r.submittedat,
    'logs', coalesce((
       select json_agg(json_build_object(
         'logid', l.logid, 'activitytypeid', l.activitytypeid, 'quantity', l.quantity,
         'starttime', l.starttime, 'endtime', l.endtime, 'note', l.note, 'photopath', l.photopath,
         'co2savedkg', l.co2savedkg, 'pointsearned', l.pointsearned,
         'co2coefficientused', l.co2coefficientused, 'pointfactorused', l.pointfactorused)
         order by t.displayorder)
       from activitylogs l join activitytypes t on t.activitytypeid = l.activitytypeid
       where l.reviewid = r.reviewid), '[]'::json))
  from dailyreviews r
  where r.userid = p_user_id and r.reviewdate = p_date;
$$;

-- 5.8 SUBMIT EVENING REVIEW — satu transaksi atomik (pengganti Unit of Work, ADR-003)
--     p_items: JSON array, tiap elemen:
--     {"logid":"uuid","activitytypeid":1,"quantity":3.0,"starttime":null,"endtime":null,
--      "note":null,"photopath":null,"co2savedkg":0.45,"pointsearned":9,
--      "co2coefficientused":0.15,"pointfactorused":1.0}
create or replace function app_submit_evening_review(
    p_user_id        uuid,
    p_review_date    date,
    p_items          jsonb,
    p_total_co2      numeric,
    p_total_points   int,
    p_new_streak     int,
    p_new_level_id   int,
    p_new_badge_ids  int[] default '{}')
returns json language plpgsql security definer set search_path = public as $$
declare
  v_today      date := (now() at time zone 'Asia/Jakarta')::date;
  v_review_id  uuid;
  v_submitted  timestamptz;
  v_item       jsonb;
  v_sum_co2    numeric;
  v_sum_points int;
begin
  if not exists (select 1 from users where userid = p_user_id) then
    raise exception 'USER_NOT_FOUND' using errcode = 'P0001';
  end if;
  if p_review_date <> v_today then
    raise exception 'REVIEW_DATE_MISMATCH' using errcode = 'P0001';     -- FR-2.6
  end if;
  if p_total_co2 < 0 or p_total_points < 0 or p_new_streak < 1 then
    raise exception 'VALIDATION_FAILED' using errcode = 'P0001';
  end if;

  begin
    insert into dailyreviews (userid, reviewdate, totalco2savedkg, totalpointsearned)
    values (p_user_id, p_review_date, p_total_co2, p_total_points)
    returning reviewid, submittedat into v_review_id, v_submitted;
  exception when unique_violation then
    raise exception 'REVIEW_ALREADY_SUBMITTED' using errcode = 'P0001'; -- FR-2.1
  end;

  for v_item in select value from jsonb_array_elements(coalesce(p_items, '[]'::jsonb)) loop
    insert into activitylogs (logid, reviewid, userid, activitytypeid, logdate, quantity,
                              starttime, endtime, note, photopath, pointsearned, co2savedkg,
                              co2coefficientused, pointfactorused)
    values (coalesce(nullif(v_item->>'logid','')::uuid, gen_random_uuid()),
            v_review_id, p_user_id,
            (v_item->>'activitytypeid')::int, p_review_date,
            (v_item->>'quantity')::numeric,
            nullif(v_item->>'starttime','')::time, nullif(v_item->>'endtime','')::time,
            nullif(v_item->>'note',''), nullif(v_item->>'photopath',''),
            (v_item->>'pointsearned')::int, (v_item->>'co2savedkg')::numeric,
            (v_item->>'co2coefficientused')::numeric, (v_item->>'pointfactorused')::numeric);
  end loop;

  select coalesce(sum(co2savedkg),0), coalesce(sum(pointsearned),0)
    into v_sum_co2, v_sum_points
  from activitylogs where reviewid = v_review_id;
  if abs(v_sum_co2 - p_total_co2) > 0.0001 or v_sum_points <> p_total_points then
    raise exception 'VALIDATION_FAILED' using errcode = 'P0001';       -- konsistensi total
  end if;

  update users
     set totalco2savedkg = totalco2savedkg + p_total_co2,
         totalpoints     = totalpoints + p_total_points,
         currentstreak   = p_new_streak,
         currentlevelid  = coalesce(p_new_level_id, currentlevelid)
   where userid = p_user_id;

  insert into userbadges (userid, badgeid)
  select p_user_id, b from unnest(coalesce(p_new_badge_ids, '{}')) as b
  on conflict do nothing;

  return json_build_object('reviewid', v_review_id, 'submittedat', v_submitted);
end $$;

-- 5.9 Total harian (line chart tren & hari refleksi tanpa aktivitas di Riwayat)
create or replace function app_get_daily_totals(p_user_id uuid, p_from date, p_to date)
returns table (reviewdate date, totalco2savedkg numeric, totalpointsearned int)
language sql stable security definer set search_path = public as $$
  select r.reviewdate, r.totalco2savedkg, r.totalpointsearned
  from dailyreviews r
  where r.userid = p_user_id
    and (p_from is null or r.reviewdate >= p_from)
    and (p_to   is null or r.reviewdate <= p_to)
  order by r.reviewdate;
$$;

-- 5.10 Breakdown per kategori (selalu 4 baris; kategori kosong = 0) — FR-4.2
create or replace function app_get_category_breakdown(p_user_id uuid, p_from date default null, p_to date default null)
returns table (categoryid int, categoryname varchar, totalco2savedkg numeric)
language sql stable security definer set search_path = public as $$
  select c.categoryid, c.categoryname, coalesce(sum(l.co2savedkg), 0)
  from activitycategories c
  left join activitytypes t on t.categoryid = c.categoryid
  left join activitylogs  l on l.activitytypeid = t.activitytypeid
                           and l.userid = p_user_id
                           and (p_from is null or l.logdate >= p_from)
                           and (p_to   is null or l.logdate <= p_to)
  group by c.categoryid, c.categoryname
  order by c.categoryid;
$$;

-- 5.11 Riwayat detail per log (FR-5.1..5.3)
create or replace function app_get_history(p_user_id uuid, p_from date default null, p_to date default null,
                                           p_category_ids int[] default null)
returns table (logid uuid, reviewid uuid, logdate date, activitytypeid int, typename varchar,
               categoryid int, categoryname varchar, inputcontroltype varchar, unitlabel varchar,
               quantity numeric, starttime time, endtime time, co2savedkg numeric, pointsearned int,
               co2coefficientused numeric, pointfactorused numeric, note text, photopath varchar)
language sql stable security definer set search_path = public as $$
  select l.logid, l.reviewid, l.logdate, t.activitytypeid, t.typename, c.categoryid, c.categoryname,
         t.inputcontroltype, t.unitlabel, l.quantity, l.starttime, l.endtime, l.co2savedkg,
         l.pointsearned, l.co2coefficientused, l.pointfactorused, l.note, l.photopath
  from activitylogs l
  join activitytypes t      on t.activitytypeid = l.activitytypeid
  join activitycategories c on c.categoryid = t.categoryid
  where l.userid = p_user_id
    and (p_from is null or l.logdate >= p_from)
    and (p_to   is null or l.logdate <= p_to)
    and (p_category_ids is null or cardinality(p_category_ids) = 0 or c.categoryid = any(p_category_ids))
  order by l.logdate desc, t.displayorder;
$$;

-- 5.12 Badge yang sudah diraih
create or replace function app_get_user_badges(p_user_id uuid)
returns table (badgeid int, earnedat timestamptz)
language sql stable security definer set search_path = public as $$
  select ub.badgeid, ub.earnedat from userbadges ub where ub.userid = p_user_id order by ub.earnedat;
$$;

-- 5.13 Leaderboard — TIDAK mengembalikan userid/email/avatar/foto (FR-7.3)
create or replace function app_get_leaderboard(p_user_id uuid, p_limit int default 20)
returns table (rank bigint, username varchar, totalco2savedkg numeric, levelname varchar, iscurrentuser boolean)
language sql stable security definer set search_path = public as $$
  select x.rank, x.username, x.totalco2savedkg, x.levelname, x.iscurrentuser
  from (
    select rank() over (order by u.totalco2savedkg desc) as rank,
           u.username, u.totalco2savedkg, lv.levelname, (u.userid = p_user_id) as iscurrentuser
    from users u left join levels lv on lv.levelid = u.currentlevelid
  ) x
  where x.rank <= greatest(p_limit, 1) or x.iscurrentuser
  order by x.rank, x.username;
$$;

-- Hak eksekusi: cabut dari PUBLIC, beri ke anon & authenticated
do $$
declare f text;
begin
  foreach f in array array[
    'app_check_availability(text,text)',
    'app_register(text,text,text,text)',
    'app_get_login_record(text)',
    'app_get_profile(uuid)',
    'app_update_profile(uuid,text,text)',
    'app_update_password(uuid,text)',
    'app_get_review_by_date(uuid,date)',
    'app_submit_evening_review(uuid,date,jsonb,numeric,int,int,int,int[])',
    'app_get_daily_totals(uuid,date,date)',
    'app_get_category_breakdown(uuid,date,date)',
    'app_get_history(uuid,date,date,int[])',
    'app_get_user_badges(uuid)',
    'app_get_leaderboard(uuid,int)']
  loop
    execute format('revoke all on function %s from public', f);
    execute format('grant execute on function %s to anon, authenticated', f);
  end loop;
end $$;

-- ---------------------------------------------------------------------
-- 6. PEMBERSIHAN DATA UJI (WAJIB — App.xaml.cs lama menyimpan password PLAINTEXT
--    "rahasia123" setiap aplikasi dijalankan; melanggar FR-1.1 & NFR Keamanan)
-- ---------------------------------------------------------------------
delete from users where passwordhash not like '$2%';   -- cascade ke review/log/badge milik user uji
alter table users add constraint ck_users_passwordhash_bcrypt check (passwordhash like '$2%');

commit;

-- =====================================================================
-- VERIFIKASI MANUAL (jalankan terpisah di SQL Editor Supabase):
--   select * from activitytypes order by displayorder;    -- 6 baris
--   select * from levels order by minpoints;                -- 4 baris, Eco Champion maxpoints NULL
--   select app_check_availability('tes','tes@x.com');
--   set role anon; select * from users;                     -- HARUS gagal: permission denied
--   set role anon; select * from activitytypes;             -- HARUS berhasil (6 baris)
--   reset role;
-- =====================================================================
