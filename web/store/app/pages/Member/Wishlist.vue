<script setup lang="ts">
useHead({ title: '會員中心 - 我的收藏' })

const config = useRuntimeConfig()
const memberAuth = useMemberAuthStore()
const cart = useCartStore()

if (!memberAuth.isAuthenticated) {
  await navigateTo('/Member/Login')
}

interface WishItem {
  productid: string
  title: string
  price: number
  fixprice: number | null
  shortener: string | null
  photo: string | null
}

const authHeader = computed(() =>
  memberAuth.accessToken ? { Authorization: `Bearer ${memberAuth.accessToken}` } : {},
)

const { data, pending, error, refresh } = await useFetch<{ items: WishItem[] }>(
  `${config.public.apiBase}/member/wishlist`,
  {
    key: 'member-wishlist',
    headers: authHeader,
    default: () => ({ items: [] }),
  },
)

const nf = new Intl.NumberFormat('zh-TW')
const imgUrl = (f: string | null) => (f ? config.public.blobUrl + f : '')
const productUrl = (title: string) => `/Product/${titleToUrlSlug(title)}`

const busy = ref<string | null>(null)

function addToCart(item: WishItem) {
  cart.add({
    productId: item.productid,
    title: item.title,
    unitPrice: item.price,
    quantity: 1,
    photo: item.photo ?? undefined,
  })
}

async function remove(item: WishItem) {
  busy.value = item.productid
  try {
    await $fetch(`${config.public.apiBase}/member/wishlist/${item.productid}`, {
      method: 'DELETE',
      headers: authHeader.value,
    })
    await refresh()
  } finally {
    busy.value = null
  }
}
</script>

<template>
  <MemberShell>
    <h2 class="mc-title">我的收藏</h2>

    <div v-if="pending" class="mc-state">載入中…</div>
    <div v-else-if="error" class="mc-state err">無法載入收藏清單。</div>

    <template v-else>
      <div v-if="!data?.items?.length" class="mc-empty">
        <p>目前還沒有收藏的商品。</p>
        <NuxtLink to="/Products" class="mc-btn">前往選購</NuxtLink>
      </div>

      <ul v-else class="wl">
        <li v-for="item in data.items" :key="item.productid" class="wl-item">
          <NuxtLink :to="productUrl(item.title)" class="wl-thumb">
            <img v-if="item.photo" :src="imgUrl(item.photo)" :alt="item.title" loading="lazy">
            <span v-else class="wl-noimg"></span>
          </NuxtLink>

          <div class="wl-info">
            <NuxtLink :to="productUrl(item.title)" class="wl-name">{{ item.title }}</NuxtLink>
            <div class="wl-price">NT$ {{ nf.format(item.price) }}</div>
          </div>

          <div class="wl-actions">
            <button class="wl-cart" @click="addToCart(item)">加入購物車</button>
            <button class="wl-del" :disabled="busy === item.productid" @click="remove(item)">
              {{ busy === item.productid ? '移除中…' : '移除' }}
            </button>
          </div>
        </li>
      </ul>
    </template>
  </MemberShell>
</template>

<style scoped>
.mc-title {
  margin: 0 0 1.25rem;
  font-size: 1.2rem;
  letter-spacing: 0.06em;
  color: #156467;
}

.mc-state {
  padding: 2rem 0;
  text-align: center;
  color: #9b9b9b;
}

.mc-state.err { color: #d0021b; }

.mc-empty {
  padding: 2.5rem 0;
  text-align: center;
  color: #777;
}

.mc-btn {
  display: inline-block;
  margin-top: 1.1rem;
  padding: 0.6rem 1.4rem;
  font-size: 0.9rem;
  letter-spacing: 0.08em;
  color: #fff;
  text-decoration: none;
  background: #26b7bc;
  border-radius: 8px;
  transition: background 0.18s;
}

.mc-btn:hover { background: #1d8e92; }

.wl {
  list-style: none;
  margin: 0;
  padding: 0;
}

.wl-item {
  display: grid;
  grid-template-columns: 72px 1fr auto;
  align-items: center;
  gap: 1rem;
  padding: 1rem 0;
  border-bottom: 1px solid #f3f3f3;
}

.wl-thumb {
  width: 72px;
  height: 72px;
  border-radius: 10px;
  overflow: hidden;
  background: #f5f5f5;
  display: block;
}

.wl-thumb img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.wl-noimg {
  display: block;
  width: 100%;
  height: 100%;
  background: #eef2f2;
}

.wl-name {
  display: block;
  font-size: 0.98rem;
  color: #3e3e3e;
  text-decoration: none;
  transition: color 0.18s;
}

.wl-name:hover { color: #26b7bc; }

.wl-price {
  margin-top: 0.35rem;
  font-size: 0.92rem;
  font-weight: 600;
  color: #156467;
}

.wl-actions {
  display: flex;
  gap: 0.5rem;
}

.wl-cart,
.wl-del {
  padding: 0.5rem 0.95rem;
  font-size: 0.85rem;
  border-radius: 7px;
  cursor: pointer;
  transition: background 0.18s, color 0.18s, opacity 0.18s;
}

.wl-cart {
  color: #fff;
  background: #26b7bc;
  border: 1px solid #26b7bc;
}

.wl-cart:hover { background: #1d8e92; }

.wl-del {
  color: #9b9b9b;
  background: #fff;
  border: 1px solid #e1e1e1;
}

.wl-del:hover:not(:disabled) {
  color: #ea5520;
  border-color: #ea5520;
}

.wl-del:disabled { opacity: 0.5; cursor: default; }

@media (max-width: 520px) {
  .wl-item {
    grid-template-columns: 60px 1fr;
    grid-template-areas:
      "thumb info"
      "actions actions";
  }

  .wl-thumb { grid-area: thumb; width: 60px; height: 60px; }
  .wl-info { grid-area: info; }
  .wl-actions { grid-area: actions; }
}
</style>
