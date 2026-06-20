// Knowledge detail (小知識) — GET /store/knowledges/detail?knowledgeid=. Adapts KnowledgeDetail
// (answer HTML + other knowledge articles) into the legacy view-model.
export interface KnowledgeDetail {
  knowledgeid: string; question: string; photo?: string
  answer?: string; description?: string; createdate: string; shortener?: string
}
export interface KnowledgeDetailData {
  blobUrl: string
  item: KnowledgeDetail | null
  others: Pick<KnowledgeDetail, 'knowledgeid' | 'question' | 'photo'>[]
  products: ViewProduct[]
  pageNumber: number
}

interface ApiKnowledgeDetail {
  knowledgeId: string; question: string; photo: string; answer: string
  keyword?: string | null; description?: string | null; createDate: string; isPublish: boolean; shortener?: string | null
  products: ApiProduct[]
  others: { knowledgeId: string; question: string; photo: string }[]
}

export function useKnowledgeDetailData(knowledgeid: string, p: number = 1) {
  const blobUrl = useRuntimeConfig().public.blobUrl as string
  return useFetch(`${useRuntimeConfig().public.apiBase}/store/knowledges/detail`, {
    key: `knowledge-detail:${knowledgeid}`,
    query: { knowledgeid, p },
    default: (): KnowledgeDetailData => ({ blobUrl, item: null, others: [], products: [], pageNumber: p }),
    transform: (api: ApiKnowledgeDetail): KnowledgeDetailData => ({
      blobUrl,
      item: {
        knowledgeid: api.knowledgeId,
        question: api.question,
        photo: api.photo,
        answer: api.answer ?? undefined,
        description: api.description ?? undefined,
        createdate: ymd(api.createDate),
        shortener: api.shortener ?? undefined,
      },
      others: api.others.map(o => ({ knowledgeid: o.knowledgeId, question: o.question, photo: o.photo })),
      products: api.products.map(mapProduct),
      pageNumber: p,
    }),
  })
}
