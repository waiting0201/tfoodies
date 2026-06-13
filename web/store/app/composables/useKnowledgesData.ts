// Knowledges (小知識) listing — GET /store/knowledges?p=&k= (PaginatedResponse<KnowledgeListItem>).
export interface KnowledgeItem {
  knowledgeid: string; question: string; photo?: string
}
export interface KnowledgesData {
  blobUrl: string
  items: KnowledgeItem[]
  currentPage: number
  totalPages: number
}

interface ApiKnowledgeListItem { knowledgeId: string; question: string; photo: string }
interface ApiPaged<T> { items: T[]; page: number; totalPages: number }

export function useKnowledgesData(p: number = 1, k?: string) {
  const blobUrl = useRuntimeConfig().public.blobUrl as string
  return useFetch(`${useRuntimeConfig().public.apiBase}/store/knowledges`, {
    key: `knowledges:${p}:${k ?? ''}`,
    query: { p, ...(k ? { k } : {}) },
    default: (): KnowledgesData => ({ blobUrl, items: [], currentPage: p, totalPages: 1 }),
    transform: (api: ApiPaged<ApiKnowledgeListItem>): KnowledgesData => ({
      blobUrl,
      items: api.items.map(i => ({ knowledgeid: i.knowledgeId, question: i.question, photo: i.photo })),
      currentPage: api.page,
      totalPages: api.totalPages,
    }),
  })
}
