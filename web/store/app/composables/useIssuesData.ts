// Issues (綠誌) listing — port of MainMsController.Issues (ViewBag.Issues = IPagedList<Issues>).
export interface IssueItem {
  issueid: string; title: string; photo?: string
}
export interface IssuesData {
  blobUrl: string
  items: IssueItem[]
  currentPage: number
  totalPages: number
}

export function useIssuesData(p: number = 1, k?: string) {
  const config = useRuntimeConfig()
  return useFetch<IssuesData>(`${config.public.apiBase}/store/issues`, {
    key: `issues:${p}:${k ?? ''}`,
    query: { p, ...(k ? { k } : {}) },
    default: (): IssuesData => ({ blobUrl: '', items: [], currentPage: p, totalPages: 1 }),
  })
}
