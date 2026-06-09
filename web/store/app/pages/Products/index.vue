<script setup lang="ts">
// Port of reference/old/tfoodies/Views/MainMs/Products.cshtml — all products (no type filter).
useHead({ title: '所有商品' })
const { data } = await useProductsData()
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
                <li><a href="/Products" class="tabber-anchor active" title="所有商品">所有商品</a></li>
                <li v-for="pt in data.producttypes" :key="pt.producttypeid">
                  <a :href="`/Products/${encodeURIComponent(pt.title)}`" class="tabber-anchor" :title="pt.title">{{ pt.title }}</a>
                </li>
              </ul>
            </div>
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
