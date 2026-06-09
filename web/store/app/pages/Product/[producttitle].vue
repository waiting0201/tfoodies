<script setup lang="ts">
// Port of reference/old/tfoodies/Views/MainMs/ProductDetail.cshtml.
// URL: /Product/{producttitle}  slug ↔ title via urlSlugToTitle().
const route = useRoute()
const slug = computed(() => String(route.params.producttitle ?? ''))
const title = computed(() => urlSlugToTitle(slug.value))

const { data } = await useProductDetailData(title.value)
const p = computed(() => data.value.product)
const ntd = (n: number) => 'NT. ' + new Intl.NumberFormat('en-US').format(Math.trunc(n))
const onSale = computed(() => p.value && p.value.fixprice > p.value.price)

useHead(() => ({
  title: p.value?.title ?? title.value,
  link: p.value?.shortener ? [{ rel: 'canonical', href: p.value.shortener }] : [],
  meta: [
    { property: 'og:title', content: p.value?.title ?? title.value },
    { property: 'og:description', content: p.value?.intro ?? '' },
    ...(p.value?.shortener ? [{ property: 'og:url', content: p.value.shortener }] : []),
  ],
}))
</script>

<template>
  <main id="main">
    <section class="restrict-wide allpadding">
      <div class="locate">
        <p>
          <a href="/Products" class="descript">所有產品/</a>
          <a v-if="p?.producttypetitle" :href="`/Products/${encodeURIComponent(p.producttypetitle)}`" class="descript">{{ p.producttypetitle }}/</a>
          <a href="javascript:;" class="descript main">{{ p?.title }}</a>
        </p>
      </div>
    </section>

    <section v-if="p" class="allpadding clr section">
      <div class="restrict-wide">
        <div class="recipe-wrap">
          <div class="food productpart">
            <div class="slider-for">
              <div v-for="photo in p.photos" :key="photo.sort" class="product-display">
                <img :src="data.blobUrl + photo.photo" :alt="p.title">
              </div>
            </div>
            <div class="slider-nav">
              <div v-for="photo in p.photos" :key="photo.sort" class="product-thumb">
                <img :src="data.blobUrl + photo.photo" :alt="p.title">
              </div>
            </div>
          </div>

          <div class="food-desc">
            <h1>{{ p.title }}</h1>
            <h2>{{ p.entitle }}</h2>
            <p>{{ p.capacity }}</p>

            <div class="sale">
              <template v-if="onSale">
                <div class="line-through">原價 NT. {{ ntd(p.fixprice) }}</div>
                <div class="centered">
                  <div class="sale-tag">特價</div>
                </div>
                <div class="product-price">NT. {{ ntd(p.price) }}</div>
              </template>
              <template v-else>
                <div class="product-price">NT. {{ ntd(p.price) }}</div>
              </template>
            </div>

            <div class="product-number">
              <div class="numb"><p>數量</p></div>
              <div class="quantity-wrap">
                <div class="quantity">
                  <div class="qty-minus"><a href="javascript:;" class="minus">-</a></div>
                  <div class="qtyinput"><input type="text" name="qty" id="qty" value="1" class="input form-input qty"></div>
                  <div class="qty-plus"><a href="javascript:;" class="plus">+</a></div>
                </div>
              </div>
            </div>

            <div class="buybtn-wrap">
              <a v-if="p.added > 0" href="javascript:;" class="btn outline-btn solidhover js-add-cart" :data-productid="p.productid" :data-title="p.title">加入購物車</a>
              <a v-else href="javascript:;" class="btn outline-btn solidhover popup-contact" :data-productid="p.productid">到貨通知我</a>
              <a href="javascript:;" class="btn outline-btn solidhover mylistbtn" :data-productid="p.productid">
                <div class="love"></div>
              </a>
            </div>

            <div class="horizon-line"></div>
            <div class="product-desc">
              <h2 class="main left">商品介紹</h2>
              <ul><li v-html="(p.intro ?? '').replace(/\n/g, '<br />')"></li></ul>
            </div>
            <div class="horizon-line"></div>
            <div class="product-desc">
              <h2 class="main left">付款方式</h2>
              <ul>
                <li>ATM轉帳付款</li>
                <li>宅配貨到付款</li>
                <li>信用卡線上付款</li>
              </ul>
            </div>
          </div>
        </div>

        <!-- RWD 版商品介紹（下方顯示） -->
        <div class="rwd-product-desc">
          <div class="product-desc">
            <h2 class="main left">商品介紹</h2>
            <p v-html="(p.intro ?? '').replace(/\n/g, '<br />')"></p>
          </div>
          <div class="horizon-line"></div>
          <div class="product-desc">
            <h2 class="main left">付款方式</h2>
            <ul>
              <li>ATM轉帳付款</li>
              <li>宅配貨到付款</li>
              <li>信用卡線上付款</li>
            </ul>
          </div>
        </div>

        <div class="horizon-line"></div>
        <section class="allsection" v-html="p.memo"></section>
        <div class="centered more"><a href="#wrapper" class="outline-btn">TOP</a></div>

        <template v-if="p.recipes.length">
          <div class="horizon-line"></div>
          <section class="allsection">
            <div class="restrict-wide">
              <h3 class="main left">適合料理</h3>
              <div class="responsive promoteSlider clr">
                <div v-for="recipe in p.recipes" :key="recipe.recipeid" class="promote-sliding product-single centered">
                  <div class="zoom-wrap">
                    <a :href="`/Recipe/${recipe.recipeid}/1`" class="blog-wrap">
                      <div class="article-single centered">
                        <div class="article-pic"><img :src="data.blobUrl + (recipe.rphoto ?? '')" :alt="recipe.title"></div>
                        <div class="article-title">{{ recipe.title }}</div>
                      </div>
                    </a>
                  </div>
                </div>
              </div>
            </div>
          </section>
          <div class="centered more"><a href="/Recipes" class="outline-btn">更多</a></div>
        </template>
      </div>
    </section>

    <!-- 品牌介紹區塊 -->
    <div
      v-if="p?.brand?.isdisplay === 1"
      :style="{ backgroundImage: `url(${data.blobUrl}${p.brand.storybgclass ?? ''})` }"
      class="allpadding clr allsection storybg productbrand"
    >
      <div class="restrict">
        <div class="centered">
          <h3>品牌介紹</h3>
          <div class="banner-title ci-title">{{ p.brand.title }}</div>
          <div class="content">
            <p v-html="(p.brand.intro ?? '').replace(/\n/g, '<br />')"></p>
            <a :href="`/Brand/${encodeURIComponent(p.brand.title)}`" class="btn yellow zindex-up">品牌介紹</a>
          </div>
        </div>
      </div>
    </div>
  </main>
</template>
