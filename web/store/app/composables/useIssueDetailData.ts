// Issue detail (綠誌) — GET /store/issues/detail?issuetitle=. Adapts IssueDetail
// (related products/recipes + other issues) into the legacy view-model.
export interface IssueDetail {
  issueid: string; title: string; photo?: string
  intro?: string; createdate: string; shortener?: string
}
export interface IssueRecipe {
  recipeid: string; title: string; rphoto?: string
}
export interface IssueDetailData {
  blobUrl: string
  item: IssueDetail | null
  others: Pick<IssueDetail, 'issueid' | 'title' | 'photo'>[]
  products: ViewProduct[]
  recipes: IssueRecipe[]
  pageNumber: number
}

interface ApiIssueDetail {
  issueId: string; title: string; photo: string; intro?: string | null
  keyword?: string | null; description?: string | null; createDate: string; isPublish: boolean; shortener?: string | null
  mediaUrls: string[]
  products: ApiProduct[]
  recipes: ApiRecipeRef[]
  others: { issueId: string; title: string; photo: string }[]
}

export function useIssueDetailData(issuetitle: string, p: number = 1) {
  const blobUrl = useRuntimeConfig().public.blobUrl as string
  return useFetch(`${useRuntimeConfig().public.apiBase}/store/issues/detail`, {
    key: `issue-detail:${issuetitle}`,
    query: { issuetitle, p },
    default: (): IssueDetailData => ({
      blobUrl, item: null, others: [], products: [], recipes: [], pageNumber: p,
    }),
    transform: (api: ApiIssueDetail): IssueDetailData => ({
      blobUrl,
      item: {
        issueid: api.issueId,
        title: api.title,
        photo: api.photo,
        intro: api.intro ?? undefined,
        createdate: ymd(api.createDate),
        shortener: api.shortener ?? undefined,
      },
      others: api.others.map(o => ({ issueid: o.issueId, title: o.title, photo: o.photo })),
      products: api.products.map(mapProduct),
      recipes: api.recipes.map(mapRecipeRef),
      pageNumber: p,
    }),
  })
}
