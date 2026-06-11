// Recipe detail — GET /store/recipes/detail?recipeid=. Adapts RecipeDetail (ingredients/
// seasonings/steps already {sort,title,value} + related products) into the view-model.
export interface RecipeIngredient { sort: number; title: string; value?: string }
export interface RecipeSeasoning { sort: number; title: string; value?: string }
export interface RecipeStep { sort: number; title?: string; value?: string }
export interface RecipeDetail {
  recipeid: string; title: string; photo?: string; rphoto?: string
  duration?: number; portion?: number
  intro?: string; youtube?: string; shortener?: string
  ingredients: RecipeIngredient[]
  seasonings: RecipeSeasoning[]
  steps: RecipeStep[]
  products: ViewProduct[]
}
export interface RecipeDetailData {
  blobUrl: string
  item: RecipeDetail | null
  pageNumber: number
}

interface ApiPart { sort: number; title: string; value?: string | null }
interface ApiRecipeDetail {
  recipeId: string; title: string; duration: number; portion: number; intro: string
  rPhoto: string; photo: string; youtube?: string | null; v?: string | null
  keyword?: string | null; description?: string | null; shortener?: string | null
  ingredients: ApiPart[]
  seasonings: ApiPart[]
  steps: ApiPart[]
  products: ApiProduct[]
}

const mapPart = (p: ApiPart) => ({ sort: p.sort, title: p.title, value: p.value ?? undefined })

export function useRecipeDetailData(recipeid: string, p: number = 1) {
  const blobUrl = useRuntimeConfig().public.blobUrl as string
  return useFetch(`${useRuntimeConfig().public.apiBase}/store/recipes/detail`, {
    key: `recipe-detail:${recipeid}`,
    query: { recipeid, p },
    default: (): RecipeDetailData => ({ blobUrl, item: null, pageNumber: p }),
    transform: (api: ApiRecipeDetail): RecipeDetailData => ({
      blobUrl,
      item: {
        recipeid: api.recipeId,
        title: api.title,
        photo: api.photo,
        rphoto: api.rPhoto,
        duration: api.duration,
        portion: api.portion,
        intro: api.intro,
        youtube: api.youtube ?? undefined,
        shortener: api.shortener ?? undefined,
        ingredients: api.ingredients.map(mapPart),
        seasonings: api.seasonings.map(mapPart),
        steps: api.steps.map(mapPart),
        products: api.products.map(mapProduct),
      },
      pageNumber: p,
    }),
  })
}
