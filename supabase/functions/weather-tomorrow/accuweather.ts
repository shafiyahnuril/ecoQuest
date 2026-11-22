export const SUPPORTED_CITIES = [
  "Ambon",
  "Balikpapan",
  "Banda Aceh",
  "Bandar Lampung",
  "Bandung",
  "Banjarmasin",
  "Bantul",
  "Batam",
  "Bekasi",
  "Bengkulu",
  "Bogor",
  "Denpasar",
  "Depok",
  "Gorontalo",
  "Gunungkidul",
  "Jakarta",
  "Jambi",
  "Jayapura",
  "Kendari",
  "Klaten",
  "Kulon Progo",
  "Kupang",
  "Magelang",
  "Makassar",
  "Malang",
  "Manado",
  "Mataram",
  "Medan",
  "Padang",
  "Palangka Raya",
  "Palembang",
  "Palu",
  "Pekanbaru",
  "Pontianak",
  "Samarinda",
  "Semarang",
  "Serang",
  "Sleman",
  "Solo",
  "Surabaya",
  "Tangerang",
  "Tanjung Pinang",
  "Yogyakarta",
] as const;

const SUPPORTED_CITY_SET = new Set<string>(SUPPORTED_CITIES);

const WIT_CITIES = new Set<string>(["Ambon", "Jayapura"]);
const WITA_CITIES = new Set<string>([
  "Balikpapan",
  "Banjarmasin",
  "Denpasar",
  "Gorontalo",
  "Kendari",
  "Kupang",
  "Makassar",
  "Manado",
  "Mataram",
  "Palu",
  "Samarinda",
]);

const CITY_SEARCH_ALIASES: Record<string, readonly string[]> = {
  // Query "Surakarta" lebih dulu karena query "Solo" di AccuWeather sempat mengembalikan desa Solo (+08:00).
  Solo: ["Surakarta", "Solo"],
};

export type LogErrorFn = (entry: Record<string, unknown>) => void;

export const defaultLogError: LogErrorFn = (entry) => {
  console.error(JSON.stringify({ level: "error", fn: "weather-tomorrow", ...entry }));
};

export function isSupportedCity(city: unknown): city is string {
  return typeof city === "string" && SUPPORTED_CITY_SET.has(city.trim());
}

export function getExpectedGmtOffset(city: string): number {
  const trimmed = city.trim();
  if (WIT_CITIES.has(trimmed)) return 9;
  if (WITA_CITIES.has(trimmed)) return 8;
  return 7;
}

export function getSearchQueriesForCity(city: string): string[] {
  const trimmed = city.trim();
  const aliases = CITY_SEARCH_ALIASES[trimmed];
  return aliases ? [...aliases] : [trimmed];
}

export function selectMatchingLocationKey(candidates: unknown, city: string): string | null {
  if (!Array.isArray(candidates)) return null;
  const expectedOffset = getExpectedGmtOffset(city);

  for (const item of candidates) {
    if (!item || typeof item !== "object" || Array.isArray(item)) continue;
    const obj = item as Record<string, unknown>;
    const key = typeof obj.Key === "string" ? obj.Key.trim() : "";
    const countryId = (obj.Country as { ID?: unknown } | undefined)?.ID;
    const gmtOffset = (obj.TimeZone as { GmtOffset?: unknown } | undefined)?.GmtOffset;

    if (
      key.length > 0 &&
      countryId === "ID" &&
      typeof gmtOffset === "number" &&
      gmtOffset === expectedOffset
    ) {
      return key;
    }
  }

  return null;
}

function isQuotaOrAuthStatus(status: number): boolean {
  return status === 401 || status === 403 || status === 429;
}

/** Mengembalikan tanggal besok di zona waktu Asia/Jakarta (UTC+7) dalam format YYYY-MM-DD. */
export function getTomorrowJakarta(now: Date = new Date()): string {
  const jakartaTomorrowMs = now.getTime() + (7 + 24) * 3_600_000;
  return new Date(jakartaTomorrowMs).toISOString().slice(0, 10);
}

export interface ParsedDailyForecast {
  date: string;
  minTempC: number;
  maxTempC: number;
  dayPhrase: string;
  dayIcon: number;
  hasPrecipitation: boolean;
  precipitationType: string | null;
  rawDayJson: string;
}

function parseDailyElement(d: Record<string, unknown>, targetDate: string): ParsedDailyForecast | null {
  const rawDate = d.Date;
  if (typeof rawDate !== "string" || rawDate.slice(0, 10) !== targetDate) {
    return null;
  }

  const temp = d.Temperature as Record<string, Record<string, unknown>> | undefined;
  const minTempC = temp?.Minimum?.Value;
  const maxTempC = temp?.Maximum?.Value;
  const day = d.Day as Record<string, unknown> | undefined;

  if (
    typeof minTempC !== "number" ||
    typeof maxTempC !== "number" ||
    !day ||
    typeof day.Icon !== "number" ||
    typeof day.HasPrecipitation !== "boolean"
  ) {
    return null;
  }

  return {
    date: targetDate,
    minTempC,
    maxTempC,
    dayPhrase: typeof day.IconPhrase === "string" ? day.IconPhrase : "",
    dayIcon: day.Icon,
    hasPrecipitation: day.HasPrecipitation,
    precipitationType: typeof day.PrecipitationType === "string" ? day.PrecipitationType : null,
    rawDayJson: JSON.stringify(d),
  };
}

export function parseAccuWeather5Day(rawJson: string, targetDate: string): ParsedDailyForecast | null {
  try {
    const doc = JSON.parse(rawJson) as { DailyForecasts?: unknown };
    if (!Array.isArray(doc?.DailyForecasts)) {
      return null;
    }

    for (const item of doc.DailyForecasts) {
      if (!item || typeof item !== "object") continue;
      const parsed = parseDailyElement(item as Record<string, unknown>, targetDate);
      if (parsed) return parsed;
    }
    return null;
  } catch {
    return null;
  }
}

export function parseCachedDayJson(rawDayJson: string, targetDate: string): Record<string, unknown> | null {
  try {
    const parsedObj = JSON.parse(rawDayJson) as Record<string, unknown>;
    if (!parsedObj || typeof parsedObj !== "object" || Array.isArray(parsedObj)) {
      return null;
    }
    const validated = parseDailyElement(parsedObj, targetDate);
    return validated ? parsedObj : null;
  } catch {
    return null;
  }
}

function formatIdNumber(n: number): string {
  const rounded = Math.round(n * 10) / 10;
  return Number.isInteger(rounded) ? String(rounded) : String(rounded).replace(".", ",");
}

export function formatSummary(phrase: string, minTempC: number, maxTempC: number): string {
  return `${phrase}, ${formatIdNumber(minTempC)}–${formatIdNumber(maxTempC)}°C`;
}

export type FetchLike = (input: string | URL | Request, init?: RequestInit) => Promise<Response>;

export async function resolveLocationKeyFromAccuWeather(
  city: string,
  apiKey: string,
  fetchFn: FetchLike = fetch,
  timeoutMs = 4000,
  logError: LogErrorFn = defaultLogError,
): Promise<string | null> {
  for (const query of getSearchQueriesForCity(city)) {
    const url =
      `https://dataservice.accuweather.com/locations/v1/cities/ID/search?apikey=${encodeURIComponent(apiKey)}&q=${encodeURIComponent(query)}&language=id-id`;
    const controller = new AbortController();
    let timedOut = false;
    const timer = setTimeout(() => {
      timedOut = true;
      controller.abort();
    }, timeoutMs);

    try {
      const resp = await fetchFn(url, { signal: controller.signal });
      if (!resp.ok) {
        logError({
          step: "resolveLocationKeyFromAccuWeather",
          city,
          query,
          httpStatus: resp.status,
          errorType: isQuotaOrAuthStatus(resp.status) ? "quota_or_auth_error" : "http_error",
        });
        return null;
      }

      let arr: unknown;
      try {
        arr = await resp.json();
      } catch {
        logError({
          step: "resolveLocationKeyFromAccuWeather",
          city,
          query,
          httpStatus: resp.status,
          errorType: "invalid_json",
        });
        continue;
      }

      const key = selectMatchingLocationKey(arr, city);
      if (key) {
        return key;
      }

      logError({
        step: "resolveLocationKeyFromAccuWeather",
        city,
        query,
        httpStatus: resp.status,
        errorType: "city_not_found",
        candidateCount: Array.isArray(arr) ? arr.length : 0,
      });
    } catch (err) {
      const errName = (err as { name?: string } | null)?.name;
      const errorType = timedOut
        ? "timeout"
        : (errName === "AbortError" || controller.signal.aborted)
        ? "abort"
        : "network_error";
      logError({
        step: "resolveLocationKeyFromAccuWeather",
        city,
        query,
        httpStatus: null,
        errorType,
      });
      return null;
    } finally {
      clearTimeout(timer);
    }
  }

  return null;
}

export async function fetchForecastFromAccuWeather(
  locationKey: string,
  targetDate: string,
  apiKey: string,
  fetchFn: FetchLike = fetch,
  timeoutMs = 4000,
  logError: LogErrorFn = defaultLogError,
): Promise<ParsedDailyForecast | null> {
  const url =
    `https://dataservice.accuweather.com/forecasts/v1/daily/5day/${encodeURIComponent(locationKey)}?apikey=${encodeURIComponent(apiKey)}&language=id-id&metric=true`;
  const controller = new AbortController();
  let timedOut = false;
  const timer = setTimeout(() => {
    timedOut = true;
    controller.abort();
  }, timeoutMs);

  try {
    const resp = await fetchFn(url, { signal: controller.signal });
    if (!resp.ok) {
      logError({
        step: "fetchForecastFromAccuWeather",
        locationKey,
        forDate: targetDate,
        httpStatus: resp.status,
        errorType: isQuotaOrAuthStatus(resp.status) ? "quota_or_auth_error" : "http_error",
      });
      return null;
    }

    const text = await resp.text();
    const parsed = parseAccuWeather5Day(text, targetDate);
    if (!parsed) {
      logError({
        step: "fetchForecastFromAccuWeather",
        locationKey,
        forDate: targetDate,
        httpStatus: resp.status,
        errorType: "target_date_not_found",
      });
    }
    return parsed;
  } catch (err) {
    const errName = (err as { name?: string } | null)?.name;
    const errorType = timedOut
      ? "timeout"
      : (errName === "AbortError" || controller.signal.aborted)
      ? "abort"
      : "network_error";
    logError({
      step: "fetchForecastFromAccuWeather",
      locationKey,
      forDate: targetDate,
      httpStatus: null,
      errorType,
    });
    return null;
  } finally {
    clearTimeout(timer);
  }
}
