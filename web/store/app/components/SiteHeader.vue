<script setup lang="ts">
// Verbatim port of reference/old/tfoodies/Views/Shared/_Header.cshtml.
// DOM structure & class names are preserved 1:1 so the legacy main.css renders identically.
// Only ~/content/* → /content/* and @Url.Action(...) → preserved route paths.
interface Brand { title: string }

withDefaults(defineProps<{
  brands?: Brand[]
}>(), {
  brands: () => [],
})

// Cart badge + dropdown are driven by the shared pinia store (the old `cartContents`/
// `cartItems` props were never passed, so the header always showed 0).
const cart = useCartStore()
const blobUrl = String(useRuntimeConfig().public.blobUrl)
onMounted(() => cart.hydrate())

// Login state from the member-auth store (hydrated app-wide by plugins/auth.client.ts).
const memberAuth = useMemberAuthStore()
const isLoggedIn = computed(() => memberAuth.isAuthenticated)
function logout() {
  memberAuth.logout()
  navigateTo('/Member/Login')
}

// On an actual add-to-cart (addPulse — not hydrate), briefly slide the mini-cart open so the
// user sees what was added, then auto-close. (Hover-to-open is disabled in the legacy-effects
// plugin per request; the badge itself is the persistent indicator — see template.)
let closeTimer: ReturnType<typeof setTimeout> | undefined
watch(() => cart.addPulse, (now, prev) => {
  if (now <= (prev ?? 0)) return
  const $ = (window as unknown as { $?: any }).$
  if (!$ || !$.fn) return
  const el = $('.shopping-cart .cart-content')
  el.stop(true, true).slideDown('fast')
  clearTimeout(closeTimer)
  closeTimer = setTimeout(() => el.stop(true, true).slideUp('fast'), 2500)
})
</script>

<template>
  <header id="header">
    <div id="topnav">
      <div class="restrict-wide">
        <div class="social-link">
          <a href="https://www.facebook.com/trulyfoodies/" class="social-icon" target="_blank" rel="nofollow"><img src="/content/images/common/facebook.png"></a>
          <a href="https://www.instagram.com/tfoodies/" class="social-icon" target="_blank" rel="nofollow"><img src="/content/images/common/instagram.png"></a>
          <a href="https://www.youtube.com/channel/UCACS-XffRxWsTb-a8uCWA4Q" class="social-icon" target="_blank" rel="nofollow"><img src="/content/images/common/youtube.png"></a>
          <div class="inline must freeship">消費滿2,000免運！</div>
          <div class="inline"> <img src="/content/images/common/freeshipping.gif"></div>
        </div>
        <div class="login-wrap">
          <div class="member">
            <template v-if="isLoggedIn">
              <a href="/Member/Center" class="member-icon"></a>
              <a href="javascript:;" class="logout" @click.prevent="logout">
                <div class="small">登出</div>
              </a>
            </template>
            <template v-else>
              <a href="/Member/Login" class="member-icon" rel="nofollow"></a>
              <a href="/Member/Login" class="logout" rel="nofollow">
                <div class="small">登入</div>
              </a>
            </template>
          </div>
          <div class="shopping-cart">
            <a href="/Cart" class="cart-icon"></a>
            <!-- 購物車有商品時才顯示 badge（.add-active 讓 .addnumber 由 display:none 變 block）-->
            <div class="addnumber" :class="{ 'add-active': cart.count > 0 }">
              <div class="addsmall">{{ cart.count }}</div>
            </div>
            <div class="cart-content">
              <!-- <div class="triangle"></div> -->
              <div class="buy-item-wrap">
                <p v-if="cart.items.length === 0" class="descript centered">購物車是空的</p>
                <div v-for="item in cart.items" :key="item.productId" class="buy-item">
                  <div class="buy-pic"><img :src="blobUrl + (item.photo ?? '')"></div>
                  <div class="buy-info">
                    <p>{{ item.title + (item.capacity ?? '') }}</p>
                    <div class="amount descript">
                      <div class="numb">數量:</div>
                      <div class="quantity-wrap">
                        <div class="quantity">
                          <div class="qtyinput">{{ item.quantity }}</div>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
              <div class="centered"><a :href="cart.items.length ? '/Checkout' : '/Cart'" class="btn basic paycheck" rel="nofollow">前往結帳</a></div>
            </div>
          </div>
        </div>
      </div>
    </div>
    <nav id="nav">
      <div class="restrict-wide">
        <div id="menuNavigation">
          <ul>
            <li class="navItem"><a href="/Products" title="所有產品">所有產品</a></li>
            <li class="navItem">
              <a href="javascript:;">品牌系列</a>
              <ul class="navContent">
                <li v-for="brand in brands" :key="brand.title"><a :href="`/Brand/${titleToUrlSlug(brand.title)}`" :title="brand.title">{{ brand.title }}</a></li>
              </ul>
            </li>
            <li class="navItem">
              <a href="/News" title="最新消息">最新消息</a>
              <ul class="navContent">
                <li><a href="/Events" title="活動花絮">活動花絮</a></li>
              </ul>
            </li>
            <li class="navItem"><a href="/Reports" title="檢驗報告">檢驗報告</a></li>
            <li class="navItem">
              <a href="/" class="logoli" title="首頁"></a>
            </li>
            <li class="navItem"><a href="/Recipes" title="美味料理">美味料理</a></li>
            <li class="navItem">
              <a href="/Issues" title="綠誌">綠誌</a>
              <ul class="navContent">
                <li><a href="/Knowledges" title="小知識">小知識</a></li>
              </ul>
            </li>
            <li class="navItem"><a href="/Blogs" title="部落客分享">部落客分享</a></li>
            <li class="navItem"><a href="/TFoodies" title="關於我們">關於我們</a></li>
          </ul>
        </div>
      </div>
    </nav>
    <div class="logo">
      <a href="/" title="首頁"><img src="/content/images/common/logo-main.png"></a>
    </div>
    <a id="btn_menu" href="javascript:;" class="sb-toggle-left"><img src="/content/images/common/mobile-menu.png" alt=""></a>
  </header>
</template>
