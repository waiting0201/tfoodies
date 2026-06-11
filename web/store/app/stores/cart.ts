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
    hydrate() {
      if (import.meta.client) {
        const raw = localStorage.getItem(STORAGE_KEY)
        if (raw) this.items = JSON.parse(raw)
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
