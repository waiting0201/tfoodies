// Recipes listing — port of MainMsController.Recipes (ViewBag.Recipes = IPagedList<Recipes>).
export interface RecipeItem {
  recipeid: string; title: string; rphoto?: string
}
export interface RecipesData {
  blobUrl: string
  items: RecipeItem[]
  currentPage: number
  totalPages: number
}

export function useRecipesData(p: number = 1, k?: string) {
  const config = useRuntimeConfig()
  return useFetch<RecipesData>(`${config.public.apiBase}/store/recipes`, {
    key: `recipes:${p}:${k ?? ''}`,
    query: { p, ...(k ? { k } : {}) },
    default: (): RecipesData => ({ blobUrl: '', items: [], currentPage: p, totalPages: 1 }),
  })
}
