// 購物車對帳：把 localStorage 裡的購物車跟後端現況核對一次。
//
// 購物車存在瀏覽器可以放很久，商品在這段期間可能調價或下架，而下單一律以 Products.price
// 現價計算 → 不對帳的話顧客會「看到 A 金額、被扣 B 金額」，或填完整張結帳表單才被告知
// 某商品已下架。改成進入購物車／結帳頁時就更新價格並標出不可購買的品項。
export interface CartProductState {
  productId: string
  title: string
  price: number
  isDisabled: boolean
}

export function useCartSync() {
  const config = useRuntimeConfig()
  const cartStore = useCartStore()

  // 已下架 / 已不存在的品項（productId → 顯示名稱），由呼叫端決定怎麼提示。
  const unavailable = ref<{ productId: string; title: string }[]>([])
  // 這次對帳有調整過價格的品項（顯示「價格已更新」用）。
  const repriced = ref<{ title: string; from: number; to: number }[]>([])
  const syncing = ref(false)

  async function syncCart() {
    if (cartStore.items.length === 0) {
      unavailable.value = []
      repriced.value = []
      return
    }
    syncing.value = true
    try {
      const res = await $fetch<{ items: CartProductState[] }>(
        `${config.public.apiBase}/store/cart/sync`,
        {
          method: 'POST',
          body: { productIds: cartStore.items.map(i => i.productId) },
          timeout: 10000,
        },
      )
      const byId = new Map(res.items.map(s => [s.productId.toLowerCase(), s]))

      const gone: { productId: string; title: string }[] = []
      const changed: { title: string; from: number; to: number }[] = []

      for (const item of cartStore.items) {
        const state = byId.get(item.productId.toLowerCase())
        // 查無此 id = 商品已不存在；isDisabled = 已下架。兩者都不能下單。
        if (!state || state.isDisabled) {
          gone.push({ productId: item.productId, title: state?.title || item.title })
          continue
        }
        if (state.price !== item.unitPrice) {
          changed.push({ title: state.title, from: item.unitPrice, to: state.price })
          item.unitPrice = state.price
        }
        // 名稱也一併更新（後台改名後，購物車顯示的還是舊名）。
        if (state.title && state.title !== item.title) item.title = state.title
      }

      unavailable.value = gone
      repriced.value = changed
      if (changed.length) cartStore.persist()
    } catch {
      // 對帳失敗不擋流程：下單時後端仍會以現價計算並擋下已下架商品（訊息會指名商品）。
      unavailable.value = []
      repriced.value = []
    } finally {
      syncing.value = false
    }
  }

  function removeUnavailable() {
    for (const u of unavailable.value) cartStore.remove(u.productId)
    unavailable.value = []
  }

  return { unavailable, repriced, syncing, syncCart, removeUnavailable }
}
