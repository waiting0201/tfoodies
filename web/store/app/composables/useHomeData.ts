// Home data for "/" (legacy MainMs/Index ViewBag: Banners, latest News/Events, hot &
// latest Products, sorted Recipes, latest Issues). Fetched from the single-entry Functions
// API; until that endpoint lands the defaults keep the page rendering its (empty) sections.
export interface Banner { style: number; photo?: string; url?: string; title?: string; subtitle?: string }
export interface HomeArticle { id: string; title: string; photo?: string; date?: string }
export interface HomeProduct {
  title: string; entitle?: string; capacity?: string; photo?: string; price: number; fixprice: number; isset?: boolean
}
export interface HomeRecipe { recipeid: string; title: string; photo?: string; rphoto?: string; v?: string | null }
export interface HomeIssue { title: string; photo?: string }

export interface HomeData {
  blobUrl: string
  banners: Banner[]
  latestNews: HomeArticle[]
  latestEvents: HomeArticle[]
  hotProducts: HomeProduct[]
  recipes: HomeRecipe[]
  latestProducts: HomeProduct[]
  latestIssues: HomeIssue[]
}

export function useHomeData() {
  const config = useRuntimeConfig()
  return useFetch<HomeData>(`${config.public.apiBase}/store/home`, {
    key: 'home',
    default: (): HomeData => ({
      blobUrl: '',
      banners: [],
      latestNews: [],
      latestEvents: [],
      hotProducts: [],
      recipes: [],
      latestProducts: [],
      latestIssues: [],
    }),
  })
}
