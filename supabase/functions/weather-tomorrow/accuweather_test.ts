import {
  fetchForecastFromAccuWeather,
  formatSummary,
  getExpectedGmtOffset,
  getSearchQueriesForCity,
  getTomorrowJakarta,
  isSupportedCity,
  parseAccuWeather5Day,
  resolveLocationKeyFromAccuWeather,
  selectMatchingLocationKey,
  type FetchLike,
} from "./accuweather.ts";

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(`Assertion failed: ${msg}`);
}

function assertEquals<T>(actual: T, expected: T, msg?: string): void {
  if (actual !== expected) {
    throw new Error(`${msg ?? "Mismatch"}: expected ${String(expected)}, got ${String(actual)}`);
  }
}

const fixture = await Deno.readTextFile(
  new URL("./fixtures/accuweather_5day.json", import.meta.url),
);

const SECRET_KEY = "ACCU_UNIT_TEST_SECRET_KEY";

Deno.test("parseAccuWeather5Day_10_Nov_is_24_to_31_cerah_berawan_without_rain", () => {
  const f = parseAccuWeather5Day(fixture, "2026-11-10");
  assert(f !== null, "forecast 10 Nov should exist");
  assertEquals(f!.minTempC, 24);
  assertEquals(f!.maxTempC, 31);
  assertEquals(f!.dayPhrase, "Cerah berawan");
  assertEquals(f!.hasPrecipitation, false);
});

Deno.test("parseAccuWeather5Day_11_Nov_has_rain", () => {
  const f = parseAccuWeather5Day(fixture, "2026-11-11");
  assert(f !== null, "forecast 11 Nov should exist");
  assertEquals(f!.hasPrecipitation, true);
  assertEquals(f!.precipitationType, "Rain");
});

Deno.test("parseAccuWeather5Day_12_Nov_max_is_34", () => {
  const f = parseAccuWeather5Day(fixture, "2026-11-12");
  assert(f !== null, "forecast 12 Nov should exist");
  assertEquals(f!.maxTempC, 34);
});

Deno.test("parseAccuWeather5Day_date_outside_fixture_is_null", () => {
  const f = parseAccuWeather5Day(fixture, "2026-11-20");
  assertEquals(f, null);
});

Deno.test("isSupportedCity_validates_whitelist_and_rejects_injection", () => {
  assertEquals(isSupportedCity("Yogyakarta"), true);
  assertEquals(isSupportedCity("Jakarta"), true);
  assertEquals(isSupportedCity("Jakarta&apikey=hack"), false);
  assertEquals(isSupportedCity("KotaFiktif"), false);
  assertEquals(isSupportedCity(""), false);
});

Deno.test("getTomorrowJakarta_uses_UTC_plus_7_boundary", () => {
  assertEquals(getTomorrowJakarta(new Date("2026-11-09T16:59:59Z")), "2026-11-10");
  assertEquals(getTomorrowJakarta(new Date("2026-11-09T17:00:00Z")), "2026-11-11");
});

Deno.test("formatSummary_matches_indonesian_format", () => {
  assertEquals(formatSummary("Cerah berawan", 24, 31), "Cerah berawan, 24–31°C");
  assertEquals(formatSummary("Hujan", 23.5, 29), "Hujan, 23,5–29°C");
});

Deno.test("getExpectedGmtOffset_maps_WIB_WITA_WIT_correctly", () => {
  assertEquals(getExpectedGmtOffset("Solo"), 7);
  assertEquals(getExpectedGmtOffset("Yogyakarta"), 7);
  assertEquals(getExpectedGmtOffset("Pontianak"), 7);
  assertEquals(getExpectedGmtOffset("Denpasar"), 8);
  assertEquals(getExpectedGmtOffset("Makassar"), 8);
  assertEquals(getExpectedGmtOffset("Balikpapan"), 8);
  assertEquals(getExpectedGmtOffset("Ambon"), 9);
  assertEquals(getExpectedGmtOffset("Jayapura"), 9);
});

Deno.test("selectMatchingLocationKey_rejects_wrong_country_and_wrong_timezone", () => {
  const candidates = [
    { Key: "999001", Country: { ID: "PH" }, TimeZone: { GmtOffset: 7 } },
    { Key: "3471178", Country: { ID: "ID" }, TimeZone: { GmtOffset: 8 } },
    { Key: "203190", Country: { ID: "ID" }, TimeZone: { GmtOffset: 7 } },
  ];
  assertEquals(selectMatchingLocationKey(candidates, "Solo"), "203190");

  const onlyWrongOffset = [
    { Key: "3471178", Country: { ID: "ID" }, TimeZone: { GmtOffset: 8 } },
  ];
  assertEquals(selectMatchingLocationKey(onlyWrongOffset, "Solo"), null);
});

Deno.test("resolveLocationKeyFromAccuWeather_uses_Surakarta_first_for_Solo", async () => {
  assertEquals(getSearchQueriesForCity("Solo").join(","), "Surakarta,Solo");

  const queriedUrls: string[] = [];
  const logs: Record<string, unknown>[] = [];
  const stubFetch: FetchLike = async (input) => {
    const url = String(input);
    queriedUrls.push(url);
    return new Response(
      JSON.stringify([
        { Key: "203190", LocalizedName: "Surakarta", Country: { ID: "ID" }, TimeZone: { GmtOffset: 7 } },
      ]),
      { status: 200 },
    );
  };

  const key = await resolveLocationKeyFromAccuWeather("Solo", SECRET_KEY, stubFetch, 4000, (e) => logs.push(e));
  assertEquals(key, "203190");
  assertEquals(queriedUrls.length, 1);
  assert(queriedUrls[0].includes("q=Surakarta"), "first query must be Surakarta");
  assertEquals(logs.length, 0);
});

Deno.test("resolveLocationKeyFromAccuWeather_logs_quota_exhausted_for_401_403_429_without_leaking_url", async () => {
  for (const status of [401, 403, 429]) {
    const logs: Record<string, unknown>[] = [];
    let fetchCount = 0;
    const stubFetch: FetchLike = async () => {
      fetchCount++;
      return new Response("Quota exceeded", { status });
    };

    const key = await resolveLocationKeyFromAccuWeather("Solo", SECRET_KEY, stubFetch, 4000, (e) => logs.push(e));
    assertEquals(key, null);
    assertEquals(fetchCount, 1, "must stop immediately on quota/auth error instead of retrying alias");
    assertEquals(logs.length, 1);
    assertEquals(logs[0].step, "resolveLocationKeyFromAccuWeather");
    assertEquals(logs[0].httpStatus, status);
    assertEquals(logs[0].errorType, "quota_or_auth_error");
    const serialized = JSON.stringify(logs[0]);
    assert(!serialized.includes(SECRET_KEY), "log must never contain apikey");
    assert(!serialized.includes("dataservice.accuweather.com"), "log must never contain URL");
  }
});

Deno.test("resolveLocationKeyFromAccuWeather_distinguishes_city_not_found_and_logs_abort_or_timeout", async () => {
  const notFoundLogs: Record<string, unknown>[] = [];
  const notFoundFetch: FetchLike = async () =>
    new Response(JSON.stringify([{ Key: "3471178", Country: { ID: "ID" }, TimeZone: { GmtOffset: 8 } }]), {
      status: 200,
    });

  const missingKey = await resolveLocationKeyFromAccuWeather(
    "Solo",
    SECRET_KEY,
    notFoundFetch,
    4000,
    (e) => notFoundLogs.push(e),
  );
  assertEquals(missingKey, null);
  assertEquals(notFoundLogs.length, 2);
  assertEquals(notFoundLogs[0].errorType, "city_not_found");
  assertEquals(notFoundLogs[0].httpStatus, 200);

  const abortLogs: Record<string, unknown>[] = [];
  const abortFetch: FetchLike = async () => {
    throw new DOMException("Aborted", "AbortError");
  };
  const abortedKey = await resolveLocationKeyFromAccuWeather(
    "Yogyakarta",
    SECRET_KEY,
    abortFetch,
    4000,
    (e) => abortLogs.push(e),
  );
  assertEquals(abortedKey, null);
  assertEquals(abortLogs.length, 1);
  assertEquals(abortLogs[0].errorType, "abort");
  assertEquals(abortLogs[0].httpStatus, null);
});

Deno.test("fetchForecastFromAccuWeather_logs_quota_and_timeout_without_leaking_url", async () => {
  const quotaLogs: Record<string, unknown>[] = [];
  const quotaFetch: FetchLike = async () => new Response("Unauthorized", { status: 403 });
  const res = await fetchForecastFromAccuWeather(
    "203190",
    "2026-11-10",
    SECRET_KEY,
    quotaFetch,
    4000,
    (e) => quotaLogs.push(e),
  );
  assertEquals(res, null);
  assertEquals(quotaLogs.length, 1);
  assertEquals(quotaLogs[0].step, "fetchForecastFromAccuWeather");
  assertEquals(quotaLogs[0].httpStatus, 403);
  assertEquals(quotaLogs[0].errorType, "quota_or_auth_error");
  assert(!JSON.stringify(quotaLogs[0]).includes(SECRET_KEY), "log must never contain apikey");

  const timeoutLogs: Record<string, unknown>[] = [];
  const slowFetch: FetchLike = (_input, init) =>
    new Promise((_resolve, reject) => {
      init?.signal?.addEventListener("abort", () => reject(new DOMException("Timed out", "AbortError")));
    });
  const timedOutRes = await fetchForecastFromAccuWeather(
    "203190",
    "2026-11-10",
    SECRET_KEY,
    slowFetch,
    15,
    (e) => timeoutLogs.push(e),
  );
  assertEquals(timedOutRes, null);
  assertEquals(timeoutLogs.length, 1);
  assertEquals(timeoutLogs[0].errorType, "timeout");
  assertEquals(timeoutLogs[0].httpStatus, null);
});
