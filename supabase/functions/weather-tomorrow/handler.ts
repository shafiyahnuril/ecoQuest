import {
  formatSummary,
  getTomorrowJakarta,
  isSupportedCity,
  parseCachedDayJson,
  type ParsedDailyForecast,
} from "./accuweather.ts";

export interface CachedWeatherRow {
  city: string;
  fordate: string;
  forecastsummary: string;
  rawresponse: string | null;
  retrievedat: string;
}

export interface WeatherDeps {
  expectedApiKey?: string;
  accuWeatherApiKey?: string;
  now?: () => Date;
  getCache: (city: string, forDate: string) => Promise<CachedWeatherRow | null>;
  upsertCache: (row: CachedWeatherRow) => Promise<void>;
  getLocationKey: (city: string) => Promise<string | null>;
  saveLocationKey: (city: string, key: string) => Promise<void>;
  resolveLocationKeyRemote: (city: string, apiKey: string) => Promise<string | null>;
  fetchForecastRemote: (locationKey: string, forDate: string, apiKey: string) => Promise<ParsedDailyForecast | null>;
}

const CACHE_TTL_MS = 24 * 3_600 * 1000;
const MAX_BODY_CHARS = 2048;

function jsonResponse(status: number, payload: unknown): Response {
  return new Response(JSON.stringify(payload), {
    status,
    headers: { "Content-Type": "application/json; charset=utf-8" },
  });
}

export async function handleWeatherTomorrow(req: Request, deps: WeatherDeps): Promise<Response> {
  if (req.method !== "POST") {
    return jsonResponse(405, { error: "METHOD_NOT_ALLOWED" });
  }

  const providedApiKey = req.headers.get("apikey")?.trim() ?? "";
  if (!providedApiKey || (deps.expectedApiKey && providedApiKey !== deps.expectedApiKey)) {
    return jsonResponse(401, { error: "UNAUTHORIZED" });
  }

  let city: string;
  try {
    const rawBody = await req.text();
    if (!rawBody || rawBody.length > MAX_BODY_CHARS) {
      return jsonResponse(400, { error: "INVALID_CITY" });
    }
    const parsed = JSON.parse(rawBody) as { city?: unknown };
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed) || !isSupportedCity(parsed.city)) {
      return jsonResponse(400, { error: "INVALID_CITY" });
    }
    city = parsed.city.trim();
  } catch {
    return jsonResponse(400, { error: "INVALID_CITY" });
  }

  const now = (deps.now ?? (() => new Date()))();
  const forDate = getTomorrowJakarta(now);

  try {
    const cached = await deps.getCache(city, forDate);
    if (cached && cached.rawresponse) {
      const retrievedMs = Date.parse(cached.retrievedat);
      if (!Number.isNaN(retrievedMs) && now.getTime() - retrievedMs < CACHE_TTL_MS) {
        const cachedDay = parseCachedDayJson(cached.rawresponse, forDate);
        if (cachedDay) {
          return jsonResponse(200, {
            city,
            forDate,
            source: "cache",
            DailyForecasts: [cachedDay],
          });
        }
      }
    }

    const accuKey = deps.accuWeatherApiKey?.trim();
    if (!accuKey) {
      return jsonResponse(503, { error: "UPSTREAM_UNAVAILABLE" });
    }

    let locationKey = await deps.getLocationKey(city);
    if (!locationKey) {
      locationKey = await deps.resolveLocationKeyRemote(city, accuKey);
      if (!locationKey) {
        return jsonResponse(503, { error: "UPSTREAM_UNAVAILABLE" });
      }
      try {
        await deps.saveLocationKey(city, locationKey);
      } catch {
        // Kegagalan simpan locationKey tidak menghalangi respons forecast.
      }
    }

    const forecast = await deps.fetchForecastRemote(locationKey, forDate, accuKey);
    if (!forecast) {
      return jsonResponse(503, { error: "UPSTREAM_UNAVAILABLE" });
    }

    try {
      await deps.upsertCache({
        city,
        fordate: forDate,
        forecastsummary: formatSummary(forecast.dayPhrase, forecast.minTempC, forecast.maxTempC),
        rawresponse: forecast.rawDayJson,
        retrievedat: now.toISOString(),
      });
    } catch {
      // Kegagalan upsert cache tidak menghalangi respons forecast ke client.
    }

    return jsonResponse(200, {
      city,
      forDate,
      source: "live",
      DailyForecasts: [JSON.parse(forecast.rawDayJson)],
    });
  } catch {
    return jsonResponse(503, { error: "UPSTREAM_UNAVAILABLE" });
  }
}
