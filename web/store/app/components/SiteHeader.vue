<script setup lang="ts">
// Verbatim port of reference/old/tfoodies/Views/Shared/_Header.cshtml.
// DOM structure & class names are preserved 1:1 so the legacy main.css renders identically.
// Only ~/content/* → /content/* and @Url.Action(...) → preserved route paths.
interface Brand { title: string }
interface CartLine { name: string; capacity?: string; photo?: string; quantity: number }

withDefaults(defineProps<{
  brands?: Brand[]
  cartContents?: CartLine[]
  cartItems?: number
  addActive?: string
  blobUrl?: string
  isLoggedIn?: boolean
}>(), {
  brands: () => [],
  cartContents: () => [],
  cartItems: 0,
  addActive: '',
  blobUrl: '',
  isLoggedIn: false,
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
              <a href="/MemberMs/Orders" class="member-icon"></a>
              <a href="/MemberMs/Logout" class="logout">
                <div class="small">登出</div>
              </a>
            </template>
            <template v-else>
              <a href="/Login" class="member-icon" rel="nofollow"></a>
              <a href="/Login" class="logout" rel="nofollow">
                <div class="small">登入</div>
              </a>
            </template>
          </div>
          <div class="shopping-cart">
            <a href="javascript:;" class="cart-icon"></a>
            <div class="addnumber" :class="addActive">
              <div class="addsmall">{{ cartItems }}</div>
            </div>
            <div class="cart-content">
              <!-- <div class="triangle"></div> -->
              <div class="buy-item-wrap">
                <div v-for="(item, i) in cartContents" :key="i" class="buy-item">
                  <div class="buy-pic"><img :src="blobUrl + (item.photo ?? '')"></div>
                  <div class="buy-info">
                    <p>{{ item.name + (item.capacity ?? '') }}</p>
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
              <div class="centered"><a href="javascript:;" class="btn basic paycheck" rel="nofollow">前往結帳</a></div>
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
