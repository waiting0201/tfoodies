// News detail — port of MainMsController.NewsDetail (Model = News, ViewBag.Newsothers).
export interface NewsDetail {
  newid: string; title: string; photo?: string
  intro?: string; summary?: string
  publishdate: string
  activitydate?: string; activityschedule?: string
  shortener?: string
}
export interface NewsDetailData {
  blobUrl: string
  item: NewsDetail | null
  others: Pick<NewsDetail, 'newid' | 'title' | 'photo'>[]
  pageNumber: number
}

export function useNewsDetailData(newid: string, p: number = 1) {
  const config = useRuntimeConfig()
  return useFetch<NewsDetailData>(`${config.public.apiBase}/store/news/detail`, {
    key: `news-detail:${newid}`,
    query: { newid, p },
    default: (): NewsDetailData => ({ blobUrl: '', item: null, others: [], pageNumber: p }),
  })
}
