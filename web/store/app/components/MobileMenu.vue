<script setup lang="ts">
// Verbatim port of reference/old/tfoodies/Views/Shared/_SubMenu.cshtml (Slidebars mobile menu).
interface Brand { title: string }
withDefaults(defineProps<{ brands?: Brand[]; isLoggedIn?: boolean }>(), {
  brands: () => [],
  isLoggedIn: false,
})
</script>

<template>
  <div class="sb-slidebar sb-left sb-style-push">
    <div class="slide-logo">
      <a href="/"><img src="/content/images/common/logo-main.png"></a>
    </div>
    <div id="slide-search">
      <ul>
        <li><a href="/Products" title="所有商品">所有商品</a></li>
        <li>
          <a href="javascript:;" class="slide-brand">品牌系列</a>
          <ul class="slide-brand-series">
            <li v-for="brand in brands" :key="brand.title"><a :href="`/Brand/${titleToUrlSlug(brand.title)}`" :title="brand.title">{{ brand.title }}</a></li>
          </ul>
        </li>
        <li>
          <a href="javascript:;" class="slide-news">最新消息</a>
          <ul class="slide-news-series">
            <li><a href="/News" title="最新消息">最新消息</a></li>
            <li><a href="/Events" title="活動花絮">活動花絮</a></li>
          </ul>
        </li>
        <li><a href="/Reports" title="檢驗報告">檢驗報告</a></li>
        <li><a href="/Recipes" title="美味料理">美味料理</a></li>
        <li><a href="/Issues" title="綠誌">綠誌</a></li>
        <li><a href="/Knowledges" title="小知識">小知識</a></li>
        <li><a href="/Blogs" title="部落客分享">部落客分享</a></li>
        <li><a href="/TFoodies" title="關於我們">關於我們</a></li>
      </ul>
      <div class="slide-social">
        <div v-if="isLoggedIn" class="centered">
          <a href="/MemberMs/Logout" class="logout">
            <div class="small white">登出</div>
          </a>
        </div>
        <div class="social-link">
          <a href="https://www.facebook.com/trulyfoodies/" class="social-icon" rel="nofollow"><img src="/content/images/common/facebook.png"></a>
          <a href="https://www.instagram.com/tfoodies/" class="social-icon" rel="nofollow"><img src="/content/images/common/instagram.png"></a>
          <a href="https://www.youtube.com/channel/UCACS-XffRxWsTb-a8uCWA4Q" class="social-icon" rel="nofollow"><img src="/content/images/common/youtube.png"></a>
        </div>
      </div>
    </div>
  </div>
</template>
