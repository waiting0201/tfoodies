// News listing — GET /store/news?p= (PaginatedResponse<NewsListItem>).
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

interface ApiNews { newId: string; title: string; summary?: string | null; photo: string; publishDate: string; shortener?: string | null }
interface ApiPaged<T> { items: T[]; page: number; totalPages: number }

export function useNewsData(p: number = 1) {
  const blobUrl = useRuntimeConfig().public.blobUrl as string
  return useFetch(`${useRuntimeConfig().public.apiBase}/store/news`, {
    key: `news:${p}`,
    query: { p },
    default: (): NewsData => ({ blobUrl, items: [], currentPage: p, totalPages: 1 }),
    transform: (api: ApiPaged<ApiNews>): NewsData => ({
      blobUrl,
      items: api.items.map(n => ({
        newid: n.newId, title: n.title, photo: n.photo,
        summary: n.summary ?? undefined, publishdate: ymd(n.publishDate),
      })),
      currentPage: api.page,
      totalPages: api.totalPages,
    }),
  })
}
