// News detail — GET /store/news/detail?newid=. Adapts NewsDetail (+ others) into the
// legacy view-model; pageNumber comes from the route (return-to-list link), not the API.
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

interface ApiNewsDetail {
  newId: string; title: string; summary?: string | null; photo: string; intro: string
  activityDate?: string | null; activitySchedule?: string | null; publishDate: string; shortener?: string | null
  mediaUrls: string[]
  others: { newId: string; title: string; photo: string }[]
}

export function useNewsDetailData(newid: string, p: number = 1) {
  const blobUrl = useRuntimeConfig().public.blobUrl as string
  return useFetch(`${useRuntimeConfig().public.apiBase}/store/news/detail`, {
    key: `news-detail:${newid}`,
    query: { newid, p },
    default: (): NewsDetailData => ({ blobUrl, item: null, others: [], pageNumber: p }),
    transform: (api: ApiNewsDetail): NewsDetailData => ({
      blobUrl,
      item: {
        newid: api.newId,
        title: api.title,
        photo: api.photo,
        intro: api.intro,
        summary: api.summary ?? undefined,
        publishdate: ymd(api.publishDate),
        activitydate: api.activityDate ?? undefined,
        activityschedule: api.activitySchedule ?? undefined,
        shortener: api.shortener ?? undefined,
      },
      others: api.others.map(o => ({ newid: o.newId, title: o.title, photo: o.photo })),
      pageNumber: p,
    }),
  })
}
