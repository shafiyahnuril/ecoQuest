-- =====================================================================
-- EcoQuest — Data demo 17 minggu (ARC-15)
-- File   : database/seed/demo_data.sql
-- Cara   : 1) Daftarkan akun demo LEWAT APLIKASI (agar hash BCrypt benar),
--             mis. username demo_dinda.
--          2) Ganti nilai v_username di bawah bila perlu.
--          3) Jalankan seluruh skrip di Supabase SQL Editor (role postgres).
-- Sifat  : Dapat diulang. Skrip MENGHAPUS semua refleksi akun demo lalu
--          membuat ulang 119 hari data (sampai KEMARIN, WIB), sehingga
--          refleksi HARI INI masih bisa diisi saat demo dan streak berlanjut.
-- Aturan : nilai CO2/poin dihitung dari koefisien di tabel activitytypes
--          (bukan angka literal) dengan rumus yang sama dengan aplikasi:
--          co2 = round(qty * koef, 4); poin = round(co2 * 20 * pf)
--          (round() numeric PostgreSQL = half away from zero, sama dengan C#).
-- PERINGATAN: jangan dijalankan untuk akun pengguna sungguhan.
-- =====================================================================
begin;

do $$
declare
  v_username   text := 'demo_dinda';                       -- << ganti bila perlu
  v_days       int  := 119;                                -- 17 minggu
  v_user       uuid;
  v_today      date := (now() at time zone 'Asia/Jakarta')::date;
  v_day        date;
  v_review     uuid;
  v_t          record;
  v_qty        numeric;
  v_start      time;
  v_end        time;
  v_minutes    int;
  v_co2        numeric;
  v_pts        int;
  v_sum_co2    numeric;
  v_sum_pts    int;
  v_streak     int := 0;
  v_max_streak int := 0;
  v_prev_day   date;
  v_logs       int;
begin
  select userid into v_user from users where lower(username) = lower(v_username);
  if v_user is null then
    raise exception 'Akun % belum ada. Daftarkan dulu lewat aplikasi.', v_username;
  end if;

  perform setseed(0.42);                                    -- hasil acak yang sama setiap dijalankan

  delete from userbadges   where userid = v_user;
  delete from dailyreviews where userid = v_user;           -- cascade ke activitylogs
  update users set totalpoints = 0, totalco2savedkg = 0, currentstreak = 0 where userid = v_user;

  for i in reverse v_days..1 loop
    v_day := v_today - i;                                   -- berakhir KEMARIN
    -- ±18% hari tanpa refleksi (streak putus), KECUALI 5 hari terakhir selalu terisi agar streak terlihat saat demo
    if i > 5 and random() < 0.18 then continue; end if;

    insert into dailyreviews (userid, reviewdate, totalco2savedkg, totalpointsearned, submittedat)
    values (v_user, v_day, 0, 0, (v_day + time '21:30') at time zone 'Asia/Jakarta')
    returning reviewid into v_review;

    v_sum_co2 := 0; v_sum_pts := 0;
    for v_t in select * from activitytypes order by displayorder loop
      continue when random() < 0.45;                        -- tidak semua aktivitas dilakukan
      v_start := null; v_end := null;
      case v_t.inputcontroltype
        when 'Numeric' then
          v_qty := round((0.5 + random() * least(coalesce(v_t.plausiblemaxperday, v_t.hardmaxperday), 15))::numeric, 2);
        when 'TimeRange' then
          v_start   := time '19:00' + make_interval(mins => (floor(random() * 6) * 30)::int);
          v_minutes := 30 + (floor(random() * 10) * 30)::int;            -- 0,5–5 jam
          v_end     := v_start + make_interval(mins => v_minutes);       -- boleh lewat tengah malam
          v_qty     := round(v_minutes / 60.0, 4);
        when 'Count' then
          v_qty := 1 + floor(random() * least(coalesce(v_t.plausiblemaxperday, v_t.hardmaxperday), 4));
        when 'Toggle' then
          v_qty := 1;
      end case;

      v_co2 := round(v_qty * v_t.co2coefficient, 4);
      v_pts := round(v_co2 * 20 * v_t.pointfactor)::int;

      insert into activitylogs (logid, reviewid, userid, activitytypeid, logdate, quantity, starttime, endtime,
                                note, photopath, pointsearned, co2savedkg, co2coefficientused, pointfactorused)
      values (gen_random_uuid(), v_review, v_user, v_t.activitytypeid, v_day, v_qty, v_start, v_end,
              null, null, v_pts, v_co2, v_t.co2coefficient, v_t.pointfactor);

      v_sum_co2 := v_sum_co2 + v_co2;
      v_sum_pts := v_sum_pts + v_pts;
    end loop;

    update dailyreviews set totalco2savedkg = v_sum_co2, totalpointsearned = v_sum_pts where reviewid = v_review;

    v_streak := case when v_prev_day = v_day - 1 then v_streak + 1 else 1 end;
    v_max_streak := greatest(v_max_streak, v_streak);
    v_prev_day := v_day;
  end loop;

  -- streak tersimpan = streak beruntun yang berakhir KEMARIN (0 bila kemarin kosong)
  if v_prev_day is distinct from v_today - 1 then v_streak := 0; end if;

  update users u set
    totalco2savedkg = coalesce((select sum(totalco2savedkg)   from dailyreviews where userid = v_user), 0),
    totalpoints     = coalesce((select sum(totalpointsearned) from dailyreviews where userid = v_user), 0),
    currentstreak   = v_streak
  where u.userid = v_user;

  update users u set currentlevelid = (
    select l.levelid from levels l
    where u.totalpoints >= l.minpoints and (l.maxpoints is null or u.totalpoints <= l.maxpoints)
    order by l.minpoints limit 1)
  where u.userid = v_user;

  select count(*) into v_logs from activitylogs where userid = v_user;
  insert into userbadges (userid, badgeid, earnedat)
  select v_user, b.badgeid, now()
  from badges b, users u
  where u.userid = v_user
    and (   (b.criteriatype = 'ActivityCount'     and v_logs            >= b.criteriavalue)
         or (b.criteriatype = 'StreakDays'        and v_max_streak      >= b.criteriavalue)
         or (b.criteriatype = 'CO2SavedThreshold' and u.totalco2savedkg >= b.criteriavalue)
         or (b.criteriatype = 'PointsThreshold'   and u.totalpoints     >= b.criteriavalue))
  on conflict do nothing;

  raise notice 'Selesai: % refleksi, % log, streak tersimpan %, streak terpanjang %',
    (select count(*) from dailyreviews where userid = v_user), v_logs, v_streak, v_max_streak;
end $$;

commit;

-- VERIFIKASI (jalankan terpisah):
-- select count(*), min(reviewdate), max(reviewdate) from dailyreviews r join users u using (userid) where u.username = 'demo_dinda';
-- select username, totalco2savedkg, totalpoints, currentstreak, currentlevelid from users where username = 'demo_dinda';
-- select abs(sum(r.totalco2savedkg) - u.totalco2savedkg) < 0.0001 as total_cocok
--   from dailyreviews r join users u using (userid) where u.username = 'demo_dinda' group by u.totalco2savedkg;
