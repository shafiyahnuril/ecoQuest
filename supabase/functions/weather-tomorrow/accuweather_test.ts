import {
  formatSummary,
  getTomorrowJakarta,
  isSupportedCity,
  parseAccuWeather5Day,
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
