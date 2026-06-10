<script setup lang="ts">
// Port of reference/old/tfoodies/Views/MainMs/Products.cshtml — filtered by product type.
// URL: /Products/{producttypetitle}  (legacy RouteConfig "ProductTypes").
const route = useRoute()
const typeTitle = computed(() => String(route.params.producttypetitle ?? ''))
const { data } = await useProductsData(typeTitle.value)

useSeo(() => ({
  title: data.value.currentType?.title ?? '商品',
  description:
    data.value.currentType?.memo ||
    `食在呼 TFoodies ${data.value.currentType?.title ?? ''}系列商品，嚴選天然安心食材，產地直送餐桌。`,
  type: 'website',
}))
</script>

<template>
  <main id="main">
    <section class="tallsection clr">
      <div class="restrict-wide">
        <div class="centered">
          <h1>所有商品</h1>
          <div id="tabber">
            <div class="tabber-selectors">
              <ul>
                <li><a href="/Products" class="tabber-anchor" title="所有商品">所有商品</a></li>
                <li v-for="pt in data.producttypes" :key="pt.producttypeid">
                  <a
                    :href="`/Products/${encodeURIComponent(pt.title)}`"
                    :class="`tabber-anchor${pt.title === typeTitle ? ' active' : ''}`"
                    :title="pt.title"
                  >{{ pt.title }}</a>
                </li>
              </ul>
            </div>

            <p v-if="data.currentType?.memo" class="restrict-small">{{ data.currentType.memo }}</p>

            <div class="direct-line"></div>

            <div class="four-column clr">
              <ProductCard
                v-for="p in data.products" :key="p.productid"
                :product="p" :blob-url="data.blobUrl" :promote-sliding="false"
              />
            </div>
          </div>
        </div>
      </div>
    </section>
  </main>
</template>
