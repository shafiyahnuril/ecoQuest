import { parseAccuWeather5Day } from "./accuweather.ts";
import { handleWeatherTomorrow, type CachedWeatherRow, type WeatherDeps } from "./handler.ts";

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

const NOW = new Date("2026-11-09T03:00:00Z"); // 2026-11-09 10:00 WIB -> besok 2026-11-10
const SECRET_ACCU_KEY = "SUPER_SECRET_ACCU_KEY_VALUE";

function baseDeps(overrides: Partial<WeatherDeps> = {}): {
  deps: WeatherDeps;
  calls: { resolve: number; forecast: number; upsert: number; saveLoc: number };
  logs: Record<string, unknown>[];
} {
  const calls = { resolve: 0, forecast: 0, upsert: 0, saveLoc: 0 };
  const logs: Record<string, unknown>[] = [];
  const deps: WeatherDeps = {
    expectedApiKey: "sb_publishable_test_key",
    accuWeatherApiKey: SECRET_ACCU_KEY,
    now: () => NOW,
    logError: (entry) => logs.push(entry),
    getCache: async () => null,
    upsertCache: async () => {
      calls.upsert++;
    },
    getLocationKey: async () => null,
    saveLocationKey: async () => {
      calls.saveLoc++;
    },
    resolveLocationKeyRemote: async () => {
      calls.resolve++;
      return "202119";
    },
    fetchForecastRemote: async (_loc, forDate) => {
      calls.forecast++;
      return parseAccuWeather5Day(fixture, forDate);
    },
    ...overrides,
  };
  return { deps, calls, logs };
}

Deno.test("GET method returns 405", async () => {
  const { deps } = baseDeps();
  const req = new Request("https://example.supabase.co/functions/v1/weather-tomorrow", {
    method: "GET",
    headers: { apikey: "sb_publishable_test_key" },
  });
  const resp = await handleWeatherTomorrow(req, deps);
  assertEquals(resp.status, 405);
});

Deno.test("Missing or wrong apikey header returns 401", async () => {
  const { deps, calls } = baseDeps();
  const noKey = new Request("https://example.supabase.co/functions/v1/weather-tomorrow", {
    method: "POST",
    body: JSON.stringify({ city: "Yogyakarta" }),
  });
  assertEquals((await handleWeatherTomorrow(noKey, deps)).status, 401);

  const badKey = new Request("https://example.supabase.co/functions/v1/weather-tomorrow", {
    method: "POST",
    headers: { apikey: "salah" },
    body: JSON.stringify({ city: "Yogyakarta" }),
  });
  assertEquals((await handleWeatherTomorrow(badKey, deps)).status, 401);
  assertEquals(calls.forecast, 0);
});

Deno.test("Unsupported city or SSRF payload returns 400 without calling AccuWeather", async () => {
  const { deps, calls } = baseDeps();
  for (const badCity of ["KotaFiktif", "Jakarta?apikey=123", "Jakarta&apikey=hack", "", null]) {
    const req = new Request("https://example.supabase.co/functions/v1/weather-tomorrow", {
      method: "POST",
      headers: { apikey: "sb_publishable_test_key" },
      body: JSON.stringify({ city: badCity }),
    });
    const resp = await handleWeatherTomorrow(req, deps);
    assertEquals(resp.status, 400, `city=${String(badCity)}`);
  }
  assertEquals(calls.resolve, 0);
  assertEquals(calls.forecast, 0);
});

Deno.test("Cache hit (< 24h) returns source=cache without calling AccuWeather", async () => {
  const parsed = parseAccuWeather5Day(fixture, "2026-11-10")!;
  const cachedRow: CachedWeatherRow = {
    city: "Yogyakarta",
    fordate: "2026-11-10",
    forecastsummary: "Cerah berawan, 24–31°C",
    rawresponse: parsed.rawDayJson,
    retrievedat: new Date(NOW.getTime() - 2 * 3_600_000).toISOString(),
  };

  const { deps, calls } = baseDeps({
    getCache: async () => cachedRow,
  });

  const req = new Request("https://example.supabase.co/functions/v1/weather-tomorrow", {
    method: "POST",
    headers: { apikey: "sb_publishable_test_key" },
    body: JSON.stringify({ city: "Yogyakarta" }),
  });
  const resp = await handleWeatherTomorrow(req, deps);
  assertEquals(resp.status, 200);
  const body = await resp.json();
  assertEquals(body.source, "cache");
  assertEquals(body.city, "Yogyakarta");
  assertEquals(body.forDate, "2026-11-10");
  assert(Array.isArray(body.DailyForecasts) && body.DailyForecasts.length === 1, "DailyForecasts returned");
  assertEquals(calls.resolve, 0);
  assertEquals(calls.forecast, 0);
  assertEquals(calls.upsert, 0);
});

Deno.test("Cache miss fetches AccuWeather, saves locationKey, and upserts weathercache", async () => {
  const { deps, calls, logs } = baseDeps();
  const req = new Request("https://example.supabase.co/functions/v1/weather-tomorrow", {
    method: "POST",
    headers: { apikey: "sb_publishable_test_key" },
    body: JSON.stringify({ city: "Yogyakarta" }),
  });
  const resp = await handleWeatherTomorrow(req, deps);
  assertEquals(resp.status, 200);
  const body = await resp.json();
  assertEquals(body.source, "live");
  assertEquals(calls.resolve, 1);
  assertEquals(calls.saveLoc, 1);
  assertEquals(calls.forecast, 1);
  assertEquals(calls.upsert, 1);
  assertEquals(logs.length, 0);
  const text = JSON.stringify(body);
  assert(!text.includes(SECRET_ACCU_KEY), "must not contain AccuWeather API key");
  assert(!text.includes("accuweather.com"), "must not contain upstream domain");
});

Deno.test("Each 503 branch and saveLocationKey failure emits structured log without leaking secrets", async () => {
  // 1. Missing API key -> 503 + structured log
  {
    const { deps, logs } = baseDeps({ accuWeatherApiKey: "" });
    const resp = await handleWeatherTomorrow(
      new Request("https://example.supabase.co/functions/v1/weather-tomorrow", {
        method: "POST",
        headers: { apikey: "sb_publishable_test_key" },
        body: JSON.stringify({ city: "Solo" }),
      }),
      deps,
    );
    assertEquals(resp.status, 503);
    assertEquals(logs.length, 1);
    assertEquals(logs[0].step, "handler.check_api_key");
    assertEquals(logs[0].errorType, "missing_api_key");
  }

  // 2. Unresolved location -> 503 + structured log
  {
    const { deps, logs } = baseDeps({ resolveLocationKeyRemote: async () => null });
    const resp = await handleWeatherTomorrow(
      new Request("https://example.supabase.co/functions/v1/weather-tomorrow", {
        method: "POST",
        headers: { apikey: "sb_publishable_test_key" },
        body: JSON.stringify({ city: "Solo" }),
      }),
      deps,
    );
    assertEquals(resp.status, 503);
    assertEquals(logs.length, 1);
    assertEquals(logs[0].step, "handler.resolve_location");
    assertEquals(logs[0].errorType, "location_unresolved");
  }

  // 3. saveLocationKey failure is logged (not silently swallowed) while still returning forecast
  {
    const { deps, logs } = baseDeps({
      saveLocationKey: async () => {
        throw new Error("rls_denied");
      },
    });
    const resp = await handleWeatherTomorrow(
      new Request("https://example.supabase.co/functions/v1/weather-tomorrow", {
        method: "POST",
        headers: { apikey: "sb_publishable_test_key" },
        body: JSON.stringify({ city: "Solo" }),
      }),
      deps,
    );
    assertEquals(resp.status, 200);
    assertEquals(logs.length, 1);
    assertEquals(logs[0].step, "handler.save_location_key");
    assertEquals(logs[0].errorType, "db_save_location_failed");
  }

  // 4. Forecast unavailable -> 503 + structured log
  {
    const { deps, logs } = baseDeps({ fetchForecastRemote: async () => null });
    const resp = await handleWeatherTomorrow(
      new Request("https://example.supabase.co/functions/v1/weather-tomorrow", {
        method: "POST",
        headers: { apikey: "sb_publishable_test_key" },
        body: JSON.stringify({ city: "Solo" }),
      }),
      deps,
    );
    assertEquals(resp.status, 503);
    assertEquals(logs.length, 1);
    assertEquals(logs[0].step, "handler.fetch_forecast");
    assertEquals(logs[0].errorType, "forecast_unavailable");
  }

  // 5. Unhandled exception with secret in message -> 503 + log without leaking secret/URL
  {
    const { deps, logs } = baseDeps({
      fetchForecastRemote: async () => {
        throw new Error(`GET https://dataservice.accuweather.com/foo?apikey=${SECRET_ACCU_KEY} failed 503`);
      },
    });
    const resp = await handleWeatherTomorrow(
      new Request("https://example.supabase.co/functions/v1/weather-tomorrow", {
        method: "POST",
        headers: { apikey: "sb_publishable_test_key" },
        body: JSON.stringify({ city: "Yogyakarta" }),
      }),
      deps,
    );
    assertEquals(resp.status, 503);
    assertEquals(logs.length, 1);
    assertEquals(logs[0].step, "handler.unhandled_exception");
    const serializedLog = JSON.stringify(logs[0]);
    assert(!serializedLog.includes(SECRET_ACCU_KEY), "log must never leak secret key");
    assert(!serializedLog.includes("accuweather.com"), "log must never leak upstream URL");
    const text = await resp.text();
    assert(!text.includes(SECRET_ACCU_KEY), "body must never leak secret key");
    assert(!text.includes("accuweather.com"), "body must never leak upstream URL");
  }
});
