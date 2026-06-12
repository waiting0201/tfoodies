// 導覽列「品牌系列」下拉清單 — GET /store/brands（isdisplay=1，依 sort）。
// 對應舊 BaseController.OnActionExecuted 填入 ViewBag.Brands 的全站共用資料。
// 以固定 key 快取，整站（header + 行動選單）共用一次請求。
export interface BrandMenuItem { title: string; logo?: string }

interface ApiBrandMenuItem { brandId: string; title: string; logo?: string | null }

export function useBrandsMenu() {
  return useFetch(`${useRuntimeConfig().public.apiBase}/store/brands`, {
    key: 'brands-menu',
    default: (): BrandMenuItem[] => [],
    transform: (api: ApiBrandMenuItem[]): BrandMenuItem[] =>
      (api ?? []).map(b => ({ title: b.title, logo: b.logo ?? undefined })),
  })
}
