import { createClient } from "@supabase/supabase-js";
import {
  fetchForecastFromAccuWeather,
  resolveLocationKeyFromAccuWeather,
} from "./accuweather.ts";
import { handleWeatherTomorrow, type CachedWeatherRow, type WeatherDeps } from "./handler.ts";

Deno.serve(async (req: Request): Promise<Response> => {
  const supabaseUrl = Deno.env.get("SUPABASE_URL") ?? "";
  const serviceRoleKey =
    Deno.env.get("SUPABASE_SERVICE_ROLE_KEY") ?? Deno.env.get("SERVICE_ROLE_KEY") ?? "";

  if (!supabaseUrl || !serviceRoleKey) {
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

      if (error || !data) return null;
      return data as CachedWeatherRow;
    },

    async upsertCache(row: CachedWeatherRow): Promise<void> {
      const { error } = await admin
        .from("weathercache")
        .upsert(row, { onConflict: "city,fordate" });
      if (error) throw new Error(error.message);
    },

    async getLocationKey(city: string): Promise<string | null> {
      const { data, error } = await admin
        .from("weatherlocations")
        .select("locationkey")
        .eq("city", city)
        .maybeSingle();

      if (error || !data) return null;
      return typeof data.locationkey === "string" ? data.locationkey : null;
    },

    async saveLocationKey(city: string, key: string): Promise<void> {
      const { error } = await admin
        .from("weatherlocations")
        .upsert(
          { city, locationkey: key, resolvedat: new Date().toISOString() },
          { onConflict: "city" },
        );
      if (error) throw new Error(error.message);
    },

    resolveLocationKeyRemote: (city, apiKey) => resolveLocationKeyFromAccuWeather(city, apiKey),
    fetchForecastRemote: (locationKey, forDate, apiKey) =>
      fetchForecastFromAccuWeather(locationKey, forDate, apiKey),
  };

  return await handleWeatherTomorrow(req, deps);
});
