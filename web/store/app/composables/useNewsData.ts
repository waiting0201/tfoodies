// News listing — port of MainMsController.News (ViewBag.News = IPagedList<News>).
export interface NewsItem {
  newid: string; title: string; photo?: string; summary?: string
  publishdate: string
}
export interface NewsData {
  blobUrl: string
  items: NewsItem[]
  currentPage: number
  totalPages: number
}

export function useNewsData(p: number = 1) {
  const config = useRuntimeConfig()
  return useFetch<NewsData>(`${config.public.apiBase}/store/news`, {
    key: `news:${p}`,
    query: { p },
    default: (): NewsData => ({ blobUrl: '', items: [], currentPage: p, totalPages: 1 }),
  })
}
