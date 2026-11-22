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

export function isSupportedCity(city: unknown): city is string {
  return typeof city === "string" && SUPPORTED_CITY_SET.has(city.trim());
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
): Promise<string | null> {
  const url =
    `https://dataservice.accuweather.com/locations/v1/cities/ID/search?apikey=${encodeURIComponent(apiKey)}&q=${encodeURIComponent(city)}&language=id-id`;
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const resp = await fetchFn(url, { signal: controller.signal });
    if (!resp.ok) return null;
    const arr = await resp.json();
    if (Array.isArray(arr) && arr.length > 0 && typeof arr[0]?.Key === "string") {
      return arr[0].Key;
    }
    return null;
  } catch {
    return null;
  } finally {
    clearTimeout(timer);
  }
}

export async function fetchForecastFromAccuWeather(
  locationKey: string,
  targetDate: string,
  apiKey: string,
  fetchFn: FetchLike = fetch,
  timeoutMs = 4000,
): Promise<ParsedDailyForecast | null> {
  const url =
    `https://dataservice.accuweather.com/forecasts/v1/daily/5day/${encodeURIComponent(locationKey)}?apikey=${encodeURIComponent(apiKey)}&language=id-id&metric=true`;
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const resp = await fetchFn(url, { signal: controller.signal });
    if (!resp.ok) return null;
    const text = await resp.text();
    return parseAccuWeather5Day(text, targetDate);
  } catch {
    return null;
  } finally {
    clearTimeout(timer);
  }
}
