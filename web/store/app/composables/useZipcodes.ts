// 縣市 / 鄉鎮市區 連動參照（對齊舊系統 Ajax/GetZipcodeByCity）。
// GET /store/zipcodes/cities → string[]；GET /store/zipcodes/areas?city= → {zipcodeId, area, zipcode}[]
export interface ZipArea { zipcodeId: number; area: string; zipcode: string }

export function useZipcodes() {
  const base = useRuntimeConfig().public.apiBase as string
  const cities = ref<string[]>([])

  async function loadCities() {
    if (cities.value.length) return
    const res = await $fetch<{ cities: string[] }>(`${base}/store/zipcodes/cities`)
    cities.value = res.cities
  }

  async function loadAreas(city: string): Promise<ZipArea[]> {
    if (!city) return []
    const res = await $fetch<{ areas: ZipArea[] }>(`${base}/store/zipcodes/areas`, { query: { city } })
    return res.areas
  }

  return { cities, loadCities, loadAreas }
}
