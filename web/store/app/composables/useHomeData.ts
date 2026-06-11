// Home "/" — legacy MainMs/Index ViewBag (Banners, latest News/Events, hot & latest
// Products, sorted Recipes, latest Issues). Fetches GET /store/home and adapts the
// camelCase API into the legacy view-model the page binds (blobUrl + yyyy-MM-dd dates).
export interface Banner { style: number; photo?: string; url?: string; title?: string; subtitle?: string }
export interface HomeArticle { id: string; title: string; photo?: string; date?: string }
export interface HomeRecipe { recipeid: string; title: string; photo?: string; rphoto?: string; v?: string | null }
export interface HomeIssue { title: string; photo?: string }

export interface HomeData {
  blobUrl: string
  banners: Banner[]
  latestNews: HomeArticle[]
  latestEvents: HomeArticle[]
  hotProducts: ViewProduct[]
  recipes: HomeRecipe[]
  latestProducts: ViewProduct[]
  latestIssues: HomeIssue[]
}

interface ApiHome {
  banners: { bannerId: string; title?: string | null; subtitle?: string | null; url?: string | null; photo: string; style: number; sort: number }[]
  hotProducts: ApiProduct[]
  newProducts: ApiProduct[]
  latestNews: { newId: string; title: string; summary?: string | null; photo: string; publishDate: string; shortener?: string | null }[]
  latestRecipes: { recipeId: string; title: string; rPhoto: string; photo: string; v?: string | null }[]
  latestIssues: { issueId: string; title: string; photo: string }[]
  latestEvent: { eventId: string; title: string; photo: string; eventDate: string } | null
}

export function useHomeData() {
  const blobUrl = useRuntimeConfig().public.blobUrl as string
  const empty = (): HomeData => ({
    blobUrl, banners: [], latestNews: [], latestEvents: [],
    hotProducts: [], recipes: [], latestProducts: [], latestIssues: [],
  })
  return useFetch(`${useRuntimeConfig().public.apiBase}/store/home`, {
    key: 'home',
    default: empty,
    transform: (api: ApiHome): HomeData => ({
      blobUrl,
      banners: api.banners.map(b => ({
        style: b.style, photo: b.photo, url: b.url ?? undefined,
        title: b.title ?? undefined, subtitle: b.subtitle ?? undefined,
      })),
      latestNews: api.latestNews.map(n => ({ id: n.newId, title: n.title, photo: n.photo, date: ymd(n.publishDate) })),
      latestEvents: api.latestEvent
        ? [{ id: api.latestEvent.eventId, title: api.latestEvent.title, photo: api.latestEvent.photo, date: ymd(api.latestEvent.eventDate) }]
        : [],
      hotProducts: api.hotProducts.map(mapProduct),
      recipes: api.latestRecipes.map(r => ({ recipeid: r.recipeId, title: r.title, photo: r.photo, rphoto: r.rPhoto, v: r.v })),
      latestProducts: api.newProducts.map(mapProduct),
      latestIssues: api.latestIssues.map(i => ({ title: i.title, photo: i.photo })),
    }),
  })
}
