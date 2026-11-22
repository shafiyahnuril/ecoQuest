import { createClient } from "@supabase/supabase-js";
import {
  defaultLogError,
  fetchForecastFromAccuWeather,
  resolveLocationKeyFromAccuWeather,
} from "./accuweather.ts";
import { handleWeatherTomorrow, type CachedWeatherRow, type WeatherDeps } from "./handler.ts";

Deno.serve(async (req: Request): Promise<Response> => {
  const supabaseUrl = Deno.env.get("SUPABASE_URL") ?? "";
  const serviceRoleKey =
    Deno.env.get("SUPABASE_SERVICE_ROLE_KEY") ?? Deno.env.get("SERVICE_ROLE_KEY") ?? "";

  if (!supabaseUrl || !serviceRoleKey) {
    defaultLogError({
      step: "index.startup_env",
      httpStatus: 500,
      errorType: "server_misconfigured",
    });
    return new Response(JSON.stringify({ error: "SERVER_MISCONFIGURED" }), {
      status: 500,
      headers: { "Content-Type": "application/json; charset=utf-8" },
    });
  }

  const admin = createClient(supabaseUrl, serviceRoleKey, {
    auth: { persistSession: false, autoRefreshToken: false },
  });

  const deps: WeatherDeps = {
    expectedApiKey:
      Deno.env.get("ECOQUEST_PUBLISHABLE_KEY") ??
      Deno.env.get("SUPABASE_PUBLISHABLE_KEY") ??
      undefined,
    accuWeatherApiKey: Deno.env.get("ACCUWEATHER_API_KEY") ?? undefined,

    async getCache(city: string, forDate: string): Promise<CachedWeatherRow | null> {
      const { data, error } = await admin
        .from("weathercache")
        .select("city, fordate, forecastsummary, rawresponse, retrievedat")
        .eq("city", city)
        .eq("fordate", forDate)
        .maybeSingle();

      if (error) {
        defaultLogError({
          step: "db.getCache",
          city,
          forDate,
          httpStatus: null,
          errorType: "db_error",
          code: error.code ?? null,
          message: error.message,
        });
        return null;
      }
      return data ? (data as CachedWeatherRow) : null;
    },

    async upsertCache(row: CachedWeatherRow): Promise<void> {
      const { error } = await admin
        .from("weathercache")
        .upsert(row, { onConflict: "city,fordate" });
      if (error) {
        defaultLogError({
          step: "db.upsertCache",
          city: row.city,
          forDate: row.fordate,
          httpStatus: null,
          errorType: "db_error",
          code: error.code ?? null,
          message: error.message,
        });
        throw new Error(error.code ?? "db_upsert_cache_failed");
      }
    },

    async getLocationKey(city: string): Promise<string | null> {
      const { data, error } = await admin
        .from("weatherlocations")
        .select("locationkey")
        .eq("city", city)
        .maybeSingle();

      if (error) {
        defaultLogError({
          step: "db.getLocationKey",
          city,
          httpStatus: null,
          errorType: "db_error",
          code: error.code ?? null,
          message: error.message,
        });
        return null;
      }
      return data && typeof data.locationkey === "string" ? data.locationkey : null;
    },

    async saveLocationKey(city: string, key: string): Promise<void> {
      const { data, error } = await admin
        .from("weatherlocations")
        .upsert(
          { city, locationkey: key, resolvedat: new Date().toISOString() },
          { onConflict: "city" },
        )
        .select("city, locationkey")
        .single();

      if (error || !data || data.locationkey !== key) {
        defaultLogError({
          step: "db.saveLocationKey",
          city,
          locationKey: key,
          httpStatus: null,
          errorType: "db_save_location_failed",
          code: error?.code ?? null,
          message: error?.message ?? "weatherlocations upsert returned empty or mismatched row",
        });
        throw new Error(error?.code ?? "weatherlocations_upsert_unverified");
      }
    },

    resolveLocationKeyRemote: (city, apiKey) => resolveLocationKeyFromAccuWeather(city, apiKey),
    fetchForecastRemote: (locationKey, forDate, apiKey) =>
      fetchForecastFromAccuWeather(locationKey, forDate, apiKey),
  };

  return await handleWeatherTomorrow(req, deps);
});
