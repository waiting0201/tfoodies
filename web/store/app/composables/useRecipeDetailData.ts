// Recipe detail — port of MainMsController.RecipeDetail (Model = Recipes).
export interface RecipeIngredient { sort: number; title: string; value?: string }
export interface RecipeSeasoning { sort: number; title: string; value?: string }
export interface RecipeStep { sort: number; title?: string; value?: string }
export interface RecipeProduct {
  productid: string; title: string; entitle?: string; capacity?: string
  photo?: string; price: number; fixprice: number; isdisabled: boolean; sort: number
}
export interface RecipeDetail {
  recipeid: string; title: string; photo?: string; rphoto?: string
  duration?: number; portion?: number
  intro?: string; youtube?: string; shortener?: string
  ingredients: RecipeIngredient[]
  seasonings: RecipeSeasoning[]
  steps: RecipeStep[]
  products: RecipeProduct[]
}
export interface RecipeDetailData {
  blobUrl: string
  item: RecipeDetail | null
  pageNumber: number
}

export function useRecipeDetailData(recipeid: string, p: number = 1) {
  const config = useRuntimeConfig()
  return useFetch<RecipeDetailData>(`${config.public.apiBase}/store/recipes/detail`, {
    key: `recipe-detail:${recipeid}`,
    query: { recipeid, p },
    default: (): RecipeDetailData => ({ blobUrl: '', item: null, pageNumber: p }),
  })
}
