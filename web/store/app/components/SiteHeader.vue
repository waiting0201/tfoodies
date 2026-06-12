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
// 登入後 header 僅顯示會員圖示（點擊進入會員中心）；登出改由會員中心側欄處理。
const memberAuth = useMemberAuthStore()
const isLoggedIn = computed(() => memberAuth.isAuthenticated)

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
              <a href="/Member/Center" class="member-icon" title="會員中心"></a>
            </template>
            <template v-else>
              <a href="/Member/Login" class="member-icon" rel="nofollow"></a>
              <a href="/Member/Login" class="logout" rel="nofollow">
                <div class="small">登入</div>
              </a>
            </template>
          </div>
          <div class="shopping-cart">
            <!-- badge 放進 cart-icon 內：錨定 32×26 的圖示本身（跨斷點固定），不受 .shopping-cart
                 各斷點不同 padding 影響，確保數字置中、位置一致。99+ 自動變膠囊。-->
            <a href="/Cart" class="cart-icon">
              <span class="addnumber" :class="{ 'add-active': cart.count > 0 }">
                <span class="addsmall">{{ cart.count > 99 ? '99+' : cart.count }}</span>
              </span>
            </a>
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

<style scoped>
/* 讓「登入/登出」文字與左側 member icon 上下置中對齊。
   main.css 用 `#topnav .login-wrap .member{display:inline-block}`（含 ID，特異度 1,2,0）
   壓過純 class 的覆寫，導致 .member 始終是 inline-block、flex 置中失效。
   因此這裡同樣帶上 #topnav 取得 ID 特異度，scoped 屬性再 +1 才能贏過 legacy。*/
/* 讓整個 .member 與 .shopping-cart 在 login-wrap 內垂直置中（對齊 header 列中線）。
   覆寫 legacy `#topnav .login-wrap{display:block}` 與子元素 float。*/
#topnav .login-wrap {
  display: flex;
  align-items: center;
}

#topnav .login-wrap .member {
  display: flex;
  align-items: center;
  padding-top: 0;
}

#topnav .login-wrap .shopping-cart {
  float: none;
}

#topnav .login-wrap .member .member-icon {
  float: none;
  top: 0;
  margin-right: 5px;
}

#topnav .login-wrap .member a.logout {
  display: flex;
  align-items: center;
  float: none;
  padding: 0;
  margin: 0;
}

/* ── 購物車數量 badge 重新設計 ──────────────────────────────
   舊版 .addnumber 錨定在帶 padding 的 .shopping-cart 上（各斷點 padding 不同），
   且數字用 line-height/固定寬置中，導致歪斜、多位數溢出。
   改為錨定固定尺寸的 .cart-icon，flex 置中，min-width 讓多位數變膠囊。*/
.shopping-cart .cart-icon {
  position: relative;
  overflow: visible;
}

.shopping-cart .cart-icon .addnumber {
  position: absolute;
  top: -8px;
  right: -10px;
  display: none;
  /* width:auto 覆蓋 legacy 固定 20px，讓 min-width/height 接管才會是正圓 */
  width: auto;
  min-width: 18px;
  height: 18px;
  padding: 0 5px;
  box-sizing: border-box;
  border-radius: 9px;
  background-color: #ea5520;
  opacity: 1;
  /* 與綠色 header 區隔、避免貼著圖示糊在一起 */
  box-shadow: 0 0 0 2px #fff;
  pointer-events: none;
}

.shopping-cart .cart-icon .addnumber.add-active {
  display: flex;
  align-items: center;
  justify-content: center;
}

.shopping-cart .cart-icon .addnumber .addsmall {
  width: auto;
  height: auto;
  line-height: 1;
  color: #fff;
  font-size: 11px;
  font-weight: 700;
  text-align: center;
  vertical-align: baseline;
}

/* 維持原本 hover 變深的互動 */
.shopping-cart:hover .cart-icon .addnumber {
  background-color: #d8430f;
}

/* 小螢幕（手機）圖示稍小，badge 同步縮一點避免過於突出 */
@media (max-width: 767px) {
  .shopping-cart .cart-icon .addnumber {
    top: -7px;
    right: -9px;
    min-width: 16px;
    height: 16px;
    border-radius: 8px;
    /* 手機 header 為綠底，白色描邊會露出一圈，移除之 */
    box-shadow: none;
  }
  .shopping-cart .cart-icon .addnumber .addsmall {
    font-size: 10px;
  }
}
</style>
