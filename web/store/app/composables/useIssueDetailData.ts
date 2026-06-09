// Issue detail (綠誌) — port of MainMsController.IssueDetail (Model = Issues, ViewBag.Issueothers).
export interface IssueDetail {
  issueid: string; title: string; photo?: string
  intro?: string; createdate: string; shortener?: string
}
export interface IssueProduct {
  productid: string; title: string; entitle?: string; capacity?: string
  photo?: string; price: number; fixprice: number; isdisabled: boolean; sort: number
}
export interface IssueRecipe {
  recipeid: string; title: string; rphoto?: string
}
export interface IssueDetailData {
  blobUrl: string
  item: IssueDetail | null
  others: Pick<IssueDetail, 'issueid' | 'title' | 'photo'>[]
  products: IssueProduct[]
  recipes: IssueRecipe[]
  pageNumber: number
}

export function useIssueDetailData(issuetitle: string, p: number = 1) {
  const config = useRuntimeConfig()
  return useFetch<IssueDetailData>(`${config.public.apiBase}/store/issues/detail`, {
    key: `issue-detail:${issuetitle}`,
    query: { issuetitle, p },
    default: (): IssueDetailData => ({
      blobUrl: '', item: null, others: [], products: [], recipes: [], pageNumber: p,
    }),
  })
}
