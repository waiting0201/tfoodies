import { defineStore } from 'pinia'

// Replaces the legacy Session-backed cart (reference/old/tfoodies/Commons/Cart.cs).
// Anonymous: persisted to localStorage. Logged-in: synced to the server cart
// (Table Storage / Redis) and merged on login — wiring lands with the cart API.
export interface CartItem {
  productId: string
  title: string
  unitPrice: number
  quantity: number
  // Optional display metadata (used by the header cart dropdown); safe to omit.
  photo?: string
  capacity?: string
}

const STORAGE_KEY = 'tfoodies.cart'

// localStorage 的內容是不可信的（舊版格式、寫入被截斷、使用者/擴充套件亂改）。
function isCartItem(v: unknown): v is CartItem {
  const i = v as CartItem
  return !!i && typeof i.productId === 'string' && typeof i.title === 'string'
    && Number.isFinite(i.unitPrice) && Number.isFinite(i.quantity) && i.quantity > 0
}

export const useCartStore = defineStore('cart', {
  state: () => ({
    items: [] as CartItem[],
    // Increments only on an explicit add() — NOT on hydrate()/updateQty(). Lets the header
    // react to "user just added something" (pulse badge + slide the mini-cart out) without
    // firing on every page load that restores a non-empty cart from localStorage. In-memory
    // only (never persisted).
    addPulse: 0,
  }),
  getters: {
    count: (s) => s.items.reduce((n, i) => n + i.quantity, 0),
    subtotal: (s) => s.items.reduce((n, i) => n + i.unitPrice * i.quantity, 0),
  },
  actions: {
    // ⚠️ 這支由 SiteHeader 在每一頁 onMounted 呼叫：一旦 JSON.parse 拋錯，整個站的每一頁都會
    // 變成錯誤頁，而使用者除了自己清除瀏覽器資料無法自救。壞掉的資料一律丟棄（比照 memberAuth）。
    hydrate() {
      if (!import.meta.client) return
      const raw = localStorage.getItem(STORAGE_KEY)
      if (!raw) return
      try {
        const parsed: unknown = JSON.parse(raw)
        const items = Array.isArray(parsed) ? parsed.filter(isCartItem) : []
        this.items = items
        // 內容有被過濾掉（部分壞資料）時把清理結果寫回，避免每次載入重複過濾。
        if (!Array.isArray(parsed) || items.length !== parsed.length) this.persist()
      } catch {
        localStorage.removeItem(STORAGE_KEY)
        this.items = []
      }
    },
    persist() {
      if (import.meta.client) localStorage.setItem(STORAGE_KEY, JSON.stringify(this.items))
    },
    add(item: CartItem) {
      const existing = this.items.find((i) => i.productId === item.productId)
      if (existing) existing.quantity += item.quantity
      else this.items.push({ ...item })
      this.addPulse++
      this.persist()
      // 漏斗第三關：加入購物車（目前最該被衝起來的指標）。
      track('add_to_cart', {
        ecommerce: {
          currency: 'TWD',
          value: item.unitPrice * item.quantity,
          items: [{
            item_id: item.productId,
            item_name: item.title,
            price: item.unitPrice,
            quantity: item.quantity,
          }],
        },
      })
    },
    updateQty(productId: string, quantity: number) {
      const it = this.items.find((i) => i.productId === productId)
      if (it) it.quantity = Math.max(1, quantity)
      this.persist()
    },
    remove(productId: string) {
      this.items = this.items.filter((i) => i.productId !== productId)
      this.persist()
    },
  },
})
