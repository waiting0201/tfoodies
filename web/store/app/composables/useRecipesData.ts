// Recipes listing — GET /store/recipes?p=&k= (PaginatedResponse<RecipeListItem>).
export interface RecipeItem {
  recipeid: string; title: string; rphoto?: string
}
export interface RecipesData {
  blobUrl: string
  items: RecipeItem[]
  currentPage: number
  totalPages: number
}

interface ApiRecipeListItem { recipeId: string; title: string; rPhoto: string }
interface ApiPaged<T> { items: T[]; page: number; totalPages: number }

export function useRecipesData(p: number = 1, k?: string) {
  const blobUrl = useRuntimeConfig().public.blobUrl as string
  return useFetch(`${useRuntimeConfig().public.apiBase}/store/recipes`, {
    key: `recipes:${p}:${k ?? ''}`,
    query: { p, ...(k ? { k } : {}) },
    default: (): RecipesData => ({ blobUrl, items: [], currentPage: p, totalPages: 1 }),
    transform: (api: ApiPaged<ApiRecipeListItem>): RecipesData => ({
      blobUrl,
      items: api.items.map(r => ({ recipeid: r.recipeId, title: r.title, rphoto: r.rPhoto })),
      currentPage: api.page,
      totalPages: api.totalPages,
    }),
  })
}
