// Issues (綠誌) listing — GET /store/issues?p=&k= (PaginatedResponse<IssueListItem>).
export interface IssueItem {
  issueid: string; title: string; photo?: string
}
export interface IssuesData {
  blobUrl: string
  items: IssueItem[]
  currentPage: number
  totalPages: number
}

interface ApiIssueListItem { issueId: string; title: string; photo: string }
interface ApiPaged<T> { items: T[]; page: number; totalPages: number }

export function useIssuesData(p: number = 1, k?: string) {
  const blobUrl = useRuntimeConfig().public.blobUrl as string
  return useFetch(`${useRuntimeConfig().public.apiBase}/store/issues`, {
    key: `issues:${p}:${k ?? ''}`,
    query: { p, ...(k ? { k } : {}) },
    default: (): IssuesData => ({ blobUrl, items: [], currentPage: p, totalPages: 1 }),
    transform: (api: ApiPaged<ApiIssueListItem>): IssuesData => ({
      blobUrl,
      items: api.items.map(i => ({ issueid: i.issueId, title: i.title, photo: i.photo })),
      currentPage: api.page,
      totalPages: api.totalPages,
    }),
  })
}
