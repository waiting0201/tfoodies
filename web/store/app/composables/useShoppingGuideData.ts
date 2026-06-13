// Shopping guide (購物說明 / 會員常見問題) — GET /store/shopping-guide.
// Returns all question types grouped with their questions (no paging); the page
// switches the active type client-side. Port of legacy PageMs/Howtobuy.
export interface ShoppingGuideQuestion {
  questionId: string
  title: string
  answer: string // 富文本 HTML（v-html 呈現）
  sort: number
}
export interface ShoppingGuideType {
  questiontypeId: string
  title: string
  sort: number
  questions: ShoppingGuideQuestion[]
}

export function useShoppingGuideData() {
  return useFetch<ShoppingGuideType[]>(`${useRuntimeConfig().public.apiBase}/store/shopping-guide`, {
    key: 'shopping-guide',
    default: (): ShoppingGuideType[] => [],
  })
}
